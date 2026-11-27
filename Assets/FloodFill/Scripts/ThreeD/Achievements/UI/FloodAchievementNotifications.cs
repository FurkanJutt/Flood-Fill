using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FloodFill.ThreeD.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD.Achievements
{
    public sealed class FloodAchievementNotifications:MonoBehaviour
    {
        public CanvasGroup panel;
        public TMP_Text title,body;
        public Image icon;
        public Image[] stars=new Image[3];
        public GameObject menuIndicator;
        private readonly Queue<AchievementUnlock> queue=new Queue<AchievementUnlock>();
        private FloodAchievementService service;
        private Coroutine showing;
        private bool bound;
        private FloodFillGameManager3D game;
        public void ConfigureOverlay()
        {
            var parentCanvas=transform.parent!=null?transform.parent.GetComponentInParent<Canvas>():null;
            var overlay=GetComponent<Canvas>();
            if(overlay==null)overlay=gameObject.AddComponent<Canvas>();
            overlay.overrideSorting=true;
            if(parentCanvas!=null)overlay.sortingLayerID=parentCanvas.rootCanvas.sortingLayerID;
            // TMP dropdowns use 30000; notifications must also stay above their lists.
            overlay.sortingOrder=32767;
        }
        private void Awake()=>ConfigureOverlay();
        public void Initialize()
        {
            if(bound)return;service=FloodAchievementService.Instance;if(service==null)return;bound=true;game=FindAnyObjectByType<FloodFillGameManager3D>();
            service.Unlocked+=Enqueue;service.Changed+=OnChanged;panel.gameObject.SetActive(false);RefreshIndicator();
        }
        private void Start()=>Initialize();
        private void OnDestroy(){if(service!=null){service.Unlocked-=Enqueue;service.Changed-=OnChanged;}}
        private void OnChanged(string id)=>RefreshIndicator();
        private void RefreshIndicator(){if(menuIndicator!=null)menuIndicator.SetActive(service!=null&&service.HasAttention());}
        private void Enqueue(AchievementUnlock unlock)
        {
            RefreshIndicator();if(!service.settings.enableNotifications)return;
            // Bounded queue: overflow remains unseen and discoverable through the menu indicator.
            if(queue.Count<service.settings.maximumQueuedNotifications)queue.Enqueue(unlock);
        }
        private void Update()
        {
            if(!bound)Initialize();if(showing!=null||queue.Count==0||service==null||!service.Ready)return;
            if(game==null)game=FindAnyObjectByType<FloodFillGameManager3D>();if(game!=null&&(game.State==FloodFillGameManager3D.GameState.Won||game.State==FloodFillGameManager3D.GameState.Lost)&&game.BoardManager.PowerUpBoardRoot.gameObject.activeInHierarchy)return;
            showing=StartCoroutine(Display(queue.Dequeue()));
        }
        private IEnumerator Display(AchievementUnlock unlock)
        {
            var d=unlock.definition;title.text=d.tiers.Count==1?"ACHIEVEMENT COMPLETED":"ACHIEVEMENT UNLOCKED";
            body.text=d.name+"  ·  TIER "+(d.tiers.IndexOf(unlock.tier)+1);icon.sprite=service.Icon(d);
            int completed=0;foreach(var t in d.tiers)if(service.Engine.Progress(d,t).completed)completed++;
            int filled=AchievementValidation.FilledStars(d.tiers.Count,completed);
            for(int i=0;i<3;i++)stars[i].sprite=(d.tiers.Count==3?service.Engine.Progress(d,d.tiers[i]).completed:i<filled)?service.settings.filledStar:service.settings.emptyStar;
            panel.gameObject.SetActive(true);panel.alpha=0;panel.blocksRaycasts=false;panel.interactable=false;panel.transform.SetAsLastSibling();
            panel.DOFade(1,service.settings.notificationAnimationDuration).SetUpdate(true);
            foreach(var star in stars)star.transform.DOPunchScale(Vector3.one*.12f,.25f,3,.6f).SetUpdate(true);
            var clip=d.tiers.Count==1?service.settings.achievementUnlockClip:service.settings.tierCompletionClip;if(clip!=null)FloodAudioManager.Instance?.PlayPowerUpClip(clip);
            yield return new WaitForSecondsRealtime(service.settings.notificationDuration);
            yield return panel.DOFade(0,service.settings.notificationAnimationDuration).SetUpdate(true).WaitForCompletion();panel.gameObject.SetActive(false);
            service.Engine.MarkTierSeen(d,unlock.tier);RefreshIndicator();showing=null;
        }
    }
}
