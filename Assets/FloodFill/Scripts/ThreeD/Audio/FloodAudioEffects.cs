using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace FloodFill.ThreeD.Audio
{
    public sealed partial class FloodAudioManager
    {
        private sealed class Voice
        {
            internal AudioSource Source;
            internal FloodSound Kind;
            internal bool Active,Stopping,WasPlaying,ToggleFeedback;
            internal float Age,Gain,Scale,PitchScale,MaximumAge;
            internal int StartFrame;
        }
        private Voice[] voices;
        private bool poolResizePending;
        private readonly float[] lastSoundAt=new float[21];
        private bool soundTimesInitialized;
        private readonly int[] soundStartCounts=new int[21];
        private Voice spreadVoice;
        private bool spreadPaused;
        private readonly Dictionary<AudioClip,AudioClip> spreadLoops=new Dictionary<AudioClip,AudioClip>();
        private float preparedSpreadOffset,preparedSpreadLength;
        private static readonly ProfilerMarker SpreadStartMarker=new ProfilerMarker("FloodAudio.StartSpread");
        private static readonly ProfilerMarker SpreadUpdateMarker=new ProfilerMarker("FloodAudio.UpdateSpread");
        public int SoundStartCount(FloodSound kind)=>soundStartCounts[(int)kind];
        private void ResizePool()
        {
            int size=Mathf.Clamp(audioSourcePoolSize,4,24);
            if(voices!=null&&voices.Length==size){poolResizePending=false;return;}
            if(voices!=null&&size<voices.Length)
            {
                bool retiring=false;
                for(int i=size;i<voices.Length;i++)if(voices[i].Active){voices[i].Stopping=true;retiring=true;}
                if(retiring){poolResizePending=true;return;}
            }
            poolResizePending=false;
            var replacement=new Voice[size];int kept=voices==null?0:Math.Min(size,voices.Length);
            for(int i=0;i<kept;i++)replacement[i]=voices[i];
            for(int i=kept;i<size;i++){var source=gameObject.AddComponent<AudioSource>();ConfigureSource(source,false);replacement[i]=new Voice{Source=source};}
            if(voices!=null)for(int i=size;i<voices.Length;i++){voices[i].Source.Stop();Destroy(voices[i].Source);}
            voices=replacement;
            if(!soundTimesInitialized){Array.Fill(lastSoundAt,float.NegativeInfinity);soundTimesInitialized=true;}
        }
        public FloodSoundSettings SoundSettings(FloodSound sound)
        {
            switch(sound)
            {
                case FloodSound.ButtonClick:return buttonClick;case FloodSound.ButtonBack:return buttonBack;case FloodSound.ButtonConfirm:return buttonConfirm;
                case FloodSound.ButtonCancel:return buttonCancel;case FloodSound.ToggleOn:return toggleOn;case FloodSound.ToggleOff:return toggleOff;
                case FloodSound.Slider:return sliderInteraction;case FloodSound.PopupOpen:return popupOpen;case FloodSound.PopupClose:return popupClose;
                case FloodSound.ColorSelected:return colorSelected;case FloodSound.BlockColorChange:return blockColorChange;case FloodSound.FloodProgress:return floodProgress;
                case FloodSound.FloodEnd:return floodEnd;case FloodSound.LargeCapture:return largeCapture;case FloodSound.LevelStarted:return levelStarted;
                case FloodSound.LevelCompleted:return levelCompleted;case FloodSound.LevelFailed:return levelFailed;
                case FloodSound.Restart:return restartLevel;case FloodSound.Pause:return pauseSound;case FloodSound.Resume:return resumeSound;default:return null;
            }
        }
        private static bool IsFloodSound(FloodSound kind)=>kind==FloodSound.BlockColorChange||kind==FloodSound.FloodProgress||kind==FloodSound.FloodEnd||kind==FloodSound.LargeCapture;
        public bool PlaySound(FloodSound kind,float volumeScale=1f,float pitchScale=1f)
            =>PlaySoundInternal(kind,volumeScale,pitchScale,false);
        private bool PlaySoundInternal(FloodSound kind,float volumeScale,float pitchScale,bool toggleFeedback,float maximumDuration=0f,float clipOffset=0f,AudioClip offsetClip=null)
        {
            if(!Application.isPlaying||Instance!=this||(!enableSoundEffects&&!toggleFeedback)||voices==null||appSuspended)return false;
            var settings=SoundSettings(kind);if(settings==null)return false;
            float now=Time.unscaledTime;
            if(now-lastSoundAt[(int)kind]<Mathf.Max(0,settings.minimumInterval))return false;
            int count=0,flood=0,available=-1;
            for(int i=0;i<voices.Length;i++)
            {
                if(voices[i]!=spreadVoice&&voices[i].Active&&!voices[i].Source.isPlaying&&!voices[i].WasPlaying)voices[i].Active=false;
                if(!voices[i].Active){if(available<0&&i<Mathf.Clamp(audioSourcePoolSize,4,24))available=i;continue;}
                if(voices[i].Kind==kind)count++;if(IsFloodSound(voices[i].Kind))flood++;
            }
            if(count>=Mathf.Max(1,settings.maximumInstances)||available<0||IsFloodSound(kind)&&flood>=Mathf.Clamp(maximumSimultaneousFloodSounds,1,4))return false;
            var clip=settings.ChooseClip();
            if(clip==null&&(kind==FloodSound.ButtonBack||kind==FloodSound.ButtonConfirm||kind==FloodSound.ButtonCancel||kind==FloodSound.ToggleOn||kind==FloodSound.ToggleOff||kind==FloodSound.Slider))clip=buttonClick.ChooseClip();
            if(clip==null)return false;
            // A Play/Start confirmation can precede asynchronous level readiness by
            // many frames. Treat that matching clip as the launch cue for this level,
            // rather than playing it again when calculation completes.
            if(kind==FloodSound.LevelStarted&&clip==levelStartConfirmationClip)return false;
            if(kind<=FloodSound.PopupClose)
                for(int i=0;i<voices.Length;i++)
                    if(voices[i].Active&&voices[i].Source.clip==clip&&voices[i].StartFrame==Time.frameCount)return false;
            var voice=voices[available];voice.Source.loop=false;voice.Kind=kind;voice.Active=true;voice.Stopping=false;voice.WasPlaying=false;voice.Age=0;voice.Gain=1;
            voice.MaximumAge=Mathf.Max(0,maximumDuration);
            voice.ToggleFeedback=toggleFeedback;
            voice.Source.outputAudioMixerGroup=toggleFeedback&&feedbackGroup!=null?feedbackGroup:effectsGroup;
            voice.StartFrame=Time.frameCount;voice.Scale=Mathf.Clamp(volumeScale,0,1.3f);voice.PitchScale=pitchScale;
            voice.Source.clip=clip;
            if(clipOffset>0&&(offsetClip==null||clip==offsetClip))voice.Source.time=Mathf.Clamp(clipOffset,0,Mathf.Max(0,clip.length-.001f));
            voice.Source.pitch=Mathf.Clamp(settings.pitch*pitchScale+FloodAudioRandom.Range(-settings.randomPitch,settings.randomPitch),.25f,2f);
            // Preserve short cue attacks: starting at zero until the next frame can
            // entirely mute a tiny UI click. Cue fades shape release/tails instead.
            float initialBus=VoiceBusGain(voice);
            voice.Source.volume=Mathf.Clamp01(settings.volume*voice.Scale*initialBus);
            voice.Source.Play();
            if(voice.MaximumAge>0)voice.Source.SetScheduledEndTime(AudioSettings.dspTime+voice.MaximumAge);
            lastSoundAt[(int)kind]=now;
            if(kind==FloodSound.ButtonConfirm&&!menuContext&&!levelHasStarted&&game!=null&&game.State==FloodFillGameManager3D.GameState.Calculating)
                levelStartConfirmationClip=clip;
            TotalSoundStarts++;soundStartCounts[(int)kind]++;
            return true;
        }
        private void UpdateVoices(float dt)
        {
            for(int i=0;i<voices.Length;i++)
            {
                var v=voices[i];if(!v.Active||v==spreadVoice)continue;
                if(!v.Source.isPlaying){v.Active=false;v.Source.clip=null;continue;}
                var settings=SoundSettings(v.Kind);v.Age+=dt;
                if(v.MaximumAge>0&&v.Age>=v.MaximumAge)
                {v.Source.volume=0;v.Source.Stop();v.Source.clip=null;v.Active=false;continue;}
                float ramp=Mathf.Max(.005f,settings.fadeDuration);
                v.Gain=Mathf.MoveTowards(v.Gain,v.Stopping?0:1,dt/ramp);
                // Tail ramp prevents hard edges even for clips without their own fade-out.
                float remaining=Mathf.Max(0,v.Source.clip.length-v.Source.time)/Mathf.Max(.25f,v.Source.pitch);
                if(v.MaximumAge>0)remaining=Mathf.Min(remaining,Mathf.Max(0,v.MaximumAge-v.Age));
                float tail=Mathf.Clamp01(remaining/ramp);
                float fallback=VoiceBusGain(v);
                v.Source.volume=Mathf.Clamp01(settings.volume*v.Scale*v.Gain*tail*fallback);
                if(v.Stopping&&v.Gain<=0){v.Source.Stop();v.Source.clip=null;v.Active=false;}
            }
            if(poolResizePending)ResizePool();
        }
        private float VoiceBusGain(Voice voice)
        {
            if(mixer==null)return masterVolume*(voice.ToggleFeedback||enableSoundEffects?soundEffectsVolume:0);
            return voice.ToggleFeedback?soundEffectsVolume:1f;
        }
        private void RefreshActiveVoices()
        {
            var counts=new int[21];int floodCount=0;
            for(int i=0;i<voices.Length;i++)
            {
                var voice=voices[i];if(!voice.Active)continue;
                var settings=SoundSettings(voice.Kind);
                counts[(int)voice.Kind]++;
                if(counts[(int)voice.Kind]>Mathf.Max(1,settings.maximumInstances))voice.Stopping=true;
                if(IsFloodSound(voice.Kind)&&++floodCount>Mathf.Clamp(maximumSimultaneousFloodSounds,1,4))voice.Stopping=true;
                // Let a currently sounding clip finish; subsequent events use new assignments.
                voice.Source.pitch=Mathf.Clamp(settings.pitch*voice.PitchScale,.25f,2f);
                voice.Source.outputAudioMixerGroup=voice.ToggleFeedback&&feedbackGroup!=null?feedbackGroup:effectsGroup;
            }
            for(int i=0;i<2;i++)musicSources[i].outputAudioMixerGroup=musicGroup;
            UpdateVoices(0);
        }
        private void RefreshFloodClipBindings()
        {
            if(floodStartOffsetClip!=blockColorChange.clip)
            {if(floodStartOffsetClip!=null)floodStartClipOffset=0;floodStartOffsetClip=blockColorChange.clip;}
            int count=blockColorChange.clip!=null?1:0;
            bool matches=blockColorChange.clip==null||spreadLoops.ContainsKey(blockColorChange.clip);
            var alternatives=blockColorChange.alternateClips;
            if(alternatives!=null)
                for(int i=0;i<alternatives.Length;i++)
                    if(alternatives[i]!=null&&alternatives[i]!=blockColorChange.clip)
                    {
                        bool duplicate=false;
                        for(int j=0;j<i;j++)if(alternatives[j]==alternatives[i]){duplicate=true;break;}
                        if(!duplicate){count++;matches&=spreadLoops.ContainsKey(alternatives[i]);}
                    }
            if(matches&&count==spreadLoops.Count&&preparedSpreadOffset==floodStartClipOffset&&preparedSpreadLength==spreadLoopSeconds)return;
            CancelFlood();DisposeSpreadClips();
            preparedSpreadOffset=floodStartClipOffset;preparedSpreadLength=spreadLoopSeconds;
            PrepareSpreadClip(blockColorChange.clip,floodStartClipOffset);
            if(alternatives!=null)for(int i=0;i<alternatives.Length;i++)PrepareSpreadClip(alternatives[i],0);
        }
        private void PrepareSpreadClip(AudioClip original,float offset)
        {
            if(original==null||spreadLoops.ContainsKey(original))return;
            // Preparing a short PCM loop is startup/explicit Refresh work, never a move
            // or frame operation. Native looping keeps sounding even during a slow frame.
            if(original.loadType==AudioClipLoadType.Streaming)
            {spreadLoops.Add(original,original);return;}
            original.LoadAudioData();
            int start=Mathf.Clamp(Mathf.RoundToInt(offset*original.frequency),0,original.samples-1);
            int frames=Mathf.Max(1,Mathf.Min(Mathf.RoundToInt(Mathf.Clamp(spreadLoopSeconds,.04f,.5f)*original.frequency),original.samples-start));
            var samples=new float[frames*original.channels];
            if(!original.GetData(samples,start)){spreadLoops.Add(original,original);return;}
            int edge=Mathf.Max(1,Mathf.Min(frames/2,Mathf.RoundToInt(original.frequency*.004f)));
            for(int i=0;i<frames;i++)
            {
                float gain=Mathf.Clamp01(Mathf.Min(i,frames-1-i)/(float)edge);
                for(int channel=0;channel<original.channels;channel++)samples[i*original.channels+channel]*=gain;
            }
            var loop=AudioClip.Create("Color Spread Loop",frames,original.channels,original.frequency,false);
            loop.SetData(samples,0);spreadLoops.Add(original,loop);
        }
        private void DisposeSpreadClips()
        {
            foreach(var pair in spreadLoops)if(pair.Value!=null&&pair.Value!=pair.Key)Destroy(pair.Value);
            spreadLoops.Clear();
        }
        private void OnFlood(FloodAudioSignals.FloodWave wave)
        {
            using(SpreadStartMarker.Auto())
            {
                CancelFlood();
                // Only three scalars are needed, independent of the number of cubes.
                waveStart=Time.time+Mathf.Max(0,wave.FirstWaveStart);
                waveLastStart=Time.time+Mathf.Max(wave.FirstWaveStart,wave.LastWaveStart);
                waveEnd=Time.time+Mathf.Max(wave.FirstWaveStart,wave.Duration);
                waveActive=enableSoundEffects&&blockColorChange.HasClip&&waveEnd>waveStart;
                UpdateFlood();
            }
        }
        private bool StartSpreadVoice()
        {
            if(!enableSoundEffects||appSuspended||voices==null)return false;
            var clip=blockColorChange.ChooseClip();
            if(clip==null||!spreadLoops.TryGetValue(clip,out var loop))return false;
            for(int i=0;i<Math.Min(voices.Length,Mathf.Clamp(audioSourcePoolSize,4,24));i++)
            {
                var voice=voices[i];if(voice.Active)continue;
                spreadVoice=voice;voice.Kind=FloodSound.BlockColorChange;voice.Active=true;
                voice.Stopping=false;voice.ToggleFeedback=false;voice.Gain=1;voice.Scale=1;
                voice.PitchScale=1;voice.Age=0;voice.MaximumAge=0;voice.WasPlaying=false;
                voice.Source.clip=loop;voice.Source.loop=true;voice.Source.time=0;
                voice.Source.outputAudioMixerGroup=effectsGroup;
                ApplySpreadEnvelope();voice.Source.Play();
                TotalSoundStarts++;soundStartCounts[(int)FloodSound.BlockColorChange]++;
                return true;
            }
            return false;
        }
        private void ApplySpreadEnvelope()
        {
            float phase=Mathf.InverseLerp(waveStart,Mathf.Max(waveStart+.001f,waveLastStart),Time.time);
            float speed=Mathf.Lerp(spreadStartSpeed,spreadEndSpeed,phase);
            spreadVoice.Source.pitch=Mathf.Clamp(blockColorChange.pitch*speed,.25f,2f);
            float fade=Mathf.Min(spreadFadeOutSeconds,Mathf.Max(.001f,(waveEnd-waveStart)*.4f));
            float gain=Mathf.SmoothStep(0,1,Mathf.Clamp01((waveEnd-Time.time)/Mathf.Max(.001f,fade)));
            spreadVoice.Source.volume=Mathf.Clamp01(blockColorChange.volume*gain*VoiceBusGain(spreadVoice));
        }
        private void UpdateFlood()
        {
            using(SpreadUpdateMarker.Auto())
            {
                if(!waveActive)return;
                if(!enableSoundEffects||Time.time>=waveEnd){CancelFlood();return;}
                if(Time.timeScale<=0)
                {
                    if(spreadVoice!=null&&!spreadPaused){spreadVoice.Source.Pause();spreadPaused=true;}
                    return;
                }
                if(spreadPaused){spreadVoice?.Source.UnPause();spreadPaused=false;}
                if(Time.time<waveStart)return;
                if(spreadVoice==null&&!StartSpreadVoice())return;
                ApplySpreadEnvelope();
            }
        }
        private void CancelFlood()
        {
            waveActive=false;spreadPaused=false;
            if(spreadVoice==null)return;
            spreadVoice.Source.Stop();spreadVoice.Source.loop=false;spreadVoice.Source.clip=null;
            spreadVoice.Active=false;spreadVoice.WasPlaying=false;spreadVoice=null;
        }
    }
}
