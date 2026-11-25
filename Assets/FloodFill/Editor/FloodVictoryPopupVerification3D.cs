using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Solver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodVictoryPopupVerification3D
    {
        private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        private static FloodFillMobileUI3D ui;
        private static FloodFillGameManager3D game;
        private static FloodVictoryPopup3D popup;
        private static int stage,checks,starts;
        private static double deadline,ready;
        private static Vector2 leafTarget,particleStart;
        private static string wonProgress;
        private static float sparkleMin=1,sparkleMax;
        private static Quaternion sparkleStart;
        private static Quaternion glowStart;
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message+" [stage "+stage+"]");}
        private static GameObject Screen(string field)=>(GameObject)typeof(FloodFillMobileUI3D).GetField(field,Flags).GetValue(ui);
        private static void Click(FloodFillUIAction action)=>popup.GetComponentsInChildren<FloodFillUIAction3D>(true).Single(b=>b.action==action).GetComponent<Button>().onClick.Invoke();
        public static void VerifyStarStatesBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            var view=UnityEngine.Object.FindAnyObjectByType<FloodVictoryPopup3D>(FindObjectsInactive.Include);
            if(view==null||view.stars.Length!=3)throw new Exception("Three victory stars are required.");
            for(int i=0;i<view.stars.Length;i++)
            {
                var star=view.stars[i];var state=star.GetComponent<FloodFillUIState3D>();var rect=star.rectTransform;var original=rect.sizeDelta;
                if(state==null||state.normalSprite==null||state.selectedSprite==null)throw new Exception("Star state references missing.");
                state.Apply(false);if(star.sprite!=state.normalSprite||Mathf.Abs(rect.rect.width-100.75f)>.01f)throw new Exception("Existing empty size was not migrated.");
                state.selectedOverrideSize=true;state.selectedSize=new Vector2(140+i*10,150+i*10);state.Apply(true);
                if(star.sprite!=state.selectedSprite||(rect.rect.size-state.selectedSize).sqrMagnitude>.001f)throw new Exception("Independent earned size failed.");
                state.normalOverrideSize=true;state.normalSize=new Vector2(90+i*5,95+i*5);state.Apply(false);
                if(star.sprite!=state.normalSprite||(rect.rect.size-state.normalSize).sqrMagnitude>.001f)throw new Exception("Independent empty size failed.");
                state.normalOverrideSize=state.selectedOverrideSize=false;state.Apply(true);
                if((rect.sizeDelta-original).sqrMagnitude>.001f)throw new Exception("Disabling overrides failed to restore original size.");
            }
            File.WriteAllText("/tmp/FloodVictoryStarStates.txt","PASS: three individually configured stars; migrated empty size, earned/empty sprite swaps, independent dimensions and restoring authored dimensions when overrides are disabled. Scene loaded in isolated project; player saves untouched.\n");
        }
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            PlayerSettings.companyName="FloodVictoryPopupVerification";PlayerSettings.productName="FloodVictoryPopupVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();FloodVictoryPopupSetup3D.Install();FloodVictoryPopupSetup3D.Install();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");deadline=EditorApplication.timeSinceStartup+180;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Win()
        {
            typeof(FloodFillGameManager3D).GetField("<ElapsedSeconds>k__BackingField",Flags).SetValue(game,125.25);
            var solution=game.LastSolverResult;Check(solution!=null&&solution.SolutionValidated,"Validated runtime solution available");
            Check(game.BoardManager.TryCreateSolverSnapshot(out var snapshot)&&FloodFillRegionGraph3D.TryBuild(snapshot,out _),"Fresh board snapshot");
            FloodFillRegionGraph3D.TryBuild(snapshot,out var graph);
            for(int i=0;i<solution.BestColorSequence.Length;i++)
            {
                int logical=graph.RepresentativeLogicalIndices[solution.BestRegionSequence[i]];
                var cell=game.BoardManager.GetCell(logical%snapshot.Width,logical/snapshot.Width%snapshot.Height,logical/(snapshot.Width*snapshot.Height));
                game.SelectColor(solution.BestColorSequence[i]);typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(game,new object[]{cell});
            }
            Check(game.State==FloodFillGameManager3D.GameState.Won,"Actual board win reached");
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Victory popup timeout, stage "+stage);
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();if(ui==null||!ui.IsBuilt)return;
                popup=(FloodVictoryPopup3D)typeof(FloodFillMobileUI3D).GetField("victoryPopup",Flags).GetValue(ui);
                if(popup.gameObject.activeInHierarchy&&popup.rewardSparkles.Length>0)
                {float alpha=popup.rewardSparkles[0].color.a;sparkleMin=Mathf.Min(sparkleMin,alpha);sparkleMax=Mathf.Max(sparkleMax,alpha);}
                if(!string.IsNullOrEmpty(game.LoadingError))throw new Exception(game.LoadingError);
                if(stage==0){Check(popup!=null&&!popup.gameObject.activeSelf,"Authored victory popup starts hidden");ui.StartSelectedLevel(0,0,1);stage=1;return;}
                if(stage==1)
                {
                    if(!game.CanInteract)return;Win();stage=2;return;
                }
                if(stage==2)
                {
                    if(!popup.gameObject.activeInHierarchy)return;
                    Check(!Screen("results").activeSelf,"Win uses new popup independently of loss screen");
                    Check(popup.level.text=="LEVEL 1"&&popup.moves.text==$"<color=#FFD341>0</color> / {game.CurrentMoveBudget}","Move count starts at zero while keeping full budget");
                    Check(popup.best.text==FloodLevelProgression3D.Get(0,0,1).bestMoves.ToString()&&popup.elapsedTime.text=="00:00"&&popup.IsCountingStatistics,"Best remains final while elapsed count starts at zero");
                    Check(popup.stars.Count(s=>s.sprite==s.GetComponent<FloodFillUIState3D>().selectedSprite)==FloodLevelProgression3D.Stars(game.MoveCount,game.CurrentMoveBudget),"Stars reflect this win's efficiency");
                    Check(popup.coinReward.text=="+100"&&popup.gemReward.text=="+5","Fixed display-only rewards");
                    Check(popup.primaryLabel.text=="NEXT LEVEL"&&popup.levelsButton.activeSelf,"Ordinary win actions");
                    Check(popup.confetti.Length==60&&popup.confetti.All(c=>!c.raycastTarget),"Bounded confetti pool never blocks input");
                    Check(popup.rewardSparkles.Length==2&&popup.rewardSparkles.All(s=>!s.raycastTarget),"Both authored reward sparkles are bound and do not block input");
                    Check(popup.heroGlow!=null&&popup.heroGlow.sprite.name=="cube_glow_512"&&!popup.heroGlow.raycastTarget,"Crown glow uses the provided sprite without blocking input");
                    Check(popup.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Victory prefab has no missing scripts after glow renderer removal");
                    glowStart=popup.heroGlow.rectTransform.localRotation;
                    sparkleStart=popup.rewardSparkles[0].rectTransform.localRotation;
                    Check(popup.stars.All(s=>s.rectTransform.localScale.magnitude<.1f),"Stars start hidden for staggered pop-in");
                    leafTarget=new Vector2(-280,395);particleStart=popup.confetti[0].rectTransform.anchoredPosition;
                    starts=popup.AnimationStartCount;wonProgress=PlayerPrefs.GetString("FloodFill3D.LevelCatalog.v1");
                    Time.timeScale=0;ready=EditorApplication.timeSinceStartup+.55;stage=3;return;
                }
                if(stage==3)
                {
                    if(EditorApplication.timeSinceStartup<ready)return;
                    Check(popup.IsCountingStatistics&&popup.elapsedTime.text!="00:00"&&popup.elapsedTime.text!=game.ElapsedTimeText,"Elapsed value advances partway using unscaled time");
                    Check(popup.moves.text!=$"<color=#FFD341>0</color> / {game.CurrentMoveBudget}"&&popup.moves.text!=$"<color=#FFD341>{game.MoveCount}</color> / {game.CurrentMoveBudget}","Moves advance partway toward total");
                    string midMoves=popup.moves.text,midTime=popup.elapsedTime.text;typeof(FloodFillMobileUI3D).GetMethod("Refresh",Flags).Invoke(ui,null);
                    Check(popup.moves.text==midMoves&&popup.elapsedTime.text==midTime&&popup.IsCountingStatistics,"Refresh does not interrupt or reset a running counter");
                    ready=EditorApplication.timeSinceStartup+1.1;stage=31;return;
                }
                if(stage==31)
                {
                    if(EditorApplication.timeSinceStartup<ready)return;
                    Check(!popup.IsCountingStatistics&&popup.moves.text==$"<color=#FFD341>{game.MoveCount}</color> / {game.CurrentMoveBudget}"&&popup.elapsedTime.text==game.ElapsedTimeText,"Counters finish at exact frozen game values");
                    Check(Vector2.Distance(popup.leftLaurel.anchoredPosition,leafTarget)<.01f&&popup.leftLaurel.localScale==Vector3.one,"Laurel emerges to its authored position");
                    Check(popup.stars.All(s=>Vector3.Distance(s.rectTransform.localScale,Vector3.one)<.01f),"Stars complete pop-in using unscaled time");
                    Check(Vector2.Distance(popup.confetti[0].rectTransform.anchoredPosition,particleStart)>20,"Confetti arcs from sides using unscaled time");
                    Check(sparkleMax-sparkleMin>.2f&&Quaternion.Angle(sparkleStart,popup.rewardSparkles[0].rectTransform.localRotation)>5,"Reward sparkles flicker and slowly rotate while time scale is zero");
                    Check(Quaternion.Angle(glowStart,popup.heroGlow.rectTransform.localRotation)>3&&popup.heroGlow.rectTransform.localScale.x>1.01f&&popup.heroGlow.color.a<.99f,"Crown glow rotates, gently pulses and fades with game time paused");
                    glowStart=popup.heroGlow.rectTransform.localRotation;
                    sparkleStart=popup.rewardSparkles[0].rectTransform.localRotation;
                    Capture("/tmp/FloodVictoryPopupCelebration.png",432,900);
                    Time.timeScale=1;typeof(FloodFillMobileUI3D).GetMethod("Refresh",Flags).Invoke(ui,null);typeof(FloodFillMobileUI3D).GetMethod("Refresh",Flags).Invoke(ui,null);
                    Check(popup.AnimationStartCount==starts,"Refreshing stats does not replay celebration");
                    Check(PlayerPrefs.GetString("FloodFill3D.LevelCatalog.v1")==wonProgress,"Displaying rewards does not change saved progression");
                    ready=EditorApplication.timeSinceStartup+Math.Max(.5,popup.confettiDuration*1.15+.12+((popup.confetti.Length-1)/2)*.035-1.6)+.15;stage=4;return;
                }
                if(stage==4)
                {
                    if(EditorApplication.timeSinceStartup<ready)return;
                    Check(popup.confetti.All(c=>!c.gameObject.activeSelf),"Confetti finishes and hides its pooled particles");
                    Check(!popup.IsAnimating,"One-shot celebration finishes");
                    Check(Quaternion.Angle(sparkleStart,popup.rewardSparkles[0].rectTransform.localRotation)>5,"Reward sparkles keep spinning after confetti finishes");
                    Check(Quaternion.Angle(glowStart,popup.heroGlow.rectTransform.localRotation)>3,"Crown glow continues animating after the celebration finishes");
                    Capture("/tmp/FloodVictoryPopup.png",432,900);Capture("/tmp/FloodVictoryPopupTablet.png",768,1024);
                    typeof(FloodFillGameManager3D).GetField("<NumberedLevel>k__BackingField",Flags).SetValue(game,100);typeof(FloodFillMobileUI3D).GetMethod("Refresh",Flags).Invoke(ui,null);
                    Check(popup.primaryLabel.text=="LEVELS"&&!popup.levelsButton.activeSelf,"Final level hides duplicate Levels action");
                    Canvas.ForceUpdateCanvases();var home=(RectTransform)popup.levelsButton.transform.parent.GetChild(0);var bottom=(RectTransform)home.parent;Check(Mathf.Abs(home.TransformPoint(home.rect.center).x-bottom.TransformPoint(bottom.rect.center).x)<.1f,"Remaining Home action is centered without a gap");
                    Click(FloodFillUIAction.Result);
                    Check(!popup.gameObject.activeSelf&&ui.GetComponentInChildren<FloodLevelSelection3D>(true).gameObject.activeSelf&&game.NumberedLevel==100,"Final-level primary opens Levels without level 101");
                    typeof(FloodFillGameManager3D).GetField("<NumberedLevel>k__BackingField",Flags).SetValue(game,1);ui.StartSelectedLevel(0,0,1);stage=5;return;
                }
                if(stage==5){if(!game.CanInteract)return;Win();stage=6;return;}
                if(stage==6)
                {
                    if(!popup.gameObject.activeInHierarchy)return;
                    Check(popup.AnimationStartCount==starts+1&&popup.levelsButton.activeSelf,"Next win replays animation and restores ordinary actions");
                    Click(FloodFillUIAction.Restart);Check(!popup.gameObject.activeSelf&&game.MoveCount==0&&game.NumberedLevel==1&&!popup.IsCountingStatistics&&popup.moves.rectTransform.localScale==Vector3.one&&popup.elapsedTime.rectTransform.localScale==Vector3.one,"Replay stops counters, resets text scale and restarts same numbered level");
                    Check(popup.confetti.All(c=>!c.gameObject.activeSelf),"Closing mid-animation cleans up confetti");
                    Check(popup.heroGlow.rectTransform.localScale==Vector3.one&&Quaternion.Angle(popup.heroGlow.rectTransform.localRotation,Quaternion.identity)<.01f&&Mathf.Approximately(popup.heroGlow.color.a,1),"Closing popup restores crown glow scale, rotation and opacity");stage=7;return;
                }
                if(stage==7){if(!game.CanInteract)return;Win();stage=8;return;}
                if(stage==8)
                {
                    if(!popup.gameObject.activeInHierarchy)return;
                    Click(FloodFillUIAction.Result);Check(!popup.gameObject.activeSelf&&game.NumberedLevel==2,"Next Level loads the next numbered level");stage=9;return;
                }
                if(stage==9)
                {
                    if(!game.CanInteract)return;
                    while(game.State==FloodFillGameManager3D.GameState.Playing)
                    {game.SelectColor(game.CurrentPlayerColor==0?1:0);typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(game,new object[]{game.BoardManager.StartingVoxel});}
                    Check(game.State==FloodFillGameManager3D.GameState.Lost,"Actual move-budget loss reached");stage=10;return;
                }
                if(stage==10)
                {
                    if(!Screen("results").activeSelf)return;
                    Check(!popup.gameObject.activeSelf,"Loss keeps the legacy popup without victory rewards or effects");
                    var title=(TMPro.TMP_Text)typeof(FloodFillMobileUI3D).GetField("resultTitle",Flags).GetValue(ui);Check(title.text=="OUT OF MOVES","Existing loss title preserved");
                    Check(ui.EditorScreens.Count(s=>s.name=="VictoryPopup")==1,"Idempotent installation creates one victory popup");
                    File.WriteAllText("/tmp/FloodVictoryPopupVerification.txt",checks+" checks passed: actual wins/loss, count-up statistics, refresh protection, counter cancellation, stats and stars, fixed rewards, unscaled leaf/star/confetti animations, repeat-refresh protection, finite particles, phone/tablet bounds, level-100 actions, Replay, Next Level, and animation cleanup.\n");
                    EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("/tmp/FloodVictoryPopupVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void Capture(string path,int width,int height)
        {
            var canvas=ui.GetComponent<Canvas>();var originalMode=canvas.renderMode;var originalCamera=canvas.worldCamera;
            var go=new GameObject("Victory preview camera",typeof(Camera));var camera=go.GetComponent<Camera>();camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.005f,.018f,.045f);
            var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();foreach(var fit in popup.GetComponentsInChildren<FloodFillUIFit3D>(true))fit.SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();camera.Render();
            var card=popup.transform.Find("Content/PopupCard") as RectTransform;var corners=new Vector3[4];card.GetWorldCorners(corners);
            foreach(var corner in corners){var p=camera.WorldToViewportPoint(corner);Check(p.x>.001f&&p.x<.999f&&p.y>.001f&&p.y<.999f,"Victory remains a popup with margins inside safe area");}
            RenderTexture.active=rt;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active=null;canvas.renderMode=originalMode;canvas.worldCamera=originalCamera;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
