using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Goals;
using FloodFill.ThreeD.Solver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodSecondaryGoalsVerification3D
    {
        private const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        private static FloodFillGameManager3D game;
        private static FloodFillMobileUI3D ui;
        private static int stage,index,checks;
        private static double deadline,ready;
        private static string boardJson,goalJson,saveRoot;
        private static int budget;
        private static List<string> measurements=new List<string>();
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message+" [case "+index+", stage "+stage+"]");}
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            PlayerSettings.companyName="FloodSecondaryGoalsVerification";PlayerSettings.productName="FloodSecondaryGoalsVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            saveRoot=Path.Combine(Application.persistentDataPath,"numbered-levels-v1");if(Directory.Exists(saveRoot))Directory.Delete(saveRoot,true);
            FloodSecondaryGoalsSetup3D.Install();
            PureChecks();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");deadline=EditorApplication.timeSinceStartup+360;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void PureChecks()
        {
            var t=new SecondarySolutionTrace{total=30,regions=15,initialCaptured=1,colors=new[]{0,1,2,3},captured=new[]{5,10,20,30},newlyCaptured=new[]{4,5,10,10},colorSelections=new[]{1,1,1,1,0,0},finalColor=3};
            var all=new[]{
                new SecondaryGoalDefinition{type=SecondaryGoalType.MoveSaver,target=4},new SecondaryGoalDefinition{type=SecondaryGoalType.FinalColor,color=3},new SecondaryGoalDefinition{type=SecondaryGoalType.BigCapture,target=10},
                new SecondaryGoalDefinition{type=SecondaryGoalType.QuickStart,target=10,deadline=2,count=33},new SecondaryGoalDefinition{type=SecondaryGoalType.ComboCapture,target=5,count=3},new SecondaryGoalDefinition{type=SecondaryGoalType.SpeedRun,seconds=90},
                new SecondaryGoalDefinition{type=SecondaryGoalType.LimitedColorUsage,color=1,target=1},new SecondaryGoalDefinition{type=SecondaryGoalType.FinalSprint,target=20,count=66,deadline=1},new SecondaryGoalDefinition{type=SecondaryGoalType.FastExpansion,target=20,deadline=3,count=66},new SecondaryGoalDefinition{type=SecondaryGoalType.FinalColorEfficiency,color=3,target=4}};
            foreach(var g in all)Check(FloodSecondaryGoalGenerator3D.Demonstrated(g,t),"Goal type witnessed "+g.type);
            Check(!FloodSecondaryGoalGenerator3D.Demonstrated(new SecondaryGoalDefinition{type=SecondaryGoalType.BigCapture,target=11},t),"Impossible goal rejected");
            t.initialColorCounts=new[]{5,5,5,5,5,5};
            var settings=ScriptableObject.CreateInstance<FloodSecondaryGoalSettings3D>();
            var a=FloodSecondaryGoalGenerator3D.Generate(t,8,0,0,42,settings,default,out _);
            var b=FloodSecondaryGoalGenerator3D.Generate(t,8,0,0,42,settings,default,out _);
            Check(string.Join("|",a.Select(JsonUtility.ToJson))==string.Join("|",b.Select(JsonUtility.ToJson)),"Deterministic selection with fixed seed and board");
            foreach(var pool in settings.pools)Array.Clear(pool.weights,0,pool.weights.Length);
            Check(FloodSecondaryGoalGenerator3D.Generate(t,8,0,0,42,settings,default,out _).Length==0,"No invented fallback when candidates are disabled");
            UnityEngine.Object.DestroyImmediate(settings);
            var combo=new FloodSecondaryGoalTracker3D("duplicates",new[]{all[4]},1,6);combo.Move(1,10,11);combo.Move(1,10,11);Check(combo.Attempt.progress[0].value==1,"Duplicate move callback ignored");
            var early=new FloodSecondaryGoalTracker3D("early",new[]{all[3]},1,6);early.Move(1,3,4);early.Move(2,3,7);early.Move(3,20,27);Check(early.State(0,false,0)==SecondaryGoalState.FailedThisAttempt,"Late expansion cannot satisfy deadline");
            var g1=new SecondaryGoalDefinition{id="persist_a",type=SecondaryGoalType.FinalColor,color=0};var g2=new SecondaryGoalDefinition{id="persist_b",type=SecondaryGoalType.BigCapture,target=5};
            FloodLevelProgression3D.RecordSecondaryGoals(0,0,99,new[]{g1,g2},new[]{true,false},12.5);
            FloodLevelProgression3D.RecordSecondaryGoals(0,0,99,new[]{g1,g2},new[]{false,true},20);
            Check(FloodLevelProgression3D.SecondaryStars(0,0,99)==2&&FloodLevelProgression3D.Get(0,0,99).bestCompletionSeconds==12.5,"Achievements merge across attempts; best time preserved");
            typeof(FloodLevelProgression3D).GetField("data",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
            Check(FloodLevelProgression3D.GoalEarned(0,0,99,"persist_a")&&FloodLevelProgression3D.GoalEarned(0,0,99,"persist_b"),"Achievement flags survive catalog reload");
            var lost=new FloodSecondaryGoalTracker3D("lost",new[]{g2},1,6);lost.Move(1,10,11);Check(!lost.Complete(false,1,0,1)[0],"Loss cannot award met objective");
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Goal verification timeout");
                game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();if(ui==null||!ui.IsBuilt)return;
                if(!string.IsNullOrEmpty(game.LoadingError))throw new Exception(game.LoadingError);
                int mode=index/8,difficulty=index/2%4,level=index%2==0?1:100;
                if(stage==0)
                {
                    if(level>1)FloodLevelProgression3D.Record(mode,difficulty,level-1,1,1);
                    ui.StartSelectedLevel(mode,difficulty,level);stage=1;return;
                }
                if(stage==1)
                {
                    if(!game.CanInteract)return;
                    var d=game.ActiveLevelDefinition;Check(d!=null&&d.mode==mode&&d.difficulty==difficulty&&d.level==level,"Saved identity");
                    Check(d.goals.Length<=3&&d.goals.Length>0,"Validated goal subset assigned");
                    Check(File.Exists(FloodFillSessionPersistence3D.LevelPath(mode,difficulty,level)),"Definition persisted before gameplay");
                    var proofWatch=System.Diagnostics.Stopwatch.StartNew();
                    Check(FloodFillRegionGraph3D.TryBuild(d.Snapshot(),out var graph),"Snapshot rebuilds graph");
                    var t=FloodSecondaryGoalGenerator3D.Trace(graph,d.witnessRegions,d.witnessColors,d.moveBudget,2048);
                    proofWatch.Stop();
                    foreach(var g in d.goals)Check(FloodSecondaryGoalGenerator3D.Demonstrated(g,t),"Selected goals jointly demonstrated by shared witness");
                    Check(d.goals.All(g=>g.type!=SecondaryGoalType.MoveSaver&&g.type!=SecondaryGoalType.FinalColorEfficiency||g.target<d.moveBudget),"Efficiency always stricter than budget");
                    Check(JsonUtility.ToJson(CompactInitialBoard3D.Pack(d.board).Unpack())==JsonUtility.ToJson(d.board),"Compact snapshot round-trip is exact");
                    boardJson=JsonUtility.ToJson(d.board);goalJson=string.Join("|",d.goals.Select(JsonUtility.ToJson));budget=d.moveBudget;
                    measurements.Add($"{mode}/{difficulty}/{level}: {t.total} voxels, {t.regions} regions, {t.Length}/{budget} witness/budget, {d.goals.Length} goals, candidates {d.goalGenerationMilliseconds:F3} ms, graph + trace revalidation {proofWatch.Elapsed.TotalMilliseconds:F3} ms, file {new FileInfo(FloodFillSessionPersistence3D.LevelPath(mode,difficulty,level)).Length} bytes");
                    if(index==0)
                    {
                        var view=UnityEngine.Object.FindAnyObjectByType<FloodSecondaryGoalsUI3D>();Check(view!=null&&!view.IsOpen,"Authored floating panel hidden by default");
                        view.Toggle();Check(view.IsOpen&&game.CanInteract,"Goals panel does not pause game");
                        Canvas.ForceUpdateCanvases();
                        var panelScreen=RectTransformUtility.WorldToScreenPoint(null,view.panel.position);
                        Vector2 outside=new Vector2(Screen.width*.5f,Screen.height*.35f);
                        var rayResults=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=outside},rayResults);
                        Check(rayResults.All(r=>r.gameObject.GetComponent<FloodGoalDismissRaycast3D>()==null),"Open panel leaves camera area transparent to raycasts");
                        view.ProcessPointer(outside,true,true,false);view.ProcessPointer(outside,false,false,true);
                        rayResults.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=outside},rayResults);
                        Check(rayResults.Any(r=>r.gameObject.GetComponent<FloodGoalDismissRaycast3D>()!=null),"Dismissal release blocks UI click-through");
                        int selected=game.SelectedColorIndex;game.SelectColor(0);Check(game.SelectedColorIndex==selected,"Dismissal cannot select a paint color");
                        Check(!view.IsOpen&&FloodSecondaryGoalsUI3D.ConsumedPointerThisFrame,"Outside tap dismisses and is consumed");
                        view.Toggle();view.ProcessPointer(new Vector2(-100,-100),true,true,false);view.ProcessPointer(new Vector2(-70,-100),false,true,false);
                        Check(!view.IsOpen,"Outside drag dismisses without preventing camera gesture");
                        Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"No duplicate EventSystem");
                        Check(UnityEngine.Object.FindObjectsByType<FloodFillGameManager3D>(FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<VoxelBoardManager3D>(FindObjectsSortMode.None).Length==1,"No duplicate gameplay or board manager");
                    }
                    ready=EditorApplication.timeSinceStartup+.05;stage=2;return;
                }
                if(stage==2)
                {
                    if(EditorApplication.timeSinceStartup<ready)return;
                    Check(!FloodSecondaryGoalsUI3D.ConsumedPointerThisFrame,"Dismissal consumes only its own frame");
                    var d=game.ActiveLevelDefinition;FloodFillRegionGraph3D.TryBuild(d.Snapshot(),out var graph);
                    var t=FloodSecondaryGoalGenerator3D.Trace(graph,d.witnessRegions,d.witnessColors,d.moveBudget,2048);
                    if(index==0)typeof(FloodFillGameManager3D).GetField("<SecondaryGoals>k__BackingField",Flags).SetValue(game,new FloodSecondaryGoalTracker3D(d.id,Array.Empty<SecondaryGoalDefinition>(),d.initialCaptured,d.palette.Length));
                    for(int m=0;m<d.witnessColors.Length;m++)
                    {
                        int logical=graph.RepresentativeLogicalIndices[d.witnessRegions[m]];var b=d.board;
                        var cell=game.BoardManager.GetCell(logical%b.width,logical/b.width%b.height,logical/(b.width*b.height));
                        game.SelectColor(d.witnessColors[m]);typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(game,new object[]{cell});
                        Check(game.BoardManager.LastNewlyCapturedCount==t.newlyCaptured[m]&&game.BoardManager.CapturedVoxelCount==t.captured[m],"Trace exactly matches live free-flood capture semantics");
                    }
                    Check(game.State==FloodFillGameManager3D.GameState.Won,"Existing win condition unchanged");
                    Check(FloodLevelProgression3D.Get(mode,difficulty,level).completed,"Existing progression recorded");
                    if(index==0)Check(FloodLevelProgression3D.SecondaryStars(mode,difficulty,level)==0,"Real level win with zero optional stars");
                    else Check(FloodLevelProgression3D.SecondaryStars(mode,difficulty,level)==d.goals.Length,"Shared witness earns all optional stars");
                    game.RestartCurrentGame();stage=3;return;
                }
                if(stage==3)
                {
                    if(!game.CanInteract)return;
                    game.BoardManager.TryCapturePersistentState(out var board);
                    Check(JsonUtility.ToJson(board)==boardJson,"Replay restores exact board snapshot");
                    Check(game.CurrentMoveBudget==budget&&string.Join("|",game.ActiveLevelDefinition.goals.Select(JsonUtility.ToJson))==goalJson,"Replay preserves exact budget and goals");
                    Check(game.MoveCount==0&&game.SecondaryGoals.Attempt.lastMove==0&&game.SecondaryGoals.Attempt.colorSelections.All(n=>n==0),"Restart resets attempt progress");
                    Check(game.LastSolverResult.SearchAlgorithm=="Saved validated level witness","Replay does not rerun solver");
                    if(index==0)
                    {
                        var d=game.ActiveLevelDefinition;FloodFillRegionGraph3D.TryBuild(d.Snapshot(),out var graph);int logical=graph.RepresentativeLogicalIndices[d.witnessRegions[0]];var b=d.board;
                        game.SelectColor(d.witnessColors[0]);typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(game,new object[]{game.BoardManager.GetCell(logical%b.width,logical/b.width%b.height,logical/(b.width*b.height))});
                        var expected=JsonUtility.ToJson(game.SecondaryGoals.Attempt);game.ReturnToMenu();game.BoardManager.ClearBoard();
                        Check(game.ContinueSavedGame(),"Continue restores numbered level with goals");Check(JsonUtility.ToJson(game.SecondaryGoals.Attempt)==expected,"Continue restores exact goal attempt progress");
                        stage=4;return;
                    }
                    Next();
                }
                else if(stage==4){if(game.IsLoading||!game.CanInteract)return;Next();}
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodGoalsVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void Next()
        {
            game.ReturnToMenu();index++;stage=0;
            if(index<24)return;
            File.WriteAllText("/tmp/FloodGoalsVerification.txt",checks+" checks passed across all 12 mode/difficulty combinations on 6- and 20-wide boards.\n"+string.Join("\n",measurements));
            File.WriteAllText("/tmp/FloodGoalsSaveRoot.txt",saveRoot);EditorApplication.Exit(0);
        }
        public static void VerifyMobileScripts()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            foreach(var target in new[]{BuildTarget.Android,BuildTarget.iOS})
            {
                string output="/tmp/FloodGoalsScripts-"+target;
                if(Directory.Exists(output))Directory.Delete(output,true);
                Directory.CreateDirectory(output);
                var settings=new UnityEditor.Build.Player.ScriptCompilationSettings{target=target,group=BuildPipeline.GetBuildTargetGroup(target)};
                var result=UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings,output);
                Check(result.assemblies!=null&&result.assemblies.Any(a=>Path.GetFileName(a)=="Assembly-CSharp.dll"),target+" runtime player scripts compile");
            }
            File.WriteAllText("/tmp/FloodGoalsMobileVerification.txt", "Android and iOS runtime player script compilation passed. Packaging, signing and device playtesting not performed.\n");
        }
        public static void BeginPreviewBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            PlayerSettings.companyName="FloodSecondaryGoalsVerification";PlayerSettings.productName="FloodSecondaryGoalsVerification";
            VerifyFreshProcess();FloodSecondaryGoalsSetup3D.Install();
            FloodFillSessionPersistence3D.Delete();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");stage=0;deadline=EditorApplication.timeSinceStartup+60;
            EditorApplication.update+=PreviewTick;EditorApplication.EnterPlaymode();
        }
        private static void PreviewTick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Preview timeout");
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();if(ui==null||!ui.IsBuilt)return;
                if(stage==0){ui.StartSelectedLevel(0,0,1);stage=1;return;}
                if(!game.CanInteract)return;
                var view=UnityEngine.Object.FindAnyObjectByType<FloodSecondaryGoalsUI3D>();
                Capture(view,false,"/tmp/FloodGoalsClosed.png");Capture(view,true,"/tmp/FloodGoalsOpen.png");
                Capture(view,true,"/tmp/FloodGoalsTablet.png",768,1024);
                var outside=new Vector2(Screen.width*.5f,Screen.height*.35f);var results=new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=outside},results);
                Check(results.All(r=>r.gameObject.GetComponent<FloodGoalDismissRaycast3D>()==null),"Styled panel leaves camera raycasts available outside");
                view.ProcessPointer(outside,true,true,false);view.ProcessPointer(outside,false,false,true);
                results.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=outside},results);
                Check(!view.IsOpen&&FloodSecondaryGoalsUI3D.ConsumedPointerThisFrame&&results.Any(r=>r.gameObject.GetComponent<FloodGoalDismissRaycast3D>()!=null),"Styled panel consumes dismissal release");
                Check((view.trigger.transform as RectTransform).rect.height>=120,"Compact trigger has expanded touch target");
                File.WriteAllText("/tmp/FloodGoalsPreviewVerification.txt","Phone and tablet rendering, safe-area bounds, authored trigger, expanded touch target, outside camera raycasts and dismissal click-through checks passed.\n");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        private static void Capture(FloodSecondaryGoalsUI3D view,bool open,string path,int width=432,int height=900)
        {
            if(open&&!view.IsOpen)view.trigger.onClick.Invoke();else if(!open)view.Close();
            Check(view.IsOpen==open,"Authored trigger opens panel");
            var canvas=ui.GetComponent<Canvas>();var camera=Camera.main;
            var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;int mask=camera.cullingMask;
            var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;camera.cullingMask|=1<<5;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            if(game.BoardManager.TryGetWorldBounds(out var bounds))UnityEngine.Object.FindAnyObjectByType<OrbitCameraController3D>().FrameBounds(bounds);
            Canvas.ForceUpdateCanvases();
            foreach(var fit in ui.EditorScreens.First(screen=>screen.activeSelf).GetComponentsInChildren<FloodFillUIFit3D>(true))fit.SendMessage("LateUpdate");
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            var corners=new Vector3[4];view.panel.GetWorldCorners(corners);foreach(var corner in corners){var p=camera.WorldToViewportPoint(corner);Check(p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1,"Goals panel fits portrait safe area");}
            RenderTexture.active=null;camera.targetTexture=null;camera.cullingMask=mask;canvas.renderMode=mode;canvas.worldCamera=oldCamera;
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
        }
        public static void VerifyFreshProcess()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            PlayerSettings.companyName="FloodSecondaryGoalsVerification";PlayerSettings.productName="FloodSecondaryGoalsVerification";
            for(int mode=0;mode<3;mode++)for(int difficulty=0;difficulty<4;difficulty++)foreach(int level in new[]{1,100})
            {Check(FloodFillSessionPersistence3D.TryLoadLevel(mode,difficulty,level,out var d,out var error),"Fresh process exact board/goal validation: "+error);Check(d.boardHash==FloodFillSessionPersistence3D.BoardHash(d.board),"Snapshot checksum survives");if(mode!=0||difficulty!=0||level!=1)Check(FloodLevelProgression3D.SecondaryStars(mode,difficulty,level)==d.goals.Length,"Achievements survive fresh process");}
            Check(FloodLevelProgression3D.SecondaryStars(0,0,99)==2,"Multi-replay union survives fresh process");
            File.WriteAllText("/tmp/FloodGoalsFreshVerification.txt",checks+" fresh-process checks passed.");
        }
    }
}
