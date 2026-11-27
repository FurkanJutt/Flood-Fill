using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Achievements;
using FloodFill.ThreeD.PowerUps;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace FloodFill.EditorTools
{
    public static class FloodAchievementVerification
    {
        private static int checks,stage;
        private static double deadline,waitUntil;
        private static FloodFillMobileUI3D ui;
        private static FloodFillGameManager3D game;
        private static FloodAchievementService service;
        private static string attemptId;
        private static FloodFillSessionSaveData3D checkpoint;
        private static double uses,hints,followed,savedMoves;
        private static VoxelCell3D target;
        private static double pausedSeconds;
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool valid,string message){checks++;if(!valid)throw new Exception(message);}
        private static AchievementDefinition D(int n)=>service.Engine.Catalog.achievements.Single(a=>a.id=="ff3d."+n.ToString("000"));
        private static double Value(int n)=>service.Engine.Progress(D(n),D(n).tiers[0]).value;
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Verification requires an isolated /tmp project.");
            try
            {
                FloodAchievementSetup.InstallBatch();VerifyAssets();VerifyImport();
                string profile="FloodAchievementVerification_"+Guid.NewGuid().ToString("N");PlayerSettings.companyName=profile;PlayerSettings.productName=profile;
                EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
                EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");
                deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
            }
            catch(Exception e){Fail(e);}
        }
        private static void VerifyAssets()
        {
            var db=AssetDatabase.LoadAssetAtPath<FloodAchievementDatabase>(FloodAchievementSetup.DatabasePath);Check(db!=null,"Database asset");var cat=db.Load();
            Check(cat.achievements.Count==112,"112 definitions");Check(cat.achievements.Sum(d=>d.tiers.Count)==242,"242 milestones");Check(cat.achievements.Count(d=>d.enabled)==103,"103 enabled achievements");Check(cat.achievements.Select(d=>d.category).Distinct().Count()==17,"17 categories");Check(db.definitions.All(a=>a.icon!=null),"All 112 icon sprites assigned");
            Check(!AchievementValidation.Validate(cat).Any(s=>s.StartsWith("ERROR:",StringComparison.Ordinal)),"No catalog errors");Check(cat.achievements.SelectMany(a=>a.tiers).All(t=>t.rewards.Count==0),"No invented rewards");
            var pdf=JsonUtility.FromJson<AchievementCatalogData>(File.ReadAllText("Documentation/Achievements/Catalog.v1.json"));foreach(var d in pdf.achievements){var actual=cat.achievements.Single(a=>a.id==d.id);Check(JsonUtility.ToJson(actual)==JsonUtility.ToJson(d),"Exact PDF definition "+d.id);}
            var config=AssetDatabase.LoadAssetAtPath<FloodAchievementSettings>(FloodAchievementSetup.SettingsPath);Check(config!=null&&config.database==db,"Settings configured");Check(config.filledStar!=null&&config.emptyStar!=null&&config.hiddenIcon!=null,"Existing star and lock sprites assigned");
            Check(config.screenPrefab!=null&&config.cardPrefab!=null&&config.detailPrefab!=null&&config.notificationPrefab!=null,"Four UI prefabs assigned");
            var canvas=AssetDatabase.LoadAssetAtPath<GameObject>(FloodAchievementSetup.CanvasPath);Check(new SerializedObject(canvas.GetComponent<FloodFillMobileUI3D>()).FindProperty("achievementScreenPrefab").objectReferenceValue==config.screenPrefab,"Screen prefab configured for lazy loading");
            Check(canvas.GetComponentInChildren<FloodAchievementScreen>(true)==null,"Screen not eagerly instantiated");
            Check(canvas.GetComponentsInChildren<FloodFillUIAction3D>(true).Count(b=>b.action==FloodFillUIAction.Achievements)==1,"One Main Menu achievement button");
            Check(canvas.GetComponentInChildren<FloodAchievementNotifications>(true).menuIndicator!=null,"Main Menu indicator assigned");
            Check(config.cardPrefab.GetComponent<FloodAchievementCard>().stars.Length==3,"Exactly three card star positions");
        }
        private static void VerifyImport()
        {
            var source=AssetDatabase.LoadAssetAtPath<FloodAchievementDatabase>(FloodAchievementSetup.DatabasePath);var db=ScriptableObject.CreateInstance<FloodAchievementDatabase>();
            var clone=ScriptableObject.CreateInstance<FloodAchievementDefinition>();clone.definition=JsonUtility.FromJson<AchievementDefinition>(JsonUtility.ToJson(source.definitions[0].definition));
            db.definitions.Add(clone);db.definitions.Add(source.definitions[1]);
            var imported=JsonUtility.FromJson<AchievementDefinition>(JsonUtility.ToJson(clone.definition));imported.name="Edited Presentation Only";
            string id=imported.id,tier=imported.tiers[0].id;
            FloodAchievementMenu.ApplyImport(db,new AchievementCatalogData{achievements=new System.Collections.Generic.List<AchievementDefinition>{imported}});
            Check(db.definitions.Count==2,"Partial JSON import preserves unrelated definitions");Check(db.definitions[0].definition.id==id&&db.definitions[0].definition.tiers[0].id==tier,"Import preserves stable IDs");Check(db.definitions[0].definition.name=="Edited Presentation Only","Import updates matching definition");
            var roundtrip=JsonUtility.FromJson<AchievementCatalogData>(JsonUtility.ToJson(source.Load(),true));Check(roundtrip.achievements.Count==112,"Export/import JSON roundtrip");UnityEngine.Object.DestroyImmediate(clone);UnityEngine.Object.DestroyImmediate(db);
        }
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Achievement verification timeout at stage "+stage);
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup<waitUntil)return;
                switch(stage)
                {
                    case 0:
                        ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();if(ui==null||!ui.IsBuilt||game==null)return;
                        service=FloodAchievementService.Instance;Check(service.Ready,"Runtime achievement service ready");
                        Check(UnityEngine.Object.FindObjectsByType<FloodAchievementService>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Single persistent service");
                        ui.OpenAchievements();Canvas.ForceUpdateCanvases();waitUntil=EditorApplication.timeSinceStartup+.3;stage++;break;
                    case 1:
                        var screen=UnityEngine.Object.FindAnyObjectByType<FloodAchievementScreen>();Check(screen!=null&&screen.gameObject.activeInHierarchy,"Main Menu opens achievement screen");
                        Check(screen.category.options.Count==18,"All 17 category options plus All");Check(screen.filter.options.Count==3,"No reward filters without rewards");
                        Check(screen.GetComponentsInChildren<FloodAchievementCard>(true).Length<=18,"Virtualized card pool");
                        var hidden=D(106);screen.OpenDetail(hidden);Check(screen.detailTitle.text=="HIDDEN ACHIEVEMENT"&&!screen.detailBody.text.Contains("Expert"),"Hidden name/requirements concealed");Check(screen.claimButtons.All(b=>!b.gameObject.activeSelf),"No fake claim buttons");
                        screen.closeDetail.onClick.Invoke();
                        if(Environment.GetCommandLineArgs().Contains("-floodScreenshots"))Capture("/tmp/FloodAchievements-Screen.png",430,932);
                        screen.back.onClick.Invoke();
                        if(Environment.GetCommandLineArgs().Contains("-floodScreenshots"))Capture("/tmp/FloodAchievements-Menu.png",430,932);Check(!screen.gameObject.activeSelf,"Back returns to Main Menu");
                        ui.StartSelectedLevel(0,0,1);stage++;break;
                    case 2:
                        if(game.IsLoading||!game.CanInteract||Time.time<game.BoardManager.VisualsSettledAt)return;
                        Check(game.CurrentAchievementAttempt!=null&&!game.CurrentAchievementAttempt.eligible,"Automated development attempts excluded by default");
                        // Explicit opt-in only inside this isolated verification company/profile.
                        game.CurrentAchievementAttempt.eligible=true;attemptId=game.CurrentAchievementAttempt.id;
                        target=game.BoardManager.StartingVoxel;uses=service.Engine.Statistics().powerUses[0];
                        Check(!game.CommitPowerUpRecolor(FloodPowerUpKind.ColorSwap,target,target.ColorIndex,game.PowerUps.settings),"Invalid same-color Swap rejected");Check(service.Engine.Statistics().powerUses[0]==uses,"Invalid action not counted");
                        Check(game.CommitPowerUpRecolor(FloodPowerUpKind.ColorSwap,target,(target.ColorIndex+1)%6,game.PowerUps.settings),"Actual Swap committed");Check(service.Engine.Statistics().powerUses[0]==uses+1,"Successful Swap counted");
                        checkpoint=game.CapturePowerUpCheckpoint();FloodFillSessionPersistence3D.Save(checkpoint);Check(service.Engine.Statistics().powerUses[0]==uses+1,"Duplicate checkpoint action ignored");
                        game.PauseGame();pausedSeconds=game.ElapsedSeconds;waitUntil=EditorApplication.timeSinceStartup+.3;stage++;break;
                    case 3:
                        Check(Math.Abs(game.ElapsedSeconds-pausedSeconds)<.03,"Pause excludes elapsed time");game.ResumeGame();game.ReturnToMenu();Check(game.ContinueSavedGame(),"Continue resumes unfinished attempt");waitUntil=EditorApplication.timeSinceStartup+1;stage++;break;
                    case 4:
                        if(game.IsLoading||!game.CanInteract||Time.time<game.BoardManager.VisualsSettledAt)return;
                        Check(game.CurrentAchievementAttempt.id==attemptId,"Continue preserves attempt ID");Check(service.Engine.Statistics().powerUses[0]==uses+1,"Continue does not duplicate usage");
                        game.CurrentAchievementAttempt.eligible=true;
                        target=game.BoardManager.StartingVoxel;var cells=new System.Collections.Generic.List<VoxelCell3D>();game.BoardManager.CollectRegion(target,cells);
                        var recommendation=new FloodHintRecommendation3D{color=(target.ColorIndex+1)%6,primary=cells.Select(c=>game.BoardManager.LogicalIndex(c)).ToArray()};
                        typeof(FloodPowerUps3D).GetProperty("CurrentHint").SetValue(game.PowerUps,recommendation);hints=service.Engine.Statistics().powerUses[1];followed=service.Engine.Statistics().hintsFollowed;
                        Check(game.CommitHintUse(game.PowerUps.settings),"Usable hint reveal transaction");Check(service.Engine.Statistics().powerUses[1]==hints+1&&service.Engine.Statistics().hintsFollowed==followed,"Reveal is separate from following");
                        game.SelectColor(recommendation.color);typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Private).Invoke(game,new object[]{target});
                        Check(service.Engine.Statistics().hintsFollowed==followed+1,"Correct recommendation associated with accepted normal move");
                        waitUntil=EditorApplication.timeSinceStartup+1.5;stage++;break;
                    case 5:
                        if(Time.time<game.BoardManager.VisualsSettledAt)return;
                        double blast=service.Engine.Statistics().powerUses[2];target=game.BoardManager.StartingVoxel;
                        Check(game.CommitPowerUpRecolor(FloodPowerUpKind.ColorBlast,target,(target.ColorIndex+1)%6,game.PowerUps.settings),"Actual Blast committed");Check(service.Engine.Statistics().powerUses[2]==blast+1,"Blast counted once");Check(game.CurrentAchievementAttempt.lastAction.direct<=5,"Full Grid Blast cannot directly edit seven cells");
                        waitUntil=EditorApplication.timeSinceStartup+.5;stage++;break;
                    case 6:
                        // A legal bridge state: a large green shell region plus an isolated green cell,
                        // separated by one purple corner. All changes are in the isolated test profile.
                        var saved=game.CapturePowerUpCheckpoint();
                        foreach(var v in saved.board.voxels)
                        {
                            bool corner=v.x==0&&v.y==0&&v.z==0;
                            v.colorIndex=v.x>=1&&!(v.x==1&&v.y==1&&v.z==0)?0:2;
                            if(v.x==0&&v.y==1&&v.z==0)v.colorIndex=0;if(corner)v.colorIndex=1;
                            v.captured=v.covered=corner;
                        }
                        saved.board.currentPlayerColor=1;
                        Check(game.BoardManager.RestorePersistentState(saved.board,Enumerable.Range(0,6).Select(i=>game.BoardManager.PaletteColor(i)).ToArray()),"Controlled bridge fixture restored");
                        typeof(FloodFillGameManager3D).GetMethod("InitializeAchievementConnections",Private).Invoke(game,null);
                        var bits=new byte[(game.BoardManager.Width*game.BoardManager.Height*game.BoardManager.Depth+7)/8];bits[0]=1;game.CurrentAchievementAttempt.capturedBits=Convert.ToBase64String(bits);
                        target=game.BoardManager.StartingVoxel;
                        Check(game.CommitPowerUpRecolor(FloodPowerUpKind.ColorSwap,target,0,game.PowerUps.settings),"Bridge Swap committed");
                        Check(game.CurrentAchievementAttempt.lastAction.connected>=50&&game.CurrentAchievementAttempt.lastAction.merged==2,"Swap distinguishes direct edit from newly joined components");
                        Check(service.Engine.Progress(D(107),D(107).tiers[0]).completed,"Small Move, Big Impact uses real connected cubes");
                        waitUntil=EditorApplication.timeSinceStartup+.5;stage++;break;
                    case 7:
                        if(Time.time<game.BoardManager.VisualsSettledAt)return;
                        Check(game.CommitPowerUpRecolor(FloodPowerUpKind.ColorSwap,game.BoardManager.StartingVoxel,1,game.PowerUps.settings),"Bridge split committed");
                        waitUntil=EditorApplication.timeSinceStartup+.5;stage++;break;
                    case 8:
                        if(Time.time<game.BoardManager.VisualsSettledAt)return;
                        double cubeCredit=service.Engine.Statistics().capturedCubes;
                        game.SelectColor(0);typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Private).Invoke(game,new object[]{game.BoardManager.StartingVoxel});
                        Check(game.CurrentAchievementAttempt.lastAction.rawConnected>=50&&game.CurrentAchievementAttempt.lastAction.connected==0,"Split/reconnect is measured physically but never credited again");
                        Check(service.Engine.Statistics().capturedCubes==cubeCredit,"Cumulative cube capture cannot be farmed by reconnecting");
                        // The deliberately isolated yellow cell must be cleared before the final-move fixture.
                        var isolated=game.BoardManager.CellAtLogical(1+game.BoardManager.Width);typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Private).Invoke(game,new object[]{isolated});
                        waitUntil=EditorApplication.timeSinceStartup+.5;stage++;break;
                    case 9:
                        typeof(FloodFillGameManager3D).GetProperty("MoveCount").SetValue(game,game.CurrentMoveBudget-1);game.SelectColor(0);target=game.BoardManager.ActiveCells.First(c=>c.ColorIndex==2);savedMoves=Value(65);
                        typeof(FloodFillGameManager3D).GetMethod("HandleVoxelClicked",Private).Invoke(game,new object[]{target});Check(game.State==FloodFillGameManager3D.GameState.Won,"Win on final allowed move preserved");
                        Check(service.Engine.Data.receipts.Single(r=>r.attemptId==attemptId).terminal,"Terminal attempt receipt committed");
                        FloodFillSessionPersistence3D.Save(game.CapturePowerUpCheckpoint());Check(Value(65)==savedMoves,"Duplicate terminal save ignored");
                        ui.OpenAchievements();waitUntil=EditorApplication.timeSinceStartup+.3;stage++;break;
                    case 10:
                        var opened=UnityEngine.Object.FindAnyObjectByType<FloodAchievementScreen>();Canvas.ForceUpdateCanvases();Check(opened.summary.text.Contains("103")&&opened.summary.text.Contains("215"),"Enabled totals exclude disabled tiers dynamically");
                        var first=opened.GetComponentsInChildren<FloodAchievementCard>(true).First(c=>c.Definition!=null&&c.Definition.id=="ff3d.001");Check(first.stars.All(s=>s.sprite==service.settings.filledStar),"Single-tier achievement fills all three sprite positions");
                        service.Flush();var disk=new AchievementPersistence(service.Persistence.Path);var loaded=disk.Load();Check(loaded.tiers.Any(p=>p.tierId=="ff3d.001.tier1"&&p.completed),"Runtime completion persisted to isolated save");
                        Check(loaded.tiers.All(p=>p.claimedRewardIds.Count==0),"Unconfigured rewards stay unclaimed");
                        Finish();break;
                }
            }
            catch(Exception e){Fail(e);}
        }
        public static void CompileMobileBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            var log=new System.Text.StringBuilder();
            foreach(var target in new[]{BuildTarget.Android,BuildTarget.iOS})
            {
                string output="/tmp/FloodAchievementPlayerScripts-"+target;Directory.CreateDirectory(output);
                var options=new UnityEditor.Build.Player.ScriptCompilationSettings{target=target,group=BuildPipeline.GetBuildTargetGroup(target)};
                var compiled=UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(options,output);
                if(compiled.assemblies==null||compiled.assemblies.Count==0)throw new Exception(target+" player script compilation failed.");
                log.AppendLine("PASS: "+target+" production player scripts compiled, "+compiled.assemblies.Count+" assemblies. No device build/run claimed.");
            }
            File.WriteAllText("/tmp/FloodAchievementPlayerCompilation.txt",log.ToString());
        }
        private static void Capture(string path,int width,int height)
        {
            var canvas=ui.GetComponent<Canvas>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;
            var go=new GameObject("Achievement UI verification camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=10;camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.01f,.02f,.04f);
            var target=new RenderTexture(width,height,24);camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();foreach(var fit in ui.GetComponentsInChildren<FloodFillUIFit3D>())fit.SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();camera.Render();
            var active=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active=active;canvas.renderMode=mode;canvas.worldCamera=oldCamera;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);
        }
        private static void Finish()
        {
            File.WriteAllText("/tmp/FloodAchievementVerification.txt","PASS: "+checks+" Unity asset, Editor import, UI, checkpoint, actual power-up, hint attribution, Continue, final-move win and persistence assertions. Isolated profile: "+Application.persistentDataPath+"\nNo device testing performed.");EditorApplication.update-=Tick;EditorApplication.Exit(0);
        }
        private static void Fail(Exception e){File.WriteAllText("/tmp/FloodAchievementVerification.txt","FAILED stage "+stage+" after "+checks+" checks\n"+e);Debug.LogException(e);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
    }
}
