using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Solver;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.EditorTools
{
    /// <summary>Batch-only integration checks, using a separate test identity and persistent-data directory.</summary>
    public static class FloodLevelSelectionVerification3D
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static int checks, stage, combination, move;
        private static double deadline;
        private static FloodFillMobileUI3D ui;
        private static FloodFillGameManager3D manager;
        private static FloodLevelSelection3D levels;
        private static FloodFillSolverResult solution;
        private static FloodFillRegionGraph3D graph;
        private static string boardJson;
        private static bool replay, layoutOnly;
        public static void BeginLayoutBatch() { layoutOnly=true; BeginBatch(); }
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        public static void BeginBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.StartsWith("/private/tmp/") && !Application.dataPath.StartsWith("/tmp/")) throw new Exception("Run in an isolated /tmp copy.");
            PlayerSettings.companyName="FloodLevelVerification";PlayerSettings.productName="FloodLevelVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            FloodLevelSelectionSetup3D.Install();
            if(!layoutOnly)CheckCatalog();
            PlayerPrefs.DeleteKey("FloodFill3D.LevelCatalog.v1");
            typeof(FloodLevelProgression3D).GetField("data",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            deadline=EditorApplication.timeSinceStartup+600;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static string savedBeforePrompt, initialBoard;
        public static void BeginRestartConfirmationBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp copy");
            PlayerSettings.companyName="FloodRestartConfirmationVerification";PlayerSettings.productName="FloodRestartConfirmationVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            FloodLevelSelectionSetup3D.InstallRestartConfirmation();
            FloodLevelSelectionSetup3D.InstallRestartConfirmation();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            stage=0;checks=0;deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.update+=TickRestartConfirmation;EditorApplication.EnterPlaymode();
        }
        private static void MakeOneMove()
        {
            manager.SelectColor((manager.CurrentPlayerColor+1)%6);
            typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(manager,new object[]{manager.BoardManager.StartingVoxel});
        }
        private static void TickRestartConfirmation()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Restart confirmation verification timed out at stage "+stage);
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();manager=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();
                if(ui==null||!ui.IsBuilt)return;
                levels=ui.GetComponentInChildren<FloodLevelSelection3D>(true);
                if(stage==0){Check(!ui.WouldRestartSavedLevel(2,0,1),"Fresh game needs no confirmation");ui.StartSelectedLevel(2,0,1);stage=1;return;}
                if(stage==1)
                {
                    if(!manager.CanInteract)return;
                    manager.BoardManager.TryCapturePersistentState(out var board);initialBoard=JsonUtility.ToJson(board);
                    MakeOneMove();Check(manager.MoveCount==1,"In-progress level has a real move");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.ReturnToMenu,0);Check(manager.HasSavedSession,"Continue available");
                    savedBeforePrompt=File.ReadAllText(FloodFillSessionPersistence3D.SavePath);
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Play,0);levels.Select(2,0);
                    Check(ui.WouldRestartSavedLevel(2,0,1),"Saved identity matches");
                    Check(!ui.WouldRestartSavedLevel(1,0,1)&&!ui.WouldRestartSavedLevel(2,1,1)&&!ui.WouldRestartSavedLevel(2,0,2),"Mode difficulty and level compared independently");
                    levels.StartLevel(2);Check(!levels.restartPrompt.activeSelf&&!manager.IsLoading,"Locked tile does not open prompt or start");
                    levels.StartLevel(1);Check(levels.restartPrompt.activeSelf&&!manager.IsLoading,"Saved tile opens confirmation before restarting");
                    Check(!levels.navigationContent.interactable&&!levels.navigationContent.blocksRaycasts,"Background controls blocked");
                    levels.StartLevel(1);Check(manager.MoveCount==1,"Repeated tile click does not restart");
                    Check(File.ReadAllText(FloodFillSessionPersistence3D.SavePath)==savedBeforePrompt,"Opening prompt preserves save");
                    Capture(876,1796,"RestartConfirmation.png");Capture(750,1334,"RestartConfirmation-Small.png");
                    levels.restartBack.onClick.Invoke();Check(!levels.restartPrompt.activeSelf&&levels.navigationContent.interactable,"Back dismisses and restores navigation");
                    Check(manager.HasSavedSession&&manager.MoveCount==1&&File.ReadAllText(FloodFillSessionPersistence3D.SavePath)==savedBeforePrompt,"Back preserves Continue and progress");
                    levels.StartLevel(1);levels.restartContinue.onClick.Invoke();
                    Check(!levels.restartPrompt.activeSelf&&manager.IsLoading,"Continue confirms restart once");
                    stage=2;return;
                }
                if(stage==2)
                {
                    if(!manager.CanInteract)return;
                    manager.BoardManager.TryCapturePersistentState(out var board);
                    Check(manager.NumberedLevel==1&&manager.NumberedMode==2&&manager.MoveCount==0&&JsonUtility.ToJson(board)==initialBoard,"Confirmed restart resets same deterministic level");
                    MakeOneMove();ui.ExecuteAuthoredAction(FloodFillUIAction.ReturnToMenu,0);ui.ExecuteAuthoredAction(FloodFillUIAction.Continue,0);
                    Check(manager.MoveCount==1&&!levels.restartPrompt.activeSelf,"Main menu Continue resumes without confirmation");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.ReturnToMenu,0);ui.ExecuteAuthoredAction(FloodFillUIAction.Play,0);
                    levels.Select(2,1);levels.StartLevel(1);
                    Check(!levels.restartPrompt.activeSelf&&manager.IsLoading&&(int)manager.SelectedDifficulty==1,"Different difficulty starts without matching-save prompt");
                    File.WriteAllText("/tmp/FloodRestartConfirmationVerification.txt",checks+" checks passed: real in-progress save, matching identity, locked tiles, modal input blocking, unchanged save on Back, confirmed deterministic restart, normal Continue resume, category isolation, repeatable installation and two portrait renders.\n");
                    EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodRestartConfirmationVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        public static void BeginResultBatch()
        {
            if(!Application.isBatchMode || !Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp copy");
            PlayerSettings.companyName="FloodResultVerification";PlayerSettings.productName="FloodResultVerification";
            FloodLevelSelectionSetup3D.RepairResultLayout();
            // Running the repair twice must not duplicate controls or listeners.
            FloodLevelSelectionSetup3D.RepairResultLayout();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
            stage=0;checks=0;deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.update+=TickResult;EditorApplication.EnterPlaymode();
        }
        private static void TickResult()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Result verification timed out");
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();manager=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();
                if(ui==null||!ui.IsBuilt)return;
                if(stage==0){ui.StartSelectedLevel(0,0,1);stage=1;return;}
                if(manager.IsLoading||!manager.CanInteract&&stage==1)return;
                bool won=stage!=2;
                typeof(FloodFillGameManager3D).GetField("<State>k__BackingField",Flags).SetValue(manager,won?FloodFillGameManager3D.GameState.Won:FloodFillGameManager3D.GameState.Lost);
                typeof(FloodFillMobileUI3D).GetMethod("Refresh",Flags).Invoke(ui,null);
                var screen=ui.EditorScreens.Single(value=>value.name=="GameResults");
                var actions=screen.transform.Find("Content/ResultActions").GetComponent<RectTransform>();
                Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
                var bindings=screen.GetComponentsInChildren<FloodFillUIAction3D>(true);
                Check(bindings.Length==4,"Exactly four authored result actions");
                Check(bindings.Single(b=>b.action==FloodFillUIAction.Restart).gameObject.activeSelf==won,"Separate replay appears only on wins");
                var primary=bindings.Single(b=>b.action==FloodFillUIAction.Result);
                Check(primary.GetComponentInChildren<TMP_Text>(true).text==(won?"NEXT LEVEL":"PLAY AGAIN"),"Primary result label");
                var buttons=actions.GetComponentsInChildren<Button>(false);Check(buttons.Length==(won?4:3),"Correct visible action count");
                var bounds=buttons.Select(b=>RectTransformUtility.CalculateRelativeRectTransformBounds(screen.transform,b.transform)).ToArray();
                for(int i=0;i<bounds.Length;i++)for(int j=i+1;j<bounds.Length;j++)Check(!bounds[i].Intersects(bounds[j]),"Result buttons do not overlap");
                var card=RectTransformUtility.CalculateRelativeRectTransformBounds(screen.transform,screen.transform.Find("Content/Card"));
                foreach(var bound in bounds)Check(card.Contains(bound.min)&&card.Contains(bound.max),"Result action fits inside card");
                var fps=(TMP_Text)typeof(FloodFillMobileUI3D).GetField("fpsLabel",Flags).GetValue(ui);if(fps!=null)fps.transform.parent.gameObject.SetActive(false);
                if(stage<=2){Capture(876,1796,won?"Result-Win.png":"Result-Loss.png");Capture(750,1334,won?"Result-Win-Small.png":"Result-Loss-Small.png");}
                if(stage++<3)return;
                File.WriteAllText("/tmp/FloodResultVerification.txt",checks+" checks passed: win, loss, win again; labels, replay visibility, nonoverlapping actions within card, four portrait renders, repeatable prefab repair.\n");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodResultVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void RestartIdentity()
        {
            if(!Application.isBatchMode || !Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp copy");
            PlayerSettings.companyName="FloodLevelRestartVerification";PlayerSettings.productName="FloodLevelRestartVerification";
        }
        public static void SeedPersistenceBatch()
        {
            RestartIdentity();PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt("FloodFill3D.CampaignLevel",47);
            FloodLevelProgression3D.Record(2,3,1,9,3);PlayerPrefs.Save();
        }
        public static void VerifyRestartedCatalogBatch()
        {
            RestartIdentity();
            Check(FloodLevelProgression3D.Completed(2,3)==1,"Completion survives process restart");
            var result=FloodLevelProgression3D.Get(2,3,1);
            Check(result.bestMoves==9&&result.stars==3&&FloodLevelProgression3D.Unlocked(2,3,2),"Best result and unlock survive restart");
            Check(FloodLevelProgression3D.Completed(0,0)==0&&PlayerPrefs.GetInt("FloodFill3D.CampaignLevel")==47,"Isolation and legacy preservation after restart");
            File.WriteAllText("/tmp/FloodLevelRestartVerification.txt",checks+" separate-process persistence checks passed.\n");
        }
        private static void CheckCatalog()
        {
            var seeds=new System.Collections.Generic.HashSet<int>();
            var legacy=JsonUtility.FromJson<FloodFillSessionSaveData3D>("{\"version\":1,\"campaignLevel\":17}");
            Check(legacy.version==1&&legacy.numberedLevel==0&&legacy.campaignLevel==17,"Old session schema still readable");
            for(int d=0;d<4;d++)for(int l=1;l<=100;l++)
            {
                int size=new[]{6,8,10,12,15,18,20}[Mathf.Min(6,(l-1)/15)];
                int seed=FloodLevelProgression3D.Seed(2,d,l);
                var mask=FloodItemShape3D.Generate(size,size,size,seed,out var bounds);
                var again=FloodItemShape3D.Generate(size,size,size,seed,out _);
                int count=0;var positions=new System.Collections.Generic.HashSet<Vector3Int>();
                for(int x=0;x<size;x++)for(int y=0;y<size;y++)for(int z=0;z<size;z++){Check(mask[x,y,z]==again[x,y,z],"Items deterministic mask");if(mask[x,y,z]){count++;positions.Add(new Vector3Int(x,y,z));}}
                Check(count>1&&bounds.MaxX>=bounds.MinX,"Playable Items mask");
                var queue=new System.Collections.Generic.Queue<Vector3Int>();var start=positions.First();queue.Enqueue(start);positions.Remove(start);
                var offsets=new[]{Vector3Int.left,Vector3Int.right,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
                while(queue.Count>0){var cell=queue.Dequeue();foreach(var offset in offsets)if(positions.Remove(cell+offset))queue.Enqueue(cell+offset);}
                Check(positions.Count==0,"Items connected mask");
            }
            PlayerPrefs.SetInt("FloodFill3D.CampaignLevel",17);
            for(int m=0;m<3;m++)for(int d=0;d<4;d++)
            {
                Check(FloodLevelProgression3D.Completed(m,d)==0,"Fresh category");
                for(int l=1;l<=100;l++){Check(seeds.Add(FloodLevelProgression3D.Seed(m,d,l)),"Unique stable identity");Check(FloodLevelProgression3D.Unlocked(m,d,l)==(l==1),"Initial locking");}
                FloodLevelProgression3D.Record(m,d,1,20,3);FloodLevelProgression3D.Record(m,d,1,30,1);
                Check(FloodLevelProgression3D.Completed(m,d)==1,"Replay not double counted");
                var r=FloodLevelProgression3D.Get(m,d,1);Check(r.bestMoves==20&&r.stars==3,"Best result retained");
                Check(FloodLevelProgression3D.Unlocked(m,d,2)&&!FloodLevelProgression3D.Unlocked(m,d,3),"Sequential unlock");
            }
            typeof(FloodLevelProgression3D).GetField("data",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
            for(int m=0;m<3;m++)for(int d=0;d<4;d++)Check(FloodLevelProgression3D.Completed(m,d)==1,"Reload persistence");
            Check(PlayerPrefs.GetInt("FloodFill3D.CampaignLevel")==17,"Legacy campaign preserved");
            bool invalid=false;try{FloodLevelProgression3D.Index(0,0,101);}catch(ArgumentOutOfRangeException){invalid=true;}Check(invalid,"Level 101 rejected");
        }
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Integration verification timed out at stage "+stage);
                if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
                if(stage==0)
                {
                    ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();manager=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();if(ui==null||!ui.IsBuilt)return;
                    levels=ui.GetComponentInChildren<FloodLevelSelection3D>(true);
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Play,0);Check(levels.gameObject.activeSelf,"PLAY opens levels");
                    Check(!ui.EditorScreens.Single(x=>x!=null&&x.name=="MainMenu").activeSelf,"Menu hides");
                    for(int m=0;m<3;m++)for(int d=0;d<4;d++)
                    {
                        levels.modes[m].onClick.Invoke();levels.difficulties[d].onClick.Invoke();Check(levels.Mode==m&&levels.Difficulty==d,"Tab context");
                        for(int p=0;p<4;p++)
                        {
                            levels.dots[p].onClick.Invoke();
                            var tiles=levels.grid.GetComponentsInChildren<FloodLevelButton3D>(false);
                            Check(tiles.Length==(p==3?10:30),"Pagination 30/30/30/10");
                            Check(tiles[0].number.text==(p*30+1).ToString(),"First number");
                            Check(levels.previous.interactable==(p>0)&&levels.next.interactable==(p<3),"Arrow boundaries");
                        }
                        Check(levels.completed.text=="0 / 100 COMPLETED","Actual progress count");
                    }
                    levels.home.onClick.Invoke();Check(!levels.gameObject.activeSelf,"HOME closes levels");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Setup,0);Check(ui.EditorScreens.Single(x=>x.name=="GameSetup").activeSelf,"Custom preserved");
                    ui.OpenLevelSelection();levels.Select(0,0);levels.SetPage(0);
                    var fps=(TMP_Text)typeof(FloodFillMobileUI3D).GetField("fpsLabel",Flags).GetValue(ui);if(fps!=null)fps.transform.parent.gameObject.SetActive(false);
                    Capture(876,1796,"LevelSelection-876x1796.png");Capture(750,1334,"LevelSelection-750x1334.png");Capture(1170,2532,"LevelSelection-1170x2532.png");
                    foreach(var star in levels.GetComponentsInChildren<FloodLevelStarGraphic3D>())
                    {
                        var renderer=star.GetComponent<CanvasRenderer>();Check(renderer!=null,"Difficulty star has renderer");
                        var mesh=renderer.GetMesh();Check(mesh!=null&&mesh.vertexCount==11,"Difficulty star rendered");
                    }
                    if(layoutOnly){File.WriteAllText("/tmp/FloodLevelLayoutVerification.txt",checks+" layout checks passed. Three portrait renders; difficulty star meshes present.\n");EditorApplication.Exit(0);return;}
                    Check(!manager.StartNumberedLevel(0,0,2),"Locked level rejected by manager");
                    Check(!manager.StartNumberedLevel(3,0,1),"Invalid mode rejected");
                    stage=1;return;
                }
                if(stage==1)
                {
                    int mode=combination/4,difficulty=combination%4;
                    ui.StartSelectedLevel(mode,difficulty,1);stage=2;return;
                }
                if(stage==2)
                {
                    if(manager.IsLoading||!manager.CanInteract)return;
                    Check(string.IsNullOrEmpty(manager.LoadingError),"Board loads");
                    Check(manager.NumberedLevel==1&&(int)manager.SelectedDifficulty==combination%4,"Gameplay identity and difficulty");
                    Check(manager.BoardManager.TryCapturePersistentState(out var board),"Capture board");
                    string json=JsonUtility.ToJson(board);
                    if(!replay)
                    {
                        boardJson=json;replay=true;
                        Check(FloodFillSessionPersistence3D.TryLoad(out var session)&&session.numberedLevel==1&&session.numberedMode==combination/4,"Saved numbered identity");
                        ui.ExecuteAuthoredAction(FloodFillUIAction.Restart,0);return;
                    }
                    Check(json==boardJson,"Replay board colors and shape identical");
                    manager.BoardManager.TryCreateSolverSnapshot(out var snapshot);Check(FloodFillRegionGraph3D.TryBuild(snapshot,out graph),"Graph for gameplay solution");
                    solution=manager.LastSolverResult;Check(solution!=null&&solution.BestRegionSequence.Length>0,"Solver solution available");move=0;stage=3;return;
                }
                if(stage==3)
                {
                    if(!manager.CanInteract)return;
                    int logical=graph.RepresentativeLogicalIndices[solution.BestRegionSequence[move]], size=manager.SelectedBoardSize;
                    manager.SelectColor(solution.BestColorSequence[move]);
                    typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Flags).Invoke(manager,new object[]{manager.BoardManager.GetCell(logical%size,logical/size%size,logical/(size*size))});
                    move++;
                    if(move<solution.BestRegionSequence.Length)return;
                    Check(manager.State==FloodFillGameManager3D.GameState.Won,"Real gameplay solution wins");
                    Check(FloodLevelProgression3D.Completed(combination/4,combination%4)==1,"Win records category");
                    Check(FloodLevelProgression3D.Unlocked(combination/4,combination%4,2),"Win unlocks next");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Result,0);Check(manager.NumberedLevel==2,"NEXT loads level 2");
                    manager.ReturnToMenu();replay=false;combination++;
                    if(combination<12){stage=1;return;}
                    // Last-level action must return to selection, never request 101.
                    for(int l=2;l<=100;l++)FloodLevelProgression3D.Record(0,0,l,10,2);
                    manager.StartNumberedLevel(0,0,100);
                    typeof(FloodFillGameManager3D).GetField("<State>k__BackingField",Flags).SetValue(manager,FloodFillGameManager3D.GameState.Won);
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Result,0);Check(levels.gameObject.activeSelf&&manager.NumberedLevel==100,"Level 100 returns to catalog");
                    File.WriteAllText("/tmp/FloodLevelVerification.txt",checks+" checks passed. All 12 catalogs and 12 playable mode/difficulty workflows; deterministic replay; real solver-driven wins; saves; pagination; three portrait renders. Items use deterministic fruit masks.\n");
                    Debug.Log("LEVEL VERIFICATION PASSED: "+checks);EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodLevelVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void Capture(int width,int height,string name)
        {
            var canvas=ui.GetComponent<Canvas>();var originalMode=canvas.renderMode;var originalCamera=canvas.worldCamera;
            var go=new GameObject("Verification camera",typeof(Camera));var camera=go.GetComponent<Camera>();camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.005f,.018f,.045f);
            var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();
            var fits=ui.EditorScreens.First(screen=>screen.activeSelf).GetComponentsInChildren<FloodFillUIFit3D>(true);foreach(var item in fits)item.SendMessage("LateUpdate");var fit=fits[0];Canvas.ForceUpdateCanvases();camera.Render();
            RenderTexture.active=rt;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes("/tmp/"+name,texture.EncodeToPNG());
            var rect=fit.GetComponent<RectTransform>();var corners=new Vector3[4];rect.GetWorldCorners(corners);
            foreach(var corner in corners){var p=camera.WorldToViewportPoint(corner);Check(p.x>=-.01f&&p.x<=1.01f&&p.y>=-.01f&&p.y<=1.01f,"Portrait content fits");}
            RenderTexture.active=null;canvas.renderMode=originalMode;canvas.worldCamera=originalCamera;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
