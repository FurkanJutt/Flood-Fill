using System;
using System.IO;
using System.Linq;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Achievements;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodAchievementSetup
    {
        public const string Definitions="Assets/FloodFill/Achievements/Definitions",DatabasePath="Assets/FloodFill/Achievements/FloodAchievementDatabase.asset",SettingsPath="Assets/FloodFill/Resources/FloodAchievements.asset";
        public const string UIFolder="Assets/FloodFill/Prefabs/UI/Achievements",CanvasPath="Assets/FloodFill/Prefabs/UI/FloodFillCanvas.prefab",Sheet="Assets/Images/Spritesheets/flood_fill_3d_achievement_icons_sheet.png";
        private static FloodUISkin skin;private static TMP_FontAsset font;
        private static readonly Color Cyan=new Color(0,.85f,1),Muted=new Color(.65f,.78f,.94f);
        [MenuItem("Tools/Flood Fill/Achievements/Install or Repair UI")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install achievements outside Play Mode.");
            Directory.CreateDirectory(Definitions);Directory.CreateDirectory(UIFolder);AssetDatabase.Refresh();
            skin=Resources.Load<FloodUISkin>("FloodUISkin");
            var oldCanvas=AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);font=oldCanvas.GetComponentInChildren<TMP_Text>(true).font;
            var db=EnsureCatalog();var settings=AssetDatabase.LoadAssetAtPath<FloodAchievementSettings>(SettingsPath);
            if(settings==null){settings=ScriptableObject.CreateInstance<FloodAchievementSettings>();AssetDatabase.CreateAsset(settings,SettingsPath);}
            settings.database=db;
            settings.filledStar=Sprites("Assets/Images/Spritesheets/level_complete_sprite_sheet.png").Single(s=>s.name=="level_complete_sprite_sheet_17");
            var levelAtlas=Sprites("Assets/Images/Spritesheets/levels_sprite_sheet.png");
            settings.emptyStar=levelAtlas.Single(s=>s.name=="levels_sprite_sheet_18");
            settings.hiddenIcon=levelAtlas.Single(s=>s.name=="levels_sprite_sheet_16");
            settings.defaultIcon=db.definitions[0].icon;settings.completedIndicator=settings.filledStar;
            if(settings.emptyStar==null||settings.hiddenIcon==null)throw new Exception("Existing empty star/lock sprites not found.");
            var card=AssetDatabase.LoadAssetAtPath<GameObject>(UIFolder+"/AchievementCard.prefab")??CreateCard();settings.cardPrefab=card;
            var cardContents=PrefabUtility.LoadPrefabContents(UIFolder+"/AchievementCard.prefab");
            try {var c=cardContents.GetComponent<FloodAchievementCard>();if(c.rewardText==null){c.rewardText=Text("Rewards",c.transform,"",16,15,-88,880,24);c.rewardText.color=Cyan;PrefabUtility.SaveAsPrefabAsset(cardContents,UIFolder+"/AchievementCard.prefab");}}
            finally {PrefabUtility.UnloadPrefabContents(cardContents);}
            var detail=AssetDatabase.LoadAssetAtPath<GameObject>(UIFolder+"/AchievementDetail.prefab")??CreateDetail(settings);settings.detailPrefab=detail;
            var screen=AssetDatabase.LoadAssetAtPath<GameObject>(UIFolder+"/AchievementScreen.prefab")??CreateScreen(card,detail);settings.screenPrefab=screen;
            var screenContents=PrefabUtility.LoadPrefabContents(UIFolder+"/AchievementScreen.prefab");
            try
            {
                foreach(var dropdown in screenContents.GetComponentsInChildren<TMP_Dropdown>(true))ConfigureDropdownPadding(dropdown);
                PrefabUtility.SaveAsPrefabAsset(screenContents,UIFolder+"/AchievementScreen.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(screenContents);}
            var notice=AssetDatabase.LoadAssetAtPath<GameObject>(UIFolder+"/AchievementNotification.prefab")??CreateNotification(settings);settings.notificationPrefab=notice;
            InstallMenu(settings);InstallCanvas(settings);
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            var errors=AchievementValidation.Validate(db.Load()).Where(s=>s.StartsWith("ERROR:",StringComparison.Ordinal)).ToArray();
            if(errors.Length>0)throw new Exception(string.Join("\n",errors));
            Debug.Log("Achievements installed: "+db.definitions.Count+" definitions, "+db.definitions.Sum(a=>a.definition.tiers.Count)+" milestones. No rewards assigned.");
        }
        public static FloodAchievementDatabase EnsureCatalog()
        {
            var db=AssetDatabase.LoadAssetAtPath<FloodAchievementDatabase>(DatabasePath);
            if(db==null){db=ScriptableObject.CreateInstance<FloodAchievementDatabase>();AssetDatabase.CreateAsset(db,DatabasePath);}
            var parsed=JsonUtility.FromJson<AchievementCatalogData>(File.ReadAllText("Documentation/Achievements/Catalog.v1.json"));
            var importer=(TextureImporter)AssetImporter.GetAtPath(Sheet);
            if(importer==null)throw new Exception("Supplied achievement icon sheet is missing.");
            if(Sprites(Sheet).Length<112)
            {
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.SaveAndReimport();
                var factories=new SpriteDataProviderFactories();factories.Init();
                var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var slices=new SpriteRect[112];
                for(int i=0;i<slices.Length;i++)slices[i]=new SpriteRect{name="ff_achievement_"+(i+1).ToString("000"),rect=new Rect((i%12)*512,5120-(i/12+1)*512,512,512),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),spriteID=new GUID(Hash128.Compute("FloodFillAchievementIcon"+i).ToString())};
                provider.SetSpriteRects(slices);
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(slices.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
                provider.Apply();importer.SaveAndReimport();
            }
            var icons=Sprites(Sheet);
            foreach(var data in parsed.achievements)
            {
                var existing=db.definitions.FirstOrDefault(a=>a!=null&&a.definition.id==data.id);
                if(existing==null)
                {
                    existing=ScriptableObject.CreateInstance<FloodAchievementDefinition>();existing.definition=data;
                    AssetDatabase.CreateAsset(existing,Definitions+"/"+data.id+".asset");db.definitions.Add(existing);
                }
                if(existing.icon==null)existing.icon=icons.FirstOrDefault(s=>s.name=="ff_achievement_"+data.order.ToString("000"));
                EditorUtility.SetDirty(existing);
            }
            EditorUtility.SetDirty(db);return db;
        }
        private static Sprite[] Sprites(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        private static Sprite FindSprite(string prefix,int number)
        {
            foreach(string guid in AssetDatabase.FindAssets(prefix+" t:Texture2D",new[]{"Assets/Images"}))
            {var sprite=Sprites(AssetDatabase.GUIDToAssetPath(guid)).FirstOrDefault(s=>s.name.EndsWith("_"+number,StringComparison.Ordinal));if(sprite!=null)return sprite;}return null;
        }
        private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=(RectTransform)go.transform;r.SetParent(parent,false);r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,y);return r;
        }
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static Image Img(string name,Transform parent,Sprite sprite,float x,float y,float w,float h)
        {
            var image=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;image.raycastTarget=false;return image;
        }
        private static TMP_Text Text(string name,Transform parent,string value,int size,float x,float y,float w,float h,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var label=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.text=value;label.fontSize=size;label.fontStyle=FontStyles.Bold;label.color=Color.white;label.alignment=align;label.raycastTarget=false;return label;
        }
        private static Button Button(string name,Transform parent,string label,float x,float y,float w,float h)
        {
            var image=Img(name,parent,skin.Find("Secondary"),x,y,w,h);image.raycastTarget=true;var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            Text("Label",image.transform,label,26,0,0,w-24,h-8);return button;
        }
        private static Image Fill(string name,Transform parent,float x,float y,float w,float h)
        {
            var track=Img(name,parent,skin.Find("ProgressTrack"),x,y,w,h);var fill=Img("Fill",track.transform,skin.Find("ProgressFill"),0,0,w,h);fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;fill.fillAmount=0;return fill;
        }
        private static GameObject Save(RectTransform root,string filename)
        {try{return PrefabUtility.SaveAsPrefabAsset(root.gameObject,UIFolder+"/"+filename+".prefab");}finally{UnityEngine.Object.DestroyImmediate(root.gameObject);}}
        private static GameObject CreateCard()
        {
            var root=Rect("AchievementCard",null,0,0,980,208);var panel=root.gameObject.AddComponent<Image>();panel.sprite=skin.Find("ResultPanel")??skin.Find("Secondary");panel.type=Image.Type.Sliced;
            var card=root.gameObject.AddComponent<FloodAchievementCard>();card.button=root.gameObject.AddComponent<Button>();card.button.targetGraphic=panel;
            card.icon=Img("Icon",root,null,-414,12,112,112);card.icon.preserveAspect=true;card.icon.type=Image.Type.Simple;
            card.title=Text("Name",root,"Achievement",30,-35,65,620,44,TextAlignmentOptions.Left);
            card.requirement=Text("Requirement",root,"Requirement",23,-20,14,650,65,TextAlignmentOptions.Left);card.requirement.fontStyle=FontStyles.Normal;card.requirement.color=Muted;
            card.tierText=Text("Tier",root,"TIER 1 OF 3",18,332,66,230,34);
            card.progressFill=Fill("Progress",root,-30,-54,610,16);card.progressText=Text("ProgressValue",root,"0 / 25",20,357,-54,190,34);
            card.rewardText=Text("Rewards",root,"",16,15,-88,880,24);card.rewardText.color=Cyan;
            for(int i=0;i<3;i++){card.stars[i]=Img("Star"+(i+1),root,null,286+i*53,5,47,47);card.stars[i].preserveAspect=true;card.stars[i].type=Image.Type.Simple;}
            return Save(root,"AchievementCard");
        }
        private static GameObject CreateDetail(FloodAchievementSettings settings)
        {
            var root=Rect("AchievementDetail",null,0,0,1080,1920);var background=root.gameObject.AddComponent<Image>();background.sprite=skin.Find("ResultPanel");background.color=new Color(.03f,.06f,.12f,.98f);Stretch(root);
            var panel=Img("DetailCard",root,skin.Find("ResultPanel"),0,0,1000,1570);panel.raycastTarget=true;
            var detail=root.gameObject.AddComponent<FloodAchievementDetailPanel>();
            detail.icon=Img("Icon",panel.transform,settings.defaultIcon,0,620,150,150);detail.icon.preserveAspect=true;detail.icon.type=Image.Type.Simple;
            detail.title=Text("Name",panel.transform,"Achievement",38,0,485,880,100);
            var bodyViewport=Rect("BodyViewport",panel.transform,0,-40,880,900);bodyViewport.gameObject.AddComponent<RectMask2D>();var scroll=bodyViewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=bodyViewport;
            detail.body=Text("Body",bodyViewport,"All tier requirements",26,0,0,860,900,TextAlignmentOptions.TopLeft);detail.body.fontStyle=FontStyles.Normal;detail.body.color=Muted;
            var bodyRect=detail.body.rectTransform;bodyRect.anchorMin=bodyRect.anchorMax=new Vector2(.5f,1);bodyRect.pivot=new Vector2(.5f,1);bodyRect.anchoredPosition=Vector2.zero;
            scroll.content=bodyRect;scroll.movementType=ScrollRect.MovementType.Clamped;
            detail.close=Button("Close",panel.transform,"BACK",0,-665,820,82);
            for(int i=0;i<3;i++){detail.claims[i]=Button("ClaimTier"+(i+1),panel.transform,"NO REWARD CONFIGURED",(i-1)*285,-550,270,72);detail.claimLabels[i]=detail.claims[i].GetComponentInChildren<TMP_Text>();detail.claims[i].gameObject.SetActive(false);}
            root.gameObject.SetActive(false);return Save(root,"AchievementDetail");
        }
        private static TMP_Dropdown Dropdown(string name,Transform parent,float x,float y,float w)
        {
            var go=TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());go.name=name;go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,82);
            foreach(var image in go.GetComponentsInChildren<Image>(true)){image.sprite=skin.Find("Secondary");image.type=Image.Type.Sliced;}
            foreach(var label in go.GetComponentsInChildren<TMP_Text>(true)){label.font=font;label.fontSize=23;label.color=Color.white;}
            var arrow=go.transform.Find("Arrow");if(arrow!=null){arrow.GetComponent<Image>().sprite=skin.Find("Back");arrow.localRotation=Quaternion.Euler(0,0,-90);}
            var dropdown=go.GetComponent<TMP_Dropdown>();dropdown.template.sizeDelta=new Vector2(0,520);ConfigureDropdownPadding(dropdown);return dropdown;
        }
        private static void ConfigureDropdownPadding(TMP_Dropdown dropdown)
        {
            // TMP derives list padding from the template item's distance to the content edges.
            var scroll=dropdown.template.GetComponent<ScrollRect>();
            var content=scroll.content;
            var item=dropdown.itemText.GetComponentInParent<Toggle>(true).GetComponent<RectTransform>();
            var toggle=item.GetComponent<Toggle>();
            var checkmark=toggle.graphic;
            toggle.graphic=null;
            if(checkmark!=null)UnityEngine.Object.DestroyImmediate(checkmark.gameObject);
            var colors=item.GetComponent<FloodAchievementDropdownItem>()??item.gameObject.AddComponent<FloodAchievementDropdownItem>();
            colors.label=dropdown.itemText;colors.normalColor=Color.white;colors.selectedColor=Cyan;
            float height=item.rect.height;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            item.anchorMin=new Vector2(item.anchorMin.x,1f);
            item.anchorMax=new Vector2(item.anchorMax.x,1f);
            item.anchoredPosition=new Vector2(item.anchoredPosition.x,-height*(1f-item.pivot.y));
        }
        private static GameObject CreateScreen(GameObject card,GameObject detail)
        {
            var root=Rect("Achievements",null,0,0,1080,1920);Stretch(root);var background=root.gameObject.AddComponent<Image>();background.color=new Color(.01f,.025f,.055f,1);
            var frame=Rect("Content",root,0,0,1080,1920);frame.gameObject.AddComponent<FloodFillUIFit3D>();
            var view=root.gameObject.AddComponent<FloodAchievementScreen>();view.cardPrefab=card.GetComponent<FloodAchievementCard>();
            view.back=Button("Back",frame,"‹",-452,853,100,84);Text("Title",frame,"ACHIEVEMENTS",44,10,853,730,80);
            view.summary=Text("Summary",frame,"ACHIEVEMENTS —\nMILESTONES —",29,0,745,980,100);view.summary.color=Muted;
            view.summaryFill=Fill("OverallProgress",frame,0,666,970,18);
            view.category=Dropdown("Category",frame,-247,587,480);view.filter=Dropdown("Filter",frame,247,587,480);
            var viewport=Rect("Viewport",frame,0,-140,1000,1330);viewport.gameObject.AddComponent<RectMask2D>();var hit=viewport.gameObject.AddComponent<Image>();hit.color=new Color(0,0,0,.001f);
            view.scroll=viewport.gameObject.AddComponent<ScrollRect>();view.scroll.horizontal=false;view.scroll.viewport=viewport;view.scroll.movementType=ScrollRect.MovementType.Clamped;view.scroll.scrollSensitivity=45;
            view.content=Rect("Cards",viewport,0,0,1000,0);view.content.anchorMin=new Vector2(0,1);view.content.anchorMax=new Vector2(1,1);view.content.pivot=new Vector2(.5f,1);view.content.sizeDelta=Vector2.zero;view.scroll.content=view.content;
            var detailInstance=(GameObject)PrefabUtility.InstantiatePrefab(detail,frame);var bindings=detailInstance.GetComponent<FloodAchievementDetailPanel>();view.detailPanel=detailInstance;view.detailTitle=bindings.title;view.detailBody=bindings.body;view.detailIcon=bindings.icon;view.closeDetail=bindings.close;view.claimButtons=bindings.claims;view.claimLabels=bindings.claimLabels;
            root.gameObject.SetActive(false);return Save(root,"AchievementScreen");
        }
        private static GameObject CreateNotification(FloodAchievementSettings settings)
        {
            var root=Rect("AchievementNotifications",null,0,0,1080,1920);root.gameObject.AddComponent<FloodFillUIFit3D>();
            var view=root.gameObject.AddComponent<FloodAchievementNotifications>();var panel=Img("Notification",root,skin.Find("Secondary"),0,-555,930,158);view.panel=panel.gameObject.AddComponent<CanvasGroup>();view.panel.blocksRaycasts=false;
            foreach(var g in panel.GetComponentsInChildren<Graphic>())g.raycastTarget=false;
            view.icon=Img("Icon",panel.transform,settings.defaultIcon,-375,0,112,112);view.icon.type=Image.Type.Simple;view.icon.preserveAspect=true;
            view.title=Text("Title",panel.transform,"ACHIEVEMENT UNLOCKED",25,20,37,620,42,TextAlignmentOptions.Left);view.title.color=Cyan;
            view.body=Text("Name",panel.transform,"Achievement name",27,-25,-10,530,65,TextAlignmentOptions.Left);
            for(int i=0;i<3;i++){view.stars[i]=Img("Star"+(i+1),panel.transform,settings.emptyStar,300+i*44,-27,40,40);view.stars[i].type=Image.Type.Simple;view.stars[i].preserveAspect=true;}
            panel.gameObject.SetActive(false);return Save(root,"AchievementNotification");
        }
        private static void InstallMenu(FloodAchievementSettings settings)
        {
            const string path="Assets/FloodFill/Prefabs/UI/MainMenu.prefab";var menu=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var existing=menu.GetComponentsInChildren<FloodFillUIAction3D>(true).FirstOrDefault(b=>b.action==FloodFillUIAction.Achievements);
                var template=menu.GetComponentsInChildren<FloodFillUIAction3D>(true).First(b=>b.action==FloodFillUIAction.Settings);
                var clone=existing!=null?existing.gameObject:UnityEngine.Object.Instantiate(template.gameObject,template.transform.parent);clone.name="ACHIEVEMENTSButton";int slot=template.transform.GetSiblingIndex()+1;if(clone.transform.GetSiblingIndex()<slot)slot--;clone.transform.SetSiblingIndex(slot);
                var binding=clone.GetComponent<FloodFillUIAction3D>();binding.action=FloodFillUIAction.Achievements;binding.audioLabel="ACHIEVEMENTS";
                var button=clone.GetComponent<Button>();for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);button.onClick.RemoveAllListeners();UnityEventTools.AddPersistentListener(button.onClick,binding.Invoke);
                foreach(var label in clone.GetComponentsInChildren<TMP_Text>(true))label.text="ACHIEVEMENTS";
                var icon=clone.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.gameObject.name=="Icon");
                if(icon!=null){icon.sprite=settings.defaultIcon;var state=icon.GetComponent<FloodFillUIState3D>();if(state!=null){state.normalSprite=state.selectedSprite=state.disabledSprite=settings.defaultIcon;}}
                var attention=clone.transform.Find("AchievementAttention");
                var indicator=attention!=null?attention.GetComponent<Image>():Img("AchievementAttention",clone.transform,settings.filledStar,290,0,30,30);indicator.type=Image.Type.Simple;indicator.preserveAspect=true;indicator.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(menu,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(menu);}
        }
        private static void InstallCanvas(FloodAchievementSettings settings)
        {
            var canvas=PrefabUtility.LoadPrefabContents(CanvasPath);
            try
            {
                var ui=canvas.GetComponent<FloodFillMobileUI3D>();var serialized=new SerializedObject(ui);var safe=(Transform)serialized.FindProperty("safeRoot").objectReferenceValue;
                var screen=canvas.GetComponentInChildren<FloodAchievementScreen>(true);
                if(screen==null)screen=((GameObject)PrefabUtility.InstantiatePrefab(settings.screenPrefab,safe)).GetComponent<FloodAchievementScreen>();
                screen.name="AchievementScreen";screen.gameObject.SetActive(false);
                var notifications=canvas.GetComponentInChildren<FloodAchievementNotifications>(true);
                if(notifications==null)notifications=((GameObject)PrefabUtility.InstantiatePrefab(settings.notificationPrefab,safe)).GetComponent<FloodAchievementNotifications>();
                notifications.menuIndicator=canvas.GetComponentsInChildren<Transform>(true).First(t=>t.name=="AchievementAttention").gameObject;
                serialized.FindProperty("achievementScreen").objectReferenceValue=screen;serialized.FindProperty("achievementNotifications").objectReferenceValue=notifications;serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(canvas,CanvasPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(canvas);}
        }
        public static void InstallBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Run installation verification in an isolated copy.");
            Install();var batchSettings=AssetDatabase.LoadAssetAtPath<FloodAchievementSettings>(SettingsPath);batchSettings.saveDebounceDuration=2;EditorUtility.SetDirty(batchSettings);FloodAchievementMenu.ApplyImport(AssetDatabase.LoadAssetAtPath<FloodAchievementDatabase>(DatabasePath),JsonUtility.FromJson<AchievementCatalogData>(File.ReadAllText("Documentation/Achievements/Catalog.v1.json")));File.WriteAllText("/tmp/FloodAchievementInstallation.txt","PASS: catalog and UI assets created and assigned.");
        }
    }
}
