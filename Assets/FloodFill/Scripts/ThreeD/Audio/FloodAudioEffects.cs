using System;
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
            internal float Age,Gain,Scale,PitchScale;
            internal int StartFrame;
        }
        private Voice[] voices;
        private bool poolResizePending;
        private readonly float[] lastSoundAt=new float[21];
        private bool soundTimesInitialized;
        private readonly int[] soundStartCounts=new int[21];
        private bool floodFinishPending;
        private FloodSound floodFinishKind;
        private float floodFinishDeadline,lastProgressUnscaled;
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
                case FloodSound.LevelCompleted:return levelCompleted;case FloodSound.LevelFailed:return levelFailed;case FloodSound.OutOfMoves:return outOfMoves;
                case FloodSound.Restart:return restartLevel;case FloodSound.Pause:return pauseSound;case FloodSound.Resume:return resumeSound;default:return null;
            }
        }
        private static bool IsFloodSound(FloodSound kind)=>kind==FloodSound.BlockColorChange||kind==FloodSound.FloodProgress||kind==FloodSound.FloodEnd||kind==FloodSound.LargeCapture;
        public bool PlaySound(FloodSound kind,float volumeScale=1f,float pitchScale=1f)
            =>PlaySoundInternal(kind,volumeScale,pitchScale,false);
        private bool PlaySoundInternal(FloodSound kind,float volumeScale,float pitchScale,bool toggleFeedback)
        {
            if(!Application.isPlaying||Instance!=this||(!enableSoundEffects&&!toggleFeedback)||voices==null||appSuspended)return false;
            var settings=SoundSettings(kind);if(settings==null)return false;
            float now=Time.unscaledTime;
            if(now-lastSoundAt[(int)kind]<Mathf.Max(0,settings.minimumInterval))return false;
            int count=0,flood=0,available=-1;
            for(int i=0;i<voices.Length;i++)
            {
                if(voices[i].Active&&!voices[i].Source.isPlaying&&!voices[i].WasPlaying)voices[i].Active=false;
                if(!voices[i].Active){if(available<0&&i<Mathf.Clamp(audioSourcePoolSize,4,24))available=i;continue;}
                if(voices[i].Kind==kind)count++;if(IsFloodSound(voices[i].Kind))flood++;
            }
            if(count>=Mathf.Max(1,settings.maximumInstances)||available<0||IsFloodSound(kind)&&flood>=Mathf.Clamp(maximumSimultaneousFloodSounds,1,4))return false;
            var clip=settings.ChooseClip();
            if(clip==null&&(kind==FloodSound.ButtonBack||kind==FloodSound.ButtonConfirm||kind==FloodSound.ButtonCancel||kind==FloodSound.ToggleOn||kind==FloodSound.ToggleOff||kind==FloodSound.Slider))clip=buttonClick.ChooseClip();
            if(clip==null)return false;
            if(kind<=FloodSound.PopupClose)
                for(int i=0;i<voices.Length;i++)
                    if(voices[i].Active&&voices[i].Source.clip==clip&&voices[i].StartFrame==Time.frameCount)return false;
            var voice=voices[available];voice.Kind=kind;voice.Active=true;voice.Stopping=false;voice.WasPlaying=false;voice.Age=0;voice.Gain=1;
            voice.ToggleFeedback=toggleFeedback;
            voice.Source.outputAudioMixerGroup=toggleFeedback&&feedbackGroup!=null?feedbackGroup:effectsGroup;
            voice.StartFrame=Time.frameCount;voice.Scale=Mathf.Clamp(volumeScale,0,1.3f);voice.PitchScale=pitchScale;
            voice.Source.clip=clip;voice.Source.pitch=Mathf.Clamp(settings.pitch*pitchScale+FloodAudioRandom.Range(-settings.randomPitch,settings.randomPitch),.25f,2f);
            // Preserve short cue attacks: starting at zero until the next frame can
            // entirely mute a tiny UI click. Cue fades shape release/tails instead.
            float initialBus=VoiceBusGain(voice);
            voice.Source.volume=Mathf.Clamp01(settings.volume*voice.Scale*initialBus);
            voice.Source.Play();lastSoundAt[(int)kind]=now;
            TotalSoundStarts++;soundStartCounts[(int)kind]++;
            return true;
        }
        private void UpdateVoices(float dt)
        {
            for(int i=0;i<voices.Length;i++)
            {
                var v=voices[i];if(!v.Active)continue;
                if(!v.Source.isPlaying){v.Active=false;v.Source.clip=null;continue;}
                var settings=SoundSettings(v.Kind);v.Age+=dt;
                float ramp=Mathf.Max(.005f,settings.fadeDuration);
                v.Gain=Mathf.MoveTowards(v.Gain,v.Stopping?0:1,dt/ramp);
                // Tail ramp prevents hard edges even for clips without their own fade-out.
                float remaining=Mathf.Max(0,v.Source.clip.length-v.Source.time)/Mathf.Max(.25f,v.Source.pitch);
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
        private void OnFlood(FloodAudioSignals.FloodWave wave)
        {
            floodFinishPending=false;waveActive=true;waveStart=Time.time;waveEnd=waveStart+Mathf.Max(0,wave.Duration);
            waveLastStart=Mathf.Max(0,wave.LastWaveStart);waveStep=Mathf.Max(0,wave.WaveStep);
            lastProgressUnscaled=Time.unscaledTime;
            waveAffected=wave.AffectedCubes;waveNew=wave.NewlyCaptured;progressCount=0;
            progressLimit=waveAffected>=minimumCubesForProgress?Mathf.Clamp(maximumProgressSoundsPerFlood,0,8):0;
            float interval=Mathf.Max(minimumProgressInterval,waveLastStart/(progressLimit+1f));
            nextProgress=waveStart+QuantizeWave(interval);
            float size=Mathf.Clamp01(Mathf.Log(1+Mathf.Max(0,waveAffected))/Mathf.Log(1+Mathf.Max(1,largeCaptureThreshold)*4));
            PlaySound(FloodSound.BlockColorChange,1+size*floodVolumeScaling,1+size*floodPitchScaling);
        }
        private float QuantizeWave(float seconds)=>waveStep>0?Mathf.Ceil(seconds/waveStep)*waveStep:seconds;
        private void UpdateFlood()
        {
            if(floodFinishPending)
            {
                if(PlaySound(floodFinishKind)||Time.unscaledTime>=floodFinishDeadline)floodFinishPending=false;
            }
            if(!waveActive)return;
            float now=Time.time;
            if(progressCount<progressLimit&&Time.unscaledTime-lastProgressUnscaled>=minimumProgressInterval&&now>=nextProgress&&now<waveEnd&&nextProgress-waveStart<=waveLastStart)
            {
                float pitch=1+FloodAudioRandom.Range(-floodPitchVariation,floodPitchVariation);
                if(PlaySound(FloodSound.FloodProgress,floodProgressVolume,pitch))lastProgressUnscaled=Time.unscaledTime;progressCount++;
                float spacing=Mathf.Max(minimumProgressInterval,waveLastStart/(progressLimit+1f));
                nextProgress=waveStart+QuantizeWave(Mathf.Max(now-waveStart+minimumProgressInterval,(progressCount+1)*spacing));
            }
            if(now<waveEnd)return;
            waveActive=false;
            // With only a single start/spread clip assigned, there are no synthetic extra events.
            if(resultPending)return; // The result cue is the finishing feedback on the final move.
            floodFinishKind=waveNew>=largeCaptureThreshold&&largeCapture.HasClip?FloodSound.LargeCapture:FloodSound.FloodEnd;
            if(!PlaySound(floodFinishKind)&&SoundSettings(floodFinishKind).HasClip)
            {
                for(int i=0;i<voices.Length;i++)if(voices[i].Active&&IsFloodSound(voices[i].Kind))voices[i].Stopping=true;
                floodFinishPending=true;floodFinishDeadline=Time.unscaledTime+.3f;
            }
        }
        private void RefreshFloodTuning()
        {
            if(!waveActive)return;
            progressLimit=waveAffected>=minimumCubesForProgress?Mathf.Clamp(maximumProgressSoundsPerFlood,0,8):0;
            nextProgress=waveStart+QuantizeWave(Mathf.Max(Time.time-waveStart+minimumProgressInterval,
                (progressCount+1)*Mathf.Max(minimumProgressInterval,waveLastStart/(progressLimit+1f))));
        }
        private void CancelFlood()
        {
            waveActive=false;floodFinishPending=false;
            if(voices!=null)for(int i=0;i<voices.Length;i++)if(voices[i].Active&&IsFloodSound(voices[i].Kind))voices[i].Stopping=true;
        }
    }
}
