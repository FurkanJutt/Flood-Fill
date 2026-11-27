using System;
using System.IO;
using System.Reflection;
using DG.Tweening;
using FloodFill.ThreeD;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
namespace FloodFill.EditorTools
{
    public static class FloodButtonAnimationVerification3D
    {
        private static int stage,frame,checks,aClicks,bClicks;
        private static double deadline;
        private static Button a,b;
        private static Canvas canvas;
        private static Touchscreen touch;
        private static Mouse mouse;
        private static void Check(bool valid,string text){checks++;if(!valid)throw new Exception(text);}
        private static void Process()
        {typeof(InputSystem).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(InputUpdateType)},null).Invoke(null,new object[]{InputUpdateType.Dynamic});EventSystem.current.currentInputModule.Process();}
        private static Vector2 Position(Button button)=>RectTransformUtility.WorldToScreenPoint(null,button.transform.position);
        private static void Tap(Button button,int id)
        {
            var position=Position(button);
            if(touch!=null)
            {
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Began,position=position,pressure=1});Process();
                InputSystem.QueueStateEvent(touch,new TouchState{touchId=id,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=position});Process();
            }
            else
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{position=position}.WithButton(MouseButton.Left));Process();
                InputSystem.QueueStateEvent(mouse,new MouseState{position=position});Process();
            }
        }
        private static Button Create(string label,float x)
        {
            var root=new GameObject(label,typeof(RectTransform),typeof(Image),typeof(Button));root.transform.SetParent(canvas.transform,false);
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(150,80);rect.anchoredPosition=new Vector2(x,0);
            var text=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(root.transform,false);
            var textRect=text.GetComponent<RectTransform>();textRect.sizeDelta=new Vector2(140,60);
            var graphic=text.GetComponent<TextMeshProUGUI>();graphic.text=label;graphic.alignment=TextAlignmentOptions.Center;graphic.raycastTarget=false;
            var button=root.GetComponent<Button>();button.onClick.AddListener(()=>{if(button==a)aClicks++;else bClicks++;FloodTouchInput3D.PressFeedback(root.transform,.08f,2);});return button;
        }
        public static void BeginBatch()
        {
            if(!Application.isBatchMode||!Application.dataPath.Contains("/tmp/"))throw new Exception("Use isolated copy.");
            PlayerSettings.companyName=PlayerSettings.productName="FloodButtonAnimationVerification";
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene("Assets/FloodFill/Scenes/FloodFill3DDemo.unity");deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying||EventSystem.current==null)return;if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Animation input verification timeout");
                if(stage==0)
                {
                    Application.runInBackground=true;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;EventSystem.current.SendMessage("OnApplicationFocus",true);
                    canvas=new GameObject("Independent feedback fixture",typeof(Canvas),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2000;
                    a=Create("A",-90);b=Create("B",90);touch=InputSystem.AddDevice<Touchscreen>();frame=Time.frameCount;stage=1;return;
                }
                if(Time.frameCount<=frame+1)return;
                if(stage==1)
                {
                    Canvas.ForceUpdateCanvases();Tap(a,1);Check(aClicks==1&&a.GetComponent<FloodButtonFeedback3D>().IsAnimating,"First touch starts its visual animation");
                    Tap(b,2);Check(bClicks==1&&a.GetComponent<FloodButtonFeedback3D>().IsAnimating,"Second button registers while the first animation is still running");
                    Check(a.transform.localScale==Vector3.one&&b.transform.localScale==Vector3.one&&a.interactable&&b.interactable,"Both touch targets stay fixed and interactable");
                    Tap(a,3);Check(aClicks==2&&b.GetComponent<FloodButtonFeedback3D>().IsAnimating,"Retriggering one button does not wait for or stop another button");
                    InputSystem.RemoveDevice(touch);touch=null;mouse=InputSystem.AddDevice<Mouse>();frame=Time.frameCount;stage=2;return;
                }
                if(stage==2)
                {
                    Tap(a,0);Tap(b,0);Check(aClicks==3&&bClicks==2&&a.GetComponent<FloodButtonFeedback3D>().IsAnimating,"Mouse clicks also remain independent during animations");
                    FloodTouchInput3D.ShowFeedback(canvas.transform);Tap(b,0);
                    Check(bClicks==3&&DOTween.IsTweening(canvas.GetComponent<CanvasGroup>())&&canvas.transform.localScale==Vector3.one,"Screen fade does not block a button or move its hit area");
                    a.gameObject.SetActive(false);Check(!a.GetComponent<FloodButtonFeedback3D>().IsAnimating&&a.transform.GetChild(0).localScale==Vector3.one,"Hiding a button cleans up only its own feedback");
                    File.WriteAllText("/tmp/FloodButtonAnimationVerification.txt",checks+" checks passed: touchscreen and mouse dispatch to a second button during a two-second first-button animation, independent retriggering, stationary hit areas, interactive screen fade, and feedback cleanup. Isolated test identity; device timing unverified.\n");EditorApplication.Exit(0);
                }
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("/tmp/FloodButtonAnimationVerification.txt",e.ToString());EditorApplication.Exit(1);}
        }
    }
}
