using System.Linq;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Goals;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodPauseNavigationSetup3D
    {
        private const string Folder="Assets/FloodFill/Prefabs/UI/";
        [MenuItem("Tools/Flood Fill/UI/Install Home Confirmation and Pause Settings")]
        public static void Install()
        {
            Edit("GameplayHUD.prefab",root=>
            {
                var home=root.GetComponentsInChildren<FloodFillUIAction3D>(true).Single(b=>b.audioLabel=="HOME");
                home.action=FloodFillUIAction.Home;
            });
            Edit("PauseMenu.prefab",root=>
            {
                var bindings=root.GetComponentsInChildren<FloodFillUIAction3D>(true);
                var restart=bindings.Single(b=>b.action==FloodFillUIAction.Restart);
                var home=bindings.Single(b=>b.action==FloodFillUIAction.ReturnToMenu||b.action==FloodFillUIAction.Home);home.action=FloodFillUIAction.Home;
                if(!bindings.Any(b=>b.action==FloodFillUIAction.Settings))
                {
                    var go=Object.Instantiate(restart.gameObject,restart.transform.parent);go.name="SETTINGSButton";
                    var binding=go.GetComponent<FloodFillUIAction3D>();binding.action=FloodFillUIAction.Settings;binding.audioLabel="SETTINGS";
                    var label=go.GetComponentsInChildren<TMP_Text>(true).Single();label.text="SETTINGS";label.gameObject.name="SETTINGS";
                    var skin=Resources.Load<FloodUISkin>("FloodUISkin");var icon=go.GetComponentsInChildren<Image>(true).FirstOrDefault(im=>im.name=="Icon");if(icon!=null)icon.sprite=skin.Find("Settings");
                    var rect=(RectTransform)go.transform;var position=rect.anchoredPosition;position.y=-90;rect.anchoredPosition=position;
                    var homeRect=(RectTransform)home.transform;position=homeRect.anchoredPosition;position.y=-210;homeRect.anchoredPosition=position;
                    go.transform.SetSiblingIndex(home.transform.GetSiblingIndex());
                }
            });
            Edit("Settings.prefab",root=>
            {
                var back=root.GetComponentsInChildren<FloodFillUIAction3D>(true).Single(b=>b.audioLabel=="BACK");back.action=FloodFillUIAction.SettingsBack;
            });
            var homePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"HomeConfirmation.prefab");
            if(homePrefab==null)homePrefab=CreateHomeConfirmation();
            Edit("HomeConfirmation.prefab",root=>
            {
                foreach(var binding in root.GetComponentsInChildren<FloodFillUIAction3D>(true))
                {
                    var graphic=binding.GetComponent<FloodGoalPanelGraphic3D>();
                    if(graphic==null)
                    {
                        var image=binding.GetComponent<Image>();if(image!=null)Object.DestroyImmediate(image);
                        graphic=binding.gameObject.AddComponent<FloodGoalPanelGraphic3D>();
                        graphic.radius=18;graphic.borderWidth=2;
                        bool primary=binding.action==FloodFillUIAction.ConfirmHome;
                        graphic.color=primary?new Color(0,.58f,.86f):new Color(.07f,.12f,.21f);
                        graphic.borderColor=primary?new Color(.15f,.85f,1):new Color(.2f,.32f,.5f);
                        binding.GetComponent<Button>().targetGraphic=graphic;
                    }
                }
            });
            Edit("FloodFillCanvas.prefab",root=>
            {
                var ui=root.GetComponent<FloodFillMobileUI3D>();var serialized=new SerializedObject(ui);
                var property=serialized.FindProperty("homeConfirmation");
                if(property.objectReferenceValue==null)
                {
                    var safe=serialized.FindProperty("safeRoot").objectReferenceValue as Transform;
                    var existing=safe.Find("HomeConfirmation");
                    var instance=existing!=null?existing.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(homePrefab,safe);
                    instance.SetActive(false);property.objectReferenceValue=instance;serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            });
            AssetDatabase.SaveAssets();
        }
        private static void Edit(string name,System.Action<GameObject> edit)
        {
            var root=PrefabUtility.LoadPrefabContents(Folder+name);
            try{edit(root);PrefabUtility.SaveAsPrefabAsset(root,Folder+name);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static GameObject CreateHomeConfirmation()
        {
            var skin=Resources.Load<FloodUISkin>("FloodUISkin");var pause=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PauseMenu.prefab");var sample=pause.GetComponentInChildren<TMP_Text>(true);
            var root=Rect("HomeConfirmation",null,0,0,0,0);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;
            var shade=root.gameObject.AddComponent<Image>();shade.color=new Color(0,.006f,.02f,.86f);shade.raycastTarget=true;
            var frame=Rect("Content",root,0,0,876,1796);frame.gameObject.AddComponent<FloodFillUIFit3D>();
            var card=Rect("Card",frame,0,0,710,460);var background=card.gameObject.AddComponent<Image>();background.sprite=skin.Find("ResultPanel");background.type=Image.Type.Sliced;background.raycastTarget=false;
            Label("Title",card,"QUIT TO HOME?",36,0,145,650,60,sample);
            var message=Label("Message",card,"Are you sure you want to quit to home?",30,0,25,620,150,sample);message.fontStyle=FontStyles.Normal;message.textWrappingMode=TextWrappingModes.Normal;
            ActionButton(card,"CONFIRM",-153,-130,skin.Find("Primary"),FloodFillUIAction.ConfirmHome,sample);
            ActionButton(card,"BACK",153,-130,skin.Find("Secondary"),FloodFillUIAction.CancelHome,sample);
            root.gameObject.SetActive(false);
            try{return PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"HomeConfirmation.prefab");}
            finally{Object.DestroyImmediate(root.gameObject);}
        }
        private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
        private static TMP_Text Label(string name,Transform parent,string text,float size,float x,float y,float w,float h,TMP_Text sample)
        {var r=Rect(name,parent,x,y,w,h);var label=r.gameObject.AddComponent<TextMeshProUGUI>();label.font=sample.font;label.fontSize=size;label.fontStyle=FontStyles.Bold;label.text=text;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;}
        private static void ActionButton(Transform parent,string text,float x,float y,Sprite sprite,FloodFillUIAction action,TMP_Text sample)
        {
            var rect=Rect(text+"Button",parent,x,y,280,82);var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.type=Image.Type.Sliced;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var binding=rect.gameObject.AddComponent<FloodFillUIAction3D>();binding.action=action;binding.audioLabel=text;
            UnityEventTools.AddPersistentListener(button.onClick,binding.Invoke);Label("Label",rect,text,28,0,0,260,70,sample);
        }
    }
}
