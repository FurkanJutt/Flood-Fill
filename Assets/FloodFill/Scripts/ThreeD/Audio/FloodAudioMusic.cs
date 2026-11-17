using UnityEngine;

namespace FloodFill.ThreeD.Audio
{
    public sealed partial class FloodAudioManager
    {
        private AudioSource[] musicSources;
        private readonly bool[] musicWasPlaying=new bool[2];
        private readonly float[] musicGain=new float[2],musicStartGain=new float[2],musicEndGain=new float[2];
        private readonly FloodMusicState[] sourceMusicState=new FloodMusicState[2];
        private FloodMusicState requestedMusic=FloodMusicState.Silent;
        private int activeMusic;
        private bool crossfading,releasing,deferredMusic;
        private float fadeElapsed,fadeDuration,nextMusicTransition;
        private AudioClip pendingMusicClip;
        private FloodMusicState pendingMusicState,pendingResolvedState;
        private float pendingMusicDuration;
        private int releaseSlot;
        private float releaseElapsed,releaseGain;

        private void CreateSources()
        {
            musicSources=new AudioSource[2];
            for(int i=0;i<2;i++){musicSources[i]=gameObject.AddComponent<AudioSource>();ConfigureSource(musicSources[i],true);}
            ResizePool();
        }
        private void ConfigureSource(AudioSource source,bool music)
        {
            source.playOnAwake=false;source.loop=music;source.spatialBlend=0;source.dopplerLevel=0;
            source.ignoreListenerPause=true;source.volume=0;source.priority=music?64:128;
            source.outputAudioMixerGroup=music?musicGroup:effectsGroup;
        }
        private float TrackVolume(FloodMusicState state)
        {
            switch(state){case FloodMusicState.MainMenu:return mainMenuMusicMultiplier;case FloodMusicState.Normal:return normalMusicMultiplier;
                case FloodMusicState.Medium:return mediumMusicMultiplier;case FloodMusicState.Hard:return hardMusicMultiplier;default:return 0;}
        }
        private AudioClip ResolveClip(FloodMusicState desired,out FloodMusicState resolved)
        {
            resolved=desired;
            if(desired==FloodMusicState.MainMenu)return mainMenuMusic;
            if(desired==FloodMusicState.Silent)return null;
            if(desired==FloodMusicState.Hard&&hardMusic!=null)return hardMusic;
            if((desired==FloodMusicState.Hard||desired==FloodMusicState.Medium)&&mediumMusic!=null){resolved=FloodMusicState.Medium;return mediumMusic;}
            if(normalMusic!=null){resolved=FloodMusicState.Normal;return normalMusic;}
            // Gameplay never borrows menu music or a higher intensity track by surprise.
            return null;
        }
        private void ReevaluateMusic(bool force)
        {
            if(!ready||preview||stoppedByUser)return;
            FloodMusicState state=menuContext?FloodMusicState.MainMenu:
                game==null||game.State==FloodFillGameManager3D.GameState.Calculating?FloodMusicState.Normal:
                FloodAudioPolicy.GameplayMusic(game.CurrentMoveBudget,game.MoveCount,enableDynamicMusic,mediumThresholdPercentage,hardThresholdMoves);
            var clip=ResolveClip(state,out var resolved);
            if(!releasing&&clip!=null&&clip==musicSources[activeMusic].clip&&musicEndGain[activeMusic]==1)
            {requestedMusic=state;sourceMusicState[activeMusic]=resolved;return;}
            if(!releasing&&clip==null&&crossfading&&musicEndGain[0]==0&&musicEndGain[1]==0)
            {requestedMusic=state;return;}
            if(state==requestedMusic&&clip==musicSources[activeMusic].clip&&!releasing)return;
            if(releasing&&clip==pendingMusicClip&&state==pendingMusicState)return;
            if(!force&&state!=FloodMusicState.Hard&&Time.unscaledTime<nextMusicTransition){deferredMusic=true;return;}
            deferredMusic=false;
            float duration=state==FloodMusicState.MainMenu?defaultCrossfadeDuration:gameplayCrossfadeDuration;
            if(requestedMusic==FloodMusicState.MainMenu)duration=Mathf.Max(duration,mainMenuFadeOut);
            if(!musicSources[0].isPlaying&&!musicSources[1].isPlaying)duration=state==FloodMusicState.MainMenu?mainMenuFadeIn:defaultFadeDuration;
            TransitionMusic(state,clip, resolved,duration);
        }
        private void PreviewMusic(FloodMusicState state)
        {
            if(!Application.isPlaying||musicSources==null)return;
            preview=true;stoppedByUser=false;duckTarget=1;
            var clip=ResolveClip(state,out var resolved);TransitionMusic(state,clip,resolved,defaultCrossfadeDuration);
        }
        private void TransitionMusic(FloodMusicState desired,AudioClip clip,FloodMusicState resolved,float duration)
        {
            requestedMusic=desired;nextMusicTransition=Time.unscaledTime+Mathf.Max(0,musicTransitionCooldown);
            if(clip==null)
            {
                releasing=false;pendingMusicClip=null;
                BeginFade(-1,duration);return;
            }
            for(int i=0;i<2;i++)
            {
                if(musicSources[i].clip==clip&&musicSources[i].isPlaying)
                {releasing=false;sourceMusicState[i]=resolved;activeMusic=i;BeginFade(i,duration);return;}
            }
            int slot=1-activeMusic;
            if(musicGain[slot]>.0001f&&musicSources[slot].isPlaying)
            {
                // Two occupied sources: release the quieter source briefly before reusing it.
                // This preserves continuity without ever playing a third track.
                releaseSlot=musicGain[0]<musicGain[1]?0:1;releaseGain=musicGain[releaseSlot];releaseElapsed=0;
                pendingMusicClip=clip;pendingMusicState=desired;pendingResolvedState=resolved;pendingMusicDuration=duration;
                releasing=true;crossfading=true;return;
            }
            StartTrack(slot,clip,resolved,duration);
        }
        private void StartTrack(int slot,AudioClip clip,FloodMusicState resolved,float duration)
        {
            musicSources[slot].Stop();musicGain[slot]=0;musicSources[slot].volume=0;
            musicSources[slot].clip=clip;sourceMusicState[slot]=resolved;musicSources[slot].Play();
            TotalMusicStarts++;activeMusic=slot;BeginFade(slot,duration);
        }
        private void BeginFade(int target,float duration)
        {
            fadeElapsed=0;fadeDuration=Mathf.Max(.01f,duration);crossfading=true;
            for(int i=0;i<2;i++){musicStartGain[i]=musicGain[i];musicEndGain[i]=i==target?1:0;}
        }
        private void UpdateMusic(float dt)
        {
            if(deferredMusic&&!preview&&!stoppedByUser&&Time.unscaledTime>=nextMusicTransition)ReevaluateMusic(true);
            if(releasing)
            {
                releaseElapsed+=dt;float t=Mathf.Clamp01(releaseElapsed/.08f);
                musicGain[releaseSlot]=releaseGain*(1f-t);
                if(t>=1)
                {
                    musicSources[releaseSlot].volume=0;musicSources[releaseSlot].Stop();releasing=false;
                    var clip=pendingMusicClip;pendingMusicClip=null;
                    if(clip!=null)StartTrack(releaseSlot,clip,pendingResolvedState,pendingMusicDuration);
                    else BeginFade(-1,defaultFadeDuration);
                }
            }
            else if(crossfading)
            {
                // Background-loaded/streamed clips can defer Play. Keep the outgoing gain
                // steady until Unity has audio data, rather than fading into a loading gap.
                var incoming=musicSources[activeMusic].clip;
                if(musicEndGain[activeMusic]>0&&incoming!=null&&incoming.loadState!=AudioDataLoadState.Loaded)
                {
                    if(incoming.loadState==AudioDataLoadState.Failed)BeginFade(-1,defaultFadeDuration);
                    ApplyMusicSourceVolumes();return;
                }
                fadeElapsed+=dt;float t=Mathf.Clamp01(fadeElapsed/Mathf.Max(.01f,fadeDuration));
                float shaped=fadeCurve==null?t:Mathf.Clamp01(fadeCurve.Evaluate(t));
                for(int i=0;i<2;i++)musicGain[i]=Mathf.Sqrt(Mathf.Lerp(musicStartGain[i]*musicStartGain[i],musicEndGain[i]*musicEndGain[i],shaped));
                if(t>=1){crossfading=false;for(int i=0;i<2;i++)if(musicEndGain[i]==0){musicSources[i].volume=0;musicSources[i].Stop();musicSources[i].clip=null;musicGain[i]=0;}}
            }
            ApplyMusicSourceVolumes();
        }
        private void ApplyMusicSourceVolumes()
        {
            float fallback=mixer==null?masterVolume*(enableMusic?musicVolume:0):1;
            for(int i=0;i<2;i++)musicSources[i].volume=Mathf.Clamp01(musicGain[i]*TrackVolume(sourceMusicState[i])*duck*fallback);
        }
    }
}
