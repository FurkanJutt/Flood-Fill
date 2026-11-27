using System;
using System.IO;
using System.Linq;
using FloodFill.ThreeD;
using FloodFill.ThreeD.PowerUps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.EditorTools
{
    public static class FloodDevelopmentToolsSetup
    {
        [MenuItem("Tools/Flood Fill/UI/Install Development Tools")]
        public static void Install()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Install outside Play Mode.");
            var canvas=PrefabUtility.LoadPrefabContents(FloodFillUIPrefabSetup.CanvasPath);
            try
            {
                canvas.GetComponent<FloodFillMobileUI3D>().BuildDevelopmentToolsForEditor();
                PrefabUtility.SaveAsPrefabAsset(canvas,FloodFillUIPrefabSetup.CanvasPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(canvas); }
        }
        private static double deadline;
        private static int stage;
        private static FloodFillMobileUI3D ui;
        private static FloodFillGameManager3D game;
        public static void VerifyBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use an isolated project.");
            Install();
            var profile="FloodDevelopmentToolsTest_"+Guid.NewGuid().ToString("N");PlayerSettings.companyName=profile;PlayerSettings.productName=profile;
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            deadline=EditorApplication.timeSinceStartup+180;stage=0;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Check(bool value,string message){if(!value)throw new Exception(message);}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Development panel verification timed out.");
                if(!Application.isPlaying)return;
                if(stage==0)
                {
                    ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();
                    if(ui==null||!ui.IsBuilt||game==null)return;
                    var entry=ui.GetComponentsInChildren<Button>(true).Single(b=>b.onClick.GetPersistentEventCount()>0&&b.onClick.GetPersistentMethodName(0)=="OpenDevelopmentTools");
                    entry.onClick.Invoke();
                    var panel=ui.transform.Find("SafeArea/DevelopmentTools");Check(panel!=null&&panel.gameObject.activeInHierarchy,"Authored development panel opens");
                    ui.SetDevelopmentFps(true);Check(PlayerPrefs.GetInt("FloodFill3D.ShowFPS")==1,"FPS on saved");ui.SetDevelopmentFps(false);Check(PlayerPrefs.GetInt("FloodFill3D.ShowFPS")==0,"FPS off saved");
                    ui.RefillDevelopmentPowerUps();var config=Resources.Load<FloodPowerUpSettings3D>("FloodPowerUps");
                    for(int i=0;i<3;i++)Check(FloodPowerUpInventory3D.Quantity((FloodPowerUpKind)i)==config.Definition((FloodPowerUpKind)i).maximumQuantity,"Refill to configured cap");
                    ui.CloseDevelopmentTools();Check(!panel.gameObject.activeSelf,"Back hides panel");
                    ui.StartSelectedLevel(0,0,1);stage=1;
                }
                else if(stage==1)
                {
                    if(game.IsLoading||!game.CanInteract||Time.time<game.BoardManager.VisualsSettledAt)return;
                    ui.OpenDevelopmentTools();ui.StartDevelopmentSolver();stage=2;
                }
                else if(stage==2)
                {
                    if(game.MoveCount==0)return;
                    Check(!game.CurrentAchievementAttempt.eligible,"Solver attempt excluded from achievements");
                    ui.StopDevelopmentSolver();ui.OpenDevelopmentTools();ui.CloseDevelopmentTools();
                    File.WriteAllText("/tmp/FloodDevelopmentToolsVerification.txt","PASS: authored panel navigation, FPS preference, capped inventory refill, actual asynchronous solver move, stop and achievement exclusion. Isolated profile; no device testing.");
                    EditorApplication.update-=Tick;EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodDevelopmentToolsVerification.txt","FAIL: "+e);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
        }
    }
}
