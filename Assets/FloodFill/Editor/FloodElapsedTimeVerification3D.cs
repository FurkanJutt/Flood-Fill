using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.EditorTools
{
    public static class FloodElapsedTimeVerification3D
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static FloodFillGameManager3D game;
        private static FloodFillMobileUI3D ui;
        private static int stage,checks;
        private static double deadline,until,baseline;
        private static FloodFillSessionSaveData3D saved;
        private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        private static T Read<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
        private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Flags).SetValue(owner,value);
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp copy.");
            PlayerSettings.companyName="FloodElapsedVerification";PlayerSettings.productName="FloodElapsedVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timer timeout at stage "+stage);
                game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();
                if(ui==null||!ui.IsBuilt)return;
                if(stage==0)
                {
                    Check(FloodFillGameManager3D.FormatElapsedTime(0)=="00:00","Zero clock");
                    Check(FloodFillGameManager3D.FormatElapsedTime(125.9)=="02:05","Minute clock");
                    Check(FloodFillGameManager3D.FormatElapsedTime(3661)=="01:01:01","Hour clock");
                    var hud=Read<GameObject>(ui,"hud");
                    Check(hud.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text=="TIME ELAPSED"),"Authored timer header");
                    Check(!hud.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text=="CAPTURED"),"No duplicate HUD capture label");
                    baseline=game.ElapsedSeconds;until=EditorApplication.timeSinceStartup+.25;stage=1;return;
                }
                if(stage==1)
                {
                    if(EditorApplication.timeSinceStartup<until)return;
                    Check(game.ElapsedSeconds==baseline,"No menu time");
                    ui.StartSelectedLevel(0,0,1);Check(game.ElapsedSeconds==0,"New level resets timer");stage=2;return;
                }
                if(stage==2)
                {
                    if(game.IsLoading||!game.CanInteract){Check(game.ElapsedSeconds==0,"Loading and spawn animation excluded");return;}
                    baseline=game.ElapsedSeconds;until=EditorApplication.timeSinceStartup+.35;stage=3;return;
                }
                if(stage==3)
                {
                    if(EditorApplication.timeSinceStartup<until)return;
                    Check(game.ElapsedSeconds>baseline+.1,"Clock advances during gameplay");
                    Set(game,"<ElapsedSeconds>k__BackingField",125.25);
                    game.PauseGame();baseline=game.ElapsedSeconds;until=EditorApplication.timeSinceStartup+.25;stage=4;return;
                }
                if(stage==4)
                {
                    if(EditorApplication.timeSinceStartup<until)return;
                    Check(game.ElapsedSeconds==baseline,"Pause freezes timer");
                    Check(Read<TMP_Text>(ui,"captured").text=="02:05","Center HUD displays elapsed time");
                    var progress=Read<Image>(ui,"progress");Check(Math.Abs(progress.fillAmount-game.CapturedPercentage/100f)<.01,"Fill bar remains capture progress");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.ReturnToMenu,0);
                    Check(FloodFillSessionPersistence3D.TryLoad(out saved)&&saved.elapsedSeconds==baseline,"Elapsed time saved with checkpoint");
                    until=EditorApplication.timeSinceStartup+.25;stage=5;return;
                }
                if(stage==5)
                {
                    if(EditorApplication.timeSinceStartup<until)return;
                    Check(game.ElapsedSeconds==baseline,"Menu preview excludes time");
                    game.BoardManager.ClearBoard();ui.ExecuteAuthoredAction(FloodFillUIAction.Continue,0);
                    Check(game.ElapsedSeconds==saved.elapsedSeconds,"Continue restores exact elapsed value");
                    stage=6;return;
                }
                if(stage==6)
                {
                    if(game.IsLoading||!game.CanInteract)return;
                    baseline=game.ElapsedSeconds;until=EditorApplication.timeSinceStartup+.3;stage=7;return;
                }
                if(stage==7)
                {
                    if(EditorApplication.timeSinceStartup<until)return;
                    Check(game.ElapsedSeconds>baseline+.1,"Continued clock resumes");
                    game.PauseGame();ui.ExecuteAuthoredAction(FloodFillUIAction.Restart,0);
                    Check(game.ElapsedSeconds==0,"Replay resets clock");stage=8;return;
                }
                if(stage==8)
                {
                    if(game.IsLoading||!game.CanInteract)return;
                    baseline=game.ElapsedSeconds;Time.timeScale=4;until=EditorApplication.timeSinceStartup+.4;stage=9;return;
                }
                if(stage==9)
                {
                    if(EditorApplication.timeSinceStartup<until)return;
                    double advanced=game.ElapsedSeconds-baseline;Time.timeScale=1;
                    Check(advanced>.1&&advanced<1,"Clock uses real time at solver speed 4x");
                    Set(game,"<State>k__BackingField",FloodFillGameManager3D.GameState.Won);baseline=game.ElapsedSeconds;
                    until=EditorApplication.timeSinceStartup+.2;stage=10;return;
                }
                if(stage==10)
                {
                    if(EditorApplication.timeSinceStartup<until)return;
                    Check(game.ElapsedSeconds==baseline,"Completed game stops timer");
                    Set(game,"<State>k__BackingField",FloodFillGameManager3D.GameState.Playing);
                    // A version-1 checkpoint without the new field still loads at zero.
                    ui.ExecuteAuthoredAction(FloodFillUIAction.ReturnToMenu,0);
                    var json=File.ReadAllText(FloodFillSessionPersistence3D.SavePath);
                    json=System.Text.RegularExpressions.Regex.Replace(json,"\\\"elapsedSeconds\\\"\\s*:\\s*[-+0-9.eE]+,?","");
                    File.WriteAllText(FloodFillSessionPersistence3D.SavePath,json);
                    Check(game.RefreshContinueAvailability(),"Old save without timer stays resumable");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Continue,0);
                    Check(game.ElapsedSeconds==0,"Legacy timer defaults to zero");
                    File.WriteAllText("/tmp/FloodElapsedVerification.txt",checks+" checks passed: HUD label/value, capture bar retained, active timer, pause/menu/loading exclusion, restart reset, saved Continue timer, real-time 4x playback, completion freeze, legacy save compatibility.\n");
                    EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("/tmp/FloodElapsedVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
    }
}
