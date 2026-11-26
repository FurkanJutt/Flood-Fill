using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FloodFill.ThreeD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodSaveRecoveryVerification3D
    {
        private static FloodFillGameManager3D game;
        private static FloodFillMobileUI3D ui;
        private static int stage,checks;
        private static double deadline;
        private static string path;
        private const string Damaged="{\"version\":999}",Checkpoint="{\"keepPreviousCheckpoint\":true}";
        private static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
        private static GameObject Field(string name)=>(GameObject)typeof(FloodFillMobileUI3D).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
        private static Button Action(FloodFillUIAction action)=>ui.GetComponentsInChildren<FloodFillUIAction3D>(true).Single(b=>b.action==action).GetComponent<Button>();
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated project.");
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            PlayerSettings.companyName=PlayerSettings.productName="FloodSaveRecoveryVerification";PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            path=FloodFillSessionPersistence3D.LevelPath(0,0,1);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,Damaged);
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static string Hash(string json){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","");}
        private static void ClearCache()
        {
            foreach(string name in new[]{"Levels","LevelOrder"})
            {var collection=typeof(FloodFillSessionPersistence3D).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);collection.GetType().GetMethod("Clear").Invoke(collection,null);}
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Recovery timeout stage "+stage);
                game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();if(ui==null||!ui.IsBuilt)return;
                if(stage==0){File.WriteAllText(FloodFillSessionPersistence3D.SavePath,Checkpoint);ui.StartSelectedLevel(0,0,1);stage=1;return;}
                if(stage==1)
                {
                    if(!game.CanRestartUnreadableSavedLevel)return;
                    Check(Action(FloodFillUIAction.PromptSavedLevelRestart).gameObject.activeInHierarchy,"Recovery button appears on damaged-save error");
                    Check(File.ReadAllText(path)==Damaged&&File.ReadAllText(FloodFillSessionPersistence3D.SavePath)==Checkpoint,"Error preserves definition and prior checkpoint");
                    Action(FloodFillUIAction.PromptSavedLevelRestart).onClick.Invoke();
                    Check(Field("savedLevelRestartConfirmation").activeInHierarchy,"Restart button opens confirmation");
                    Check(Field("savedLevelRestartConfirmation").GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="This will overwrite the previous save. do you want to start over?"),"Exact overwrite warning is displayed");
                    Check(File.ReadAllText(path)==Damaged,"Opening warning does not overwrite");
                    Action(FloodFillUIAction.CancelSavedLevelRestart).onClick.Invoke();
                    Check(!Field("savedLevelRestartConfirmation").activeSelf&&File.ReadAllText(path)==Damaged&&File.ReadAllText(FloodFillSessionPersistence3D.SavePath)==Checkpoint,"Back preserves both saves and returns to error");
                    Action(FloodFillUIAction.PromptSavedLevelRestart).onClick.Invoke();Action(FloodFillUIAction.ConfirmSavedLevelRestart).onClick.Invoke();stage=2;return;
                }
                if(stage==2)
                {
                    if(!string.IsNullOrEmpty(game.LoadingError))throw new Exception(game.LoadingError);
                    if(!game.CanInteract)return;
                    Check(File.Exists(path)&&File.ReadAllText(path)!=Damaged,"Confirmed restart writes a regenerated definition");
                    Check(Directory.GetFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".replaced-*.bak").Any(p=>File.ReadAllText(p)==Damaged),"Previous bad file is archived");
                    Check(!game.CanRestartUnreadableSavedLevel&&!Action(FloodFillUIAction.PromptSavedLevelRestart).gameObject.activeSelf,"Recovery option hides after successful restart");
                    Check(!game.RestartUnreadableSavedLevel(),"Recovery API rejects healthy gameplay");
                    ClearCache();Check(FloodFillSessionPersistence3D.TryLoadLevel(0,0,1,out var definition,out _),"Regenerated definition reloads successfully");
                    string canonical=FloodFillSessionPersistence3D.BoardHash(definition.board);
                    foreach(var voxel in definition.board.voxels)voxel.covered=voxel.captured;
                    Check(FloodFillSessionPersistence3D.BoardHash(definition.board)==canonical,"Attempt coverage never changes puzzle checksum");
                    definition.boardHash=Hash(JsonUtility.ToJson(definition.board));File.WriteAllText(path,JsonUtility.ToJson(definition));ClearCache();
                    Check(FloodFillSessionPersistence3D.TryLoadLevel(0,0,1,out _,out _),"Coverage-rollout hash survives compact snapshot unpacking");
                    definition.boardHash=canonical;File.WriteAllText(path,JsonUtility.ToJson(definition));ClearCache();
                    Check(FloodFillSessionPersistence3D.TryLoadLevel(0,0,1,out _,out _),"Pre-coverage canonical hash remains compatible");
                    definition.boardHash="TAMPERED";File.WriteAllText(path,JsonUtility.ToJson(definition));ClearCache();
                    Check(!FloodFillSessionPersistence3D.TryLoadLevel(0,0,1,out _,out _),"Genuinely invalid checksum is still rejected");
                    File.WriteAllText("/tmp/FloodSaveRecoveryVerification.txt",checks+" checks passed: confirmation/back wiring, unchanged files before confirmation, checkpoint preservation, archived bad file, regeneration, healthy-state guards and legacy/coverage checksum compatibility. Isolated test identity; original player saves untouched.\n");EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodSaveRecoveryVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
    }
}
