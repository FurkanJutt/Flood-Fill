using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using FloodFill.ThreeD.Audio;
using UnityEditor;
using UnityEngine;
namespace FloodFill.EditorTools
{
    public static class FloodButtonAudioVerification3D
    {
        private static FloodAudioManager audio;
        private static Button button;
        private static int stage,checks,starts,burst;
        private static float readyAt;
        private static double deadline;
        private static AudioClip tone;
        private static AudioSource previousSource;
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static object Get(object owner,string field)=>owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(owner);
        private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(owner,value);
        private static void Check(bool value,string text){checks++;if(!value)throw new Exception(text+" [stage "+stage+"]");}
        private static IEnumerable<object> Voices()=>((Array)Get(audio,"voices")).Cast<object>().Where(v=>(bool)Get(v,"Active"));
        private static bool Interaction(object v){var k=(FloodSound)Get(v,"Kind");return k<=FloodSound.ColorSelected||k==FloodSound.Restart||k==FloodSound.Pause||k==FloodSound.Resume||k==FloodSound.PowerUp;}
        public static void BeginRapidBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated project.");
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            PlayerSettings.companyName=PlayerSettings.productName="FloodButtonAudioVerification";PlayerPrefs.DeleteAll();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Audio rapid test timeout");
                audio=FloodAudioManager.Instance;var ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();if(audio==null||ui==null||!ui.IsBuilt||audio.GetComponents<AudioSource>().Length==0)return;
                if(stage==0)
                {
                    audio.SetSfxEnabled(true);audio.SetMasterVolume(1);audio.SetSfxVolume(1);
                    var cache=(Dictionary<AudioClip,AudioClip>)Get(audio,"uiFeedbackClips");
                    foreach(var kind in new[]{FloodSound.ButtonClick,FloodSound.ButtonBack,FloodSound.ButtonConfirm,FloodSound.ColorSelected})
                    {
                        var original=audio.SoundSettings(kind).clip;Check(cache.TryGetValue(original,out var cue)&&cue!=original,"Real UI clip is prepared without editing the asset");
                        Check(cue.length<=audio.uiFeedbackMaximumDuration+.001f,"Prepared UI clip has a bounded short lifetime");
                        var samples=new float[cue.samples*cue.channels];Check(cue.GetData(samples,0),"Prepared PCM is ready before a press");
                        int n=Math.Min(samples.Length,(int)(cue.frequency*.02f)*cue.channels);Check(samples.Take(n).Any(x=>Mathf.Abs(x)>.005f),"Leading silence removed from real UI cue");
                    }
                    var root=new GameObject("Rapid click fixture",typeof(RectTransform),typeof(Image),typeof(Button));root.transform.SetParent(ui.transform,false);button=root.GetComponent<Button>();audio.BindControl(button,"PLAY");audio.BindControl(button,"PLAY");
                    starts=audio.TotalSoundStarts;for(int i=0;i<100;i++)button.onClick.Invoke();
                    Check(audio.TotalSoundStarts==starts+1,"Repeated binding and 100 same-frame clicks produce one sound");
                    Check(Voices().Count(Interaction)==1,"Only one UI feedback voice");
                    Check(!audio.PlaySound(FloodSound.ButtonBack)&&!audio.PlaySound(FloodSound.ColorSelected),"Shared cooldown suppresses cross-button duplicate feedback");
                    previousSource=(AudioSource)Get(Voices().Single(Interaction),"Source");readyAt=Time.unscaledTime+.1f;stage=1;return;
                }
                if(stage==1)
                {
                    if(Time.unscaledTime<readyAt)return;
                    if(burst==0)
                    {
                        button.onClick.Invoke();Check(Voices().Count(Interaction)==1,"Retrigger keeps one UI voice");
                        Check((AudioSource)Get(Voices().Single(Interaction),"Source")==previousSource,"Accepted latest click reuses the feedback source");
                    }
                    else Check(audio.PlaySound(burst%2==0?FloodSound.ButtonBack:FloodSound.ButtonConfirm),"Spaced rapid cross-button press accepted");
                    Check(Voices().Count(Interaction)<=1,"Rapid input cannot stack feedback voices");
                    burst++;readyAt=Time.unscaledTime+(burst<8?.1f:.4f);if(burst>=8){starts=audio.TotalSoundStarts;stage=2;}return;
                }
                if(stage==2)
                {
                    if(Time.unscaledTime<readyAt)return;
                    Check(audio.TotalSoundStarts==starts&&Voices().All(v=>!Interaction(v)),"No queued or delayed feedback plays after release");
                    Check(audio.PlaySound(FloodSound.ButtonClick),"New click after quiet period works");
                    typeof(FloodAudioManager).GetMethod("OnApplicationPause",Private).Invoke(audio,new object[]{true});
                    Check(Voices().All(v=>!Interaction(v)),"App suspension discards pending UI feedback");
                    typeof(FloodAudioManager).GetMethod("OnApplicationPause",Private).Invoke(audio,new object[]{false});starts=audio.TotalSoundStarts;readyAt=Time.unscaledTime+.35f;stage=3;return;
                }
                if(stage==3)
                {
                    if(Time.unscaledTime<readyAt)return;Check(audio.TotalSoundStarts==starts,"Resume does not replay old button clicks");
                    tone=AudioClip.Create("Unmodified gameplay fixture",44100,1,44100,false);var data=new float[44100];for(int i=0;i<data.Length;i++)data[i]=Mathf.Sin(i*.08f)*.1f;tone.SetData(data,0);
                    audio.levelCompleted.clip=tone;audio.levelCompleted.alternateClips=Array.Empty<AudioClip>();audio.levelCompleted.minimumInterval=0;
                    Check(audio.PlaySound(FloodSound.ButtonBack)&&audio.PlaySound(FloodSound.LevelCompleted),"Gameplay cue coexists with UI feedback");
                    Check(Voices().Any(v=>(FloodSound)Get(v,"Kind")==FloodSound.LevelCompleted&&((AudioSource)Get(v,"Source")).clip==tone&&(float)Get(v,"MaximumAge")==0),"Gameplay cue remains untrimmed with original lifetime");
                    readyAt=Time.unscaledTime+.35f;stage=4;return;
                }
                if(stage==4)
                {
                    if(Time.unscaledTime<readyAt)return;Check(Voices().Any(v=>(FloodSound)Get(v,"Kind")==FloodSound.LevelCompleted),"Long gameplay cue continues beyond UI time limit");
                    audio.SetSfxEnabled(false);starts=audio.TotalSoundStarts;for(int i=0;i<100;i++)button.onClick.Invoke();Check(audio.TotalSoundStarts==starts,"Muted rapid input remains silent");audio.SetSfxEnabled(true);
                    Check(audio.PlaySound(FloodSound.ButtonClick),"Feedback works after unmute");
                    var pending=Voices().Single(Interaction);((AudioSource)Get(pending,"Source")).Stop();Set(pending,"HasStarted",false);Set(pending,"RequestedAt",Time.realtimeSinceStartupAsDouble);
                    audio.levelFailed.clip=tone;audio.levelFailed.alternateClips=Array.Empty<AudioClip>();Check(audio.PlaySound(FloodSound.LevelFailed),"Gameplay can start while UI DSP startup is pending");
                    Check(Voices().Contains(pending),"Not-yet-started feedback voice remains reserved");readyAt=Time.unscaledTime+.35f;stage=5;return;
                }
                if(stage==5)
                {
                    if(Time.unscaledTime<readyAt)return;Check(Voices().All(v=>!Interaction(v)),"Failed pending startup is stopped without late replay");
                    File.WriteAllText("/tmp/FloodButtonAudioVerification.txt",checks+" checks passed: real click PCM preparation/onset, repeated binding, 100-click burst, cross-button throttling, single-voice replacement, no delayed replay, suspend/resume, mute, DSP startup reservation and preserved long gameplay cues. Desktop Unity test; physical-device latency/listening remains unverified. Original saves untouched.\n");EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodButtonAudioVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        public static void AnalyzeBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated project.");
            var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FloodFill/Audio/FloodAudioManager.prefab").GetComponent<FloodAudioManager>();var lines=new List<string>();
            foreach(var kind in new[]{FloodSound.ButtonClick,FloodSound.ButtonBack,FloodSound.ButtonConfirm,FloodSound.ColorSelected})
            {
                var clip=root.SoundSettings(kind).clip;clip.LoadAudioData();var data=new float[clip.samples*clip.channels];if(!clip.GetData(data,0))throw new Exception("Cannot inspect "+clip.name);
                int stride=Mathf.Max(1,(int)(clip.frequency*.005f))*clip.channels;var values=new List<float>();float peak=0;
                for(int i=0;i<data.Length;i+=stride){double sum=0;int n=Math.Min(stride,data.Length-i);for(int j=0;j<n;j++)sum+=data[i+j]*data[i+j];float rms=(float)Math.Sqrt(sum/n);values.Add(rms);peak=Math.Max(peak,rms);}
                var windows=new List<Vector2>();
                for(int i=0;i<values.Count;i++)if(values[i]>peak*.12f){float t=i*.005f;if(windows.Count>0&&t-windows[windows.Count-1].y<.06f){var w=windows[windows.Count-1];w.y=t+.005f;windows[windows.Count-1]=w;}else windows.Add(new Vector2(t,t+.005f));}
                lines.Add(kind+" / "+clip.name+": "+clip.length+"s; audible windows "+string.Join(", ",windows));
            }
            File.WriteAllLines("/tmp/FloodButtonAudioAnalysis.txt",lines);
        }
    }
}
