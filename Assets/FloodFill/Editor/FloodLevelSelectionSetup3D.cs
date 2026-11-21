using System;
using System.Linq;
using FloodFill.ThreeD;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.EditorTools
{
    public static class FloodLevelSelectionSetup3D
    {
        private const string Folder = "Assets/FloodFill/Prefabs/UI/";
        private static Sprite[] atlas;
        private static FloodUISkin skin;
        private static TMP_FontAsset font;
        private static Sprite S(int i) => atlas.Single(s => s.name == "levels_sprite_sheet_" + i);
        private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer=5;
            var r = go.GetComponent<RectTransform>(); r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f); r.sizeDelta=new Vector2(w,h); r.anchoredPosition=new Vector2(x,y); return r;
        }
        private static Image Img(string name, Transform parent, Sprite sprite, float x,float y,float w,float h)
        {
            var image=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>(); image.sprite=sprite; image.raycastTarget=false; return image;
        }
        private static TMP_Text Text(string name, Transform parent,string value,float size,float x,float y,float w,float h)
        {
            var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;
            t.text=value;t.fontSize=size;t.fontStyle=FontStyles.Bold;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;t.raycastTarget=false;
            return t;
        }
        private static Button Button(string name,Transform parent,Sprite sprite,float x,float y,float w,float h)
        {
            var im=Img(name,parent,sprite,x,y,w,h);im.raycastTarget=true;
            var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;
            var c=b.colors;c.disabledColor=new Color(.45f,.5f,.6f,.65f);c.pressedColor=new Color(.65f,.85f,1);b.colors=c;
            return b;
        }
        [MenuItem("Tools/Flood Fill/UI/Install Level Selection")]
        public static void Install()
        {
            atlas=AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Spritesheets/levels_sprite_sheet.png").OfType<Sprite>().ToArray();
            skin=Resources.Load<FloodUISkin>("FloodUISkin");
            var canvas=PrefabUtility.LoadPrefabContents(FloodFillUIPrefabSetup.CanvasPath);
            try
            {
                var ui=canvas.GetComponent<FloodFillMobileUI3D>();
                font=canvas.GetComponentInChildren<TMP_Text>(true).font;
                var serialized=new SerializedObject(ui); var safe=serialized.FindProperty("safeRoot").objectReferenceValue as Transform;
                var old=safe.Find("LevelSelectionPanel");if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var rules=Resources.Load<FloodLevelRules3D>("FloodLevelRules");
                if(rules==null){rules=ScriptableObject.CreateInstance<FloodLevelRules3D>();AssetDatabase.CreateAsset(rules,"Assets/FloodFill/Resources/FloodLevelRules.asset");}
                var panel=Rect("LevelSelectionPanel",safe,0,0,0,0);panel.anchorMin=Vector2.zero;panel.anchorMax=Vector2.one;panel.sizeDelta=Vector2.zero;
                var bg=panel.gameObject.AddComponent<Image>();bg.color=new Color(.005f,.018f,.045f,1);
                var content=Rect("Content",panel,0,0,876,1796);content.gameObject.AddComponent<FloodFillUIFit3D>();
                var c=panel.gameObject.AddComponent<FloodLevelSelection3D>();c.rules=rules;
                var header=Rect("Header",content,0,0,876,1796);
                c.home=Button("HomeButton",header,S(2),-356,790,112,74);Img("HomeIcon",c.home.transform,S(11),0,0,43,36);
                Text("Title",header,"LEVELS",34,0,790,240,65);
                var pause=Button("TopRightButton",header,S(2),356,790,112,74);pause.interactable=false;
                Img("PauseLeft",pause.transform,S(12),-10,0,11,35);Img("PauseRight",pause.transform,S(13),10,0,11,35);
                var cyan=new Color(0,.85f,1);
                Img("LeftAccent",header,null,-185,790,70,2).color=cyan;Img("RightAccent",header,null,185,790,70,2).color=cyan;
                var modeRow=Rect("GameModeTabs",content,0,674,810,94);
                c.modes=new Button[3];c.modeBackgrounds=new Image[3];
                string[] names={"FULL GRID","RANDOM SHAPE","ITEMS"};
                for(int i=0;i<3;i++)
                {
                    var b=Button(names[i],modeRow,S(0),(i-1)*270,0,260,94);c.modes[i]=b;c.modeBackgrounds[i]=b.image;
                    Text("Label",b.transform,names[i],17,0,-28,246,30);
                    if(i==0) for(int q=0;q<4;q++) Img("GridIcon",b.transform,null,(q%2)*22-11,21-(q/2)*22,19,19).color=Color.white;
                    else if(i==1) {for(int q=0;q<3;q++) Img("ConnectedCube",b.transform,skin.Find("Custom"),q==0?0:q==1?-14:14,q==0?24:2,27,28);}
                    else Img("ItemsIcon",b.transform,skin.Find("Custom"),0,13,45,46);
                }
                c.tabNormal=S(0);c.tabSelected=skin.Find("Primary");
                var difficultyRow=Rect("DifficultyTabs",content,0,553,816,82);
                c.difficulties=new Button[4];c.difficultyBackgrounds=new Image[4];
                string[] labels={"EASY","MEDIUM","HARD","EXPERT"};
                for(int i=0;i<4;i++)
                {
                    var b=Button(labels[i],difficultyRow,S(0),(i-1.5f)*205,0,194,82);c.difficulties[i]=b;c.difficultyBackgrounds[i]=b.image;
                    for(int q=0;q<=i;q++) {var star=Rect("Star",b.transform,(q-i*.5f)*29,13,26,26).gameObject.AddComponent<FloodLevelStarGraphic3D>();star.raycastTarget=false;star.color=new Color(.9f,.96f,1);}
                    Text("Label",b.transform,labels[i],17,0,-22,180,25);
                }
                Img("Divider",content,null,0,514,820,2).color=new Color(0,.5f,.8f);
                var progress=Img("ProgressPanel",content,S(3),0,421,814,120).rectTransform;
                // Reuse the existing voxel icon instead of introducing a new decoration asset.
                Img("VoxelIcon",progress,skin.Find("Custom"),-330,0,68,70);
                c.completed=Text("Completed",progress,"0 / 100 COMPLETED",20,-24,23,460,34);
                Img("Track",progress,S(4),-5,-23,486,24);
                c.fill=Img("Fill",progress,S(5),-5,-23,486,24);c.fill.type=Image.Type.Filled;c.fill.fillMethod=Image.FillMethod.Horizontal;c.fill.fillAmount=0;
                c.percentage=Text("Percentage",progress,"0%",43,330,1,140,64);c.percentage.color=cyan;
                c.grid=Rect("LevelGrid",content,0,-133,818,908);
                var layout=c.grid.gameObject.AddComponent<GridLayoutGroup>();layout.cellSize=new Vector2(150,134);layout.spacing=new Vector2(17,20);layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=5;layout.childAlignment=TextAnchor.UpperCenter;
                var tile=Rect("LevelButton",null,0,0,150,134);var view=tile.gameObject.AddComponent<FloodLevelButton3D>();
                view.background=Img("Background",tile,S(8),0,0,150,134);view.background.raycastTarget=true;
                view.button=tile.gameObject.AddComponent<Button>();view.button.targetGraphic=view.background;
                var colors=view.button.colors;colors.disabledColor=Color.white;view.button.colors=colors;
                view.number=Text("LevelNumber",tile,"1",45,0,23,136,65);
                view.lockIcon=Img("Lock",tile,S(16),0,-32,32,42);
                view.stars=new Image[3];for(int i=0;i<3;i++)view.stars[i]=Img("Star"+(i+1),tile,S(18),(i-1)*34,-33,30,30);
                view.available=S(6);view.current=S(7);view.locked=S(8);view.goldStar=S(17);view.grayStar=S(18);
                c.levelPrefab=PrefabUtility.SaveAsPrefabAsset(tile.gameObject,Folder+"LevelButton.prefab").GetComponent<FloodLevelButton3D>();UnityEngine.Object.DestroyImmediate(tile.gameObject);
                for(int i=0;i<30;i++)
                {
                    var preview=(GameObject)PrefabUtility.InstantiatePrefab(c.levelPrefab.gameObject,c.grid);
                    preview.name="LevelButton"+(i+1);var v=preview.GetComponent<FloodLevelButton3D>();v.number.text=(i+1).ToString();
                    v.background.sprite=i==0?v.current:v.locked;v.lockIcon.gameObject.SetActive(i!=0);
                    foreach(var star in v.stars)star.gameObject.SetActive(i==0);
                    v.button.interactable=i==0;v.number.color=i==0?Color.white:new Color(.65f,.71f,.81f);
                    if(i==0)v.background.rectTransform.sizeDelta=new Vector2(180,164);
                }
                var pagination=Rect("Pagination",content,0,-687,650,82);
                c.previous=Button("PreviousButton",pagination,S(2),-248,0,84,76);Img("PreviousIcon",c.previous.transform,S(14),0,0,23,36);
                c.next=Button("NextButton",pagination,S(2),248,0,84,76);Img("NextIcon",c.next.transform,S(15),0,0,23,36);
                c.dots=new Button[4];c.dotImages=new Image[4];c.dotNormal=S(9);c.dotSelected=S(10);
                for(int i=0;i<4;i++){var b=Button("Page"+(i+1),pagination,S(9),(i-1.5f)*40,0,40,56);b.image.sprite=null;b.image.color=Color.clear;var dot=Img("Dot",b.transform,S(9),0,0,22,22);c.dots[i]=b;c.dotImages[i]=dot;}
                BuildRestartConfirmation(c);
                panel.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject,Folder+"LevelSelectionPanel.prefab",InteractionMode.AutomatedAction);
                serialized.Update();serialized.FindProperty("levelSelection").objectReferenceValue=c;serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(canvas,FloodFillUIPrefabSetup.CanvasPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(canvas);}
            AddResultControls();RepairResultButtonSpacing();AssetDatabase.SaveAssets();
            Debug.Log("Level selection installed: authored panel, reusable tile, rules and PLAY integration.");
        }
        [MenuItem("Tools/Flood Fill/UI/Install Saved Level Restart Confirmation")]
        public static void InstallRestartConfirmation()
        {
            var root=PrefabUtility.LoadPrefabContents(Folder+"LevelSelectionPanel.prefab");
            try
            {
                skin=Resources.Load<FloodUISkin>("FloodUISkin");font=root.GetComponentInChildren<TMP_Text>(true).font;
                BuildRestartConfirmation(root.GetComponent<FloodLevelSelection3D>());
                root.GetComponent<FloodLevelSelection3D>().restartCard.GetComponent<Image>().sprite=skin.Find("ResultPanel");
                PrefabUtility.SaveAsPrefabAsset(root,Folder+"LevelSelectionPanel.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        private static void BuildRestartConfirmation(FloodLevelSelection3D controller)
        {
            if(controller.restartPrompt!=null)return;
            var content=controller.transform.Find("Content");
            controller.navigationContent=content.GetComponent<CanvasGroup>();
            if(controller.navigationContent==null)controller.navigationContent=content.gameObject.AddComponent<CanvasGroup>();
            var overlay=Rect("RestartConfirmation",controller.transform,0,0,0,0);
            overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
            var shade=overlay.gameObject.AddComponent<Image>();shade.color=new Color(0,.006f,.02f,.86f);shade.raycastTarget=true;
            var frame=Rect("Content",overlay,0,0,876,1796);frame.gameObject.AddComponent<FloodFillUIFit3D>();
            var card=Img("Card",frame,skin.Find("ResultPanel"),0,0,710,430);card.type=Image.Type.Sliced;
            Text("Title",card.transform,"RESTART LEVEL?",36,0,130,650,65);
            var message=Text("Message",card.transform,"This will restart the level.\nAre you sure you want to continue?",28,0,25,620,140);message.fontStyle=FontStyles.Normal;
            controller.restartContinue=Button("Continue",card.transform,skin.Find("Primary"),-153,-125,280,82);
            Text("Label",controller.restartContinue.transform,"CONTINUE",28,0,0,260,70);
            controller.restartBack=Button("Back",card.transform,skin.Find("Secondary"),153,-125,280,82);
            Text("Label",controller.restartBack.transform,"BACK",28,0,0,260,70);
            controller.restartPrompt=overlay.gameObject;controller.restartCard=card.rectTransform;
            overlay.gameObject.SetActive(false);
        }
        [MenuItem("Tools/Flood Fill/UI/Repair Result Button Spacing")]
        public static void RepairResultButtonSpacing()
        {
            string path=Folder+"GameResults.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var actions=root.transform.Find("Content/ResultActions") as RectTransform;
                foreach(var binding in root.GetComponentsInChildren<FloodFillUIAction3D>(true))
                {
                    var button=binding.GetComponent<RectTransform>();
                    if(button==null||!button.IsChildOf(actions))continue;
                    RectTransform row=button.parent==actions?null:button.parent as RectTransform;
                    float preferred=LayoutUtility.GetPreferredHeight(button);
                    if(row==null)
                    {
                        row=Rect(binding.action+"Slot",actions,0,0,580,Mathf.Max(1,preferred));
                        row.SetSiblingIndex(button.GetSiblingIndex());button.SetParent(row,false);
                        if(preferred>0)button.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,preferred);
                    }
                    var element=row.GetComponent<LayoutElement>();if(element==null)element=row.gameObject.AddComponent<LayoutElement>();
                    element.preferredWidth=element.minWidth=580;element.flexibleWidth=0;
                    var slot=row.GetComponent<FloodFillResultButtonSlot3D>();if(slot==null)slot=row.gameObject.AddComponent<FloodFillResultButtonSlot3D>();
                    slot.buttonImage=binding.GetComponent<Image>();slot.measuredSprite=slot.buttonImage.sprite;
                    slot.bodyPixelY=MeasureBodyY(slot.measuredSprite);slot.Refresh();
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        private static Vector2 MeasureBodyY(Sprite sprite)
        {
            Texture2D texture=sprite.texture;var previous=RenderTexture.active;
            var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Texture2D readable=null;
            try
            {
                Graphics.Blit(texture,target);RenderTexture.active=target;
                readable=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);
                readable.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);readable.Apply();
                var pixels=readable.GetPixels32();Rect rect=sprite.rect;
                int bottom=(int)rect.yMax,top=(int)rect.yMin;
                for(int y=(int)rect.yMin;y<(int)rect.yMax;y++)for(int x=(int)rect.xMin;x<(int)rect.xMax;x++)
                    if(pixels[y*texture.width+x].a>=230){bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y+1);}
                return top>bottom?new Vector2(bottom-rect.yMin,top-rect.yMin):new Vector2(0,rect.height);
            }
            finally
            {
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);
                if(readable!=null)UnityEngine.Object.DestroyImmediate(readable);
            }
        }
        [MenuItem("Tools/Flood Fill/UI/Repair Result Screen Layout")]
        public static void RepairResultLayout()
        {
            skin=Resources.Load<FloodUISkin>("FloodUISkin");
            AddResultControls();RepairResultButtonSpacing();AssetDatabase.SaveAssets();
        }
        private static void AddResultControls()
        {
            string path=Folder+"GameResults.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                font=root.GetComponentInChildren<TMP_Text>(true).font;
                var parent=root.transform.Find("Content");if(parent==null)parent=root.transform;
                foreach(var entry in new[]{("LEVELS",FloodFillUIAction.Levels,-150f),("REPLAY",FloodFillUIAction.Restart,150f)})
                {
                    if(root.GetComponentsInChildren<FloodFillUIAction3D>(true).Any(value=>value.action==entry.Item2))continue;
                    var b=Button(entry.Item1,parent,skin.Find("Secondary"),entry.Item3,-325,280,78);Text("Label",b.transform,entry.Item1,28,0,0,260,65);
                    var action=b.gameObject.AddComponent<FloodFillUIAction3D>();action.action=entry.Item2;action.audioLabel=entry.Item1;UnityEventTools.AddPersistentListener(b.onClick,action.Invoke);
                }
                var actions=parent.Find("ResultActions") as RectTransform;
                if(actions==null)actions=Rect("ResultActions",parent,0,-160,580,410);
                actions.anchorMin=actions.anchorMax=new Vector2(.5f,.5f);actions.pivot=new Vector2(.5f,1);
                actions.anchoredPosition=new Vector2(0,-160);actions.sizeDelta=new Vector2(580,410);
                var layout=actions.GetComponent<VerticalLayoutGroup>();if(layout==null)layout=actions.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing=18;layout.childAlignment=TextAnchor.UpperCenter;layout.childControlWidth=true;layout.childControlHeight=true;
                layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;
                var bindings=root.GetComponentsInChildren<FloodFillUIAction3D>(true);
                var order=new[]{FloodFillUIAction.Result,FloodFillUIAction.Restart,FloodFillUIAction.Levels,FloodFillUIAction.ReturnToMenu};
                for(int i=0;i<order.Length;i++)
                {
                    var binding=bindings.Single(value=>value.action==order[i]);
                    var button=binding.GetComponent<RectTransform>();
                    var existingSlot=binding.GetComponentInParent<FloodFillResultButtonSlot3D>(true);
                    if(existingSlot!=null){existingSlot.transform.SetParent(actions,false);existingSlot.transform.SetAsLastSibling();}
                    else {button.SetParent(actions,false);button.SetAsLastSibling();}
                    button.anchorMin=button.anchorMax=new Vector2(.5f,.5f);button.pivot=new Vector2(.5f,.5f);button.localScale=Vector3.one;
                    var element=button.GetComponent<LayoutElement>();if(element==null)element=button.gameObject.AddComponent<LayoutElement>();
                    element.minWidth=element.preferredWidth=580;element.minHeight=element.preferredHeight=i==0?100:84;element.flexibleHeight=element.flexibleWidth=0;
                    var container=button.Find("ButtonContent") as RectTransform;
                    if(container!=null)
                    {
                        foreach(var group in container.GetComponents<LayoutGroup>())UnityEngine.Object.DestroyImmediate(group);
                        var fitter=container.GetComponent<ContentSizeFitter>();if(fitter!=null)UnityEngine.Object.DestroyImmediate(fitter);
                        container.anchorMin=Vector2.zero;container.anchorMax=Vector2.one;container.offsetMin=container.offsetMax=Vector2.zero;
                    }
                    foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))
                    {
                        text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,.5f);
                        text.rectTransform.anchoredPosition=new Vector2(container!=null?22:0,0);text.rectTransform.sizeDelta=new Vector2(container!=null?430:540,70);
                        text.alignment=TextAlignmentOptions.Center;text.fontSize=i==0?34:30;
                        if(i==0)text.text="NEXT LEVEL";
                    }
                    if(container!=null)
                    {
                        var icon=container.Find("Icon") as RectTransform;
                        if(icon!=null){icon.anchorMin=icon.anchorMax=new Vector2(.5f,.5f);icon.anchoredPosition=new Vector2(-225,0);icon.sizeDelta=new Vector2(42,42);}
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
