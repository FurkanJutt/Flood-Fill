using System.Linq;
using FloodFill.ThreeD.Goals;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodSecondaryGoalsSetup3D
    {
        [MenuItem("Tools/Flood Fill/UI/Install Secondary Goals")]
        public static void Install()
        {
            const string path="Assets/FloodFill/Prefabs/UI/GameplayHUD.prefab";
            var hud=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var existing=hud.GetComponentInChildren<FloodSecondaryGoalsUI3D>(true);
                if(existing!=null)
                {
                    InstallRowDiamonds(existing);Style(existing);PrefabUtility.SaveAsPrefabAsset(hud,path);return;
                }
                var track=hud.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="ProgressTrack");
                var sample=hud.GetComponentInChildren<TMP_Text>(true);
                var buttonImage=hud.GetComponentsInChildren<Button>(true).First().targetGraphic as Image;
                Sprite rounded=buttonImage!=null?buttonImage.sprite:null;
                var root=Rect("GoalsUI",track.parent,Vector2.zero,Vector2.zero);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
                var ui=root.gameObject.AddComponent<FloodSecondaryGoalsUI3D>();
                var dismiss=Rect("DismissTap",root,Vector2.zero,Vector2.zero);dismiss.anchorMin=Vector2.zero;dismiss.anchorMax=Vector2.one;dismiss.offsetMin=dismiss.offsetMax=Vector2.zero;
                dismiss.gameObject.AddComponent<Image>().color=Color.clear;dismiss.gameObject.AddComponent<FloodGoalDismissRaycast3D>();
                var trigger=Rect("GoalsTrigger",root,new Vector2(-370,-363),new Vector2(250,78));
                var image=trigger.gameObject.AddComponent<Image>();image.sprite=rounded;image.type=Image.Type.Sliced;image.color=new Color(.10f,.22f,.35f,.98f);
                var outline=trigger.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.1f,.8f,1,.8f);outline.effectDistance=new Vector2(1,-1);
                ui.trigger=trigger.gameObject.AddComponent<Button>();var colors=ui.trigger.colors;colors.pressedColor=new Color(.45f,.75f,1);ui.trigger.colors=colors;
                Label("ChecklistIcon",trigger,"≡",sample,30,new Vector2(-98,0),new Vector2(32,60));
                ui.triggerLabel=Label("Label",trigger,"GOALS 0/3",sample,28,new Vector2(18,0),new Vector2(200,60));
                ui.panel=Rect("FloatingGoalsPanel",root,new Vector2(0,-415),new Vector2(1000,230));ui.panel.pivot=new Vector2(.5f,1);
                var background=ui.panel.gameObject.AddComponent<Image>();background.sprite=rounded;background.type=Image.Type.Sliced;background.color=new Color(.035f,.07f,.135f,.98f);
                var border=ui.panel.gameObject.AddComponent<Outline>();border.effectColor=new Color(.1f,.75f,1,.75f);border.effectDistance=new Vector2(1,-1);
                ui.header=Label("Header",ui.panel,"OPTIONAL GOALS",sample,24,new Vector2(0,-25),new Vector2(950,40));ui.header.rectTransform.anchorMin=ui.header.rectTransform.anchorMax=new Vector2(.5f,1);
                var container=Rect("GoalsContainer",ui.panel,new Vector2(0,-58),new Vector2(950,165));container.pivot=new Vector2(.5f,1);
                ui.rows=new TMP_Text[3];
                for(int i=0;i<3;i++) {ui.rows[i]=Label("GoalRow"+(i+1),container,"• Goal",sample,27,new Vector2(0,-27-i*52),new Vector2(950,52));ui.rows[i].rectTransform.anchorMin=ui.rows[i].rectTransform.anchorMax=new Vector2(.5f,1);ui.rows[i].alignment=TextAlignmentOptions.MidlineLeft;ui.rows[i].enableAutoSizing=true;ui.rows[i].fontSizeMin=22;ui.rows[i].fontSizeMax=27;}
                InstallRowDiamonds(ui);
                Style(ui);
                ui.panel.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(hud,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(hud);}
            if(Resources.Load<FloodSecondaryGoalSettings3D>("FloodSecondaryGoals")==null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<FloodSecondaryGoalSettings3D>(),"Assets/FloodFill/Resources/FloodSecondaryGoals.asset");
            AssetDatabase.SaveAssets();
        }
        private static void InstallRowDiamonds(FloodSecondaryGoalsUI3D ui)
        {
            ui.filledDiamond=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Spritesheets/diamond_filled_512.png");
            ui.unfilledDiamond=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Spritesheets/diamond_unfilled_512.png");
            if(ui.filledDiamond==null||ui.unfilledDiamond==null)throw new System.InvalidOperationException("Goal diamond sprites are missing.");
            ui.rowDiamonds=new Image[ui.rows.Length];
            for(int i=0;i<ui.rows.Length;i++)
            {
                var row=ui.rows[i];var child=row.transform.Find("GoalDiamond");Image diamond;
                if(child!=null)diamond=child.GetComponent<Image>();
                else
                {
                    var go=new GameObject("GoalDiamond",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=row.gameObject.layer;
                    var rect=go.GetComponent<RectTransform>();rect.SetParent(row.transform,false);rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.anchoredPosition=new Vector2(14,0);rect.sizeDelta=Vector2.one*24;
                    diamond=go.GetComponent<Image>();diamond.raycastTarget=false;diamond.preserveAspect=true;
                    var margin=row.margin;margin.x=40;row.margin=margin;
                }
                diamond.sprite=ui.unfilledDiamond;diamond.color=new Color32(255,255,255,165);ui.rowDiamonds[i]=diamond;
                if(row.text=="• Goal")row.text="Goal";
            }
        }
        private static void Style(FloodSecondaryGoalsUI3D ui)
        {
            var trigger=ui.trigger.transform as RectTransform;
            if(FloodUIBackgroundSprites3D.IsStyled(trigger.GetComponent<Image>()))return;
            trigger.sizeDelta=new Vector2(250,120);trigger.anchoredPosition=new Vector2(-370,-376);
            ui.panel.anchoredPosition=new Vector2(0,-440);
            foreach(var rect in new[]{trigger,ui.panel})
            {
                foreach(var outline in rect.GetComponents<Outline>())UnityEngine.Object.DestroyImmediate(outline);
                bool isTrigger=rect==trigger;
                var image=FloodUIBackgroundSprites3D.Apply(rect,new Color(.045f,.075f,.14f,.98f),new Color(.12f,.65f,.95f,.85f),isTrigger?18:24,2,isTrigger?16:0);
                if(isTrigger)ui.trigger.targetGraphic=image;
            }
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.anchoredPosition=position;r.sizeDelta=size;return r;}
        private static TMP_Text Label(string name,Transform parent,string value,TMP_Text sample,float size,Vector2 position,Vector2 dimensions)
        {var r=Rect(name,parent,position,dimensions);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=sample.font;t.fontSize=size;t.color=Color.white;t.text=value;t.raycastTarget=false;t.alignment=TextAlignmentOptions.Center;return t;}
    }
}
