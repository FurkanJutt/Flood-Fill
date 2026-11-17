#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FloodFill.EditorTools
{
    [InitializeOnLoad]
    public static class FloodAudioVerification
    {
        private const string Running="FloodAudioVerification.Running";
        private static IEnumerator tests;
        private static double waitUntil,deadline;
        private static int assertions;
        private static readonly List<string> report=new List<string>();
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static FloodAudioVerification(){if(SessionState.GetBool(Running,false))EditorApplication.update+=Tick;}
        public static void BeginBatch()
        {
            // Batch verification runs in an isolated copy; isolate preferences/session files too.
            if(!Application.isBatchMode)throw new InvalidOperationException("Use batch mode in a temporary project copy.");
            PlayerSettings.companyName="FloodAudioVerification";PlayerSettings.productName="FloodAudioVerification";
            foreach(string key in new[]{"Master","Music","Sfx","MusicEnabled","SfxEnabled"})PlayerPrefs.DeleteKey("FloodFill3D.Audio."+key);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            SessionState.SetBool(Running,true);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            if(tests==null){tests=Run();deadline=EditorApplication.timeSinceStartup+180;}
            if(EditorApplication.timeSinceStartup>deadline){Finish(new Exception("Verification timed out."));return;}
            if(EditorApplication.timeSinceStartup<waitUntil)return;
            try
            {
                if(!tests.MoveNext()){Finish(null);return;}
                waitUntil=EditorApplication.timeSinceStartup+(tests.Current is float seconds?seconds:0);
            }
            catch(Exception error){Finish(error);}
        }
        private static void Finish(Exception error)
        {
            Time.timeScale=1;SessionState.SetBool(Running,false);EditorApplication.update-=Tick;
            report.Add(error==null?$"PASS: {assertions} assertions; native Play Mode verification complete.":"FAILED: "+error);
            Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/FloodAudioVerification.txt",report);
            Debug.Log(string.Join("\n",report));EditorApplication.Exit(error==null?0:1);
        }
        private static void Check(bool condition,string message)
        {assertions++;if(!condition)throw new InvalidOperationException(message);}
        private static object Get(object target,string field)=>target.GetType().GetField(field,Flags).GetValue(target);
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,Flags).SetValue(target,value);
        private static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Flags).Invoke(target,args);
        private static T Find<T>(string name) where T:Component
        {foreach(var value in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include))if(value.name==name)return value;throw new Exception("Missing control: "+name);}
        private static AudioClip Tone(string name,int frequency=440)
        {
            var clip=AudioClip.Create(name,44100,1,22050,false);var samples=new float[44100];
            for(int i=0;i<samples.Length;i++)samples[i]=.02f*Mathf.Sin(i*2*Mathf.PI*frequency/22050f)*Mathf.Min(1,i/200f)*Mathf.Min(1,(samples.Length-i)/200f);
            clip.SetData(samples,0);return clip;
        }
        private static void Counter(FloodFillGameManager3D game,int budget,int used)
        {Set(game,"currentMoveBudget",budget);Set(game,"<MoveCount>k__BackingField",used);Call(game,"RefreshUI");}
        private static IEnumerator Run()
        {
            yield return .6f;
            var audio=FloodAudioManager.Instance;var game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();
            Check(audio!=null&&game!=null,"Scene audio/game initialization");
            var mixer=(AudioMixer)Get(audio,"mixer");Check(mixer!=null,"Real mixer assigned");
            Check(mixer.FindMatchingGroups("Music").Length==1&&mixer.FindMatchingGroups("SFX").Length==1,"Mixer group routing");
            foreach(string parameter in new[]{"MasterVolume","MusicVolume","SfxVolume"})Check(mixer.GetFloat(parameter,out _),"Exposed mixer parameter "+parameter);
            var music=(AudioSource[])Get(audio,"musicSources");
            Check(music.Length==2&&music[0].loop&&music[1].loop,"Exactly two looping music sources");
            Check(audio.GetComponents<AudioSource>().Length==audio.audioSourcePoolSize+2,"Fixed reusable source pool");
            Check(audio.CurrentMusicState==FloodMusicState.MainMenu,"Menu music state at startup");
            Check(music[0].isPlaying||music[1].isPlaying,"Menu clip playing");
            int starts=audio.TotalMusicStarts;FloodAudioSignals.Popup(true);FloodAudioSignals.Popup(false);yield return .1f;
            Check(audio.TotalMusicStarts==starts,"Popups do not restart music");
            var rngBefore=UnityEngine.Random.state;audio.TestButtonClick();Check(rngBefore.Equals(UnityEngine.Random.state),"Audio does not consume gameplay RNG");
            bool attackPresent=false;
            foreach(object voice in (Array)Get(audio,"voices"))
                if((bool)Get(voice,"Active")&&(FloodSound)Get(voice,"Kind")==FloodSound.ButtonClick&&((AudioSource)Get(voice,"Source")).volume>0)attackPresent=true;
            Check(attackPresent,"Short cue attack is audible immediately, before the next frame");
            report.Add("PASS menu initialization, looping, routing, source count, popup continuity.");

            audio.defaultFadeDuration=.08f;audio.defaultCrossfadeDuration=.12f;audio.gameplayCrossfadeDuration=.12f;
            audio.mainMenuFadeIn=.12f;audio.mainMenuFadeOut=.12f;audio.musicTransitionCooldown=.02f;
            var tone=Tone("Audio verification short cue");
            foreach(FloodSound sound in Enum.GetValues(typeof(FloodSound)))
            {var settings=audio.SoundSettings(sound);settings.clip=tone;settings.minimumInterval=0;settings.maximumInstances=2;settings.volume=.1f;settings.randomPitch=0;}
            audio.sliderInteraction.minimumInterval=.12f;
            // The real generated Play button hides its own screen before the audio callback runs.
            var play=Find<Button>("PLAYButton");audio.BindControl(play,"PLAY");audio.BindControl(play,"PLAY");
            int confirms=audio.SoundStartCount(FloodSound.ButtonConfirm);play.onClick.Invoke();
            Check(audio.SoundStartCount(FloodSound.ButtonConfirm)==confirms+1,"Button has exactly one sound after repeat binding");
            for(int i=0;i<200&&game.State!=FloodFillGameManager3D.GameState.Playing;i++)yield return .05f;
            Check(game.State==FloodFillGameManager3D.GameState.Playing,"Generated level becomes playable");
            Check(audio.SoundStartCount(FloodSound.LevelStarted)==1,"Level start cue once");
            Counter(game,35,0);audio.ReturnToGameMusic();yield return .35f;
            Check(audio.CurrentMusicState==FloodMusicState.Normal,"35 remaining is normal");
            Counter(game,35,17);yield return .2f;Check(audio.CurrentMusicState==FloodMusicState.Normal,"18 remaining is normal");
            Counter(game,35,18);yield return .25f;Check(audio.CurrentMusicState==FloodMusicState.Medium,"17 remaining is medium");
            Counter(game,35,25);yield return .25f;Check(audio.CurrentMusicState==FloodMusicState.Hard,"10 remaining is hard");
            Counter(game,15,5);yield return .1f;Check(audio.CurrentMusicState==FloodMusicState.Hard,"Hard has priority above 50 percent");
            starts=audio.TotalMusicStarts;Call(game,"RefreshUI");Call(game,"RefreshUI");yield return .05f;
            Check(audio.TotalMusicStarts==starts,"Repeated UI/game notifications do not restart track");
            audio.enableDynamicMusic=false;audio.RefreshAudioSettings();yield return .2f;Check(audio.CurrentMusicState==FloodMusicState.Normal,"Dynamic toggle refresh");
            audio.enableDynamicMusic=true;audio.RefreshAudioSettings();yield return .2f;
            report.Add("PASS authoritative move thresholds, hard priority, dynamic refresh, no repeated restarts.");

            audio.TestNormalMusic();yield return .03f;audio.TestMediumMusic();yield return .03f;audio.TestHardMusic();
            for(int frame=0;frame<35;frame++)
            {
                int playing=0;foreach(var source in music)if(source.isPlaying)playing++;
                Check(playing<=2,"Interrupted transitions never overlap three tracks");
                var gain=(float[])Get(audio,"musicGain");Check(gain[0]*gain[0]+gain[1]*gain[1]<=1.001f,"Crossfade power bounded");
                yield return .01f;
            }
            Check(audio.CurrentMusicClip==audio.hardMusic.name&&!audio.IsCrossfading,"Rapid interruption settles on hard");
            int moves=game.MoveCount;audio.TestMainMenuMusic();yield return .25f;
            var changed=Tone("Changed menu assignment",330);audio.mainMenuMusic=changed;audio.RefreshAudioSettings();
            // Refresh exits preview and follows the real gameplay state; switch to the menu first.
            FloodAudioSignals.Menu(true);yield return .4f;
            Check(audio.CurrentMusicClip==changed.name,"Live assignment refresh selects the newly assigned clip");
            starts=audio.TotalMusicStarts;audio.RefreshAudioSettings();yield return .1f;
            Check(audio.TotalMusicStarts==starts&&game.MoveCount==moves,"Unchanged refresh and previews leave playback/counter intact");
            report.Add("PASS interrupted equal-power fades, live clip assignment, preview safety and refresh idempotence.");

            Find<Button>("SETTINGSButton").onClick.Invoke();yield return .05f;
            var master=Find<Slider>("MASTER VOLUME Slider");var musicSlider=Find<Slider>("MUSIC VOLUME Slider");var sfx=Find<Slider>("SOUND EFFECTS Slider");
            master.value=.3f;musicSlider.value=.2f;sfx.value=.5f;
            Check(mixer.GetFloat("MasterVolume",out float db)&&Mathf.Abs(db-FloodAudioPolicy.Decibels(.3f))<.05f,"Master slider immediate dB");
            Check(mixer.GetFloat("MusicVolume",out db)&&Mathf.Abs(db-FloodAudioPolicy.Decibels(.2f))<.05f,"Music slider immediate dB");
            Check(mixer.GetFloat("SfxVolume",out db)&&Mathf.Abs(db-FloodAudioPolicy.Decibels(.5f))<.05f,"SFX slider immediate dB");
            audio.SetMusicEnabled(false);Check(mixer.GetFloat("MusicVolume",out db)&&db<=-79.9f&&Mathf.Approximately(audio.musicVolume,.2f),"Music mute preserves volume");
            audio.SetMusicEnabled(true);Check(mixer.GetFloat("MusicVolume",out db)&&Mathf.Abs(db-FloodAudioPolicy.Decibels(.2f))<.05f,"Music unmute restores volume");
            audio.SetSfxEnabled(false);Check(mixer.GetFloat("SfxVolume",out db)&&db<=-79.9f,"SFX mute independent");audio.SetSfxEnabled(true);
            int slides=audio.SoundStartCount(FloodSound.Slider);for(int i=0;i<20;i++)musicSlider.value=.2f+i*.001f;
            Check(audio.SoundStartCount(FloodSound.Slider)-slides<=1,"Slider sound throttled");
            audio.SaveSettings();Check(Mathf.Approximately(PlayerPrefs.GetFloat("FloodFill3D.Audio.Master"),.3f),"Namespaced settings persistence");
            audio.audioSourcePoolSize=14;audio.RefreshAudioSettings();
            Check(audio.GetComponents<AudioSource>().Length==16,"Explicit pool growth creates only the requested sources");
            audio.audioSourcePoolSize=12;audio.RefreshAudioSettings();yield return .1f;
            Check(audio.GetComponents<AudioSource>().Length==14,"Pool shrink returns to the configured count");
            report.Add("PASS existing settings UI sliders, immediate exposed volumes, independent mute, persistence, throttling, pool refresh.");

            audio.StopAllAudio();yield return .15f;audio.ReturnToGameMusic();FloodAudioSignals.Menu(false);
            Counter(game,35,0);yield return .2f;
            int selected=audio.SoundStartCount(FloodSound.ColorSelected);game.SelectColor(1);game.SelectColor(1);game.SelectColor(-1);
            Check(audio.SoundStartCount(FloodSound.ColorSelected)==selected+1,"Only accepted color selections sound");
            var color=Find<Button>("Color_2");selected=audio.SoundStartCount(FloodSound.ColorSelected);
            int clicks=audio.SoundStartCount(FloodSound.ButtonClick);color.onClick.Invoke();
            Check(audio.SoundStartCount(FloodSound.ColorSelected)==selected+1&&audio.SoundStartCount(FloodSound.ButtonClick)==clicks,"Palette generic click suppressed");
            audio.StopAllAudio();yield return .15f;audio.ReturnToGameMusic();
            audio.maximumSimultaneousFloodSounds=2;audio.maximumProgressSoundsPerFlood=3;audio.minimumCubesForProgress=1;
            var board=game.BoardManager;var voxel=board.StartingVoxel;game.SelectColor((voxel.ColorIndex+1)%6);
            int floodStarts=audio.SoundStartCount(FloodSound.BlockColorChange);int progress=audio.SoundStartCount(FloodSound.FloodProgress);
            Call(game,"HandleVoxelClicked",voxel);
            Check(audio.SoundStartCount(FloodSound.BlockColorChange)==floodStarts+1,"One primary cue from actual recolor handler");
            Time.timeScale=0;int frozen=audio.SoundStartCount(FloodSound.FloodProgress);
            audio.TestMediumMusic();yield return .25f;
            Check(audio.SoundStartCount(FloodSound.FloodProgress)==frozen&&!audio.IsCrossfading,"Wave clock pauses; music/UI fades stay unscaled");
            audio.PlaySound(FloodSound.ButtonClick);Check(audio.ActiveSfxCount>0,"UI SFX play at timeScale zero");Time.timeScale=1;
            for(float elapsed=0;elapsed<board.LastRecolorAnimationDuration+.6f;elapsed+=.05f)
            {
                Check(CountFloodVoices(audio)<=2,"Flood concurrency bounded");yield return .05f;
            }
            Check(audio.SoundStartCount(FloodSound.FloodProgress)-progress<=3,"Progress events capped independently of cube count");
            FloodAudioSignals.Restart();FloodAudioSignals.ResetLevel();yield return .1f;Check(CountFloodVoices(audio)==0,"Restart fades/reset flood voices");
            report.Add("PASS accepted-color de-duplication, real flood timing, scaled pause synchronization, unscaled audio, flood caps/reset.");

            audio.StopAllAudio();yield return .15f;audio.ReturnToGameMusic();
            Set(board,"<LastRecolorAnimationDuration>k__BackingField",0f);
            int complete=audio.SoundStartCount(FloodSound.LevelCompleted);FloodAudioSignals.Result(true);FloodAudioSignals.Result(true);yield return .1f;
            Check(audio.SoundStartCount(FloodSound.LevelCompleted)==complete+1,"Completion once");
            FloodAudioSignals.ResetLevel();int failed=audio.SoundStartCount(FloodSound.LevelFailed);FloodAudioSignals.Result(false);FloodAudioSignals.Result(false);yield return .1f;
            Check(audio.SoundStartCount(FloodSound.LevelFailed)==failed+1,"Failure once");
            int pauses=audio.SoundStartCount(FloodSound.Pause);game.PauseGame();game.PauseGame();game.ResumeGame();game.ResumeGame();
            Check(audio.SoundStartCount(FloodSound.Pause)==pauses+1,"Pause/resume duplicate callbacks guarded");
            audio.StopAllAudio();yield return .1f;audio.ReturnToGameMusic();
            int sourceCount=audio.GetComponents<AudioSource>().Length;
            game.RestartCurrentGame();game.RestartCurrentGame();
            for(int i=0;i<200&&game.State!=FloodFillGameManager3D.GameState.Playing;i++)yield return .05f;
            Check(game.State==FloodFillGameManager3D.GameState.Playing&&game.MoveCount==0,"Rapid restarts complete safely");
            Check(audio.GetComponents<AudioSource>().Length==sourceCount&&audio.StartingMoveBudget==game.CurrentMoveBudget&&audio.RemainingMoves==game.CurrentMoveBudget,"Restart uses restored authoritative budget without extra sources");
            report.Add("PASS rapid real level restarts and recalculated authoritative music budget.");
            // Free-flood can repaint a small outside component into an existing captured
            // area. Previously captured mass must not inflate this move's sound intensity.
            var all=(List<VoxelCell3D>)Get(board,"allVoxels");var previousCaptured=new HashSet<VoxelCell3D>();
            foreach(var cell in all)if(cell.IsCaptured)previousCaptured.Add(cell);
            var directions=new[]{Vector3Int.left,Vector3Int.right,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
            VoxelCell3D outside=null;
            foreach(var cell in all)
            {
                if(cell.IsCaptured||cell.ColorIndex==board.CurrentPlayerColor)continue;
                foreach(var direction in directions)
                {
                    var neighbor=board.GetCell(cell.X+direction.x,cell.Y+direction.y,cell.Z+direction.z);
                    if(neighbor!=null&&neighbor.IsCaptured){outside=cell;break;}
                }
                if(outside!=null)break;
            }
            Check(outside!=null,"Outside component adjacent to captured region exists");
            var expectedAffected=new HashSet<VoxelCell3D>{outside};var pending=new Queue<VoxelCell3D>();pending.Enqueue(outside);
            while(pending.Count>0)
            {
                var cell=pending.Dequeue();
                foreach(var direction in directions)
                {
                    var neighbor=board.GetCell(cell.X+direction.x,cell.Y+direction.y,cell.Z+direction.z);
                    if(neighbor!=null&&neighbor.ColorIndex==outside.ColorIndex&&expectedAffected.Add(neighbor))pending.Enqueue(neighbor);
                }
            }
            FloodAudioSignals.FloodWave observed=default;Action<FloodAudioSignals.FloodWave> observer=wave=>observed=wave;
            FloodAudioSignals.FloodStarted+=observer;
            try {game.SelectColor(board.CurrentPlayerColor);Call(game,"HandleVoxelClicked",outside);}
            finally {FloodAudioSignals.FloodStarted-=observer;}
            foreach(var cell in all)if(cell.IsCaptured&&!previousCaptured.Contains(cell))expectedAffected.Add(cell);
            Check(observed.AffectedCubes==expectedAffected.Count,"Wave intensity counts the changed union, excluding unchanged captured mass");
            Check(observed.LastWaveStart<=observed.Duration,"Wave timing includes only real animated waves");
            FloodAudioSignals.ResetLevel();yield return .1f;
            report.Add("PASS free-flood outside-component merge metadata and actual animated-wave bounds.");
            audio.largeCapture.clip=null;audio.largeCapture.alternateClips=new[]{tone};
            int large=audio.SoundStartCount(FloodSound.LargeCapture);
            FloodAudioSignals.BeginFlood(new FloodAudioSignals.FloodWave(100,100,.08f,0,.065f));yield return .2f;
            Check(audio.SoundStartCount(FloodSound.LargeCapture)==large+1,"Alternate-only large capture is selected by the wave scheduler");
            audio.mainMenuMusic=null;audio.normalMusic=null;audio.mediumMusic=null;audio.hardMusic=null;audio.RefreshAudioSettings();yield return .25f;
            Check(!music[0].isPlaying&&!music[1].isPlaying,"Missing clips resolve safely to silence");
            audio.buttonClick.clip=null;audio.buttonClick.alternateClips=new[]{tone};yield return 2.1f;
            Check(audio.PlaySound(FloodSound.ButtonClick),"Alternate-only clip assignment works");
            report.Add("PASS single result cues, pause/resume guard, missing clips, alternate-only sound configuration.");

            var instance=audio;var extra=SceneManager.CreateScene("Audio scene transition check");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(FloodAudioSetup.PrefabPath);var duplicate=UnityEngine.Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(duplicate,extra);
            yield return .1f;
            Check(UnityEngine.Object.FindObjectsByType<FloodAudioManager>().Length==1&&FloodAudioManager.Instance==instance,"Duplicate manager rejected across scenes");
            SceneManager.UnloadSceneAsync(extra);yield return .1f;Check(FloodAudioManager.Instance==instance,"Manager persists after scene unload");
            audio.mainMenuMusic=tone;FloodAudioSignals.Menu(true);audio.RefreshAudioSettings();yield return .2f;
            Call(audio,"OnApplicationPause",true);yield return .1f;Call(audio,"OnApplicationPause",false);yield return .15f;
            Check(music[0].isPlaying||music[1].isPlaying,"App background/resume restores owned sources");
            audio.masterVolume=.37f;audio.musicVolume=.26f;audio.soundEffectsVolume=.48f;audio.enableMusic=false;audio.SaveSettings();
            UnityEngine.Object.Destroy(audio.gameObject);yield return .1f;
            var recreated=UnityEngine.Object.Instantiate(prefab);yield return .2f;audio=FloodAudioManager.Instance;
            Check(Mathf.Approximately(audio.masterVolume,.37f)&&Mathf.Approximately(audio.musicVolume,.26f)&&Mathf.Approximately(audio.soundEffectsVolume,.48f)&&!audio.enableMusic,"Saved levels/mute restored by fresh manager initialization");
            Check(audio.GetComponents<AudioSource>().Length==audio.audioSourcePoolSize+2,"Reinitialization has no surplus sources");
            report.Add("PASS scene persistence, duplicate prevention, app suspension/resume, startup persistence and source reuse.");
        }
        private static int CountFloodVoices(FloodAudioManager audio)
        {
            int count=0;var voices=(Array)Get(audio,"voices");
            foreach(object voice in voices)
            {
                if(!(bool)Get(voice,"Active"))continue;var kind=(FloodSound)Get(voice,"Kind");
                if(kind==FloodSound.BlockColorChange||kind==FloodSound.FloodProgress||kind==FloodSound.FloodEnd||kind==FloodSound.LargeCapture)count++;
            }
            return count;
        }
    }
}
#endif
