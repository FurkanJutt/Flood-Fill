using System;
using System.IO;
using System.Reflection;
using System.Linq;
using FloodFill.ThreeD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    public static class AutoSolverWorkflowVerification
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static AutoSolverWindow window;
        private static FloodFillGameManager3D manager;
        private static FloodFillMobileUI3D ui;
        private static int stage,checks;
        private static double deadline,readyAt;
        private static string original;
        private static int stoppedMoves;
        private static bool liveSummaryChecked;
        private static void Set(string field,object value)=>typeof(AutoSolverWindow).GetField(field,Flags).SetValue(window,value);
        private static T Get<T>(string field)=>(T)typeof(AutoSolverWindow).GetField(field,Flags).GetValue(window);
        private static void Call(string method)=>typeof(AutoSolverWindow).GetMethod(method,Flags).Invoke(window,null);
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Run in an isolated /tmp project copy.");
            PlayerSettings.companyName="FloodAutoSolverWorkflow";PlayerSettings.productName="FloodAutoSolverWorkflow";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            deadline=EditorApplication.timeSinceStartup+180;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Auto Solver verification timeout, stage "+stage+", "+(window!=null?Get<string>("status"):"no window"));
                manager=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();
                if(ui==null||!ui.IsBuilt)return;
                if(stage==0)
                {
                    window=ScriptableObject.CreateInstance<AutoSolverWindow>();
                    Set("autoStartSolver",false);Set("autoNextLevel",false);Set("playbackSpeed",4f);Set("animationDuration",.01f);
                    Set("enableCaptureAnimation",false);Set("enableColorTransition",false);Set("enableCubePop",false);
                    ui.StartSelectedLevel(2,0,1);stage=1;return;
                }
                if(stage==1)
                {
                    if(!manager.CanInteract)return;
                    manager.BoardManager.TryCapturePersistentState(out var board);original=JsonUtility.ToJson(board);
                    Call("RestartLevel");Check(manager.NumberedLevel==1&&manager.NumberedMode==2,"Restart retains numbered identity");
                    stage=2;readyAt=0;return;
                }
                if(stage==2)
                {
                    if(!manager.CanInteract)return;
                    if(readyAt==0)readyAt=EditorApplication.timeSinceStartup+1;
                    if(EditorApplication.timeSinceStartup<readyAt)return;
                    Check(Get<object>("searchTask")==null&&!Get<AutoSolverPlaybackController>("playback").IsPlaying&&manager.MoveCount==0,"Auto Start off leaves board untouched");
                    manager.BoardManager.TryCapturePersistentState(out var board);Check(JsonUtility.ToJson(board)==original,"Restart regenerates identical board");
                    Set("autoStartSolver",true);stage=20;readyAt=EditorApplication.timeSinceStartup+1;return;
                }
                if(stage==20)
                {
                    if(EditorApplication.timeSinceStartup<readyAt)return;
                    Check(Get<object>("searchTask")==null&&!Get<AutoSolverPlaybackController>("playback").IsPlaying&&manager.MoveCount==0,"Checking Auto Start alone does not solve");
                    Check(!Get<bool>("solverSessionStarted"),"Checkbox does not arm the run");
                    Check(AutoSolverSummaryStore.Shared.Data.attempts.Count==0,"Idle checkbox creates no summary attempts");
                    Call("StartAutoSolve");Check(Get<bool>("solverSessionStarted"),"Start button arms the run");
                    var started=AutoSolverLevelSummary.Build(AutoSolverSummaryStore.Shared.Data.attempts).Single(a=>a.LatestAttempt.level==1);
                    Check(!started.Completed&&started.LatestAttempt.outcome=="Running"&&started.LatestAttempt.moves==0,"Level row appears immediately when solver starts");
                    stage=3;readyAt=0;return;
                }
                if(stage==3)
                {
                    if(!liveSummaryChecked&&manager.State==FloodFillGameManager3D.GameState.Playing&&manager.MoveCount>0)
                    {
                        var live=AutoSolverLevelSummary.Build(AutoSolverSummaryStore.Shared.Data.attempts).Single(a=>a.LatestAttempt.level==1);
                        Check(!live.Completed&&live.LatestAttempt.moves==manager.MoveCount&&!string.IsNullOrEmpty(live.LatestAttempt.updatedUtc),"Unfinished row updates during actual playback");
                        liveSummaryChecked=true;
                    }
                    if(manager.State!=FloodFillGameManager3D.GameState.Won)return;
                    if(readyAt==0)readyAt=EditorApplication.timeSinceStartup+1.5;
                    if(EditorApplication.timeSinceStartup<readyAt)return;
                    Check(manager.NumberedLevel==1&&manager.MoveCount>0,"Auto solve wins; Auto Next off does not advance");
                    Check(liveSummaryChecked,"Observed live summary before first win");
                    Check(FloodLevelProgression3D.Get(2,0,1).completed,"Auto solve records numbered progression");
                    var summary=AutoSolverSummaryStore.Shared.Data.attempts.Single(a=>a.level==1);
                    Check(summary.Won&&summary.moves==manager.MoveCount&&summary.moveBudget==manager.CurrentMoveBudget,"Summary records actual win and budget");
                    Check(summary.seed==manager.BoardManager.LastGenerationSeed&&summary.mode==2&&summary.difficulty==0,"Summary records identity difficulty and seed");
                    Check(summary.solverMoves==manager.MoveCount&&summary.searchMilliseconds>0&&summary.finishedUtc!=null,"Summary captures solver moves timing and completion date");
                    Call("StartNextLevel");
                    Check(manager.NumberedLevel==2&&manager.NumberedMode==2&&(int)manager.SelectedDifficulty==0,"Manual Next retains mode and difficulty");
                    stage=4;return;
                }
                if(stage==4)
                {
                    if(manager.State!=FloodFillGameManager3D.GameState.Won)return;
                    Check(manager.NumberedLevel==2&&manager.MoveCount>0,"Auto Start solves the next loaded board");
                    Set("autoNextLevel",true);stage=5;return;
                }
                if(stage==5)
                {
                    if(manager.NumberedLevel<3)return;
                    Check(manager.NumberedLevel==3&&manager.NumberedMode==2,"Auto Next advances exactly once in category");
                    Check(FloodLevelProgression3D.Get(2,0,2).completed,"Second win saves progression");
                    Call("StopAutoSolve");
                    Check(!Get<bool>("solverSessionStarted")&&Get<bool>("autoStartSolver")&&Get<bool>("autoNextLevel"),"Stop disarms run and preserves checkbox preferences");
                    stage=6;readyAt=0;return;
                }
                if(stage==6)
                {
                    if(!manager.CanInteract)return;
                    if(readyAt==0){stoppedMoves=manager.MoveCount;readyAt=EditorApplication.timeSinceStartup+1;}
                    if(EditorApplication.timeSinceStartup<readyAt)return;
                    Check(Get<object>("searchTask")==null&&!Get<AutoSolverPlaybackController>("playback").IsPlaying&&manager.MoveCount==stoppedMoves,"Stop prevents automatic solving of the next loaded board");
                    Call("StartAutoSolve");Call("StopAutoSolve");Call("StartAutoSolve");Call("StopAutoSolve");
                    var tries=AutoSolverSummaryStore.Shared.Data.attempts.Where(a=>a.level==3).ToList();
                    Check(tries.Count==1&&tries[0].searches==2,"Stopping and restarting solver on same board retains one try");
                    Call("RestartLevel");stage=60;return;
                }
                if(stage==60)
                {
                    if(!manager.CanInteract)return;
                    Call("StartAutoSolve");Call("StopAutoSolve");
                    Check(AutoSolverSummaryStore.Shared.Data.attempts.Count(a=>a.level==3)==2,"Board restart counts a new try");
                    Get<AutoSolverSummaryRecorder>("summaryRecorder").Finish("Stopped");
                    var adapter=AutoSolverAdapter.FindActive();adapter.TryCaptureSnapshot(out var snapshot,out _);
                    var reopenedRecorder=new AutoSolverSummaryRecorder();reopenedRecorder.Begin(adapter,snapshot);
                    Check(AutoSolverSummaryStore.Shared.Data.attempts.Count(a=>a.level==3)==2,"Reopening recorder on same board does not add a try");
                    reopenedRecorder.Finish("Window closed");
                    AutoSolverSummaryWindow.OpenWindow();
                    typeof(FloodFillGameManager3D).GetField("<NumberedLevel>k__BackingField",Flags).SetValue(manager,100);
                    typeof(FloodFillGameManager3D).GetField("<State>k__BackingField",Flags).SetValue(manager,FloodFillGameManager3D.GameState.Won);
                    Check(!AutoSolverAdapter.FindActive().CanStartNextLevel,"Next disabled at level 100");
                    Set("solverSessionStarted",true);Set("autoNextLevel",true);Call("TickAutomation");Check(manager.NumberedLevel==100,"Auto Next never requests 101");
                    ui.LevelSelectionHome();manager.ReturnToMenu();Set("autoStartSolver",true);
                    readyAt=EditorApplication.timeSinceStartup+1;stage=7;return;
                }
                if(stage==7)
                {
                    if(EditorApplication.timeSinceStartup<readyAt)return;
                    Check(Get<object>("searchTask")==null&&!Get<AutoSolverPlaybackController>("playback").IsPlaying,"Automation stays idle with menu board hidden");
                    UnityEngine.Object.DestroyImmediate(window);window=null;
                    File.WriteAllText("/tmp/FloodAutoSolverWorkflowVerification.txt",checks+" checks passed: deterministic numbered restart, manual Next, checkbox idle until Start, Stop disarms continuation, independent automation toggles, actual solver wins on successive Items levels, saved progression, level 100 boundary, and hidden-menu guard.\n");
                    EditorApplication.Exit(0);
                }
            }
            catch(Exception e)
            {
                Debug.LogException(e);if(window!=null)UnityEngine.Object.DestroyImmediate(window);
                File.WriteAllText("/tmp/FloodAutoSolverWorkflowVerification.txt",e.ToString());EditorApplication.Exit(1);
            }
        }
    }
}
