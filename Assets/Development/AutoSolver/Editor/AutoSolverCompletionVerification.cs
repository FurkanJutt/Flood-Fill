using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Text;
using FloodFill.ThreeD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    public static class AutoSolverCompletionVerification
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static int checks,stage;
        private static bool windowMode;
        private static AutoSolverWindow window;
        public static void BeginWindowBatch() { windowMode=true; BeginBatch(); }
        private static double deadline;
        private static readonly StringBuilder report=new StringBuilder();
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        private static void Set(object instance,string field,object value)=>instance.GetType().GetField(field,Flags).SetValue(instance,value);
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project copy.");
            PlayerSettings.companyName="FloodCompletionVerification";PlayerSettings.productName="FloodCompletionVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            FloodLevelProgression3D.Record(0,3,19,1,1);
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Completion verification timed out");
                var ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();var game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();
                if(ui==null||!ui.IsBuilt)return;
                if(stage==0){ui.StartSelectedLevel(0,3,20);stage=1;return;}
                if(windowMode)
                {
                    if(stage==1)
                    {
                        if(!game.CanInteract)return;
                        window=ScriptableObject.CreateInstance<AutoSolverWindow>();
                        Set(window,"autoStartSolver",false);Set(window,"autoNextLevel",false);Set(window,"playbackSpeed",4f);Set(window,"animationDuration",.01f);
                        Set(window,"enableCaptureAnimation",false);Set(window,"enableColorTransition",false);Set(window,"enableCubePop",false);
                        typeof(AutoSolverWindow).GetMethod("StartAutoSolve",Flags).Invoke(window,null);stage=2;return;
                    }
                    if(game.State==FloodFillGameManager3D.GameState.Lost)throw new Exception("Window playback still lost the level");
                    if(game.State!=FloodFillGameManager3D.GameState.Won)return;
                    Check(game.BoardManager.IsFullyCaptured&&game.MoveCount<=game.CurrentMoveBudget,"Window automatically selects a budget-fitting plan and wins");
                    var record=AutoSolverSummaryStore.Shared.Data.attempts.Last(a=>a.mode==0&&a.difficulty==3&&a.level==20);
                    Check(record.Won&&record.searchResult=="Within budget"&&record.plannedMoves<=record.moveBudget,"Summary distinguishes fitting plan from actual win");
                    Check(record.moves==game.MoveCount&&record.moveBudget==game.CurrentMoveBudget,"Summary records actual successful moves and budget");
                    UnityEngine.Object.DestroyImmediate(window);window=null;
                    File.WriteAllText("/tmp/FloodCompletionWindowVerification.txt",checks+" checks passed. Auto Solver window won reported level 20 in "+game.MoveCount+" / "+game.CurrentMoveBudget+" moves; summary correctly records planned moves, budget and actual win.\n");
                    EditorApplication.Exit(0);return;
                }
                if(!game.CanInteract)return;
                var board=game.BoardManager;var adapter=AutoSolverAdapter.FindActive();
                Check(adapter.TryCaptureSnapshot(out var snapshot,out _),"Original level snapshot");
                Check(board.TotalVoxelCount==296&&board.LastGenerationSeed==1212414315,"Exact reported level identity");
                Check(AutoSolverGraphBuilder.TryBuild(snapshot,out var graph,out _),"Level graph");
                Check(board.TryCapturePersistentState(out var original),"Original board save");
                int budget=game.CurrentMoveBudget;
                Check(adapter.TryGetBudgetSolution(snapshot,graph,out var fallback),"Budget solution independently validates");
                Check(fallback.Moves.Length==budget&&fallback.FitsMoveBudget,"Expert solution fits exact budget");
                var independent=AutoSolverSearch.Solve(graph,budget,3000,60000,32,CancellationToken.None);
                report.AppendLine("Level 20 / Full Grid / Expert, seed "+board.LastGenerationSeed+", voxels "+board.TotalVoxelCount+". Budget "+budget+", engine solution "+fallback.Moves.Length+", separate editor search "+independent.Moves.Length+".");
                var longer=new AutoSolverResult{Conclusion=AutoSolverConclusion.Solved,ValidationPassed=true,RemainingMoveLimit=budget,
                    Moves=fallback.Moves.Concat(new[]{new AutoSolverMove(0,(fallback.Moves.Last().ColorIndex+1)%6)}).ToArray()};
                Check(AutoSolverSearch.TryValidate(graph,graph.RegionColors,longer.Moves,out _),"Over-budget plan is mathematically valid");
                var playback=new AutoSolverPlaybackController();
                playback.Start(adapter,snapshot,graph,longer,4,.01f,false,false,false,false,out var error);
                Check(!playback.IsPlaying&&error.Contains("Playback blocked")&&game.MoveCount==0,"Over-budget playback blocked before moves");
                if(independent.ValidationPassed&&!independent.FitsMoveBudget)
                {
                    for(int i=0;i<budget&&game.State==FloodFillGameManager3D.GameState.Playing;i++)
                    {
                        var move=independent.Moves[i];adapter.TryInvokeMove(snapshot.Voxels[graph.RepresentativeVoxelIndices[move.RegionId]],move.ColorIndex,out _);
                    }
                    Check(game.State==FloodFillGameManager3D.GameState.Lost&&!board.IsFullyCaptured,"Prior over-budget playback correctly loses");
                    report.AppendLine("Reproduced prior playback: "+game.MoveCount+" moves, "+board.CapturedVoxelCount+" / "+board.TotalVoxelCount+" captured ("+board.CapturedPercentage+"%), state "+game.State+".");
                    Check(board.DisplayedCapturedPercentage<100,"Near-complete loss never displays 100 percent");
                }
                Restore(game,original,budget,adapter.Palette);
                adapter=AutoSolverAdapter.FindActive();adapter.TryCaptureSnapshot(out snapshot,out _);AutoSolverGraphBuilder.TryBuild(snapshot,out graph,out _);
                Check(adapter.TryGetBudgetSolution(snapshot,graph,out fallback),"Original budget solution reusable after deterministic restore");
                playback.Start(adapter,snapshot,graph,fallback,4,.01f,false,false,false,false,out error);
                Check(playback.IsPlaying&&string.IsNullOrEmpty(error),"Within-budget playback accepted");
                double now=EditorApplication.timeSinceStartup;
                for(int i=0;i<=fallback.Moves.Length;i++)playback.Tick(now+=1);
                Check(game.State==FloodFillGameManager3D.GameState.Won&&board.IsFullyCaptured&&game.MoveCount==budget,"Validated budget plan wins on final allowed move");
                Check(board.DisplayedCapturedPercentage==100,"Actual completion displays 100 percent");
                report.AppendLine("Budget solution playback won in "+game.MoveCount+" / "+budget+" moves, capturing "+board.CapturedVoxelCount+" / "+board.TotalVoxelCount+".");
                var near=JsonUtility.FromJson<VoxelBoardSaveData3D>(JsonUtility.ToJson(original));
                for(int i=0;i<near.voxels.Length;i++){near.voxels[i].colorIndex=i==near.voxels.Length-1?1:0;near.voxels[i].captured=i!=near.voxels.Length-1;}
                near.currentPlayerColor=0;
                Restore(game,near,1,adapter.Palette);
                Check(board.CapturedVoxelCount==295&&board.DisplayedCapturedPercentage==99,"295 / 296 HUD stays below 100");
                Check(adapter.CapturedPercentageLabel=="99.66%","Auto Solver near-completion percentage is truthful");
                var displayWindow=ScriptableObject.CreateInstance<AutoSolverWindow>();
                var displayPlayback=(AutoSolverPlaybackController)typeof(AutoSolverWindow).GetField("playback",Flags).GetValue(displayWindow);
                var displayPlan=new AutoSolverResult{Moves=new AutoSolverMove[18]};
                Set(displayWindow,"solution",displayPlan);Set(displayPlayback,"result",displayPlan);Set(displayPlayback,"nextMoveIndex",17);
                var progress=typeof(AutoSolverWindow).GetProperty("PlaybackProgressLabel",Flags);
                Check((string)progress.GetValue(displayWindow)=="17 / 18","Stopped solver progress retains played moves");
                Set(displayWindow,"solution",new AutoSolverResult{Moves=new AutoSolverMove[12]});
                Check((string)progress.GetValue(displayWindow)=="0 / 12","Fresh analysis does not inherit old playback progress");
                UnityEngine.Object.DestroyImmediate(displayWindow);
                game.SelectColor(2);typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(game,new object[]{board.StartingVoxel});
                Check(game.State==FloodFillGameManager3D.GameState.Lost&&board.CapturedVoxelCount==295,"Final wrong move loses with one voxel remaining");
                typeof(FloodFillMobileUI3D).GetMethod("Refresh",Flags).Invoke(ui,null);
                var detail=(TMPro.TMP_Text)typeof(FloodFillMobileUI3D).GetField("resultDetails",Flags).GetValue(ui);
                Check(detail.text.Contains("295 / 296 (99.66%)")&&!detail.text.Contains("100%"),"Result shows exact counts and truthful percentage");
                Check(detail.text.Contains("Moves used: 1 / 1"),"Result shows moves used and given budget");
                var live=AutoSolverAdapter.FindActive();live.TryCaptureSnapshot(out _,out _);
                Check(!live.TryGetBudgetSolution(snapshot,graph,out _),"Initial cached solution not reused after a move");
                Restore(game,near,1,adapter.Palette);game.SelectColor(1);
                typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(game,new object[]{board.StartingVoxel});
                Check(game.State==FloodFillGameManager3D.GameState.Won&&board.CapturedVoxelCount==296,"Completing last voxel on final allowed move wins");
                Check(adapter.CapturedPercentageLabel=="100%","Auto Solver reports 100 only on actual completion");
                File.WriteAllText("/tmp/FloodCompletionVerification.txt",checks+" checks passed.\n"+report);
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);if(window!=null)UnityEngine.Object.DestroyImmediate(window);File.WriteAllText("/tmp/FloodCompletionVerification.txt",report+e.ToString());EditorApplication.Exit(1);}
        }
        private static void Restore(FloodFillGameManager3D game,VoxelBoardSaveData3D saved,int budget,Color[] palette)
        {
            game.StopAllCoroutines();Set(game,"resultCoroutine",null);
            Check(game.BoardManager.RestorePersistentState(saved,palette),"Restore controlled board");
            Set(game,"<MoveCount>k__BackingField",0);Set(game,"<State>k__BackingField",FloodFillGameManager3D.GameState.Playing);
            Set(game,"<SelectedColorIndex>k__BackingField",-1);Set(game,"currentMoveBudget",budget);
            game.BoardManager.SetBoardVisible(true);game.BoardManager.SetInputEnabled(true);
        }
    }
}
