using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Goals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodGoalDiamondsVerification3D
    {
        private static int stage,checks;
        private static double ready,deadline;
        private static FloodFillMobileUI3D ui;
        private static FloodLevelSelection3D levels;
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        private static void Award(int level,int count)
        {
            var goals=Enumerable.Range(0,3).Select(i=>new SecondaryGoalDefinition{id="diamond_test_"+i}).ToArray();
            FloodLevelProgression3D.RecordSecondaryGoals(0,0,level,goals,Enumerable.Range(0,3).Select(i=>i<count).ToArray(),10);
        }
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            PlayerSettings.companyName="FloodGoalDiamondsVerification";PlayerSettings.productName="FloodGoalDiamondsVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            FloodGoalDiamondsSetup3D.Install();FloodGoalDiamondsSetup3D.Install();
            for(int level=1;level<=5;level++){FloodLevelProgression3D.Record(0,0,level,10,level==5?1:3);Award(level,level==5?3:level-1);}
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");deadline=EditorApplication.timeSinceStartup+90;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Diamond verification timeout");
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();if(ui==null||!ui.IsBuilt)return;
                levels=ui.GetComponentInChildren<FloodLevelSelection3D>(true);
                if(stage==0){ui.OpenLevelSelection();levels.Open(0,0);ready=EditorApplication.timeSinceStartup+.4;stage=1;return;}
                if(EditorApplication.timeSinceStartup<ready)return;
                var tiles=(FloodLevelButton3D[])typeof(FloodLevelSelection3D).GetField("tiles",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(levels);
                if(stage==1)
                {
                    for(int t=0;t<7;t++)
                    {
                        var tile=tiles[t];int filled=t<4?t:t==4?3:0;
                        Check(tile.goalDiamonds.Length==3,"Three authored goal slots");
                        Check(tile.goalDiamonds.Count(d=>d.gameObject.activeInHierarchy&&d.sprite==tile.filledGoalDiamond)==filled,"Filled diamonds match saved goals");
                        Check(tile.stars.Count(s=>s.sprite==tile.goldStar)==(t<4?3:t==4?1:0),"Completion stars independent of secondary goals");
                        Check(tile.stars.All(s=>s.color==Color.white),"Red-star tint removed");
                        for(int i=0;i<3;i++)
                        {
                            var d=tile.goalDiamonds[i];var star=tile.stars[i];
                            Check(d.sprite==(i<filled?tile.filledGoalDiamond:tile.unfilledGoalDiamond),"Correct diamond sprite");
                            Check(d.rectTransform.anchoredPosition.y<star.rectTransform.anchoredPosition.y,"Diamonds below stars");
                            Check(!d.raycastTarget&&Mathf.Abs(d.color.a-(i<filled?1f:165f/255f))<.0001f,"Filled/empty opacity and non-blocking input");
                            Check(tile.transform.GetComponentsInChildren<Image>(true).Count(im=>im.name=="GoalDiamond"+(i+1))==1,"Idempotent installer");
                            Check(star.rectTransform.sizeDelta==Vector2.one*(i<(t<4?3:t==4?1:0)?41.954f:30f),"Existing glowing/gray star sizes retained");
                        }
                    }
                    Check(tiles[5].goalDiamonds.All(d=>d.gameObject.activeInHierarchy&&d.sprite==tiles[5].unfilledGoalDiamond),"Unlocked unplayed level has empty diamonds");
                    Check(tiles[6].goalDiamonds.All(d=>!d.gameObject.activeInHierarchy)&&tiles[6].lockIcon.gameObject.activeSelf,"Locked tile keeps its lock without diamonds");
                    levels.Open(1,0);Check(tiles.All(t=>t.goalDiamonds.All(d=>!d.gameObject.activeInHierarchy||d.sprite==t.unfilledGoalDiamond)),"Category reuse clears old goal rewards");
                    levels.Open(0,0);Check(tiles[3].goalDiamonds.All(d=>d.sprite==tiles[3].filledGoalDiamond),"Saved goals restore on category return");
                    for(int level=6;level<=100;level++)FloodLevelProgression3D.Record(0,0,level,10,level%7==0?2:3);
                    for(int level=61;level<=90;level++)Award(level,(level-61)%4);
                    levels.SetPage(2);ready=EditorApplication.timeSinceStartup+.4;stage=2;return;
                }
                Capture();
                File.WriteAllText("/tmp/FloodGoalDiamondsVerification.txt",checks+" checks passed: independent completion stars and goal diamonds, 0–3 rewards, empty and locked states, exact sprites, sizes, non-blocking input, repeated installation and category reuse.\n");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodGoalDiamondsVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void Capture()
        {
            var canvas=ui.GetComponent<Canvas>();var originalMode=canvas.renderMode;var originalCamera=canvas.worldCamera;
            var go=new GameObject("Diamond preview camera",typeof(Camera));var camera=go.GetComponent<Camera>();camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.005f,.018f,.045f);
            var rt=new RenderTexture(864,1800,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Canvas.ForceUpdateCanvases();foreach(var fit in ui.EditorScreens.First(s=>s.activeSelf).GetComponentsInChildren<FloodFillUIFit3D>(true))fit.SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();camera.Render();
            RenderTexture.active=rt;var texture=new Texture2D(864,1800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,864,1800),0,0);texture.Apply();File.WriteAllBytes("/tmp/FloodGoalDiamonds.png",texture.EncodeToPNG());
            RenderTexture.active=null;canvas.renderMode=originalMode;canvas.worldCamera=originalCamera;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(go);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
