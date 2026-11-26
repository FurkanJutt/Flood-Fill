using System.Collections.Generic;
using UnityEngine;
namespace FloodFill.ThreeD.Audio
{
    public sealed partial class FloodAudioManager
    {
        private readonly Dictionary<AudioClip,AudioClip> uiFeedbackClips=new Dictionary<AudioClip,AudioClip>();
        private float lastUIFeedbackAt=float.NegativeInfinity;
        private int lastUIFeedbackFrame=-1;
        private static bool IsUIFeedback(FloodSound kind)=>kind<=FloodSound.ColorSelected||kind==FloodSound.Restart||kind==FloodSound.Pause||kind==FloodSound.Resume||kind==FloodSound.PowerUp;
        // A source can report not-playing before its first DSP block. Keep it reserved
        // briefly, then explicitly stop it if startup failed, rather than reuse pending Play calls.
        private static bool VoiceFinished(Voice voice)=>!voice.Source.isPlaying&&(voice.HasStarted||Time.realtimeSinceStartupAsDouble-voice.RequestedAt>.25);
        private void CancelUIFeedback()
        {
            if(voices==null)return;
            foreach(var voice in voices)if(voice.Active&&IsUIFeedback(voice.Kind))
            {voice.Source.Stop();voice.Source.clip=null;voice.Active=false;voice.WasPlaying=false;}
        }
        private void DisposeUIFeedbackClips()
        {
            CancelUIFeedback();
            foreach(var clip in uiFeedbackClips.Values)if(clip!=null&&!uiFeedbackClips.ContainsKey(clip))Destroy(clip);
            uiFeedbackClips.Clear();
        }
        private void RefreshUIFeedbackClips()
        {
            DisposeUIFeedbackClips();
            foreach(FloodSound kind in System.Enum.GetValues(typeof(FloodSound)))
            {
                if(!IsUIFeedback(kind))continue;var settings=SoundSettings(kind);if(settings==null)continue;
                PrepareUIFeedbackClip(settings.clip);
                if(settings.alternateClips!=null)foreach(var clip in settings.alternateClips)PrepareUIFeedbackClip(clip);
            }
        }
        private void PrepareUIFeedbackClip(AudioClip original)
        {
            if(original==null||uiFeedbackClips.ContainsKey(original))return;
            // Preparation is startup/explicit Refresh work only. A press does no PCM reads,
            // allocations or deferred loading. Streaming clips retain their original fallback.
            uiFeedbackClips.Add(original,original);
            if(original.loadType!=AudioClipLoadType.DecompressOnLoad)return;
            original.LoadAudioData();
            int channels=original.channels,frequency=original.frequency;
            int frames=Mathf.Min(original.samples,frequency*2);if(frames<=0)return;
            var samples=new float[frames*channels];if(!original.GetData(samples,0))return;
            float peak=0;for(int i=0;i<samples.Length;i++)peak=Mathf.Max(peak,Mathf.Abs(samples[i]));
            float threshold=Mathf.Max(.001f,peak*.03f);int first=-1,last=-1,quiet=0;
            int quietFrames=Mathf.Max(1,Mathf.RoundToInt(frequency*.05f));
            for(int frame=0;frame<frames;frame++)
            {
                bool audible=false;for(int channel=0;channel<channels;channel++)if(Mathf.Abs(samples[frame*channels+channel])>=threshold){audible=true;break;}
                if(audible){if(first<0)first=frame;last=frame;quiet=0;}
                else if(first>=0&&++quiet>=quietFrames)break;
            }
            if(first<0)return;
            int start=Mathf.Max(0,first-Mathf.RoundToInt(frequency*.005f));
            int end=Mathf.Min(frames,last+Mathf.RoundToInt(frequency*.015f));
            int count=Mathf.Max(1,Mathf.Min(end-start,Mathf.RoundToInt(frequency*Mathf.Max(.05f,uiFeedbackMaximumDuration))));
            var trimmed=new float[count*channels];System.Array.Copy(samples,start*channels,trimmed,0,trimmed.Length);
            int fade=Mathf.Max(1,Mathf.Min(count/2,Mathf.RoundToInt(frequency*.002f)));
            for(int frame=0;frame<count;frame++)
            {
                float gain=Mathf.Clamp01(Mathf.Min(frame,count-frame-1)/(float)fade);
                for(int channel=0;channel<channels;channel++)trimmed[frame*channels+channel]*=gain;
            }
            var cue=AudioClip.Create(original.name+" (UI feedback)",count,channels,frequency,false);cue.SetData(trimmed,0);
            uiFeedbackClips[original]=cue;
        }
    }
}
