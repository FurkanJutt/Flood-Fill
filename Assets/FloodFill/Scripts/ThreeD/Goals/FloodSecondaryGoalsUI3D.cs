using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace FloodFill.ThreeD.Goals
{
    [DefaultExecutionOrder(-10000)]
    public sealed class FloodSecondaryGoalsUI3D : MonoBehaviour
    {
        public Button trigger;
        public TMP_Text triggerLabel, header;
        public RectTransform panel;
        public TMP_Text[] rows;
        public static bool ConsumedPointerThisFrame => consumedFrame == Time.frameCount;
        private static int consumedFrame=-1;
        private FloodFillGameManager3D game;
        private FloodSecondaryGoalTracker3D tracker;
        private bool pointerActive;
        private Vector2 pointerStart;
        private float nextRefresh;
        public bool IsOpen => panel!=null&&panel.gameObject.activeSelf;
        private void OnEnable()
        {
            if(trigger==null)return;
            if(game==null)game=FindAnyObjectByType<FloodFillGameManager3D>();
            if(game!=null)game.UIChanged+=Refresh;
            trigger.onClick.AddListener(Toggle);Close();Refresh();
        }
        private void OnDisable()
        {
            if(game!=null)game.UIChanged-=Refresh;
            if(tracker!=null)tracker.Changed-=Refresh;
            tracker=null;if(trigger!=null)trigger.onClick.RemoveListener(Toggle);Close();
        }
        public void Toggle(){if(IsOpen)Close();else if(tracker!=null&&tracker.Goals.Length>0){panel.gameObject.SetActive(true);Refresh();}}
        public void Close(){if(panel!=null)panel.gameObject.SetActive(false);pointerActive=false;}
        public void Refresh()
        {
            var current=game!=null?game.SecondaryGoals:null;
            if(tracker!=current){if(tracker!=null)tracker.Changed-=Refresh;tracker=current;if(tracker!=null)tracker.Changed+=Refresh;Close();}
            bool available=tracker!=null&&tracker.Goals.Length>0;trigger.gameObject.SetActive(game!=null&&game.NumberedLevel>0);
            trigger.interactable=available;
            if(!available){triggerLabel.text="GOALS —";return;}
            int permanent=0;
            for(int i=0;i<tracker.Goals.Length;i++)if(Earned(i))permanent++;
            triggerLabel.text=$"GOALS {permanent}/{tracker.Goals.Length}";
            header.text="OPTIONAL GOALS · Stars saved on a win";
            for(int i=0;i<rows.Length;i++)
            {
                rows[i].gameObject.SetActive(i<tracker.Goals.Length);if(i>=tracker.Goals.Length)continue;
                var g=tracker.Goals[i];var p=tracker.Attempt.progress[i];var state=tracker.State(i,Earned(i),game.ElapsedSeconds);
                string mark=state==SecondaryGoalState.PreviouslyEarned?"EARNED":state==SecondaryGoalState.CompletedThisAttempt?"DONE":state==SecondaryGoalState.FailedThisAttempt?"MISSED":"•";
                string progress="";
                if(g.type==SecondaryGoalType.BigCapture)progress=$"  {p.value}/{g.target}";
                else if(g.type==SecondaryGoalType.ComboCapture)progress=$"  {p.value}/{g.count}";
                else if(g.type==SecondaryGoalType.LimitedColorUsage)progress=$"  {p.value}/{g.target}";
                else if(g.type==SecondaryGoalType.QuickStart||g.type==SecondaryGoalType.FastExpansion)progress=$"  {p.value}/{g.target} cubes";
                else if(g.type==SecondaryGoalType.SpeedRun)progress="  "+game.ElapsedTimeText;
                rows[i].text=$"<b>{mark}</b>  {g.Description(game.GoalSettings.colorNames,game.GoalSettings.descriptionFormats)}{progress}";
                rows[i].color=state==SecondaryGoalState.PreviouslyEarned?new Color(.3f,.85f,1):state==SecondaryGoalState.CompletedThisAttempt?new Color(.4f,1,.65f):state==SecondaryGoalState.FailedThisAttempt?new Color(.85f,.47f,.5f):new Color(.85f,.91f,1);
            }
        }
        private bool Earned(int i)=>FloodLevelProgression3D.GoalEarned(game.NumberedMode,(int)game.SelectedDifficulty,game.NumberedLevel,tracker.Goals[i].id);
        private void Update()
        {
            if(!IsOpen)return;
            if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.2f;Refresh();}
            Vector2 position=default;bool down=false,up=false,held=false;
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null&&(Touchscreen.current.primaryTouch.press.isPressed||Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
            {var touch=Touchscreen.current.primaryTouch;position=touch.position.ReadValue();down=touch.press.wasPressedThisFrame;up=touch.press.wasReleasedThisFrame;held=touch.press.isPressed;}
            else if(Mouse.current!=null){position=Mouse.current.position.ReadValue();down=Mouse.current.leftButton.wasPressedThisFrame;up=Mouse.current.leftButton.wasReleasedThisFrame;held=Mouse.current.leftButton.isPressed;}
#else
            if(Input.touchCount>0){var touch=Input.GetTouch(0);position=touch.position;down=touch.phase==TouchPhase.Began;up=touch.phase==TouchPhase.Ended;held=!up;}
            else {position=Input.mousePosition;down=Input.GetMouseButtonDown(0);up=Input.GetMouseButtonUp(0);held=Input.GetMouseButton(0);}
#endif
            ProcessPointer(position,down,held,up);
        }
        // Outside drags stay available to the camera. Only the release of an outside tap is consumed.
        public void ProcessPointer(Vector2 position,bool down,bool held,bool up)
        {
            if(!IsOpen)return;
            Camera camera=GetComponentInParent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay?null:GetComponentInParent<Canvas>().worldCamera;
            if(down) {pointerActive=!RectTransformUtility.RectangleContainsScreenPoint(panel,position,camera)&&!RectTransformUtility.RectangleContainsScreenPoint(trigger.transform as RectTransform,position,camera);pointerStart=position;}
            if(!pointerActive)return;
            if(Vector2.Distance(position,pointerStart)>12){Close();return;}
            if(up){consumedFrame=Time.frameCount;Close();}
            else if(!held&&!down)pointerActive=false;
        }
    }
}
