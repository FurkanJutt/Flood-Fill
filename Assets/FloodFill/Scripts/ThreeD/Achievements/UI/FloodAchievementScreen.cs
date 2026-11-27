using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD.Achievements
{
    public sealed class FloodAchievementScreen:MonoBehaviour
    {
        public Button back;
        public TMP_Text summary;
        public Image summaryFill;
        public TMP_Dropdown category,filter;
        public ScrollRect scroll;
        public RectTransform content;
        public FloodAchievementCard cardPrefab;
        public GameObject detailPanel;
        public TMP_Text detailTitle,detailBody;
        public Image detailIcon;
        public Button closeDetail;
        public Button[] claimButtons=new Button[3];
        public TMP_Text[] claimLabels=new TMP_Text[3];
        public float rowHeight=218;
        private readonly List<FloodAchievementCard> pool=new List<FloodAchievementCard>();
        private readonly List<AchievementDefinition> visible=new List<AchievementDefinition>();
        private readonly List<string> categories=new List<string>();
        private FloodAchievementService service;
        private FloodFillMobileUI3D owner;
        private AchievementDefinition detail;
        private int first=-1;
        private bool initialized;
        public void Initialize(FloodFillMobileUI3D ui)
        {
            owner=ui;if(initialized)return;initialized=true;service=FloodAchievementService.Instance;
            foreach(var control in GetComponentsInChildren<Selectable>(true))FloodFill.ThreeD.Audio.FloodAudioManager.Instance?.BindControl(control,control.name,false);
            back.onClick.AddListener(()=>{FloodTouchInput3D.PressFeedback(back.transform);owner.CloseAchievements();});closeDetail.onClick.AddListener(()=>{FloodTouchInput3D.PressFeedback(closeDetail.transform);detailPanel.SetActive(false);});
            category.onValueChanged.AddListener(_=>Rebuild());filter.onValueChanged.AddListener(_=>Rebuild());
            scroll.onValueChanged.AddListener(_=>RefreshVisible(false));
            for(int i=0;i<claimButtons.Length;i++){int n=i;claimButtons[i].onClick.AddListener(()=>Claim(n));}
            if(service!=null){service.Changed+=OnChanged;service.Unlocked+=OnUnlocked;}
        }
        private void OnEnable()
        {
            if(!initialized)Initialize(GetComponentInParent<FloodFillMobileUI3D>());
            if(service==null||!service.Ready){summary.text="Achievements are unavailable.";return;}
            BuildSelectors();Rebuild();
        }
        private void OnDestroy(){if(service!=null){service.Changed-=OnChanged;service.Unlocked-=OnUnlocked;}}
        private void BuildSelectors()
        {
            string previous=categories.Count>category.value?categories[category.value]:"ALL CATEGORIES";
            categories.Clear();categories.Add("ALL CATEGORIES");
            bool rewards=false;
            foreach(var d in service.Engine.Catalog.achievements)if(d.enabled&&(!d.hidden||service.settings.enableHiddenAchievements||!FloodAchievementCard.IsHidden(d,service.Engine,service.settings)))
            {
                if(!categories.Contains(d.category))categories.Add(d.category);
                foreach(var t in d.tiers)rewards|=AchievementValidation.HasRewards(t);
            }
            category.ClearOptions();category.AddOptions(categories);category.SetValueWithoutNotify(Math.Max(0,categories.IndexOf(previous)));
            int selected=filter.value;var options=new List<string>{"ALL ACHIEVEMENTS","IN PROGRESS","COMPLETED"};
            if(rewards&&service.settings.enableRewardUI)options.Add("REWARDS AVAILABLE");
            filter.ClearOptions();filter.AddOptions(options);filter.SetValueWithoutNotify(Math.Min(selected,options.Count-1));
        }
        private bool Complete(AchievementDefinition d){foreach(var t in d.tiers)if(!service.Engine.Progress(d,t).completed)return false;return true;}
        private bool RewardAvailable(AchievementDefinition d)
        {
            if(FloodAchievementCard.IsHidden(d,service.Engine,service.settings))return false;
            foreach(var t in d.tiers){var p=service.Engine.Progress(d,t);if(p.completed)foreach(var r in t.rewards)if(r.Valid&&!p.claimedRewardIds.Contains(r.id))return true;}return false;
        }
        public void Rebuild()
        {
            if(service==null||!service.Ready)return;visible.Clear();
            foreach(var d in service.Engine.Catalog.achievements)
            {
                if(!d.enabled||d.hidden&&!service.settings.enableHiddenAchievements&&FloodAchievementCard.IsHidden(d,service.Engine,service.settings)||category.value>0&&d.category!=categories[category.value])continue;
                bool done=Complete(d);if(filter.value==1&&done||filter.value==2&&!done||filter.value==3&&!RewardAvailable(d))continue;
                visible.Add(d);
            }
            content.sizeDelta=new Vector2(content.sizeDelta.x,visible.Count*rowHeight);content.anchoredPosition=Vector2.zero;first=-1;
            RefreshVisible(true);RefreshSummary();detailPanel.SetActive(false);
        }
        private void RefreshSummary()
        {
            int total=0,completed=0,tiers=0,earned=0;
            foreach(var d in service.Engine.Catalog.achievements)if(d.enabled)
            {total++;bool done=true;foreach(var t in d.tiers){tiers++;if(service.Engine.Progress(d,t).completed)earned++;else done=false;}if(done)completed++;}
            float ratio=tiers>0?earned/(float)tiers:0;summary.text="ACHIEVEMENTS  "+completed+" / "+total+"  ("+(total>0?completed*100f/total:0).ToString("0")+"%)\nMILESTONES  "+earned+" / "+tiers+"  ("+(ratio*100).ToString("0")+"%)";summaryFill.fillAmount=ratio;
        }
        private void RefreshVisible(bool force)
        {
            if(service==null||!service.Ready)return;
            int wanted=Mathf.Clamp(Mathf.CeilToInt(scroll.viewport.rect.height/rowHeight)+2,3,18);
            while(pool.Count<wanted){var card=Instantiate(cardPrefab,content);pool.Add(card);}
            int next=Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0,content.anchoredPosition.y)/rowHeight),0,Math.Max(0,visible.Count-1));if(next==first&&!force)return;first=next;
            for(int i=0;i<pool.Count;i++)
            {
                int n=first+i;var card=pool[i];bool active=i<wanted&&n<visible.Count;card.gameObject.SetActive(active);if(!active)continue;
                var rect=(RectTransform)card.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(-12,rowHeight-10);rect.anchoredPosition=new Vector2(0,-n*rowHeight);
                card.Bind(visible[n],service,OpenDetail);
            }
        }
        private void OnChanged(string id)
        {
            if(!gameObject.activeInHierarchy)return;
            if(string.IsNullOrEmpty(id)){BuildSelectors();Rebuild();return;}
            if(filter.value!=0){Rebuild();return;}
            foreach(var card in pool)if(card.gameObject.activeSelf&&card.Definition!=null&&card.Definition.id==id)card.Bind(card.Definition,service,OpenDetail);
            RefreshSummary();if(detailPanel.activeSelf&&detail!=null&&detail.id==id)RefreshDetail();
        }
        private void OnUnlocked(AchievementUnlock unlock)
        {if(!gameObject.activeInHierarchy)return;foreach(var card in pool)if(card.gameObject.activeSelf&&card.Definition==unlock.definition)card.PopStars();}
        public void OpenDetail(AchievementDefinition d)
        {
            detail=d;detailPanel.SetActive(true);RefreshDetail();service.Engine.MarkSeen(d);FloodTouchInput3D.ShowFeedback(detailPanel.transform);
        }
        private void RefreshDetail()
        {
            if(detail==null)return;bool hidden=FloodAchievementCard.IsHidden(detail,service.Engine,service.settings);
            detailTitle.text=hidden?"HIDDEN ACHIEVEMENT":detail.name;detailIcon.sprite=hidden?service.settings.hiddenIcon:service.Icon(detail);
            for(int i=0;i<claimButtons.Length;i++)claimButtons[i].gameObject.SetActive(false);
            if(hidden){detailBody.text="Keep playing to discover this achievement.";return;}
            var text=new System.Text.StringBuilder();text.AppendLine(detail.description);text.AppendLine();int activeIndex=detail.tiers.FindIndex(t=>!service.Engine.Progress(detail,t).completed);
            for(int i=0;i<detail.tiers.Count;i++)
            {
                var t=detail.tiers[i];var p=service.Engine.Progress(detail,t);text.Append("TIER ").Append(i+1).Append(" — ").AppendLine(p.completed?"COMPLETED":i==activeIndex?"CURRENT TIER":"LOCKED TIER");text.AppendLine(t.description);
                if(!p.completed)text.Append("Progress: ").Append(p.hasValue?p.value.ToString("0.##"):"—").Append(" / ").Append(t.target.ToString("0.##")).AppendLine();
                bool configured=AchievementValidation.HasRewards(t),available=false;
                if(!configured)text.AppendLine("No Reward Configured");
                else foreach(var r in t.rewards)if(r.Valid){bool claimed=p.claimedRewardIds.Contains(r.id);available|=!claimed;text.Append(claimed?"CLAIMED: ":"REWARD: ").Append(r.displayName??r.identifier).Append(" × ").Append(r.quantity).AppendLine();}
                text.AppendLine();
                if(configured&&p.completed&&available&&service.settings.enableRewardUI)
                {
                    claimButtons[i].gameObject.SetActive(true);claimButtons[i].interactable=service.RewardService!=null;
                    claimLabels[i].text=service.RewardService==null?"REWARDS COMING LATER":"CLAIM TIER "+(i+1);
                }
            }
            detailBody.text=text.ToString();
            detailBody.ForceMeshUpdate();var bodyRect=detailBody.rectTransform;bodyRect.sizeDelta=new Vector2(bodyRect.sizeDelta.x,Mathf.Max(900,detailBody.preferredHeight+24));
        }
        private void Claim(int index){if(detail==null||index>=detail.tiers.Count)return;bool claimed=service.Engine.TryClaim(detail,detail.tiers[index],service.RewardService,out string error);service.Flush();RefreshDetail();if(!claimed&&!string.IsNullOrEmpty(error))detailBody.text+="\n"+error;}
    }
}
