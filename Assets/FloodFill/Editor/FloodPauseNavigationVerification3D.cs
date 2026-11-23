using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Audio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodPauseNavigationVerification3D
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static FloodFillMobileUI3D ui;
        private static FloodFillGameManager3D game;
        private static int stage,checks;
        private static double ready,deadline,elapsed;
        private static string boardJson,goalJson;
        private static bool menuAudio;
        private static GameObject Screen(string field)=>(GameObject)typeof(FloodFillMobileUI3D).GetField(field,Flags).GetValue(ui);
        private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        private static void Click(string screen,FloodFillUIAction action)=>Screen(screen).GetComponentsInChildren<FloodFillUIAction3D>(true).Single(b=>b.action==action).GetComponent<Button>().onClick.Invoke();
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            PlayerSettings.companyName="FloodPauseNavigationVerification";PlayerSettings.productName="FloodPauseNavigationVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            FloodPauseNavigationSetup3D.Install();FloodPauseNavigationSetup3D.Install();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Navigation verification timeout at stage "+stage);
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();if(ui==null||!ui.IsBuilt)return;
                if(!string.IsNullOrEmpty(game.LoadingError))throw new Exception(game.LoadingError);
                if(stage==0){FloodAudioSignals.MenuContextChanged+=value=>menuAudio=value;ui.StartSelectedLevel(0,0,1);stage=1;return;}
                if(stage==1)
                {
                    if(!game.CanInteract)return;
                    game.SelectColor((game.CurrentPlayerColor+1)%6);
                    typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(game,new object[]{game.BoardManager.StartingVoxel});
                    game.BoardManager.TryCapturePersistentState(out var board);boardJson=JsonUtility.ToJson(board);goalJson=JsonUtility.ToJson(game.SecondaryGoals?.Attempt);
                    Check(game.MoveCount==1,"Actual game has an unfinished move");
                    Click("hud",FloodFillUIAction.Home);
                    Check(Screen("homeConfirmation").activeSelf&&!Screen("pause").activeSelf,"Home opens confirmation independently of Pause");
                    Check(Screen("homeConfirmation").GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text=="Are you sure you want to quit to home?"),"Exact confirmation question");
                    Check(!game.CanInteract&&!menuAudio,"Confirmation pauses gameplay and keeps gameplay audio context");
                    elapsed=game.ElapsedSeconds;ready=EditorApplication.timeSinceStartup+.25;stage=2;return;
                }
                if(stage==2)
                {
                    if(EditorApplication.timeSinceStartup<ready)return;
                    Check(game.ElapsedSeconds==elapsed,"Confirmation freezes elapsed clock");
                    Click("homeConfirmation",FloodFillUIAction.CancelHome);
                    Check(Screen("hud").activeSelf&&game.CanInteract,"Confirmation Back resumes same game");
                    game.BoardManager.TryCapturePersistentState(out var board);Check(JsonUtility.ToJson(board)==boardJson&&game.MoveCount==1,"Cancel preserves board and moves");
                    Click("hud",FloodFillUIAction.Pause);
                    Check(Screen("pause").activeSelf&&!Screen("homeConfirmation").activeSelf,"Pause opens its own menu");
                    Check(Screen("pause").GetComponentsInChildren<FloodFillUIAction3D>(true).Count(b=>b.action==FloodFillUIAction.Settings)==1,"One authored pause Settings button after repeated installation");
                    Click("pause",FloodFillUIAction.Settings);
                    Check(Screen("settings").activeSelf&&!Screen("pause").activeSelf&&!game.CanInteract,"Pause Settings keeps game paused");
                    Check(!menuAudio,"Pause Settings retains gameplay audio context");
                    elapsed=game.ElapsedSeconds;ready=EditorApplication.timeSinceStartup+.25;stage=3;return;
                }
                if(stage==3)
                {
                    if(EditorApplication.timeSinceStartup<ready)return;
                    Check(game.ElapsedSeconds==elapsed,"Settings from Pause freezes elapsed clock");
                    Click("settings",FloodFillUIAction.SettingsBack);
                    Check(Screen("pause").activeSelf&&!game.CanInteract,"Settings Back returns to Pause without resuming");
                    Click("pause",FloodFillUIAction.Home);
                    Check(Screen("homeConfirmation").activeSelf,"Pause Main Menu also confirms");
                    Click("homeConfirmation",FloodFillUIAction.CancelHome);
                    Check(Screen("pause").activeSelf&&!game.CanInteract,"Cancel from Pause returns to paused menu");
                    Click("pause",FloodFillUIAction.Resume);Check(Screen("hud").activeSelf&&game.CanInteract,"Resume restores input");
                    Click("hud",FloodFillUIAction.Home);Click("homeConfirmation",FloodFillUIAction.ConfirmHome);
                    Check(Screen("menu").activeSelf&&!Screen("homeConfirmation").activeSelf&&menuAudio,"Confirm quits to Home");
                    Check(game.RefreshContinueAvailability()&&FloodFillSessionPersistence3D.TryLoad(out var save)&&save.moveCount==1&&JsonUtility.ToJson(save.board)==boardJson,"Confirm saves unfinished game for Continue");
                    Click("menu",FloodFillUIAction.Settings);Check(Screen("settings").activeSelf&&menuAudio,"Main-menu Settings retains menu audio context");
                    Click("settings",FloodFillUIAction.SettingsBack);Check(Screen("menu").activeSelf,"Main-menu Settings Back returns Home");
                    Click("menu",FloodFillUIAction.Continue);stage=4;return;
                }
                if(stage==4)
                {
                    if(!game.CanInteract)return;
                    game.BoardManager.TryCapturePersistentState(out var board);
                    Check(Screen("hud").activeSelf&&game.MoveCount==1&&JsonUtility.ToJson(board)==boardJson,"Continue rebuilds exact unfinished board");
                    Check(JsonUtility.ToJson(game.SecondaryGoals?.Attempt)==goalJson,"Goal attempt survives Home and Continue");
                    Check(ui.EditorScreens.Count(s=>s.name=="HomeConfirmation")==1,"One confirmation screen after repeated installation");
                    Click("hud",FloodFillUIAction.Home);ready=EditorApplication.timeSinceStartup+.3;stage=5;return;
                }
                if(stage==5)
                {
                    if(EditorApplication.timeSinceStartup<ready)return;
                    Capture("/tmp/FloodHomeConfirmation.png");
                    Click("homeConfirmation",FloodFillUIAction.CancelHome);Click("hud",FloodFillUIAction.Pause);
                    ready=EditorApplication.timeSinceStartup+.3;stage=6;return;
                }
                if(stage==6)
                {
                    if(EditorApplication.timeSinceStartup<ready)return;
                    Capture("/tmp/FloodPauseSettings.png");
                    File.WriteAllText("/tmp/FloodPauseNavigationVerification.txt",checks+" checks passed: distinct Home/Pause, confirmation cancel/confirm, pause Settings/back, menu Settings/back, pause/audio/clock context, idempotent controls, saved unfinished board and Continue including goal progress.\n");
                    EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodPauseNavigationVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void Capture(string path)
        {
            var canvas=ui.GetComponent<Canvas>();var originalMode=canvas.renderMode;var originalCamera=canvas.worldCamera;
            var go=new GameObject("Navigation preview camera",typeof(Camera));var camera=go.GetComponent<Camera>();camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.005f,.018f,.045f);
            var rt=new RenderTexture(432,900,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();foreach(var fit in ui.EditorScreens.First(s=>s.activeSelf).GetComponentsInChildren<FloodFillUIFit3D>(true))fit.SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();camera.Render();
            RenderTexture.active=rt;var texture=new Texture2D(432,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,432,900),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active=null;canvas.renderMode=originalMode;canvas.worldCamera=originalCamera;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
