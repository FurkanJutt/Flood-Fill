using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using FloodFill.ThreeD;
using FloodFill.ThreeD.PowerUps;
using FloodFill.ThreeD.Solver;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodPowerUpVerification3D
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static FloodFillGameManager3D game;
        private static FloodFillMobileUI3D ui;
        private static FloodPowerUps3D powers;
        private static int stage,combination,checks,swapBefore,blastBefore,hintBefore,budget,moves,score,winSignals;
        private static RenderTexture gameplayTarget;
        private static double deadline;
        private static float ready;
        private static VoxelCell3D target;
        private static int targetLogical,oldColor;
        private static string original;
        private static FloodFillSessionSaveData3D postSwap;
        private static readonly List<string> report=new List<string>();
        private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message+" [stage "+stage+"]");}
        private static void Click(VoxelCell3D c)=>typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Private).Invoke(game,new object[]{c});
        private static void SaveReport(string text){File.WriteAllText("/tmp/FloodPowerUpVerification.txt",text);}
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use an isolated project.");
            PlayerSettings.companyName="FloodPowerUpVerification";PlayerSettings.productName="FloodPowerUpVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            foreach(string suffix in new[]{"",".bak",".tmp"}){string p=FloodPowerUpInventory3D.StorePath+suffix;if(File.Exists(p))File.Delete(p);}
            FloodPowerUpInventory3D.ResetCacheForVerification();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var child in root.GetComponentsInChildren<Transform>(true))Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)==0,"Scene has no missing scripts");
            deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        public static void CompilePlayerRuntimeBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use an isolated project.");
            var options=new UnityEditor.Build.Player.ScriptCompilationSettings {group=BuildTargetGroup.Standalone,target=BuildTarget.StandaloneOSX,options=UnityEditor.Build.Player.ScriptCompilationOptions.None};
            Directory.CreateDirectory("/tmp/FloodPowerUpPlayerScripts");
            var result=UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(options,"/tmp/FloodPowerUpPlayerScripts");
            if(result.assemblies==null||result.assemblies.Count==0)throw new Exception("Production player script compilation returned no assemblies.");
            File.WriteAllText("/tmp/FloodPowerUpPlayerCompilation.txt","PASS: non-development StandaloneOSX player scripts compiled without UNITY_EDITOR. "+result.assemblies.Count+" assemblies. This is script compilation, not an Android/iOS device build.\n");
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out at stage "+stage+" / "+(powers!=null?powers.State.ToString():"no controller"));
                game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();if(game==null||ui==null||!ui.IsBuilt)return;
                powers=game.PowerUps;
                if(!string.IsNullOrEmpty(game.LoadingError))throw new Exception(game.LoadingError);
                if(stage==0)
                {
                    Check(powers!=null,"Controller is installed");
                    Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap)==3&&FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint)==3&&FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorBlast)==3,"Old-save migration begins with three independent quantities");
                    Screen.SetResolution(420,900,false);gameplayTarget=new RenderTexture(420,900,24);Camera.main.targetTexture=gameplayTarget;ready=Time.time+.25f;stage=-1;return;
                }
                if(stage==-1){if(Time.time<ready)return;ui.StartSelectedLevel(0,0,1);stage=1;return;}
                if(stage==1)
                {
                    if(!powers.Ready)return;
                    Check(ui.GetComponentsInChildren<FloodPowerUpBar3D>(true).Length==1,"One authored power-up bar");
                    Capture("/tmp/FloodPowerUpUI.png",420,900);
                    original=File.ReadAllText(FloodFillSessionPersistence3D.LevelPath(0,0,1));
                    var board=game.BoardManager;target=board.GetCell(2,5,2);targetLogical=board.LogicalIndex(target);oldColor=target.ColorIndex;
                    moves=game.MoveCount;score=game.Score;budget=game.CurrentMoveBudget;swapBefore=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap);
                    powers.Select(FloodPowerUpKind.ColorSwap);Click(target);
                    Check(powers.Target==target&&powers.TargetColor==-1&&game.MoveCount==moves,"Cube-first targeting intercepts normal input before a paint is selected");
                    game.SelectColor(oldColor);Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap)==swapBefore&&game.MoveCount==moves,"Same-color Swap is invalid without charge");
                    var before=board.ActiveCells.ToDictionary(c=>board.LogicalIndex(c),c=>c.ColorIndex);
                    game.SelectColor((oldColor+1)%6);
                    Check(powers.IsExecuting&&board.ActiveCells.Count(c=>c.ColorIndex!=before[board.LogicalIndex(c)])==1,"Swap directly changes exactly one cube");
                    Check(game.MoveCount==moves&&game.Score==score&&game.CurrentMoveBudget==budget,"Swap preserves normal moves, score and allowance");
                    Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap)==swapBefore-1,"Swap spends exactly one inventory unit");
                    Check(game.PowerUpsUsed(FloodPowerUpKind.ColorSwap)==1,"Attempt tracks Swap usage");
                    postSwap=JsonUtility.FromJson<FloodFillSessionSaveData3D>(JsonUtility.ToJson(game.CapturePowerUpCheckpoint()));
                    ready=Time.time+.4f;stage=2;return;
                }
                if(stage==2)
                {
                    if(Time.time<ready||!powers.Ready)return;
                    Check(File.ReadAllText(FloodFillSessionPersistence3D.LevelPath(0,0,1))==original,"Power-up does not alter immutable board/seed/budget/goals");
                    Check(game.LastSolverResult==null,"Old solver result invalidated");
                    powers.Select(FloodPowerUpKind.ColorBlast);game.SelectColor((target.ColorIndex+2)%6);
                    var board=game.BoardManager;var affected=new List<VoxelCell3D>();board.GetPowerTargets(target,true,affected);
                    var direct=new HashSet<int>(affected.Select(board.LogicalIndex));var before=board.ActiveCells.ToDictionary(c=>board.LogicalIndex(c),c=>c.ColorIndex);
                    blastBefore=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorBlast);Click(target);
                    Check(direct.Count<=7&&affected.All(c=>c.ColorIndex==(oldColor+3)%6),"Blast targets occupied center and face neighbors only");
                    Check(board.ActiveCells.All(c=>direct.Contains(board.LogicalIndex(c))||before[board.LogicalIndex(c)]==c.ColorIndex),"Blast never directly recolors other cells");
                    Check(game.Score==score&&game.MoveCount==moves&&game.CurrentMoveBudget==budget,"Blast has no default score or move cost");
                    Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorBlast)==blastBefore-1,"Blast consumes once");
                    ready=Time.time+.5f;stage=3;return;
                }
                if(stage==3)
                {
                    if(Time.time<ready||!powers.Ready)return;
                    powers.Select(FloodPowerUpKind.ColorSwap);powers.Select(FloodPowerUpKind.ColorSwap);
                    Check(powers.State==FloodPowerUpState.Idle,"Selected button toggles cancellation");
                    hintBefore=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint);powers.Select(FloodPowerUpKind.SmartHint);
                    Check(powers.State==FloodPowerUpState.Calculating,"Hint starts asynchronous calculation");
                    powers.CancelActivePowerUp();Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint)==hintBefore,"Cancelled calculation is free");
                    powers.Select(FloodPowerUpKind.SmartHint);ready=Time.time+8;stage=4;return;
                }
                if(stage==4)
                {
                    if(powers.State!=FloodPowerUpState.HintDisplayed){if(Time.time>ready)throw new Exception("Hint did not reveal; state "+powers.State);return;}
                    Check(powers.CurrentHint!=null&&powers.CurrentHint.primary.Length>0,"Hint contains a current connected region");
                    Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint)==hintBefore-1,"Valid displayed hint spends once");
                    Check(game.MoveCount==moves&&game.Score==score,"Displaying hint does not play or score a move");
                    Check(powers.CurrentHint.primary.All(i=>game.BoardManager.CellAtLogical(i).ColorIndex==game.BoardManager.CellAtLogical(powers.CurrentHint.primary[0]).ColorIndex),"Highlighted region uses current post-power-up colors");
                    Capture("/tmp/FloodPowerUpHint.png",420,900);
                    var view=game.PowerUpCamera.CapturePersistentState();
                    game.PowerUpCamera.FocusHint(new FloodHintView3D{yaw=view.yaw+90,pitch=view.pitch,center=view.targetPosition,distance=view.distance,angle=90},powers.settings);
                    typeof(OrbitCameraController3D).GetMethod("Orbit",Private).Invoke(game.PowerUpCamera,new object[]{new Vector2(8,0)});
                    Check(!game.PowerUpCamera.IsHintFocusMoving&&powers.CurrentHint!=null,"Manual orbit cancels focus but keeps displayed hint");
                    Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint)==hintBefore-1,"Manual camera control does not charge again");
                    powers.CancelActivePowerUp();ui.ExecuteAuthoredAction(FloodFillUIAction.Home,0);ui.ExecuteAuthoredAction(FloodFillUIAction.ConfirmHome,0);
                    Check(game.RefreshContinueAvailability(),"Edited unfinished board is resumable");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Continue,0);stage=5;return;
                }
                if(stage==5)
                {
                    if(!powers.Ready)return;
                    Check(game.BoardManager.CellAtLogical(targetLogical).ColorIndex==(oldColor+3)%6,"Continue restores power-up-modified board");
                    Check(game.PowerUpsUsed(FloodPowerUpKind.ColorSwap)==1&&game.PowerUpsUsed(FloodPowerUpKind.ColorBlast)==1&&game.PowerUpsUsed(FloodPowerUpKind.SmartHint)==1,"Continue restores usage counters");
                    int swap=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap);ui.ExecuteAuthoredAction(FloodFillUIAction.Restart,0);stage=6;swapBefore=swap;return;
                }
                if(stage==6)
                {
                    if(!powers.Ready)return;
                    Check(game.BoardManager.CellAtLogical(targetLogical).ColorIndex==oldColor,"Restart restores original numbered colors");
                    Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap)==swapBefore,"Restart does not refund consumed inventory");
                    Check(game.PowerUpsUsed(FloodPowerUpKind.ColorSwap)==0,"Restart resets attempt-only usage");
                    VerifyJournal();
                    VerifyCameraSides();
                    VerifyGeometryAndGoals();
                    VerifyConfiguration();
                    hintBefore=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint);
                    powers.Select(FloodPowerUpKind.SmartHint);game.SelectColor((game.BoardManager.StartingVoxel.ColorIndex+1)%6);Click(game.BoardManager.StartingVoxel);
                    Check(powers.State==FloodPowerUpState.Idle&&FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint)==hintBefore,"Normal move invalidates pending worker without charging");
                    ready=Time.time+.5f;stage=8;return;
                }
                if(stage==8)
                {
                    if(Time.time<ready||!powers.Ready)return;
                    powers.Select(FloodPowerUpKind.SmartHint);ui.ExecuteAuthoredAction(FloodFillUIAction.Pause,0);
                    Check(powers.State==FloodPowerUpState.Idle&&FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint)==hintBefore,"Pause cancels pending calculation without charging");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Resume,0);
                    powers.settings.autoExecuteRecommendedMove=true;FloodPowerUpInventory3D.Add(FloodPowerUpKind.SmartHint,2);
                    moves=game.MoveCount;hintBefore=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint);powers.Select(FloodPowerUpKind.SmartHint);stage=9;return;
                }
                if(stage==9)
                {
                    if(powers.State!=FloodPowerUpState.HintDisplayed)return;
                    Check(game.MoveCount==moves,"Automatic execution waits for the visible preview");ready=Time.unscaledTime+powers.settings.automaticExecutionPreviewDuration+.4f;stage=10;return;
                }
                if(stage==10)
                {
                    if(Time.unscaledTime<ready||Time.time<game.BoardManager.VisualsSettledAt)return;
                    Check(game.MoveCount==moves+1&&powers.State==FloodPowerUpState.Idle,"Automatic hint execution uses exactly one normal move and clears hint");
                    Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.SmartHint)==hintBefore-1,"Automatic execution spends only the original displayed hint");
                    powers.Select(FloodPowerUpKind.SmartHint);stage=11;return;
                }
                if(stage==11)
                {
                    if(powers.State!=FloodPowerUpState.HintDisplayed)return;
                    moves=game.MoveCount;powers.CancelActivePowerUp();ready=Time.unscaledTime+powers.settings.automaticExecutionPreviewDuration+.3f;stage=12;return;
                }
                if(stage==12)
                {
                    if(Time.unscaledTime<ready)return;
                    Check(game.MoveCount==moves,"Cancellation suppresses pending automatic execution");powers.settings.autoExecuteRecommendedMove=false;
                    report.Add("Optional automatic execution: preview delay, one normal move/one hint charge, cancellation and stale worker/pause cancellation passed.");
                    combination=0;ui.StartSelectedLevel(0,0,1);stage=13;return;
                }
                if(stage==13)
                {
                    if(!powers.Ready)return;
                    Check(game.NumberedLevel==1&&game.CurrentMoveBudget>0,"Actual mode/difficulty launch has an authoritative budget");
                    Check(game.BoardManager.TryCreateSolverSnapshot(out var snapshot)&&FloodFillRegionGraph3D.TryBuild(snapshot,out _),"Actual mode/difficulty board is analyzable");
                    int count=game.MoveCount,allowance=game.CurrentMoveBudget;
                    FloodPowerUpInventory3D.Add(FloodPowerUpKind.ColorSwap,1);var cell=game.BoardManager.StartingVoxel;
                    powers.Select(FloodPowerUpKind.ColorSwap);game.SelectColor((cell.ColorIndex+1)%6);Click(cell);powers.CancelActivePowerUp();
                    Check(game.MoveCount==count&&game.CurrentMoveBudget==allowance&&game.PowerUpsUsed(FloodPowerUpKind.ColorSwap)==1,"Actual mode/difficulty power-up preserves moves and budget");
                    combination++;
                    if(combination<12){ui.StartSelectedLevel(combination/4,combination%4,1);return;}
                    game.StartConfiguredGame(1,0,0);stage=14;return;
                }
                if(stage==14)
                {
                    if(!powers.Ready)return;
                    Check(game.NumberedLevel==0,"Custom game fixture launched");powers.settings.allowPowerUpsInCustomGames=false;Check(!powers.Ready,"Custom games honor disable setting");powers.settings.allowPowerUpsInCustomGames=true;Check(powers.Ready,"Custom games support enabled power-ups");
                    report.Add("All 12 actual mode/difficulty level-one launches retained budgets and accepted Swap; custom-game availability toggles passed.");
                    ui.StartSelectedLevel(0,0,1);stage=15;return;
                }
                if(stage==15)
                {
                    if(!powers.Ready)return;VerifySplitsScoreAndVictory();ready=Time.time+2;stage=7;return;
                }
                if(stage==7)
                {
                    if(Time.time<ready)return;
                    Check(game.State==FloodFillGameManager3D.GameState.Won&&winSignals==1,"Power-up victory is finalized exactly once");
                    Check(FloodLevelProgression3D.Get(0,0,1).completed&&FloodLevelProgression3D.Unlocked(0,0,2),"Power-up victory saves completion and unlocks next level");
                    Check(FloodLevelProgression3D.Get(0,0,1).stars==FloodLevelProgression3D.Stars(game.MoveCount,game.CurrentMoveBudget),"Power-up use does not cap completion stars");
                    Check(File.ReadAllText(FloodFillSessionPersistence3D.LevelPath(0,0,1))==original,"Original numbered definition remains byte-identical");
                    Capture("/tmp/FloodPowerUpVictory.png",420,900);
                    report.Insert(0,checks+" checks passed in Unity "+Application.unityVersion+" on "+SystemInfo.graphicsDeviceName+". Isolated save identity; original player saves unchanged.");
                    report.Add("Actual authored UI, target/color order, exact direct edits, inventory spending, current-state hint/camera, cancellation, Continue/restart, write-ahead recovery, geometry variants and goal semantics passed.");
                    SaveReport(string.Join("\n",report));EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);SaveReport(e.ToString());EditorApplication.Exit(1);}
        }
        [Serializable] private sealed class JournalFixture
        {public int version=1;public int[] quantities;public long revision;public bool hasPendingSession;public FloodFillSessionSaveData3D pendingSession;}
        private static void VerifyConfiguration()
        {
            var settings=powers.settings;
            settings.colorSwap.enabled=false;Check(!powers.Available(FloodPowerUpKind.ColorSwap),"Individual power-up disable is honored");settings.colorSwap.enabled=true;
            int stock=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap);FloodPowerUpInventory3D.Add(FloodPowerUpKind.ColorSwap,-stock);
            Check(!powers.Available(FloodPowerUpKind.ColorSwap),"Zero inventory is unavailable");FloodPowerUpInventory3D.Add(FloodPowerUpKind.ColorSwap,stock);
            settings.colorSwap.moveCost=game.CurrentMoveBudget+1;Check(!powers.Available(FloodPowerUpKind.ColorSwap),"Unaffordable configured move cost is unavailable");settings.colorSwap.moveCost=0;
            settings.enablePowerUpSystem=false;Check(!powers.Ready,"Global disable prevents targeting");settings.enablePowerUpSystem=true;
            powers.enabled=false;powers.enabled=true;Check(powers.Ready,"Controller reactivation restores event bindings");
            foreach(var icon in ui.GetComponentsInChildren<FloodPowerUpIcon3D>(true))Check(icon.canvasRenderer!=null,"Native optional-sprite fallback has a CanvasRenderer");
            var bar=ui.GetComponentInChildren<FloodPowerUpBar3D>(true);var buttons=bar.GetComponentsInChildren<Button>();Canvas.ForceUpdateCanvases();
            Check(buttons.Length==3,"Three power-up buttons present");
            float width=((RectTransform)buttons[0].transform).rect.width;
            Check(buttons.All(b=>Mathf.Abs(((RectTransform)b.transform).rect.width-width)<1),"Power-up buttons have equal responsive widths");
        }
        private static void VerifyJournal()
        {
            var after=game.CapturePowerUpCheckpoint();after.powerUpRevision=FloodPowerUpInventory3D.Revision+1;
            var journal=JsonUtility.FromJson<JournalFixture>(File.ReadAllText(FloodPowerUpInventory3D.StorePath));
            journal.pendingSession=after;journal.hasPendingSession=true;journal.revision=after.powerUpRevision;
            File.WriteAllText(FloodPowerUpInventory3D.StorePath,JsonUtility.ToJson(journal));
            int quantity=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap);
            File.Delete(FloodFillSessionPersistence3D.SavePath);FloodPowerUpInventory3D.ResetCacheForVerification();FloodPowerUpInventory3D.Configure(powers.settings);
            Check(FloodFillSessionPersistence3D.TryLoad(out var recovered)&&recovered.powerUpRevision==after.powerUpRevision,"Pending journal restores the paired session after interrupted save");
            Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap)==quantity,"Journal recovery cannot refund inventory");
        }
        private static void VerifyCameraSides()
        {
            var camera=game.PowerUpCamera;var saved=camera.CapturePersistentState();var board=game.BoardManager;int m=board.Width/2,n=board.Width-1;
            var coords=new[]{new Vector3Int(m,n,m),new Vector3Int(m,0,m),new Vector3Int(n,m,m),new Vector3Int(0,m,m),new Vector3Int(m,m,0),new Vector3Int(m,m,n)};
            foreach(var c in coords)
            {
                var cell=board.GetCell(c.x,c.y,c.z);var region=new List<VoxelCell3D>{cell};
                Check(camera.TryFindHintView(board,region,powers.settings,out var view),"Hint plans a visible angle for face "+c);
                SetCamera(camera,view);Physics.SyncTransforms();Check(camera.IsHintSelectable(view.representative),"Planned representative is genuinely ray-selectable on face "+c);
            }
            camera.RestorePersistentState(saved);
            report.Add("Hint camera planned and verified selectable representatives on all six cube faces, including bottom/back, within existing pitch/zoom limits.");
        }
        private static void SetCamera(OrbitCameraController3D camera,FloodHintView3D view)
        {
            camera.CancelHintFocus();
            typeof(OrbitCameraController3D).GetField("yaw",Private).SetValue(camera,view.yaw);typeof(OrbitCameraController3D).GetField("pitch",Private).SetValue(camera,view.pitch);
            typeof(OrbitCameraController3D).GetField("distance",Private).SetValue(camera,view.distance);typeof(OrbitCameraController3D).GetField("targetPosition",Private).SetValue(camera,view.center);
            typeof(OrbitCameraController3D).GetMethod("ApplyCameraTransform",Private).Invoke(camera,null);
        }
        private static void VerifySplitsScoreAndVictory()
        {
            var board=game.BoardManager;
            var small=new VoxelBoardSaveData3D {width=6,height=6,depth=6,volumeMode=0,currentPlayerColor=0,voxels=new[]{
                new VoxelSaveData3D{x=0,y=0,z=0,colorIndex=0,captured=true,starting=true},new VoxelSaveData3D{x=1,y=0,z=0,colorIndex=0,captured=true},new VoxelSaveData3D{x=2,y=0,z=0,colorIndex=0,captured=true},new VoxelSaveData3D{x=3,y=0,z=0,colorIndex=2,captured=false}}};
            Check(board.RestorePersistentState(small,new[]{new Color(.25f,.78f,.38f),new Color(.62f,.32f,.86f),new Color(.98f,.82f,.2f),new Color(1,.52f,.16f),new Color(.2f,.48f,.95f),new Color(.3f,.82f,.8f)}),"Connected bridge fixture restores");
            typeof(FloodFillGameManager3D).GetMethod("ResetPowerUpAttempt",Private).Invoke(game,null);FloodPowerUpInventory3D.Add(FloodPowerUpKind.ColorSwap,5);
            var bridge=board.GetCell(1,0,0);int stock=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap);
            powers.Select(FloodPowerUpKind.ColorSwap);game.SelectColor(1);Click(bridge);
            Check(board.GetCell(0,0,0).ColorIndex==0&&board.GetCell(2,0,0).ColorIndex==0&&bridge.ColorIndex==1,"Swap splits a bridge without recoloring its former region");
            Check(board.TryCreateSolverSnapshot(out var snap)&&FloodFillRegionGraph3D.TryBuild(snap,out var graph)&&graph.RegionCount==4,"Split regions and adjacency are rebuilt correctly");
            powers.CancelActivePowerUp();Check(game.CanInteract,"Cancelling committed animation restores interaction without refund");
            Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap)==stock-1,"Interrupted animation remains committed once");
            game.ExecuteHintMove(bridge,0);int firstScore=game.Score;Check(firstScore>0,"Normal captures retain original score formula after assistance");
            board.ResetPowerVisuals();powers.Select(FloodPowerUpKind.ColorSwap);game.SelectColor(1);Click(bridge);powers.CancelActivePowerUp();
            game.ExecuteHintMove(bridge,0);Check(game.Score==firstScore,"Splitting/reconnecting rewarded cubes cannot farm score");
            board.ResetPowerVisuals();var before=JsonUtility.ToJson(game.CapturePowerUpCheckpoint().board);int currentMoves=game.MoveCount,currentScore=game.Score,currentStock=FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap);
            powers.PreviewSwap();powers.PreviewBlast();
            Check(JsonUtility.ToJson(game.CapturePowerUpCheckpoint().board)==before&&game.MoveCount==currentMoves&&game.Score==currentScore&&FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap)==currentStock,"Development previews never modify authoritative play or inventory");
            powers.ResetVisualStates();powers.ReloadSettings();Check(FloodPowerUpInventory3D.Quantity(FloodPowerUpKind.ColorSwap)==currentStock,"Settings reload does not refill inventory");
            powers.settings.colorSwap.moveCost=1;powers.settings.awardScoreForPowerUpCaptures=true;
            powers.Select(FloodPowerUpKind.ColorSwap);game.SelectColor(1);Click(bridge);powers.CancelActivePowerUp();
            Check(game.MoveCount==currentMoves+1&&game.Score==currentScore,"Configured move cost counts once and optional scoring cannot reward old cubes twice");
            Check(game.CapturePowerUpCheckpoint().secondaryGoalAttempt.lastMove==game.MoveCount,"Configured positive cost keeps saved goal move history compatible");
            powers.settings.colorSwap.moveCost=0;powers.settings.awardScoreForPowerUpCaptures=false;
            var palette=game.CapturePowerUpCheckpoint();
            // Restore the real board geometry, with one remaining different cube, to verify normal completion integration.
            var definition=FloodFillSessionPersistence3D.TryLoadLevel(0,0,1,out var saved,out _)?saved:null;
            var final=JsonUtility.FromJson<VoxelBoardSaveData3D>(JsonUtility.ToJson(definition.board));
            foreach(var v in final.voxels){v.colorIndex=0;v.captured=true;}var last=final.voxels.Last(v=>!v.starting);last.colorIndex=1;last.captured=false;final.currentPlayerColor=0;
            Check(board.RestorePersistentState(final,definition.palette),"Final-cube fixture uses original geometry");
            FloodFill.ThreeD.Audio.FloodAudioSignals.ResultScheduled+=won=>{if(won)winSignals++;};
            powers.Select(FloodPowerUpKind.ColorSwap);game.SelectColor(0);Click(board.GetCell(last.x,last.y,last.z));
            Check(game.State==FloodFillGameManager3D.GameState.Won&&winSignals==1,"Final power-up uses the normal victory sequence once");
        }
        private static void VerifyGeometryAndGoals()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FloodFill/Prefabs/VoxelCell3D.prefab").GetComponent<VoxelCell3D>();
            var palette=new[]{Color.green,Color.magenta,Color.yellow,Color.red,Color.blue,Color.cyan};int cases=0;
            foreach(var mode in new[]{VoxelBoardManager3D.VoxelVolumeMode.HollowCube,VoxelBoardManager3D.VoxelVolumeMode.Procedural,VoxelBoardManager3D.VoxelVolumeMode.Items})
            foreach(int size in new[]{6,8,10,12,15,18,20})
            foreach(int difficulty in new[]{0,1,2,3})
            {
                var root=new GameObject("Geometry fixture");var board=root.AddComponent<VoxelBoardManager3D>();var cells=new GameObject("Cells").transform;cells.SetParent(root.transform);
                board.Configure(cells,prefab,size,size,size,1,.01f,mode);board.NumberedLevelSeed=1313+difficulty*104729;
                Check(board.GenerateBoard(palette),"Geometry fixture generates");var center=board.StartingVoxel;
                int color=(center.ColorIndex+1)%6;var before=board.ActiveCells.ToDictionary(c=>board.LogicalIndex(c),c=>c.ColorIndex);
                Check(board.TryPowerRecolor(center,color,false,powers.settings),"Swap works for mode/size/difficulty fixture");
                Check(board.ActiveCells.Count(c=>before[board.LogicalIndex(c)]!=c.ColorIndex)==1,"Swap edits one cell in fixture");
                var affected=new List<VoxelCell3D>();board.GetPowerTargets(center,true,affected);var set=new HashSet<int>(affected.Select(board.LogicalIndex));
                before=board.ActiveCells.ToDictionary(c=>board.LogicalIndex(c),c=>c.ColorIndex);int blast=(color+1)%6;
                Check(board.TryPowerRecolor(center,blast,true,powers.settings)&&affected.Count<=7,"Blast works with bounded direct targets");
                Check(board.ActiveCells.All(c=>set.Contains(board.LogicalIndex(c))?c.ColorIndex==blast:c.ColorIndex==before[board.LogicalIndex(c)]),"Blast preserves non-target colors");
                Check(board.TryCreateSolverSnapshot(out var snapshot)&&FloodFillRegionGraph3D.TryBuild(snapshot,out _),"Post-edit region graph rebuilds");
                if(size==20&&difficulty==3)
                {
                    var watch=System.Diagnostics.Stopwatch.StartNew();var captured=board.CapturedCells.Select(board.LogicalIndex).ToArray();
                    var options=new MoveSolverSettings{greedySearchTimeBudgetMs=100,beamSearchTimeBudgetMs=100,runtimeExactSearchTimeBudgetMs=20,exactMaxExpandedStates=3000,maximumSearchMemoryMegabytes=8};
                    var result=System.Threading.Tasks.Task.Run(()=>
                    {
                        using(var cancel=new System.Threading.CancellationTokenSource(1000))
                        {try{return FloodHintService3D.Calculate(snapshot,captured,options,null,cancel.Token);}catch(OperationCanceledException){return FloodHintService3D.LegalFallback(snapshot,captured,System.Threading.CancellationToken.None);}}
                    }).GetAwaiter().GetResult();
                    Check(result.recommendations.Count>0&&result.recommendations[0].primary.Length>0,"Large-board worker returns a legal current-state hint");
                    var suggestion=result.recommendations[0];Check(board.CellAtLogical(suggestion.targetLogical).ColorIndex!=suggestion.color,"Large-board hint changes its representative color");
                    report.Add("20x20x20 "+mode+" hint worker including graph/fallback: "+watch.ElapsedMilliseconds+"ms on desktop; not a mobile performance guarantee.");
                }
                board.ClearPool();UnityEngine.Object.DestroyImmediate(root);cases++;
            }
            var goals=new[]{new FloodFill.ThreeD.Goals.SecondaryGoalDefinition{type=FloodFill.ThreeD.Goals.SecondaryGoalType.BigCapture,target=2},new FloodFill.ThreeD.Goals.SecondaryGoalDefinition{type=FloodFill.ThreeD.Goals.SecondaryGoalType.QuickStart,target=4,deadline=2}};
            var tracker=new FloodFill.ThreeD.Goals.FloodSecondaryGoalTracker3D("fixture",goals,1,6);tracker.PowerUpChange(0,5,6,false);
            Check(!tracker.Attempt.progress[0].met&&tracker.Attempt.progress[1].met&&tracker.Attempt.lastMove==0,"Power-up does not invent a normal capture move; percentage goals observe actual state");
            report.Add(cases+" combinations: all 3 modes, 7 dimensions, 4 difficulty seeds; one-cell Swap, face-only Blast and rebuilt region graph checked.");
        }
        private static void Capture(string path,int width,int height)
        {
            var canvas=ui.GetComponent<Canvas>();var camera=Camera.main;var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldTexture=camera.targetTexture;
            var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();foreach(var fit in ui.GetComponentsInChildren<FloodFillUIFit3D>(true))fit.SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=null;canvas.renderMode=mode;canvas.worldCamera=oldCamera;camera.targetTexture=oldTexture;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
