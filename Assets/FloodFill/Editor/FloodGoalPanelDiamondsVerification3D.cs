using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Goals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace FloodFill.EditorTools
{
    public static class FloodGoalPanelDiamondsVerification3D
    {
        private static int stage,checks;
        private static double deadline;
        private static FloodFillGameManager3D game;
        private static FloodFillMobileUI3D ui;
        private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated /tmp project.");
            PlayerSettings.companyName="FloodGoalPanelDiamondsVerification";PlayerSettings.productName="FloodGoalPanelDiamondsVerification";
            PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            FloodSecondaryGoalsSetup3D.Install();FloodSecondaryGoalsSetup3D.Install();
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");deadline=EditorApplication.timeSinceStartup+90;
            EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Goal panel diamond timeout");
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();if(ui==null||!ui.IsBuilt)return;
                if(stage==0){ui.StartSelectedLevel(0,0,1);stage=1;return;}
                if(!string.IsNullOrEmpty(game.LoadingError))throw new Exception(game.LoadingError);
                if(!game.CanInteract)return;
                var view=UnityEngine.Object.FindAnyObjectByType<FloodSecondaryGoalsUI3D>();
                var goals=new[]{new SecondaryGoalDefinition{id="panel_big",type=SecondaryGoalType.BigCapture,target=9},new SecondaryGoalDefinition{id="panel_quick",type=SecondaryGoalType.QuickStart,target=30,deadline=1,count=25},new SecondaryGoalDefinition{id="panel_combo",type=SecondaryGoalType.ComboCapture,target=5,count=3}};
                var tracker=new FloodSecondaryGoalTracker3D("panel_test",goals,1,6);
                typeof(FloodFillGameManager3D).GetField("<SecondaryGoals>k__BackingField",Flags).SetValue(game,tracker);view.Refresh();view.Toggle();
                Check(view.IsOpen&&game.CanInteract,"Panel remains non-pausing");
                Check(view.rowDiamonds.Length==3&&view.filledDiamond!=null&&view.unfilledDiamond!=null,"Authored icon references");
                for(int i=0;i<3;i++)
                {
                    Check(view.rowDiamonds[i].sprite==view.unfilledDiamond,"Pending goal unfilled");
                    Check(!view.rowDiamonds[i].raycastTarget,"Goal icons do not intercept input");
                    Check(view.rows[i].transform.Find("GoalDiamond")!=null&&view.rows[i].transform.childCount==1,"Idempotent icon installation");
                    Check(view.rows[i].margin.x>=view.rowDiamonds[i].rectTransform.anchoredPosition.x+view.rowDiamonds[i].rectTransform.rect.width*.5f,"Icon has separate text space");
                }
                tracker.Move(1,9,10);
                Check(view.rowDiamonds[0].sprite==view.filledDiamond&&view.rowDiamonds[0].color==Color.white,"Reached goal filled");
                Check(view.rows[0].color==(Color)new Color32(128,241,249,255),"Reached text matches diamond cyan face");
                Check(view.rowDiamonds[1].sprite==view.unfilledDiamond,"Missed goal remains unfilled");
                Check(view.rows[1].color==new Color(.85f,.47f,.5f),"Missed text keeps original red");
                Check(view.rowDiamonds[2].sprite==view.unfilledDiamond&&view.rows[2].color==new Color(.85f,.91f,1),"In-progress goal keeps neutral text");
                Check(view.rows.All(r=>!r.text.Contains("DONE")&&!r.text.Contains("MISSED")&&!r.text.Contains("EARNED")),"No status words remain");
                Capture();
                FloodLevelProgression3D.RecordSecondaryGoals(0,0,1,goals,new[]{false,false,true},10);view.Refresh();
                Check(view.rowDiamonds[2].sprite==view.filledDiamond&&view.rows[2].color==view.completedGoalTextColor,"Permanent achievement fills diamond and uses cyan");
                for(int i=0;i<goals.Length;i++)goals[i].id+="_reset";
                tracker=new FloodSecondaryGoalTracker3D("panel_reset",goals,1,6);
                typeof(FloodFillGameManager3D).GetField("<SecondaryGoals>k__BackingField",Flags).SetValue(game,tracker);view.Refresh();
                Check(view.rowDiamonds.All(d=>d.sprite==view.unfilledDiamond)&&view.rows.All(r=>r.color==new Color(.85f,.91f,1)),"New attempt resets sprites and text colors");
                File.WriteAllText("/tmp/FloodGoalPanelDiamondsVerification.txt",checks+" checks passed: pending, completed, missed, in-progress and permanent states; no status words; cyan/red text; non-blocking icons; text spacing; installation and state reset.\n");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodGoalPanelDiamondsVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void Capture()
        {
            var canvas=ui.GetComponent<Canvas>();var mode=canvas.renderMode;var old=canvas.worldCamera;var camera=Camera.main;int mask=camera.cullingMask;
            var rt=new RenderTexture(432,900,24);camera.targetTexture=rt;camera.cullingMask|=1<<5;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            if(game.BoardManager.TryGetWorldBounds(out var bounds))UnityEngine.Object.FindAnyObjectByType<OrbitCameraController3D>().FrameBounds(bounds);
            Canvas.ForceUpdateCanvases();foreach(var fit in ui.EditorScreens.First(s=>s.activeSelf).GetComponentsInChildren<FloodFillUIFit3D>(true))fit.SendMessage("LateUpdate");Canvas.ForceUpdateCanvases();camera.Render();
            RenderTexture.active=rt;var texture=new Texture2D(432,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,432,900),0,0);texture.Apply();File.WriteAllBytes("/tmp/FloodGoalPanelDiamonds.png",texture.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;camera.cullingMask=mask;canvas.renderMode=mode;canvas.worldCamera=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
