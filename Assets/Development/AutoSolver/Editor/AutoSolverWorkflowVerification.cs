using System;
using System.IO;
using System.Reflection;
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
                    Set("autoStartSolver",true);stage=3;readyAt=0;return;
                }
                if(stage==3)
                {
                    if(manager.State!=FloodFillGameManager3D.GameState.Won)return;
                    if(readyAt==0)readyAt=EditorApplication.timeSinceStartup+1.5;
                    if(EditorApplication.timeSinceStartup<readyAt)return;
                    Check(manager.NumberedLevel==1&&manager.MoveCount>0,"Auto solve wins; Auto Next off does not advance");
                    Check(FloodLevelProgression3D.Get(2,0,1).completed,"Auto solve records numbered progression");
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
                    Set("autoNextLevel",false);Set("autoStartSolver",false);Call("CancelSearch");Get<AutoSolverPlaybackController>("playback").Stop();
                    stage=6;return;
                }
                if(stage==6)
                {
                    if(!manager.CanInteract)return;
                    typeof(FloodFillGameManager3D).GetField("<NumberedLevel>k__BackingField",Flags).SetValue(manager,100);
                    typeof(FloodFillGameManager3D).GetField("<State>k__BackingField",Flags).SetValue(manager,FloodFillGameManager3D.GameState.Won);
                    Check(!AutoSolverAdapter.FindActive().CanStartNextLevel,"Next disabled at level 100");
                    Set("autoNextLevel",true);Call("TickAutomation");Check(manager.NumberedLevel==100,"Auto Next never requests 101");
                    ui.LevelSelectionHome();manager.ReturnToMenu();Set("autoStartSolver",true);
                    readyAt=EditorApplication.timeSinceStartup+1;stage=7;return;
                }
                if(stage==7)
                {
                    if(EditorApplication.timeSinceStartup<readyAt)return;
                    Check(Get<object>("searchTask")==null&&!Get<AutoSolverPlaybackController>("playback").IsPlaying,"Automation stays idle with menu board hidden");
                    UnityEngine.Object.DestroyImmediate(window);window=null;
                    File.WriteAllText("/tmp/FloodAutoSolverWorkflowVerification.txt",checks+" checks passed: deterministic numbered restart, manual Next, independent automation toggles, actual solver wins on successive Items levels, saved progression, level 100 boundary, and hidden-menu guard.\n");
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
