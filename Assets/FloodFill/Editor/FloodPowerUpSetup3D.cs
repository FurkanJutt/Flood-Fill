using System;
using System.IO;
using System.Linq;
using FloodFill.ThreeD;
using FloodFill.ThreeD.PowerUps;
using FloodFill.ThreeD.Audio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodPowerUpSetup3D
    {
        public const string BarPath="Assets/FloodFill/Prefabs/UI/PowerUpBar.prefab",SettingsPath="Assets/FloodFill/Resources/FloodPowerUps.asset";
        [MenuItem("Tools/Flood Fill/Power-Ups/Install In Open Scene")]
        public static void Install()
        {
            var settings=EnsureSettings();RepairBarIcons();InstallHud(settings);
            foreach(var game in UnityEngine.Object.FindObjectsByType<FloodFillGameManager3D>(FindObjectsInactive.Include))
            {
                var component=game.GetComponent<FloodPowerUps3D>();if(component==null)component=Undo.AddComponent<FloodPowerUps3D>(game.gameObject);
                component.game=game;component.settings=settings;
                component.ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();
                EditorUtility.SetDirty(component);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
        }
        private static void RepairBarIcons()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(BarPath)==null)return;
            var root=PrefabUtility.LoadPrefabContents(BarPath);bool changed=false;
            try
            {
                foreach(var icon in root.GetComponentsInChildren<FloodPowerUpIcon3D>(true))
                    if(icon.GetComponent<CanvasRenderer>()==null){icon.gameObject.AddComponent<CanvasRenderer>();changed=true;}
                if(changed)PrefabUtility.SaveAsPrefabAsset(root,BarPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static FloodPowerUpSettings3D EnsureSettings()
        {
            var settings=AssetDatabase.LoadAssetAtPath<FloodPowerUpSettings3D>(SettingsPath);if(settings!=null)return settings;
            var shader=Shader.Find("FloodFill/Power Up Outline");if(shader==null)throw new Exception("Power-up outline shader has not imported.");
            string materialPath="Assets/FloodFill/Art/PowerUps/PowerUpOutline.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
            settings=ScriptableObject.CreateInstance<FloodPowerUpSettings3D>();settings.outlineMaterial=material;
            var audio=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FloodFill/Audio/FloodAudioManager.prefab").GetComponent<FloodAudioManager>();
            settings.selectionSound=audio.colorSelected.clip;settings.cancellationSound=audio.buttonCancel.clip;
            settings.colorSwap.activationSound=audio.buttonConfirm.clip;settings.colorBlast.activationSound=audio.blockColorChange.clip;
            settings.hintActivationSound=audio.colorSelected.clip;settings.hintRevealedSound=audio.buttonConfirm.clip;
            settings.invalidActionSound=audio.buttonCancel.clip;settings.unavailableSound=audio.buttonCancel.clip;
            var tabs=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FloodFill/Prefabs/UI/LevelSelectionPanel.prefab").GetComponent<FloodLevelSelection3D>();
            settings.availableButtonSprite=tabs.tabNormal;settings.selectedButtonSprite=tabs.tabSelected;settings.disabledButtonSprite=tabs.tabNormal;
            AssetDatabase.CreateAsset(settings,SettingsPath);return settings;
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.layer=5;var rect=(RectTransform)go.transform;rect.SetParent(parent,false);rect.sizeDelta=size;return rect;
        }
        private static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,string label,float size)
        {
            var rect=Rect(name,parent,new Vector2(200,30));var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=label;text.fontSize=size;text.fontStyle=FontStyles.Bold;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;return text;
        }
        private static GameObject CreateBar(FloodPowerUpSettings3D settings,TMP_FontAsset font)
        {
            var root=Rect("PowerUpBar",null,new Vector2(790,110));
            var row=root.gameObject.AddComponent<HorizontalLayoutGroup>();row.spacing=settings.buttonSpacing;row.childControlHeight=row.childControlWidth=true;row.childForceExpandHeight=row.childForceExpandWidth=true;
            var bar=root.gameObject.AddComponent<FloodPowerUpBar3D>();bar.buttons=new Button[3];bar.backgrounds=new Image[3];bar.icons=new Image[3];bar.fallbackIcons=new FloodPowerUpIcon3D[3];bar.names=new TMP_Text[3];bar.quantities=new TMP_Text[3];bar.outlines=new Outline[3];
            string[] labels={"COLOR SWAP","SMART HINT","COLOR BLAST"};
            for(int i=0;i<3;i++)
            {
                var rect=Rect(labels[i],root,settings.buttonSize);var element=rect.gameObject.AddComponent<LayoutElement>();element.flexibleWidth=1;
                var image=rect.gameObject.AddComponent<Image>();image.sprite=settings.availableButtonSprite;image.type=Image.Type.Sliced;
                var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;rect.gameObject.AddComponent<FloodAudioUIOverride>().silent=true;
                var outline=rect.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(3,-3);outline.effectColor=settings.selectedGlow;outline.enabled=false;
                var icon=Rect("Icon",rect,new Vector2(44,44));icon.anchorMin=icon.anchorMax=new Vector2(.5f,.65f);icon.anchoredPosition=Vector2.zero;
                var sprite=icon.gameObject.AddComponent<Image>();sprite.preserveAspect=true;sprite.raycastTarget=false;
                var fallback=Rect("DefaultIcon",rect,new Vector2(44,44));fallback.anchorMin=fallback.anchorMax=icon.anchorMin;fallback.anchoredPosition=Vector2.zero;
                var graphic=fallback.gameObject.AddComponent<FloodPowerUpIcon3D>();graphic.kind=(FloodPowerUpKind)i;graphic.color=new Color(.7f,.96f,1);graphic.raycastTarget=false;
                var title=Text("Name",rect,font,labels[i],21);title.rectTransform.anchorMin=new Vector2(0,.05f);title.rectTransform.anchorMax=new Vector2(1,.32f);title.rectTransform.sizeDelta=Vector2.zero;title.rectTransform.anchoredPosition=Vector2.zero;
                var quantity=Text("Quantity",rect,font,"3",25);quantity.rectTransform.anchorMin=quantity.rectTransform.anchorMax=new Vector2(.9f,.75f);quantity.rectTransform.sizeDelta=new Vector2(46,35);quantity.rectTransform.anchoredPosition=Vector2.zero;
                bar.buttons[i]=button;bar.backgrounds[i]=image;bar.icons[i]=sprite;bar.fallbackIcons[i]=graphic;bar.names[i]=title;bar.quantities[i]=quantity;bar.outlines[i]=outline;
            }
            try{return PrefabUtility.SaveAsPrefabAsset(root.gameObject,BarPath);}finally{UnityEngine.Object.DestroyImmediate(root.gameObject);}
        }
        private static void InstallHud(FloodPowerUpSettings3D settings)
        {
            string path="Assets/FloodFill/Prefabs/UI/GameplayHUD.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(root.GetComponentInChildren<FloodPowerUpBar3D>(true)!=null)return;
                var palette=root.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="ColorDock");
                var instruction=root.GetComponentsInChildren<TMP_Text>(true).First(t=>t.text.Contains("Choose a color"));
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(BarPath)??CreateBar(settings,instruction.font);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,palette.parent);var rect=(RectTransform)instance.transform;
                rect.anchorMin=palette.anchorMin;rect.anchorMax=palette.anchorMax;rect.pivot=new Vector2(.5f,.5f);
                float top=palette.anchoredPosition.y+palette.rect.height*(1-palette.pivot.y);
                rect.sizeDelta=new Vector2(palette.rect.width,settings.buttonSize.y);rect.anchoredPosition=new Vector2(palette.anchoredPosition.x,top+18+settings.buttonSize.y*.5f);
                var textRect=instruction.rectTransform;textRect.anchoredPosition=new Vector2(20,rect.anchoredPosition.y+rect.rect.height*.5f+50);textRect.sizeDelta=new Vector2(810,90);instruction.enableAutoSizing=true;instruction.fontSizeMin=19;instruction.fontSizeMax=26;
                var touch=root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="TouchHint");
                if(touch!=null){touch.anchorMin=touch.anchorMax=new Vector2(0,.5f);touch.anchoredPosition=new Vector2(24,0);touch.sizeDelta=new Vector2(40,40);instruction.margin=new Vector4(60,0,0,0);}
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void InstallBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use an isolated /tmp project.");
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");Install();Install();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }
}
