using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FloodFill.ThreeD.Achievements;
using UnityEditor;
using UnityEngine;
namespace FloodFill.EditorTools
{
    public sealed class FloodAchievementMenu:EditorWindow
    {
        private FloodAchievementDatabase database;
        private FloodAchievementDefinition selected;
        private SerializedObject serialized;
        private Vector2 listScroll,editScroll;
        private string search="";
        private int category,enabledFilter,hiddenFilter,testTier;
        private List<string> validation=new List<string>();
        private AchievementEngine testEngine,readOnlyEngine;
        private bool showTesting;
        private double simulatedProgress;
        private const string TestPath="UserSettings/FloodAchievementTestProgress.json";
        [MenuItem("Tools/FloodFill/Achievement Menu")]
        public static void Open(){var window=GetWindow<FloodAchievementMenu>("Achievement Menu");window.minSize=new Vector2(1040,640);window.Show();}
        private void OnEnable(){Reload();Undo.undoRedoPerformed+=OnUndo;}
        private void OnDisable(){Undo.undoRedoPerformed-=OnUndo;}
        private void OnUndo(){serialized=null;Repaint();}
        private void Reload()
        {
            database=AssetDatabase.LoadAssetAtPath<FloodAchievementDatabase>(FloodAchievementSetup.DatabasePath);
            if(database!=null)validation=AchievementValidation.Validate(database.Load());serialized=null;
        }
        private void OnGUI()
        {
            if(database==null){EditorGUILayout.HelpBox("Install the predefined catalog and authored UI first.",MessageType.Info);if(GUILayout.Button("Install Complete Achievement System")){FloodAchievementSetup.Install();Reload();}return;}
            Toolbar();EditorGUILayout.BeginHorizontal();DrawList();DrawSelected();EditorGUILayout.EndHorizontal();
        }
        private void Toolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if(GUILayout.Button("Add",EditorStyles.toolbarButton))Add();
            using(new EditorGUI.DisabledScope(selected==null))
            {
                if(GUILayout.Button("Duplicate",EditorStyles.toolbarButton))Duplicate();
                if(GUILayout.Button("Delete / Disable",EditorStyles.toolbarButton))Delete();
            }
            if(GUILayout.Button("Save Changes",EditorStyles.toolbarButton)){serialized?.ApplyModifiedProperties();AssetDatabase.SaveAssets();ValidateAll();}
            if(GUILayout.Button("Validate All",EditorStyles.toolbarButton))ValidateAll();
            if(GUILayout.Button("Refresh",EditorStyles.toolbarButton))Reload();
            if(GUILayout.Button("Export JSON",EditorStyles.toolbarButton))Export();
            if(GUILayout.Button("Import JSON",EditorStyles.toolbarButton))Import();
            GUILayout.FlexibleSpace();showTesting=GUILayout.Toggle(showTesting,"Isolated Testing",EditorStyles.toolbarButton);EditorGUILayout.EndHorizontal();
        }
        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));search=EditorGUILayout.TextField("Search",search);
            var categories=new List<string>{"All categories"};foreach(var a in database.definitions)if(a!=null&&!categories.Contains(a.definition.category))categories.Add(a.definition.category);
            category=EditorGUILayout.Popup("Category",Math.Min(category,categories.Count-1),categories.ToArray());
            enabledFilter=EditorGUILayout.Popup("Availability",enabledFilter,new[]{"All","Enabled","Disabled"});hiddenFilter=EditorGUILayout.Popup("Visibility",hiddenFilter,new[]{"All","Visible","Hidden"});
            listScroll=EditorGUILayout.BeginScrollView(listScroll);
            foreach(var a in database.definitions.OrderBy(a=>a!=null?a.definition.order:int.MaxValue))
            {
                if(a==null)continue;var d=a.definition;
                if(category>0&&d.category!=categories[category]||enabledFilter==1&&!d.enabled||enabledFilter==2&&d.enabled||hiddenFilter==1&&d.hidden||hiddenFilter==2&&!d.hidden)continue;
                if(!string.IsNullOrEmpty(search)&&!(d.name+" "+d.id+" "+d.description).ToLowerInvariant().Contains(search.ToLowerInvariant()))continue;
                EditorGUILayout.BeginHorizontal(a==selected?"SelectionRect":GUIStyle.none);
                if(a.icon!=null)GUILayout.Label(AssetPreview.GetAssetPreview(a.icon)??AssetPreview.GetMiniThumbnail(a.icon),GUILayout.Width(38),GUILayout.Height(38));
                if(GUILayout.Button(d.name+"\n"+d.tiers.Count+" tier(s)"+(d.enabled?"":"  · DISABLED"),EditorStyles.label,GUILayout.Height(43))){selected=a;serialized=null;testTier=0;}
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();EditorGUILayout.LabelField(database.definitions.Count+" definitions",EditorStyles.miniLabel);EditorGUILayout.EndVertical();
        }
        private void DrawSelected()
        {
            EditorGUILayout.BeginVertical();editScroll=EditorGUILayout.BeginScrollView(editScroll);
            if(selected==null){EditorGUILayout.HelpBox("Select an achievement to edit its stable definition, tiers and future rewards.",MessageType.Info);}
            else
            {
                if(serialized==null||serialized.targetObject!=selected)serialized=new SerializedObject(selected);serialized.Update();var data=serialized.FindProperty("definition");
                Header("General Information");EditorGUILayout.LabelField("Stable ID",selected.definition.id);
                Field(data,"name");Field(data,"description");Field(data,"category");EditorGUILayout.PropertyField(serialized.FindProperty("icon"));Field(data,"iconKey");Field(data,"version");Field(data,"enabled");Field(data,"hidden");Field(data,"disabledReason");Field(data,"order");
                Header("Completion Conditions");int previousMetric=data.FindPropertyRelative("metric").enumValueIndex;Field(data,"metric");
                if(previousMetric!=data.FindPropertyRelative("metric").enumValueIndex){ResetFilters(data.FindPropertyRelative("filters"));var oldTiers=data.FindPropertyRelative("tiers");for(int i=0;i<oldTiers.arraySize;i++)oldTiers.GetArrayElementAtIndex(i).FindPropertyRelative("overrideFilters").boolValue=false;}
                var metric=(AchievementMetric)data.FindPropertyRelative("metric").enumValueIndex;
                if(metric==AchievementMetric.WinCount||metric==AchievementMetric.ColorMinimum||metric==AchievementMetric.ColorMaximum)
                {var aggregation=data.FindPropertyRelative("aggregation");aggregation.enumValueIndex=EditorGUILayout.IntPopup("Count policy",Math.Min(1,aggregation.enumValueIndex),new[]{"All qualifying wins","Unique level identities"},new[]{0,1});}
                else EditorGUILayout.LabelField("Aggregation",metric==AchievementMetric.FastestSeconds?"Best (minimum) successful time":"Defined by the selected condition");
                if(metric==AchievementMetric.CatalogLevels||metric==AchievementMetric.FullCategories||metric==AchievementMetric.TouchedCategories)Field(data,"calculation");else data.FindPropertyRelative("calculation").enumValueIndex=0;
                EditorGUILayout.HelpBox("Condition/threshold edits preserve completed tier IDs and claim receipts. Increase the definition version for a released rules change. Presentation changes never reset progress.",MessageType.Info);
                Header("Gameplay Filters");DrawFilters(data.FindPropertyRelative("filters"),selected.definition.metric);
                Header("Tiers & Rewards");DrawTiers(data.FindPropertyRelative("tiers"));
                if(serialized.ApplyModifiedProperties()){EditorUtility.SetDirty(selected);validation=AchievementValidation.Validate(database.Load());}
                if(GUILayout.Button("Validate Selected"))validation=AchievementValidation.Validate(new AchievementCatalogData{achievements=new List<AchievementDefinition>{selected.definition}});
                if(GUILayout.Button("Ping Definition Asset"))EditorGUIUtility.PingObject(selected);
            }
            Header("Validation");
            int shown=0;foreach(string message in validation)
            {
                if(selected!=null&&!message.Contains(selected.definition.id)&&!message.Contains("Missing or unsupported catalog"))continue;
                EditorGUILayout.HelpBox(message,message.StartsWith("ERROR:",StringComparison.Ordinal)?MessageType.Error:MessageType.Warning);if(++shown>30)break;
            }
            if(shown==0)EditorGUILayout.HelpBox("No configuration errors. Practical gameplay feasibility is not established by static validation.",MessageType.Info);
            if(showTesting)DrawTesting();EditorGUILayout.EndScrollView();EditorGUILayout.EndVertical();
        }
        private static void Header(string title){EditorGUILayout.Space(10);EditorGUILayout.LabelField(title,EditorStyles.boldLabel);}
        private static void Field(SerializedProperty p,string name)
        {
            var f=p.FindPropertyRelative(name);if(f==null)return;
            if(name=="description"){EditorGUILayout.LabelField("Description");f.stringValue=EditorGUILayout.TextArea(f.stringValue,GUILayout.MinHeight(54));return;}
            if(name=="mode"){f.intValue=EditorGUILayout.IntPopup("Game mode",f.intValue,new[]{"Any","Full Grid","Random Shape","Items"},new[]{-1,0,1,2});return;}
            if(name=="difficulty"){f.intValue=EditorGUILayout.IntPopup("Difficulty",f.intValue,new[]{"Any","Easy","Medium / Normal","Hard","Expert / Perfect"},new[]{-1,0,1,2,3});return;}
            if(name=="custom"){f.intValue=EditorGUILayout.IntPopup("Game kind",f.intValue,new[]{"Any","Numbered / Legacy","Custom"},new[]{-1,0,1});return;}
            if(name=="size"){f.intValue=EditorGUILayout.IntPopup("Board size",f.intValue,new[]{"Any","6","8","10","12","15","18","20"},new[]{0,6,8,10,12,15,18,20});return;}
            EditorGUILayout.PropertyField(f,true);
        }
        private static void ResetFilters(SerializedProperty p)
        {
            var defaults=new AchievementFilters();
            foreach(var field in typeof(AchievementFilters).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public))
            {
                var property=p.FindPropertyRelative(field.Name);if(property==null)continue;var value=field.GetValue(defaults);
                if(value is bool b)property.boolValue=b;else if(value is int i)property.intValue=i;else if(value is double d)property.doubleValue=d;
            }
        }
        private static void DrawFilters(SerializedProperty f,AchievementMetric metric)
        {
            if(metric==AchievementMetric.ActiveDays||metric==AchievementMetric.DailyStreak||AchievementEngine.IsMeta(metric)){EditorGUILayout.LabelField("This condition uses global calendar/catalog state.");return;}
            bool catalog=metric<=AchievementMetric.FullGoalCategory||metric==AchievementMetric.SizeCollection;
            if(metric!=AchievementMetric.ModeMinimum)Field(f,"mode");Field(f,"difficulty");
            if(!catalog||metric==AchievementMetric.SizeCollection)Field(f,"custom");
            if(metric!=AchievementMetric.FirstStarSequence&&metric!=AchievementMetric.FullGoalCategory)Field(f,"size");
            if(metric!=AchievementMetric.ActiveHours&&metric!=AchievementMetric.FirstStarSequence&&metric!=AchievementMetric.FullGoalCategory)Field(f,"minimumOccupied");
            if(!catalog&&metric!=AchievementMetric.ActiveHours)foreach(string field in new[]{"allowReplays","allowPowerUps"})Field(f,field);
            EditorGUILayout.LabelField("-1 = any mode/difficulty/game kind. Custom: 0 numbered/legacy, 1 custom.",EditorStyles.wordWrappedMiniLabel);
            if(metric==AchievementMetric.StarLevels)Field(f,"stars");
            bool action=metric==AchievementMetric.ActionCount||metric>=AchievementMetric.CapturedCubes&&metric<=AchievementMetric.CaptureChain||metric==AchievementMetric.MultiplierCount||metric==AchievementMetric.MultiplierChain;
            bool win=metric==AchievementMetric.WinCount||metric==AchievementMetric.WinStreak||metric==AchievementMetric.FastestSeconds||metric>=AchievementMetric.ColorCollection&&metric<=AchievementMetric.ColorRotation||metric==AchievementMetric.ReturnAfterDays;
            if(action)
            {
                foreach(string field in new[]{"powerKind","minimumDirect","minimumMerged","minimumConnected","minimumCapturePercent","hintFollowed","requireWin"})Field(f,field);
                EditorGUILayout.LabelField("Power: -2 any successful power, -1 unrestricted, 0 Swap, 1 Hint, 2 Blast.",EditorStyles.wordWrappedMiniLabel);
            }
            if(win||action&&f.FindPropertyRelative("requireWin").boolValue)
                foreach(string field in new[]{"stars","goals","allGoals","finalColor","maxSeconds","maxMoveRatio","maxTargetColors","minimumFailures","finalMove","noZeroMoves","normalFinish","powerKinds","improvedMoves","improvedTime","improvedScore","improvedGoals","oneToThreeStars","minPowerUses"})Field(f,field);
            if(metric==AchievementMetric.WinCount)foreach(string field in new[]{"powerKind","powerFinish","minimumDirect","minimumMerged","minimumConnected","minimumCapturePercent"})Field(f,field);
        }
        private void DrawTiers(SerializedProperty tiers)
        {
            int count=EditorGUILayout.IntSlider("Tier count",Mathf.Clamp(tiers.arraySize,1,3),1,3);
            while(tiers.arraySize<count)
            {
                tiers.InsertArrayElementAtIndex(tiers.arraySize);var t=tiers.GetArrayElementAtIndex(tiers.arraySize-1);
                t.FindPropertyRelative("id").stringValue="tier."+Guid.NewGuid().ToString("N");t.FindPropertyRelative("target").doubleValue=tiers.arraySize;t.FindPropertyRelative("description").stringValue="New milestone";t.FindPropertyRelative("rewards").arraySize=0;
            }
            while(tiers.arraySize>count)tiers.DeleteArrayElementAtIndex(tiers.arraySize-1);
            for(int i=0;i<tiers.arraySize;i++)
            {
                var t=tiers.GetArrayElementAtIndex(i);EditorGUILayout.BeginVertical("box");EditorGUILayout.LabelField("Tier "+(i+1),EditorStyles.boldLabel);EditorGUILayout.SelectableLabel(t.FindPropertyRelative("id").stringValue,EditorStyles.miniLabel,GUILayout.Height(18));
                Field(t,"target");Field(t,"description");Field(t,"overrideFilters");if(t.FindPropertyRelative("overrideFilters").boolValue)DrawFilters(t.FindPropertyRelative("filters"),selected.definition.metric);
                var rewards=t.FindPropertyRelative("rewards");if(rewards.arraySize==0)EditorGUILayout.LabelField("No Reward Configured",EditorStyles.miniLabel);
                for(int j=0;j<rewards.arraySize;j++)
                {
                    var r=rewards.GetArrayElementAtIndex(j);EditorGUILayout.BeginVertical("box");EditorGUILayout.LabelField("Reward entry: "+r.FindPropertyRelative("id").stringValue,EditorStyles.miniLabel);
                    foreach(string name in new[]{"enabled","type","identifier","quantity","displayName","iconKey"})Field(r,name);
                    string key=r.FindPropertyRelative("iconKey").stringValue;var current=database.Icon(key);var assigned=(Sprite)EditorGUILayout.ObjectField("Optional icon",current,typeof(Sprite),false);
                    if(assigned!=current){Undo.RecordObject(database,"Assign reward icon");if(string.IsNullOrEmpty(key))key="reward."+r.FindPropertyRelative("id").stringValue;r.FindPropertyRelative("iconKey").stringValue=key;database.SetIcon(key,assigned);EditorUtility.SetDirty(database);}
                    if(GUILayout.Button("Remove Reward Entry")){rewards.DeleteArrayElementAtIndex(j);EditorGUILayout.EndVertical();break;}EditorGUILayout.EndVertical();
                }
                if(GUILayout.Button("Add Future Reward Entry"))
                {
                    rewards.InsertArrayElementAtIndex(rewards.arraySize);var r=rewards.GetArrayElementAtIndex(rewards.arraySize-1);r.FindPropertyRelative("id").stringValue="reward."+Guid.NewGuid().ToString("N");r.FindPropertyRelative("enabled").boolValue=false;r.FindPropertyRelative("quantity").longValue=0;
                }
                EditorGUILayout.EndVertical();
            }
        }
        private void ValidateAll(){validation=AchievementValidation.Validate(database.Load());foreach(var asset in database.definitions)if(asset!=null&&asset.icon==null&&database.Icon(asset.definition.iconKey)==null)validation.Add("WARNING: "+asset.definition.id+": no local icon assigned; generic fallback is used.");Repaint();}
        private void Add()
        {
            var d=new AchievementDefinition{id="achievement."+Guid.NewGuid().ToString("N"),name="New Achievement",description="Describe the requirement.",category="General Progression",order=database.definitions.Count+1,metric=AchievementMetric.WinCount};
            d.tiers.Add(new AchievementTier{id="tier."+Guid.NewGuid().ToString("N"),description="Complete one qualifying level.",target=1});Create(d,null);
        }
        private void Create(AchievementDefinition d,Sprite icon)
        {
            if(string.IsNullOrWhiteSpace(d.iconKey))d.iconKey="achievement.icon."+d.id;
            var a=CreateInstance<FloodAchievementDefinition>();a.definition=d;a.icon=icon;Directory.CreateDirectory(FloodAchievementSetup.Definitions);
            AssetDatabase.CreateAsset(a,FloodAchievementSetup.Definitions+"/"+d.id+".asset");Undo.RegisterCreatedObjectUndo(a,"Create achievement");Undo.RecordObject(database,"Add achievement");database.definitions.Add(a);EditorUtility.SetDirty(database);selected=a;serialized=null;AssetDatabase.SaveAssets();ValidateAll();
        }
        private void Duplicate()
        {
            var d=JsonUtility.FromJson<AchievementDefinition>(JsonUtility.ToJson(selected.definition));d.id="achievement."+Guid.NewGuid().ToString("N");d.name+=" Copy";d.iconKey="achievement.icon."+d.id;
            foreach(var t in d.tiers){t.id="tier."+Guid.NewGuid().ToString("N");foreach(var r in t.rewards)r.id="reward."+Guid.NewGuid().ToString("N");}Create(d,selected.icon);
        }
        private void Delete()
        {
            int choice=EditorUtility.DisplayDialogComplex("Released Achievement","Player saves may reference this stable ID. Disabling preserves references and is recommended.","Disable","Cancel","Delete Permanently");
            if(choice==1)return;if(choice==0){Undo.RecordObject(selected,"Disable achievement");selected.definition.enabled=false;selected.definition.disabledReason="Disabled by catalog author.";EditorUtility.SetDirty(selected);}
            else{Undo.RecordObject(database,"Remove achievement");database.definitions.Remove(selected);AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(selected));selected=null;serialized=null;EditorUtility.SetDirty(database);}AssetDatabase.SaveAssets();ValidateAll();
        }
        private void Export()
        {
            string path=EditorUtility.SaveFilePanel("Export definitions only","","FloodFillAchievementCatalog.v1.json","json");if(string.IsNullOrEmpty(path))return;
            File.WriteAllText(path,JsonUtility.ToJson(database.Load(),true));
        }
        private void Import()
        {
            string path=EditorUtility.OpenFilePanel("Import achievement catalog","","json");if(string.IsNullOrEmpty(path))return;
            try
            {
                var catalog=JsonUtility.FromJson<AchievementCatalogData>(File.ReadAllText(path));var issues=AchievementValidation.Validate(catalog);issues.AddRange(AchievementValidation.Validate(Prospective(database,catalog)));var errors=issues.Where(i=>i.StartsWith("ERROR:",StringComparison.Ordinal)).ToArray();
                if(errors.Length>0){EditorUtility.DisplayDialog("Import Rejected",string.Join("\n",errors.Take(12)),"Back");return;}
                int added=0,updated=0,unchanged=0,disabled=0;foreach(var d in catalog.achievements)
                {var old=database.definitions.FirstOrDefault(a=>a!=null&&a.definition.id==d.id);if(old==null)added++;else if(JsonUtility.ToJson(old.definition)==JsonUtility.ToJson(d))unchanged++;else updated++;if(!d.enabled)disabled++;}
                if(!EditorUtility.DisplayDialog("Import Preview",$"New: {added}\nUpdated: {updated}\nUnchanged: {unchanged}\nDisabled: {disabled}\nInvalid: 0\nWarnings: {issues.Count}\n\nMerge by stable ID. Unrelated definitions and all player progress are preserved.","Apply Merge","Back"))return;
                ApplyImport(database,catalog);selected=null;serialized=null;ValidateAll();
            }
            catch(Exception e){EditorUtility.DisplayDialog("Import Failed",e.Message,"Back");}
        }
        private static AchievementCatalogData Prospective(FloodAchievementDatabase db,AchievementCatalogData incoming)
        {
            var result=db.Load();result.version=incoming.version;
            foreach(var d in incoming.achievements){int at=result.achievements.FindIndex(a=>a.id==d.id);if(at<0)result.achievements.Add(d);else result.achievements[at]=d;}return result;
        }
        public static void ApplyImport(FloodAchievementDatabase db,AchievementCatalogData catalog)
        {
            var incomingErrors=AchievementValidation.Validate(catalog).Where(s=>s.StartsWith("ERROR:",StringComparison.Ordinal)).ToArray();if(incomingErrors.Length>0)throw new ArgumentException(string.Join("\n",incomingErrors));
            var errors=AchievementValidation.Validate(Prospective(db,catalog)).Where(s=>s.StartsWith("ERROR:",StringComparison.Ordinal)).ToArray();if(errors.Length>0)throw new ArgumentException(string.Join("\n",errors));
            Undo.RecordObject(db,"Import achievement catalog");
            foreach(var d in catalog.achievements)
            {
                var asset=db.definitions.FirstOrDefault(a=>a!=null&&a.definition.id==d.id);
                if(asset==null){asset=CreateInstance<FloodAchievementDefinition>();AssetDatabase.CreateAsset(asset,FloodAchievementSetup.Definitions+"/"+d.id+".asset");db.definitions.Add(asset);}
                else Undo.RecordObject(asset,"Update achievement definition");asset.definition=d;EditorUtility.SetDirty(asset);
            }
            db.catalogVersion=catalog.version;EditorUtility.SetDirty(db);AssetDatabase.SaveAssets();
        }
        private void DrawTesting()
        {
            Header("ISOLATED DEVELOPMENT TESTING");EditorGUILayout.HelpBox("All simulations below use a separate in-memory engine and UserSettings/FloodAchievementTestProgress.json. They never write normal player achievements, level progress, or inventory.",MessageType.Info);
            if(testEngine==null)testEngine=new AchievementEngine(database.Load(),new AchievementSaveData());
            if(Application.isPlaying)
            {
                var real=FloodAchievementService.Instance;if(real!=null&&real.Ready){var stats=real.Engine.Statistics();EditorGUILayout.LabelField("Lifetime wins / failures / attempts",$"{stats.wins} / {stats.failures} / {stats.attempts}");EditorGUILayout.LabelField("Lifetime Swap / Hint / Blast",$"{stats.powerUses[0]} / {stats.powerUses[1]} / {stats.powerUses[2]}");EditorGUILayout.LabelField("Read-only live statistics",$"Active hours: {real.Engine.Data.activeSeconds/3600:0.##} · Records: {real.Engine.Data.levels.Count}");var game=UnityEngine.Object.FindAnyObjectByType<FloodFill.ThreeD.FloodFillGameManager3D>();var a=game?.CurrentAchievementAttempt;if(a!=null)EditorGUILayout.LabelField("Current attempt",$"{a.id} · moves {a.moves} · eligible {a.eligible}");}
            }
            if(GUILayout.Button("Inspect Saved Player Data (Read Only)"))
            {
                var config=AssetDatabase.LoadAssetAtPath<FloodAchievementSettings>(FloodAchievementSetup.SettingsPath);
                if(config!=null){var path=Path.Combine(Application.persistentDataPath,Path.GetFileName(config.saveFileName));readOnlyEngine=new AchievementEngine(database.Load(),new AchievementPersistence(path).Load());}
            }
            if(readOnlyEngine!=null)
            {
                var stats=readOnlyEngine.Statistics();EditorGUILayout.LabelField("Saved player — read only",$"Wins {stats.wins} · failures {stats.failures} · active hours {readOnlyEngine.Data.activeSeconds/3600:0.##}");
                if(selected!=null)foreach(var tier in selected.definition.tiers){var state=readOnlyEngine.Progress(selected.definition,tier);EditorGUILayout.LabelField(tier.id,$"{state.value:0.##} · completed {state.completed} · claims {state.claimedRewardIds.Count}");}
            }
            if(selected!=null)
            {
                testTier=EditorGUILayout.IntSlider("Selected test tier",Mathf.Clamp(testTier,0,selected.definition.tiers.Count-1),0,selected.definition.tiers.Count-1);var t=selected.definition.tiers[testTier];var p=testEngine.Progress(selected.definition,t);
                EditorGUILayout.LabelField("Isolated tier",$"Progress {p.value:0.##} / {t.target} · completed {p.completed} · seen {p.seen} · claims {p.claimedRewardIds.Count}");
                simulatedProgress=EditorGUILayout.DoubleField("Simulated progress",simulatedProgress);
                if(GUILayout.Button("Simulate Progress (Test Only)")){p.value=simulatedProgress;p.hasValue=true;SaveTest();}
                if(GUILayout.Button("Complete Selected Tier (Test Only)")){p.value=t.target;p.hasValue=true;p.completed=true;p.completedUtc=DateTime.UtcNow.ToString("O");SaveTest();}
                if(GUILayout.Button("Simulate Qualifying Win (Test Only)"))
                {
                    var f=selected.definition.Filters(t);var a=new AchievementAttempt{id=Guid.NewGuid().ToString("N"),key="test:"+Guid.NewGuid().ToString("N"),mode=Math.Max(0,f.mode),difficulty=Math.Max(0,f.difficulty),custom=f.custom==1,level=f.custom==1?0:1,size=f.size>0?f.size:6,occupied=152,budget=20,moves=10,seconds=10,stars=3,finalColor=f.finalColor>=0?f.finalColor:0,targetMask=1,goalsAssigned=3,goalsEarned=3};
                    a.lastAction=new AchievementAction{sequence=1,kind=f.powerKind>=0?f.powerKind:-1,direct=7,merged=10,connected=100,capturePercent=75,multiplier=7,hintFollowed=true,finishing=true,scoreDelta=100};testEngine.Commit(a,true,false);SaveTest();
                }
                if(GUILayout.Button("Preview Notification / Hidden State / Rewards"))FloodAchievementPreview.Open(selected,testEngine);
            }
            if(GUILayout.Button("Reload Test Definitions / Refresh Runtime UI")){testEngine.Reload(database.Load());if(Application.isPlaying)FloodAchievementService.Instance?.ReloadCatalog();}
            if(GUILayout.Button("Save Isolated Test State"))SaveTest();
            if(GUILayout.Button("Load Isolated Test State")&&File.Exists(TestPath))testEngine=new AchievementEngine(database.Load(),JsonUtility.FromJson<AchievementSaveData>(File.ReadAllText(TestPath)));
            if(GUILayout.Button("Reset Isolated Test Progress")&&EditorUtility.DisplayDialog("Reset Test Data?","Only the isolated achievement test file will be cleared.","Reset Test Data","Back")){testEngine=new AchievementEngine(database.Load(),new AchievementSaveData());SaveTest();}
        }
        private void SaveTest(){Directory.CreateDirectory("UserSettings");File.WriteAllText(TestPath,JsonUtility.ToJson(testEngine.Data,true));}
    }
    internal sealed class FloodAchievementPreview:EditorWindow
    {
        private FloodAchievementDefinition asset;private AchievementEngine engine;private bool conceal;private int tier;private bool claimed;
        public static void Open(FloodAchievementDefinition a,AchievementEngine test){var w=GetWindow<FloodAchievementPreview>("Achievement Preview");w.asset=a;w.engine=test;w.minSize=new Vector2(500,300);w.Show();}
        private static void DrawSprite(Rect rect,Sprite sprite)
        {
            if(sprite==null)return;var source=sprite.textureRect;var texture=sprite.texture;GUI.DrawTextureWithTexCoords(rect,texture,new Rect(source.x/texture.width,source.y/texture.height,source.width/texture.width,source.height/texture.height),true);
        }
        private void OnGUI()
        {
            if(asset==null||engine==null)return;conceal=EditorGUILayout.Toggle("Preview concealed",conceal);claimed=EditorGUILayout.Toggle("Preview claimed",claimed);tier=EditorGUILayout.IntSlider("Completed tiers",tier,0,asset.definition.tiers.Count);
            EditorGUILayout.Space();EditorGUILayout.BeginVertical("box");GUILayout.Label("ACHIEVEMENT UNLOCKED",EditorStyles.boldLabel);GUILayout.Label(conceal?"Hidden Achievement":asset.definition.name,EditorStyles.largeLabel);
            if(!conceal&&asset.icon!=null)GUILayout.Label(AssetPreview.GetAssetPreview(asset.icon)??AssetPreview.GetMiniThumbnail(asset.icon),GUILayout.Width(80),GUILayout.Height(80));
            GUILayout.Label(conceal?"???":asset.definition.tiers[Math.Min(tier,asset.definition.tiers.Count-1)].description,EditorStyles.wordWrappedLabel);
            int filled=conceal?0:AchievementValidation.FilledStars(asset.definition.tiers.Count,tier);var settings=AssetDatabase.LoadAssetAtPath<FloodAchievementSettings>(FloodAchievementSetup.SettingsPath);var area=GUILayoutUtility.GetRect(180,55);
            if(settings!=null)for(int i=0;i<3;i++)DrawSprite(new Rect(area.x+i*54,area.y,50,50),i<filled?settings.filledStar:settings.emptyStar);
            GUILayout.Label(conceal?"":claimed?"Preview: Reward Claimed":"Preview: No Reward Configured");EditorGUILayout.EndVertical();
            EditorGUILayout.HelpBox("Preview only. No runtime progress or reward claims are modified.",MessageType.Info);
        }
    }
}
