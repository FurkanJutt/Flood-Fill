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
        private static int stage,level;
        private static FloodFillMobileUI3D ui;
        private static FloodFillGameManager3D game;
        public static void VerifyBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use an isolated project.");
            Install();
            var profile="FloodDevelopmentToolsTest_"+Guid.NewGuid().ToString("N");PlayerSettings.companyName=profile;PlayerSettings.productName=profile;
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            deadline=EditorApplication.timeSinceStartup+180;stage=0;level=1;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
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
                    // Batch harness only: simulate ordinary device eligibility, not the batch exclusion.
                    game.CurrentAchievementAttempt.eligible=true;game.CurrentAchievementAttempt.ineligibleReason=null;
                    ui.OpenDevelopmentTools();ui.StartDevelopmentSolver();stage=2;
                }
                else if(stage==2)
                {
                    if(game.State!=FloodFillGameManager3D.GameState.Won)return;
                    Check(game.CurrentAchievementAttempt.eligible,"In-game solver completion remains eligible");
                    var service=FloodFill.ThreeD.Achievements.FloodAchievementService.Instance;
                    var first=service.Engine.Catalog.achievements.Single(d=>d.id=="ff3d.001");
                    var easy=service.Engine.Catalog.achievements.Single(d=>d.id=="ff3d.007");
                    Check(service.Engine.Progress(first,first.tiers[0]).completed,"First Steps recorded");
                    Check(service.Engine.Progress(easy,easy.tiers[0]).value==level,"Easy Going counts every solver completion");
                    if(level<7){level++;ui.StartSelectedLevel(0,0,level);stage=1;return;}
                    service.Flush();
                    var loaded=service.Persistence.Load();
                    var restored=new FloodFill.ThreeD.Achievements.AchievementEngine(service.Engine.Catalog,loaded);
                    Check(restored.Progress(easy,easy.tiers[0]).value==7,"Seven-level progress persists across reload");
                    File.WriteAllText("/tmp/FloodDevelopmentToolsVerification.txt","PASS: Settings button, panel, FPS, refill, seven consecutive in-game solver wins, First Steps, Easy Going 7/10, and saved progress reload. Isolated profile; no device testing.");
                    EditorApplication.update-=Tick;EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodDevelopmentToolsVerification.txt","FAIL: "+e);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
        }
    }
}
