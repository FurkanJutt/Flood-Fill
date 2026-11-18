using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FloodFill.ThreeD.Audio
{
    [DefaultExecutionOrder(-1000), DisallowMultipleComponent]
    public sealed partial class FloodAudioManager : MonoBehaviour
    {
        public static FloodAudioManager Instance { get; private set; }
        private const string Pref="FloodFill3D.Audio.";
        [BoxGroup("Mixer Routing")] [SerializeField] private AudioMixer mixer;
        [BoxGroup("Mixer Routing")] [SerializeField] private AudioMixerGroup musicGroup;
        [BoxGroup("Mixer Routing")] [SerializeField] private AudioMixerGroup effectsGroup;
        [BoxGroup("Mixer Routing"),Tooltip("Master routing for the SFX mute control confirmation only.")] [SerializeField] private AudioMixerGroup feedbackGroup;
        [BoxGroup("Master Settings"),Range(0,1)] public float masterVolume=1f;
        [BoxGroup("Master Settings"),Range(0,1)] public float musicVolume=.65f;
        [BoxGroup("Master Settings"),Range(0,1)] public float soundEffectsVolume=.8f;
        [BoxGroup("Master Settings")] public bool enableMusic=true;
        [BoxGroup("Master Settings")] public bool enableSoundEffects=true;
        [BoxGroup("Master Settings"),Min(.01f)] public float defaultFadeDuration=.5f;
        [BoxGroup("Master Settings"),Min(.01f)] public float defaultCrossfadeDuration=1.75f;
        [BoxGroup("Master Settings"),Range(4,24)] public int audioSourcePoolSize=12;
        [BoxGroup("Master Settings")] public AnimationCurve fadeCurve=AnimationCurve.EaseInOut(0,0,1,1);
        [BoxGroup("Main Menu Music")] public AudioClip mainMenuMusic;
        [BoxGroup("Main Menu Music"),Range(0,1)] public float mainMenuMusicMultiplier=.8f;
        [BoxGroup("Main Menu Music"),Min(.01f)] public float mainMenuFadeIn=1.5f;
        [BoxGroup("Main Menu Music"),Min(.01f)] public float mainMenuFadeOut=1.5f;
        [BoxGroup("Gameplay Music")] public AudioClip normalMusic;
        [BoxGroup("Gameplay Music")] public AudioClip mediumMusic;
        [BoxGroup("Gameplay Music")] public AudioClip hardMusic;
        [BoxGroup("Gameplay Music"),Range(0,1)] public float normalMusicMultiplier=.7f;
        [BoxGroup("Gameplay Music"),Range(0,1)] public float mediumMusicMultiplier=.7f;
        [BoxGroup("Gameplay Music"),Range(0,1)] public float hardMusicMultiplier=.7f;
        [BoxGroup("Gameplay Music"),Min(.01f)] public float gameplayCrossfadeDuration=1.75f;
        [BoxGroup("Gameplay Music"),Min(0)] public float musicTransitionCooldown=.4f;
        [BoxGroup("Gameplay Music")] public bool enableDynamicMusic=true;
        [BoxGroup("Gameplay Music"),ShowIf(nameof(enableDynamicMusic)),Range(0,1)] [Tooltip("Fraction of starting moves: 0.5 = 50%. Hard music has priority.")] public float mediumThresholdPercentage=.5f;
        [BoxGroup("Gameplay Music"),ShowIf(nameof(enableDynamicMusic)),Min(0)] public int hardThresholdMoves=10;
        [BoxGroup("Gameplay Music"),Range(0,1)] public float resultMusicDuck=.3f;

        [Foldout("UI Sound Effects")] public FloodSoundSettings buttonClick=new FloodSoundSettings();
        [Foldout("UI Sound Effects")] public FloodSoundSettings buttonBack=new FloodSoundSettings();
        [Foldout("UI Sound Effects")] public FloodSoundSettings buttonConfirm=new FloodSoundSettings();
        [Foldout("UI Sound Effects")] public FloodSoundSettings buttonCancel=new FloodSoundSettings();
        [Foldout("UI Sound Effects")] public FloodSoundSettings toggleOn=new FloodSoundSettings();
        [Foldout("UI Sound Effects")] public FloodSoundSettings toggleOff=new FloodSoundSettings();
        [Foldout("UI Sound Effects")] public FloodSoundSettings sliderInteraction=new FloodSoundSettings{minimumInterval=.12f,volume=.35f,maximumInstances=1};
        [Foldout("UI Sound Effects")] public FloodSoundSettings popupOpen=new FloodSoundSettings();
        [Foldout("UI Sound Effects")] public FloodSoundSettings popupClose=new FloodSoundSettings();
        [Foldout("Gameplay Sound Effects")] public FloodSoundSettings colorSelected=new FloodSoundSettings{maximumInstances=1};
        [Foldout("Gameplay Sound Effects"),InspectorName("Color Spread")] public FloodSoundSettings blockColorChange=new FloodSoundSettings{volume=.5f};
        [HideInInspector] public FloodSoundSettings floodProgress=new FloodSoundSettings{volume=.3f,minimumInterval=.12f,maximumInstances=2};
        [HideInInspector] public FloodSoundSettings floodEnd=new FloodSoundSettings{volume=.45f};
        [HideInInspector] public FloodSoundSettings largeCapture=new FloodSoundSettings{volume=.6f,maximumInstances=1};
        [Foldout("Gameplay Sound Effects")] public FloodSoundSettings levelStarted=new FloodSoundSettings();
        [Foldout("Gameplay Sound Effects")] public FloodSoundSettings levelCompleted=new FloodSoundSettings{maximumInstances=1};
        [Foldout("Gameplay Sound Effects")] public FloodSoundSettings levelFailed=new FloodSoundSettings{maximumInstances=1};
        [Foldout("Gameplay Sound Effects")] public FloodSoundSettings outOfMoves=new FloodSoundSettings{maximumInstances=1};
        [Foldout("Gameplay Sound Effects")] public FloodSoundSettings restartLevel=new FloodSoundSettings{maximumInstances=1};
        [Foldout("Gameplay Sound Effects")] public FloodSoundSettings pauseSound=new FloodSoundSettings{maximumInstances=1};
        [Foldout("Gameplay Sound Effects")] public FloodSoundSettings resumeSound=new FloodSoundSettings{maximumInstances=1};
        // Retain old serialized assignments for existing scenes; gameplay uses only Color Spread.
        [HideInInspector] public int minimumCubesForProgress=6,largeCaptureThreshold=30;
        [HideInInspector] public float minimumProgressInterval=.12f;
        [HideInInspector] public int maximumProgressSoundsPerFlood=4,maximumSimultaneousFloodSounds=2;
        [HideInInspector] public float floodProgressVolume=.45f,floodStartPlaybackSeconds=.22f,floodProgressPlaybackSeconds=.14f;
        [HideInInspector] public float floodProgressClipOffset;
        [HideInInspector] public float floodPitchVariation=.025f,floodVolumeScaling=.12f,floodPitchScaling=.05f;
        [BoxGroup("Color Spread"),Min(0),Tooltip("Start of the audible section of the primary clip; skip leading silence.")]
        public float floodStartClipOffset;
        [BoxGroup("Color Spread"),Range(.04f,.5f),Tooltip("Length of the repeating section, prepared once when settings are applied.")]
        public float spreadLoopSeconds=.16f;
        [BoxGroup("Color Spread"),Range(.25f,2f)] public float spreadStartSpeed=.8f;
        [BoxGroup("Color Spread"),Range(.25f,2f)] public float spreadEndSpeed=1.65f;
        [BoxGroup("Color Spread"),Range(.02f,.5f)] public float spreadFadeOutSeconds=.2f;
        [SerializeField,HideInInspector] private AudioClip floodStartOffsetClip;
        [SerializeField,HideInInspector] private AudioClip floodProgressOffsetClip;

        [ShowNativeProperty] public FloodMusicState CurrentMusicState=>requestedMusic;
        [ShowNativeProperty] public string CurrentMusicClip
        {
            get
            {
                // NaughtyAttributes reads diagnostics before Awake and during reloads.
                // An array can exist while its slots/index are not initialized yet.
                if(musicSources==null||activeMusic<0||activeMusic>=musicSources.Length)return "None";
                var source=musicSources[activeMusic];
                var clip=source!=null?source.clip:null;
                return clip!=null?clip.name:"None";
            }
        }
        [ShowNativeProperty] public string CurrentGameState=>game==null?"Main menu":game.State.ToString();
        [ShowNativeProperty] public int RemainingMoves=>game==null?0:Mathf.Max(0,game.CurrentMoveBudget-game.MoveCount);
        [ShowNativeProperty] public int StartingMoveBudget=>game==null?0:game.CurrentMoveBudget;
        [ShowNativeProperty] public float CurrentMusicVolume=>enableMusic?musicVolume:0;
        [ShowNativeProperty] public float CurrentSfxVolume=>enableSoundEffects?soundEffectsVolume:0;
        [ShowNativeProperty] public bool IsCrossfading=>crossfading;
        [ShowNativeProperty] public bool IsColorSpreadPlaying=>spreadVoice!=null&&spreadVoice.Source.isPlaying;
        [ShowNativeProperty] public float ColorSpreadProgress=>waveActive?Mathf.InverseLerp(waveStart,waveLastStart,Time.time):0;
        [ShowNativeProperty] public int TotalMusicStarts { get; private set; }
        [ShowNativeProperty] public int TotalSoundStarts { get; private set; }
        [ShowNativeProperty] public int ActiveSfxCount { get {int count=0;if(voices!=null)for(int i=0;i<voices.Length;i++)if(voices[i]!=null&&voices[i].Source!=null&&voices[i].Active)count++;return count;} }
        public event System.Action SettingsChanged;
        private FloodFillGameManager3D game;
        private FloodAudioUIBinder ui;
        private bool ready,menuContext=true,paused,appSuspended,preview,stoppedByUser,resultHandled,levelHasStarted;
        private AudioClip levelStartConfirmationClip;
        private bool resultPending,resultWon;
        private float resultAt;
        private float duck=1f,duckTarget=1f;
        private bool waveActive;
        private float waveStart,waveEnd,waveLastStart;

        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);
            LoadSettings();RefreshFloodClipBindings();CreateSources();ui=new FloodAudioUIBinder();
            FloodAudioSignals.ManagerAvailable+=AttachGame;FloodAudioSignals.ManagerRemoved+=DetachGame;
            FloodAudioSignals.ColorAccepted+=OnColorSelected;FloodAudioSignals.FloodStarted+=OnFlood;
            FloodAudioSignals.LevelReset+=OnLevelReset;FloodAudioSignals.RestartRequested+=OnRestart;
            FloodAudioSignals.PauseChanged+=OnPause;FloodAudioSignals.ResultScheduled+=OnResult;
            FloodAudioSignals.MenuContextChanged+=OnMenu;FloodAudioSignals.PopupChanged+=OnPopup;
            SceneManager.sceneLoaded+=OnSceneLoaded;
        }
        private void Start()
        {
            if(Instance!=this)return;
            ready=true;ApplyVolumes();FindGameOnSceneLoad();ui.Scan(true);ReevaluateMusic(true);
        }
        private void OnSceneLoaded(Scene scene,LoadSceneMode mode)
        {if(ready){FindGameOnSceneLoad();ui.Scan(true);}}
        private void FindGameOnSceneLoad()
        {
            // Scene-load discovery only, never in Update or a flood callback.
            var manager=FindAnyObjectByType<FloodFillGameManager3D>();
            if(manager!=null)AttachGame(manager);else if(game==null){menuContext=true;ReevaluateMusic(true);}
        }
        private void AttachGame(FloodFillGameManager3D manager)
        {
            if(game==manager)return;
            if(game!=null){game.UIChanged-=OnGameChanged;game.ManagerReady-=OnGameChanged;}
            game=manager;if(game!=null){game.UIChanged+=OnGameChanged;game.ManagerReady+=OnGameChanged;}
            levelHasStarted=false;levelStartConfirmationClip=null;resultHandled=false;OnGameChanged();
        }
        private void DetachGame(FloodFillGameManager3D manager)
        {if(game!=manager)return;game.UIChanged-=OnGameChanged;game.ManagerReady-=OnGameChanged;game=null;OnMenu(true);}
        private void OnGameChanged()
        {
            if(game==null)return;
            if(!menuContext&&game.State==FloodFillGameManager3D.GameState.Playing&&!levelHasStarted)
            {levelHasStarted=true;PlaySound(FloodSound.LevelStarted);levelStartConfirmationClip=null;}
            if(!menuContext&&(game.State==FloodFillGameManager3D.GameState.Won||game.State==FloodFillGameManager3D.GameState.Lost))
                OnResult(game.State==FloodFillGameManager3D.GameState.Won);
            if(!preview)duckTarget=!menuContext&&resultHandled?resultMusicDuck:1f;
            ReevaluateMusic(false);
        }
        private void OnMenu(bool value)
        {
            if(menuContext==value){OnGameChanged();return;}
            menuContext=value;preview=false;stoppedByUser=false;
            if(value){CancelFlood();levelStartConfirmationClip=null;resultPending=false;duckTarget=1;paused=false;}
            OnGameChanged();ReevaluateMusic(true);
        }
        private void OnLevelReset()
        {CancelFlood();resultPending=false;resultHandled=false;levelHasStarted=false;levelStartConfirmationClip=null;paused=false;duckTarget=1;preview=false;stoppedByUser=false;}
        private void OnRestart(){CancelFlood();PlaySound(FloodSound.Restart);}
        private void OnPause(bool value)
        {if(paused==value)return;paused=value;PlaySound(value?FloodSound.Pause:FloodSound.Resume);}
        private void OnPopup(bool value)=>PlaySound(value?FloodSound.PopupOpen:FloodSound.PopupClose);
        private void OnColorSelected()=>PlaySound(FloodSound.ColorSelected);
        private void OnResult(bool won)
        {
            if(resultHandled)return;resultHandled=true;resultPending=true;resultWon=won;
            resultAt=Time.time+(game!=null&&game.BoardManager!=null?game.BoardManager.LastRecolorAnimationDuration:0f);
            duckTarget=resultMusicDuck;
            if(!won&&(outOfMoves.clip==null||outOfMoves.clip!=levelFailed.clip))PlaySound(FloodSound.OutOfMoves);
        }
        private void Update()
        {
            if(!ready||appSuspended)return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            duck=Mathf.MoveTowards(duck,duckTarget,dt/Mathf.Max(.01f,defaultFadeDuration));
            UpdateMusic(dt);UpdateVoices(dt);UpdateFlood();ui.Scan();
            if(resultPending&&Time.time>=resultAt){resultPending=false;PlaySound(resultWon?FloodSound.LevelCompleted:FloodSound.LevelFailed);}
        }
        public void BindControl(Selectable control,string label=null,bool silent=false)=>ui?.Bind(control,label,silent);
        private void LoadSettings()
        {
            masterVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(Pref+"Master",masterVolume));
            musicVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(Pref+"Music",musicVolume));
            soundEffectsVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(Pref+"Sfx",soundEffectsVolume));
            enableMusic=PlayerPrefs.GetInt(Pref+"MusicEnabled",enableMusic?1:0)!=0;
            enableSoundEffects=PlayerPrefs.GetInt(Pref+"SfxEnabled",enableSoundEffects?1:0)!=0;
        }
        public void SaveSettings()
        {
            if(Instance!=this)return;
            PlayerPrefs.SetFloat(Pref+"Master",masterVolume);PlayerPrefs.SetFloat(Pref+"Music",musicVolume);PlayerPrefs.SetFloat(Pref+"Sfx",soundEffectsVolume);
            PlayerPrefs.SetInt(Pref+"MusicEnabled",enableMusic?1:0);PlayerPrefs.SetInt(Pref+"SfxEnabled",enableSoundEffects?1:0);PlayerPrefs.Save();
        }
        private void ApplyVolumes()
        {
            masterVolume=Mathf.Clamp01(masterVolume);musicVolume=Mathf.Clamp01(musicVolume);soundEffectsVolume=Mathf.Clamp01(soundEffectsVolume);
            if(mixer!=null){mixer.SetFloat("MasterVolume",FloodAudioPolicy.Decibels(masterVolume));mixer.SetFloat("MusicVolume",FloodAudioPolicy.Decibels(enableMusic?musicVolume:0));mixer.SetFloat("SfxVolume",FloodAudioPolicy.Decibels(enableSoundEffects?soundEffectsVolume:0));}
            SettingsChanged?.Invoke();
        }
        public void SetMasterVolume(float value){masterVolume=value;ApplyVolumes();}
        public void SetMusicVolume(float value){musicVolume=value;ApplyVolumes();}
        public void SetSfxVolume(float value){soundEffectsVolume=value;ApplyVolumes();}
        public void SetMusicEnabled(bool value){enableMusic=value;ApplyVolumes();}
        public void SetSfxEnabled(bool value){enableSoundEffects=value;ApplyVolumes();}
        public void SetSfxEnabledFromUI(bool value)
        {
            if(enableSoundEffects==value)return;
            SetSfxEnabled(value);
            // Same shared cue as the music toggle. Only this control's confirmation
            // bypasses its own mute; Master and the stored SFX level still apply.
            PlaySoundInternal(value?FloodSound.ToggleOn:FloodSound.ToggleOff,1f,1f,true);
        }
        private void OnApplicationPause(bool value)
        {
            if(appSuspended==value||musicSources==null)return;
            if(value)SaveSettings();appSuspended=value;
            for(int i=0;i<2;i++){if(value){musicWasPlaying[i]=musicSources[i].isPlaying;if(musicWasPlaying[i])musicSources[i].Pause();}else if(musicWasPlaying[i])musicSources[i].UnPause();}
            if(voices!=null)for(int i=0;i<voices.Length;i++){if(value){voices[i].WasPlaying=voices[i].Source.isPlaying;if(voices[i].WasPlaying)voices[i].Source.Pause();}else if(voices[i].WasPlaying)voices[i].Source.UnPause();}
        }
        private void OnApplicationFocus(bool focus){if(!focus)SaveSettings();}
        private void OnApplicationQuit()=>SaveSettings();
        private void OnDestroy()
        {
            if(Instance!=this)return;
            SaveSettings();SceneManager.sceneLoaded-=OnSceneLoaded;
            FloodAudioSignals.ManagerAvailable-=AttachGame;FloodAudioSignals.ManagerRemoved-=DetachGame;
            FloodAudioSignals.ColorAccepted-=OnColorSelected;FloodAudioSignals.FloodStarted-=OnFlood;
            FloodAudioSignals.LevelReset-=OnLevelReset;FloodAudioSignals.RestartRequested-=OnRestart;
            FloodAudioSignals.PauseChanged-=OnPause;FloodAudioSignals.ResultScheduled-=OnResult;
            FloodAudioSignals.MenuContextChanged-=OnMenu;FloodAudioSignals.PopupChanged-=OnPopup;
            if(game!=null){game.UIChanged-=OnGameChanged;game.ManagerReady-=OnGameChanged;}
            DisposeSpreadClips();ui?.Dispose();Instance=null;
        }
        [Button("Refresh / Reapply Audio Settings")]
        public void RefreshAudioSettings()
        {
            if(!Application.isPlaying||Instance!=this)return;
            RefreshFloodClipBindings();ApplyVolumes();ResizePool();RefreshActiveVoices();preview=false;stoppedByUser=false;duckTarget=!menuContext&&resultHandled?resultMusicDuck:1f;
            if(releasing)pendingMusicDuration=Mathf.Max(.01f,requestedMusic==FloodMusicState.MainMenu?defaultCrossfadeDuration:gameplayCrossfadeDuration);
            if(crossfading){fadeElapsed=0;fadeDuration=Mathf.Max(.01f,requestedMusic==FloodMusicState.MainMenu?defaultCrossfadeDuration:gameplayCrossfadeDuration);musicStartGain[0]=musicGain[0];musicStartGain[1]=musicGain[1];}
            ReevaluateMusic(true);
        }
        [Button("Test Main Menu Music")] public void TestMainMenuMusic()=>PreviewMusic(FloodMusicState.MainMenu);
        [Button("Test Normal Music")] public void TestNormalMusic()=>PreviewMusic(FloodMusicState.Normal);
        [Button("Test Medium Music")] public void TestMediumMusic()=>PreviewMusic(FloodMusicState.Medium);
        [Button("Test Hard Music")] public void TestHardMusic()=>PreviewMusic(FloodMusicState.Hard);
        [Button("Test Button Click")] public void TestButtonClick()=>PlaySound(FloodSound.ButtonClick);
        [Button("Test Color Selection")] public void TestColorSelection()=>PlaySound(FloodSound.ColorSelected);
        [Button("Test Flood Spread")] public void TestFloodSpread(){if(Application.isPlaying)OnFlood(new FloodAudioSignals.FloodWave(20,10,1.2f,.8f,.065f));}
        [Button("Test Level Complete")] public void TestLevelComplete()=>PlaySound(FloodSound.LevelCompleted);
        [Button("Test Level Failed")] public void TestLevelFailed()=>PlaySound(FloodSound.LevelFailed);
        [Button("Return To Game Music")] public void ReturnToGameMusic(){if(!Application.isPlaying)return;preview=false;stoppedByUser=false;duckTarget=!menuContext&&resultHandled?resultMusicDuck:1f;ReevaluateMusic(true);}
        [Button("Stop All Audio")]
        public void StopAllAudio()
        {
            if(!Application.isPlaying||musicSources==null)return;
            stoppedByUser=true;preview=false;CancelFlood();resultPending=false;
            TransitionMusic(FloodMusicState.Silent,null,0,defaultFadeDuration);
            if(voices!=null)for(int i=0;i<voices.Length;i++)if(voices[i].Active)voices[i].Stopping=true;
        }
    }
}
