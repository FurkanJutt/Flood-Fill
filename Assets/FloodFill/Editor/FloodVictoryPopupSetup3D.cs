using System.Linq;
using FloodFill.ThreeD;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodVictoryPopupSetup3D
    {
        private const string Folder="Assets/FloodFill/Prefabs/UI/";
        private static Sprite[] atlas;
        private static TMP_FontAsset font;
        private static readonly Color Muted=new Color(.66f,.77f,.91f);
        private static Sprite S(int index)=>atlas.Single(s=>s.name=="level_complete_sprite_sheet_"+index);
        [MenuItem("Tools/Flood Fill/UI/Install Victory Popup")]
        public static void Install()
        {
            atlas=AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Spritesheets/level_complete_sprite_sheet.png").OfType<Sprite>().ToArray();
            var canvas=PrefabUtility.LoadPrefabContents(Folder+"FloodFillCanvas.prefab");
            try
            {
                var ui=canvas.GetComponent<FloodFillMobileUI3D>();var data=new SerializedObject(ui);
                font=((TMP_Text)data.FindProperty("resultTitle").objectReferenceValue).font;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"VictoryPopup.prefab");
                if(prefab==null)prefab=Create();
                else UpgradeConfetti();
                var property=data.FindProperty("victoryPopup");
                if(property.objectReferenceValue==null)
                {
                    var safe=(Transform)data.FindProperty("safeRoot").objectReferenceValue;
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,safe);instance.SetActive(false);
                    property.objectReferenceValue=instance.GetComponent<FloodVictoryPopup3D>();data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(canvas,Folder+"FloodFillCanvas.prefab");
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(canvas);}
            AssetDatabase.SaveAssets();
        }
        private static void UpgradeConfetti()
        {
            var root=PrefabUtility.LoadPrefabContents(Folder+"VictoryPopup.prefab");
            try
            {
                var view=root.GetComponent<FloodVictoryPopup3D>();bool changed=false;
                if(view.heroGlow==null)
                {view.heroGlow=root.GetComponentsInChildren<Image>(true).Single(image=>image.name=="HeroGlow");changed=true;}
                if(view.rewardSparkles==null||view.rewardSparkles.Length==0)
                {
                    view.rewardSparkles=root.GetComponentsInChildren<Image>(true).Where(image=>image.name=="RewardSparkleLeft"||image.name=="RewardSparkleRight").ToArray();changed=true;
                }
                if(view.confetti.Length<60)
                {
                    var original=view.confetti;var expanded=original.ToList();
                    while(expanded.Count<60)
                    {
                        var template=original[expanded.Count%original.Length];
                        var copy=Object.Instantiate(template.gameObject,template.transform.parent);
                        copy.name="Confetti"+(expanded.Count+1);copy.SetActive(false);expanded.Add(copy.GetComponent<Image>());
                    }
                    view.confetti=expanded.ToArray();changed=true;
                }
                if(Mathf.Approximately(view.confettiDuration,2.5f)){view.confettiDuration=3.5f;changed=true;}
                if(changed)PrefabUtility.SaveAsPrefabAsset(root,Folder+"VictoryPopup.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static GameObject Create()
        {
            var root=Rect("VictoryPopup",null,0,0,0,0);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;
            var shade=root.gameObject.AddComponent<Image>();shade.color=new Color(0,.006f,.02f,.80f);shade.raycastTarget=true;
            var view=root.gameObject.AddComponent<FloodVictoryPopup3D>();
            var content=Rect("Content",root,0,0,1080,2000);content.gameObject.AddComponent<FloodFillUIFit3D>();
            var card=Rect("PopupCard",content,0,0,1000,1840);Panel(card,new Color(.025f,.043f,.083f,.99f),new Color(.15f,.24f,.39f));
            view.heroGlow=Glow("HeroGlow",card,0,825,300,300);view.heroGlow.preserveAspect=true;
            Img("Crown",card,S(16),0,825,145,105);
            view.leftLaurel=Img("LeftLaurel",card,S(14),-280,395,112,300).rectTransform;view.leftLaurel.pivot=new Vector2(.5f,0);
            view.rightLaurel=Img("RightLaurel",card,S(15),280,395,112,300).rectTransform;view.rightLaurel.pivot=new Vector2(.5f,0);
            var particles=Rect("SideConfetti",card,0,0,1000,1840);view.confetti=new Image[60];
            for(int i=0;i<view.confetti.Length;i++)
            {view.confetti[i]=Img("Confetti"+(i+1),particles,S(23+i%8),0,0,40+(i%3)*8,38+(i%4)*6);view.confetti[i].preserveAspect=true;view.confetti[i].gameObject.SetActive(false);}
            Img("Banner",card,S(13),0,365,940,180);
            Text("LevelComplete",card,"LEVEL COMPLETE!",60,0,362,820,100,new Color(1,.98f,.85f));
            view.level=Text("Level",card,"LEVEL 41",40,0,235,420,70,Muted);view.level.characterSpacing=4;
            Line("LevelLeftLine",card,-290,235,130,3,new Color(.30f,.43f,.64f));Line("LevelRightLine",card,290,235,130,3,new Color(.30f,.43f,.64f));
            var stats=Rect("Statistics",card,0,25,890,300);Panel(stats,new Color(.055f,.085f,.15f,.98f),new Color(.16f,.23f,.36f));
            Text("MovesHeader",stats,"MOVES",28,-280,95,270,50,Muted);Text("BestHeader",stats,"BEST",28,0,95,260,50,Muted);Text("ElapsedTimeHeader",stats,"TIME ELAPSED",28,280,95,270,50,Muted);
            view.moves=Text("Moves",stats,"24 / 35",50,-280,25,270,74,Color.white);
            Img("BestCrown",stats,S(16),-55,25,46,34);view.best=Text("Best",stats,"24",50,25,25,160,74,new Color(1,.83f,.18f));
            view.elapsedTime=Text("ElapsedTime",stats,"00:00",50,280,25,260,74,new Color(.34f,.90f,.60f));
            Line("StatsDivider1",stats,-140,63,3,126,new Color(.17f,.23f,.36f));Line("StatsDivider2",stats,140,63,3,126,new Color(.17f,.23f,.36f));Line("StatsRule",stats,0,-22,810,3,new Color(.17f,.23f,.36f));
            Glow("StarRays",stats,0,-90,800,140);Img("StarRail",stats,S(4),0,-90,805,110);
            view.goldStar=S(17);view.emptyStar=AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Spritesheets/levels_sprite_sheet.png").OfType<Sprite>().Single(s=>s.name=="levels_sprite_sheet_18");
            view.stars=new Image[3];for(int i=0;i<3;i++)view.stars[i]=Img("Star"+(i+1),stats,view.goldStar,(i-1)*145,-88,i==1?150:130,i==1?150:130);
            Text("RewardsHeader",card,"REWARDS",36,0,-185,500,65,Muted);
            var coin=Img("CoinRewardCard",card,S(5),-178,-340,300,212).rectTransform;
            Img("Coins",coin,S(19),0,32,140,88);view.coinReward=Text("CoinReward",coin,"+100",44,0,-58,230,65,new Color(1,.98f,.85f));
            var gem=Img("GemRewardCard",card,S(5),178,-340,300,212).rectTransform;
            Img("Gem",gem,S(20),0,32,122,110);view.gemReward=Text("GemReward",gem,"+5",44,0,-58,230,65,new Color(1,.98f,.85f));
            view.rewardSparkles=new[]{Img("RewardSparkleLeft",card,S(22),-355,-315,30,30),Img("RewardSparkleRight",card,S(22),355,-315,30,30)};
            var next=Button("NextLevel",card,S(0),0,-570,900,154,FloodFillUIAction.Result);
            view.primaryLabel=ButtonContent(next,"NEXT LEVEL",6,46);
            var replay=Button("Replay",card,S(1),0,-700,870,114,FloodFillUIAction.Restart);ButtonContent(replay,"REPLAY",7,42);
            var bottom=Rect("BottomActions",card,0,-825,870,110);var layout=bottom.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=28;layout.childAlignment=TextAnchor.MiddleCenter;layout.childControlWidth=layout.childControlHeight=false;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            var home=Button("Home",bottom,S(2),0,0,405,110,FloodFillUIAction.ReturnToMenu);ButtonContent(home,"HOME",8,36);
            var levels=Button("Levels",bottom,S(2),0,0,405,110,FloodFillUIAction.Levels);ButtonContent(levels,"LEVELS",99,36);view.levelsButton=levels.gameObject;
            root.gameObject.SetActive(false);
            try{return PrefabUtility.SaveAsPrefabAsset(root.gameObject,Folder+"VictoryPopup.prefab");}
            finally{Object.DestroyImmediate(root.gameObject);}
        }
        private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
        private static Image Img(string name,Transform parent,Sprite sprite,float x,float y,float w,float h)
        {var r=Rect(name,parent,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;return image;}
        private static void Line(string name,Transform parent,float x,float y,float w,float h,Color color){var image=Img(name,parent,null,x,y,w,h);image.color=color;}
        private static Image Glow(string name,Transform parent,float x,float y,float w,float h)
        {return Img(name,parent,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Spritesheets/cube_glow_512.png"),x,y,w,h);}
        private static void Panel(RectTransform rect,Color color,Color border){FloodUIBackgroundSprites3D.Apply(rect,color,border,38,3,0,false);}
        private static TMP_Text Text(string name,Transform parent,string value,float size,float x,float y,float w,float h,Color color)
        {var r=Rect(name,parent,x,y,w,h);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontStyle=FontStyles.Bold;text.fontSize=size;text.color=color;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;}
        private static RectTransform Button(string name,Transform parent,Sprite sprite,float x,float y,float w,float h,FloodFillUIAction action)
        {var image=Img(name,parent,sprite,x,y,w,h);image.raycastTarget=true;var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;var binding=image.gameObject.AddComponent<FloodFillUIAction3D>();binding.action=action;binding.audioLabel=name;UnityEventTools.AddPersistentListener(button.onClick,binding.Invoke);return image.rectTransform;}
        private static TMP_Text ButtonContent(RectTransform button,string label,int icon,float size)
        {
            var content=Rect("ButtonContent",button,0,0,button.rect.width,button.rect.height);
            var layout=content.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=24;layout.childAlignment=TextAnchor.MiddleCenter;layout.childControlWidth=layout.childControlHeight=false;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            var symbol=Rect("Icon",content,0,0,icon==6?50:44,50);
            if(icon==99)for(int i=0;i<4;i++)Img("Square"+i,symbol,S(9),(i%2==0?-1:1)*12,(i<2?1:-1)*12,20,20);
            else{var image=symbol.gameObject.AddComponent<Image>();image.sprite=S(icon);image.preserveAspect=true;image.raycastTarget=false;}
            var text=Text("Label",content,label,size,0,0,600,button.rect.height,Color.white);text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,text.GetPreferredValues(label).x+8);return text;
        }
    }
}
