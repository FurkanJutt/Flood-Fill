using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FloodFill.ThreeD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodMobileTouchVerification3D
    {
        private static FloodFillMobileUI3D ui;
        private static FloodFillGameManager3D game;
        private static Touchscreen touch;
        private static Button pending;
        private static RenderTexture portrait;
        private static int stage,step,phase,frame,clicks,expected,checks;
        private static double deadline;
        private static float ready;
        private static string checkpoint;
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool value,string text){checks++;if(!value)throw new Exception(text+" [stage "+stage+" step "+step+"]");}
        private static object Get(object owner,string field)=>owner.GetType().GetField(field,Private).GetValue(owner);
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated copy.");
            PlayerSettings.companyName=PlayerSettings.productName="FloodMobileTouchVerification";PlayerPrefs.DeleteAll();FloodFillSessionPersistence3D.Delete();
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static Button VisibleAction(FloodFillUIAction action)=>ui.GetComponentsInChildren<FloodFillUIAction3D>().First(a=>a.action==action&&a.GetComponent<Button>().interactable).GetComponent<Button>();
        private static Vector2 Position(Button button)
        {
            var canvas=button.GetComponentInParent<Canvas>();var rect=(RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        }
        private static void Event(int id,UnityEngine.InputSystem.TouchPhase touchPhase,Vector2 position)
        {InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=touchPhase,position=position,pressure=touchPhase==UnityEngine.InputSystem.TouchPhase.Ended?0:1});typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null).Invoke(null,new object[]{InputUpdateType.Dynamic});EventSystem.current.currentInputModule.Process();}
        private static Button NextButton()
        {
            var levels=ui.GetComponentInChildren<FloodLevelSelection3D>(true);
            switch(step)
            {
                case 0:return VisibleAction(FloodFillUIAction.Settings);
                case 1:return VisibleAction(FloodFillUIAction.SettingsBack);
                case 2:return VisibleAction(FloodFillUIAction.Play);
                case 3:return levels.modes[1];case 4:return levels.difficulties[2];case 5:return levels.next;case 6:return levels.previous;case 7:return levels.home;
                default:return step%2==0?VisibleAction(FloodFillUIAction.Settings):VisibleAction(FloodFillUIAction.SettingsBack);
            }
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying)return;if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Touch verification timeout");
                ui=UnityEngine.Object.FindAnyObjectByType<FloodFillMobileUI3D>();game=UnityEngine.Object.FindAnyObjectByType<FloodFillGameManager3D>();if(ui==null||!ui.IsBuilt)return;
                if(stage==0){Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;EventSystem.current.SendMessage("OnApplicationFocus",true);Screen.SetResolution(420,900,false);portrait=new RenderTexture(420,900,24);Camera.main.targetTexture=portrait;touch=InputSystem.AddDevice<Touchscreen>("Flood rapid touch fixture");var inputModule=EventSystem.current.GetComponent<InputSystemUIInputModule>();inputModule.enabled=false;inputModule.enabled=true;Check(FloodTouchInput3D.UsesTouch,"Touch device activates stable touch targets");ready=Time.unscaledTime+.3f;stage=1;return;}
                if(stage==1)
                {
                    if(Time.unscaledTime<ready)return;
                    if(phase==0)
                    {
                        Canvas.ForceUpdateCanvases();pending=NextButton();pending.onClick.AddListener(CountClick);expected=clicks+1;

                        Event(1,UnityEngine.InputSystem.TouchPhase.Began,Position(pending));frame=Time.frameCount;phase=1;return;
                    }
                    if(Time.frameCount<=frame+1)return;
                    if(phase==1){Event(1,UnityEngine.InputSystem.TouchPhase.Ended,Position(pending));frame=Time.frameCount;phase=2;return;}
                    Check(clicks==expected,"Real touchscreen press/release registers once: "+clicks+" expected "+expected);
                    Check(pending.transform.localScale==Vector3.one,"Button touch bounds stay fixed");pending.onClick.RemoveListener(CountClick);
                    if(step==2)Check(ui.GetComponentInChildren<FloodLevelSelection3D>().transform.localScale==Vector3.one,"New screen is immediately at its final hit-test geometry");
                    step++;phase=0;if(step<24)return;
                    ui.OpenLevelSelection();frame=Time.frameCount;stage=2;return;
                }
                if(stage==2)
                {
                    if(Time.frameCount<=frame+1)return;Canvas.ForceUpdateCanvases();var levels=ui.GetComponentInChildren<FloodLevelSelection3D>();
                    levels.modes[2].onClick.AddListener(CountClick);levels.difficulties[1].onClick.AddListener(CountClick);expected=clicks+2;
                    Event(11,UnityEngine.InputSystem.TouchPhase.Began,Position(levels.modes[2]));Event(12,UnityEngine.InputSystem.TouchPhase.Began,Position(levels.difficulties[1]));frame=Time.frameCount;stage=3;return;
                }
                if(stage==3)
                {
                    if(Time.frameCount<=frame+1)return;var levels=ui.GetComponentInChildren<FloodLevelSelection3D>();
                    Event(11,UnityEngine.InputSystem.TouchPhase.Ended,Position(levels.modes[2]));Event(12,UnityEngine.InputSystem.TouchPhase.Ended,Position(levels.difficulties[1]));frame=Time.frameCount;stage=4;return;
                }
                if(stage==4)
                {
                    if(Time.frameCount<=frame+1)return;var levels=ui.GetComponentInChildren<FloodLevelSelection3D>();
                    Check(clicks==expected&&levels.Mode==2&&levels.Difficulty==1,"Two different fingers/buttons register independently without shared input throttling");
                    ui.StartSelectedLevel(0,0,1);stage=5;return;
                }
                if(stage==5)
                {
                    if(!string.IsNullOrEmpty(game.LoadingError))throw new Exception(game.LoadingError);if(!game.CanInteract)return;
                    checkpoint=File.ReadAllText(FloodFillSessionPersistence3D.SavePath);
                    for(int i=0;i<60;i++)game.SelectColor(i%6);
                    Check(game.SelectedColorIndex==5,"Rapid palette selections update immediately");
                    Check(File.ReadAllText(FloodFillSessionPersistence3D.SavePath)==checkpoint&&(bool)Get(game,"selectionSavePending"),"Palette tap callbacks do no whole-board checkpoint writes");
                    ready=Time.unscaledTime+.25f;stage=6;return;
                }
                if(stage==6)
                {
                    if(Time.unscaledTime<ready)return;
                    Check(FloodFillSessionPersistence3D.TryLoad(out var saved)&&saved.selectedColorIndex==5,"Selection-only checkpoint flushes after the burst");
                    game.SelectColor(4);ui.ExecuteAuthoredAction(FloodFillUIAction.Home,0);ui.ExecuteAuthoredAction(FloodFillUIAction.ConfirmHome,0);
                    Check(FloodFillSessionPersistence3D.TryLoad(out saved)&&saved.selectedColorIndex==4,"Leaving gameplay immediately flushes the final selection");
                    Check(game.RefreshContinueAvailability(),"Valid unfinished game remains resumable");var cached=Get(game,"cachedContinueSession");
                    for(int i=0;i<20;i++)Check(game.RefreshContinueAvailability()&&ReferenceEquals(cached,Get(game,"availableSession")),"Unchanged Continue checkpoint reuses validated data");
                    var menuSaveStamp=File.GetLastWriteTimeUtc(FloodFillSessionPersistence3D.SavePath);ui.OpenLevelSelection();
                    Check(File.GetLastWriteTimeUtc(FloodFillSessionPersistence3D.SavePath)==menuSaveStamp,"Opening Levels from the menu does not rewrite the entire unfinished board");ui.LevelSelectionHome();
                    checkpoint=File.ReadAllText(FloodFillSessionPersistence3D.SavePath);File.WriteAllText(FloodFillSessionPersistence3D.SavePath,"{invalid");Check(!game.RefreshContinueAvailability(),"Changed or damaged checkpoint invalidates the Continue cache");
                    File.WriteAllText(FloodFillSessionPersistence3D.SavePath,checkpoint);Check(game.RefreshContinueAvailability(),"Repaired checkpoint is revalidated");
                    ui.ExecuteAuthoredAction(FloodFillUIAction.Continue,0);stage=7;return;
                }
                if(stage==7)
                {
                    if(!game.CanInteract)return;Check(game.SelectedColorIndex==4,"Continue restores the final selected color");
                    InputSystem.RemoveDevice(touch);
                    File.WriteAllText("/tmp/FloodMobileTouchVerification.txt",checks+" checks passed: actual InputSystem touchscreen events across menu/settings/levels, rapid sequential taps, simultaneous fingers, fixed hit-test geometry, immediate palette response, coalesced selection saves, navigation flush and Continue cache invalidation/restore. Desktop simulation; mobile device performance still requires retesting. Original saves untouched.\n");EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodMobileTouchVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
        private static void CountClick(){clicks++;}
    }
}
