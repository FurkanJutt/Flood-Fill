using FloodFill.ThreeD;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodGoalDiamondsSetup3D
    {
        [MenuItem("Tools/Flood Fill/UI/Install Goal Diamonds")]
        public static void Install()
        {
            const string path="Assets/FloodFill/Prefabs/UI/LevelButton.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try { Configure(root.GetComponent<FloodLevelButton3D>());PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void Configure(FloodLevelButton3D tile)
        {
            tile.filledGoalDiamond=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Spritesheets/diamond_filled_512.png");
            tile.unfilledGoalDiamond=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Spritesheets/diamond_unfilled_512.png");
            if(tile.filledGoalDiamond==null||tile.unfilledGoalDiamond==null)throw new System.InvalidOperationException("Goal diamond sprites are missing.");
            bool firstInstall=tile.transform.Find("GoalDiamond1")==null;
            if(firstInstall)
            {
                var numberPosition=tile.number.rectTransform.anchoredPosition;numberPosition.y=31;tile.number.rectTransform.anchoredPosition=numberPosition;
                foreach(var star in tile.stars){var p=star.rectTransform.anchoredPosition;p.y=-10;star.rectTransform.anchoredPosition=p;}
            }
            tile.goalDiamonds=new Image[3];
            for(int i=0;i<3;i++)
            {
                var child=tile.transform.Find("GoalDiamond"+(i+1));Image image;
                if(child!=null)image=child.GetComponent<Image>();
                else
                {
                    var go=new GameObject("GoalDiamond"+(i+1),typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.layer=tile.gameObject.layer;
                    var rect=go.GetComponent<RectTransform>();rect.SetParent(tile.transform,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2((i-1)*26,-43);rect.sizeDelta=Vector2.one*24;
                    image=go.GetComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;
                }
                image.sprite=tile.unfilledGoalDiamond;image.color=new Color32(255,255,255,165);tile.goalDiamonds[i]=image;
            }
        }
    }
}
