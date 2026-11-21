using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FloodFill.ThreeD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.EditorTools
{
    public static class FloodContinueVerification3D
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static FloodFillGameManager3D game;
        private static FloodFillMobileUI3D ui;
        private static int stage,combination,checks;
        private static bool cold;
        private static double deadline;
        private static FloodFillSessionSaveData3D expected;
        private static string boardJson;
        private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        private static T Read<T>(object owner,string field)=>(T)owner.GetType().GetField(field,Flags).GetValue(owner);
        private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Flags).SetValue(owner,value);
        public static void BeginBatch() => Begin(false);
        public static void BeginColdBatch() => Begin(true);
        private static void Begin(bool resume)
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp copy");
            cold=resume;
            PlayerSettings.companyName="FloodContinueVerification";PlayerSettings.productName="FloodContinueVerification";
            if(!cold){PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();}
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            deadline=EditorApplication.timeSinceStartup+240;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void RefreshMenu()
        {
            typeof(FloodFillMobileUI3D).GetMethod("RefreshContinue",Flags).Invoke(ui,null);
        }
        private static void CheckUnavailable(string message)
        {
            RefreshMenu();var button=Read<GameObject>(ui,"continueButton");
            Check(!game.HasSavedSession&&!button.activeSelf&&!button.GetComponent<Button>().interactable,message+": hidden and disabled");
            ui.ExecuteAuthoredAction(FloodFillUIAction.Continue,0);
            Check(Read<GameObject>(ui,"menu").activeSelf&&!Read<GameObject>(ui,"hud").activeSelf,message+": no empty HUD");
        }
        private static FloodFillSessionSaveData3D Copy() => JsonUtility.FromJson<FloodFillSessionSaveData3D>(JsonUtility.ToJson(expected));
        private static void Invalid(string label,Action<FloodFillSessionSaveData3D> change)
        {
            var saved=Copy();change(saved);Check(FloodFillSessionPersistence3D.Save(saved),label+": write fixture");CheckUnavailable(label);
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Continue timeout at "+stage+" / "+combination);
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();
                if(ui==null||!ui.IsBuilt)return;
                if(cold)
                {
                    if(stage==0)
                    {
                        Check(FloodFillSessionPersistence3D.TryLoad(out expected),"Cold saved file exists");
                        Check(game.HasSavedSession&&game.BoardManager.HasActiveBoard,"Cold start restores nonempty saved board");
                        Check(Read<GameObject>(ui,"continueButton").activeSelf,"Cold Continue visible");
                        game.PauseGame();Check(!game.CanInteract,"Paused state before Continue");
                        ui.ExecuteAuthoredAction(FloodFillUIAction.Continue,0);stage=1;return;
                    }
                    if(game.IsLoading||!game.CanInteract)return;
                    AssertResumed();Finish("/tmp/FloodContinueColdVerification.txt","Separate-process Continue restores exact unfinished board, identity, moves and budget.");return;
                }
                if(stage==0)
                {
                    CheckUnavailable("Fresh game");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Play,0);ui.LevelSelectionHome();
                    CheckUnavailable("Visiting Levels without starting");
                    Check(!File.Exists(FloodFillSessionPersistence3D.SavePath),"Menu navigation never invents a save");
                    stage=1;
                }
                if(stage==1)
                {
                    ui.StartSelectedLevel(combination/4,combination%4,1);stage=2;return;
                }
                if(stage==2)
                {
                    if(game.IsLoading||!game.CanInteract)return;
                    game.SelectColor((game.CurrentPlayerColor+1)%6);
                    typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(game,new object[]{game.BoardManager.StartingVoxel});
                    Check(game.MoveCount==1&&game.State==FloodFillGameManager3D.GameState.Playing,"Create unfinished game");
                    PlayerPrefs.SetInt("FloodFill3D.ShowCurrentBlock",combination%2);
                    ui.ExecuteAuthoredAction(FloodFillUIAction.ReturnToMenu,0);
                    Check(FloodFillSessionPersistence3D.TryLoad(out expected),"Unfinished checkpoint saved");
                    boardJson=JsonUtility.ToJson(expected.board);
                    Check(game.HasSavedSession&&Read<GameObject>(ui,"continueButton").activeSelf,"Valid checkpoint enables Continue");
                    game.BoardManager.ClearBoard();Check(!game.BoardManager.HasActiveBoard&&!game.CanInteract,"Reproduce empty in-memory board");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Continue,0);
                    Check(game.IsLoading&&game.BoardManager.HasActiveBoard,"Continue restores actual voxels before showing gameplay");
                    stage=3;return;
                }
                if(stage==3)
                {
                    if(game.IsLoading||!game.CanInteract)return;
                    AssertResumed();
                    if(++combination<12){ui.ExecuteAuthoredAction(FloodFillUIAction.ReturnToMenu,0);stage=1;return;}
                    ui.ExecuteAuthoredAction(FloodFillUIAction.ReturnToMenu,0);stage=4;
                }
                if(stage==4)
                {
                    Invalid("Won game",s=>s.gameState=(int)FloodFillGameManager3D.GameState.Won);
                    Invalid("Lost game",s=>s.gameState=(int)FloodFillGameManager3D.GameState.Lost);
                    Invalid("Exhausted budget",s=>s.moveCount=s.currentMoveBudget);
                    Invalid("Empty voxel save",s=>s.board.voxels=Array.Empty<VoxelSaveData3D>());
                    Invalid("Missing board",s=>s.board=null);
                    Invalid("Duplicate voxels",s=>{s.board.voxels[1].x=s.board.voxels[0].x;s.board.voxels[1].y=s.board.voxels[0].y;s.board.voxels[1].z=s.board.voxels[0].z;});
                    Invalid("Bad color",s=>s.board.voxels[0].colorIndex=99);
                    Invalid("Missing starting voxel",s=>{foreach(var v in s.board.voxels)v.starting=false;});
                    Invalid("No captured region",s=>{foreach(var v in s.board.voxels)v.captured=false;});
                    Invalid("Fully completed board with Playing flag",s=>{foreach(var v in s.board.voxels){v.captured=true;v.colorIndex=s.board.currentPlayerColor;}});
                    Invalid("Disconnected captured region",s=>
                    {
                        var a=s.board.voxels[0];var b=s.board.voxels.Last(v=>Math.Abs(v.x-a.x)+Math.Abs(v.y-a.y)+Math.Abs(v.z-a.z)>1);
                        foreach(var v in s.board.voxels){v.captured=false;v.colorIndex=(s.board.currentPlayerColor+1)%6;}
                        a.captured=b.captured=true;a.colorIndex=b.colorIndex=s.board.currentPlayerColor;
                    });
                    Invalid("Disconnected occupied board",s=>
                    {
                        var a=s.board.voxels[0];var b=s.board.voxels.Last(v=>Math.Abs(v.x-a.x)+Math.Abs(v.y-a.y)+Math.Abs(v.z-a.z)>1);
                        a.starting=true;b.starting=false;a.captured=true;b.captured=false;a.colorIndex=0;b.colorIndex=1;
                        s.board.currentPlayerColor=0;s.board.voxels=new[]{a,b};
                    });
                    Invalid("Mismatched dimensions",s=>s.board.width++);
                    Invalid("Invalid level identity",s=>s.numberedLevel=101);
                    Invalid("Invalid difficulty",s=>s.difficulty=99);
                    Invalid("Inconsistent mode",s=>s.numberedMode=0);
                    File.WriteAllText(FloodFillSessionPersistence3D.SavePath,"{invalid");CheckUnavailable("Malformed JSON");
                    FloodFillSessionPersistence3D.Save(expected);RefreshMenu();
                    Check(game.HasSavedSession,"Repaired file is recognized");
                    File.Delete(FloodFillSessionPersistence3D.SavePath);Check(game.HasSavedSession,"Cached flag was still true before click");
                    CheckUnavailable("File removed after menu appeared");
                    FloodFillSessionPersistence3D.Save(expected);
                    var prefab=game.BoardManager.VoxelPrefab;Set(game.BoardManager,"voxelPrefab",null);CheckUnavailable("Missing voxel prefab");Set(game.BoardManager,"voxelPrefab",prefab);
                    var boardRoot=Read<Transform>(game.BoardManager,"boardRoot");Set(game.BoardManager,"boardRoot",null);CheckUnavailable("Missing board root");Set(game.BoardManager,"boardRoot",boardRoot);
                    // Legacy saves can have a known budget without solver-profile metadata.
                    expected.moveBudgetProfile=default;
                    // Preserve a valid unfinished checkpoint for the separate cold-start process.
                    Check(FloodFillSessionPersistence3D.Save(expected),"Restore valid checkpoint without solver metadata");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Continue,0);stage=5;return;
                }
                if(stage==5)
                {
                    if(game.IsLoading||!game.CanInteract)return;
                    AssertResumed();
                    ui.ExecuteAuthoredAction(FloodFillUIAction.ReturnToMenu,0);
                    Finish("/tmp/FloodContinueVerification.txt","All 12 mode/difficulty continuations, restored empty boards, menu previews, exact snapshots, invalid/completed/missing saves, no phantom Continue or empty HUD.");
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText(cold?"/tmp/FloodContinueColdVerification.txt":"/tmp/FloodContinueVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void AssertResumed()
        {
            Check(game.BoardManager.HasActiveBoard&&game.BoardManager.TotalVoxelCount==expected.board.voxels.Length,"All saved voxels restored");
            Check(game.BoardManager.TryCapturePersistentState(out var current)&&JsonUtility.ToJson(current)==JsonUtility.ToJson(expected.board),"Exact board colors capture seed and dimensions preserved");
            Check(game.MoveCount==expected.moveCount&&game.CurrentMoveBudget==expected.currentMoveBudget&&game.Score==expected.score,"Saved moves budget and score preserved, including legacy budget metadata");
            Check(game.NumberedLevel==expected.numberedLevel&&game.NumberedMode==expected.numberedMode&&(int)game.SelectedDifficulty==expected.difficulty,"Saved level identity preserved");
            var root=Read<Transform>(game.BoardManager,"boardRoot");Check(root.gameObject.activeInHierarchy&&Read<GameObject>(ui,"hud").activeSelf,"Visible board and correct HUD");
            Check(game.BoardManager.TryGetWorldBounds(out var bounds)&&bounds.size.sqrMagnitude>0,"Restored board has renderable bounds");
        }
        private static void Finish(string path,string description){File.WriteAllText(path,checks+" checks passed. "+description+"\n");EditorApplication.Exit(0);}
    }
}
