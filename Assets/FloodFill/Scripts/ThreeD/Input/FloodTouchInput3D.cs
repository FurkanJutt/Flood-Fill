using DG.Tweening;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace FloodFill.ThreeD
{
    // Keep touch targets stationary while fingers are down. Input remains with uGUI.
    public static class FloodTouchInput3D
    {
        public static bool UsesTouch
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Application.isMobilePlatform||Touchscreen.current!=null;
#else
                return Application.isMobilePlatform;
#endif
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureMobileFrameRate()
        {
            if(Application.isMobilePlatform)Application.targetFrameRate=60;
        }
        public static void PressFeedback(Transform target,float amount=.045f,float duration=.18f)
        {
            target.DOKill();target.localScale=Vector3.one;
            var feedback=target.GetComponent<FloodButtonFeedback3D>();
            if(feedback==null)feedback=target.gameObject.AddComponent<FloodButtonFeedback3D>();
            feedback.Play(amount,duration);
        }
        public static void ShowFeedback(Transform target)
        {
            target.DOKill();target.localScale=Vector3.one;
            var fade=target.GetComponent<CanvasGroup>();if(fade==null)fade=target.gameObject.AddComponent<CanvasGroup>();
            fade.DOKill();fade.alpha=.85f;fade.DOFade(1,.12f).SetUpdate(true);
        }
    }
}
