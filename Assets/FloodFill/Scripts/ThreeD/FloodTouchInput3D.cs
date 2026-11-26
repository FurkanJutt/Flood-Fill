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
            if(!UsesTouch)target.DOPunchScale(Vector3.one*amount,duration,6).SetUpdate(true);
        }
        public static void ShowFeedback(Transform target)
        {
            target.DOKill();target.localScale=Vector3.one;
            if(!UsesTouch){target.localScale=Vector3.one*.96f;target.DOScale(1,.22f).SetEase(Ease.OutBack).SetUpdate(true);}
        }
    }
}
