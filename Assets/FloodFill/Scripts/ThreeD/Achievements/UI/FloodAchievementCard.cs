using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FloodFill.ThreeD.Achievements
{
    public sealed class FloodAchievementCard:MonoBehaviour
    {
        public Image icon,progressFill;
        public Image[] stars=new Image[3];
        public TMP_Text title,requirement,progressText,tierText,rewardText;
        public Button button;
        public AchievementDefinition Definition {get;private set;}
        private Action<AchievementDefinition> selected;
        private void Awake(){FloodFill.ThreeD.Audio.FloodAudioManager.Instance?.BindControl(button,"ACHIEVEMENT",false);button.onClick.AddListener(()=>{FloodTouchInput3D.PressFeedback(button.transform);selected?.Invoke(Definition);});}
        public void Bind(AchievementDefinition d,FloodAchievementService service,Action<AchievementDefinition> onSelected)
        {
            Definition=d;selected=onSelected;var engine=service.Engine;int completed=0;AchievementTier active=null;
            foreach(var tier in d.tiers){if(engine.Progress(d,tier).completed)completed++;else if(active==null)active=tier;}
            bool hidden=IsHidden(d,engine,service.settings),done=completed==d.tiers.Count;
            title.text=hidden?"HIDDEN ACHIEVEMENT":d.name;icon.sprite=hidden?service.settings.hiddenIcon:service.Icon(d);
            int filled=hidden?0:AchievementValidation.FilledStars(d.tiers.Count,completed);
            for(int i=0;i<3;i++){stars[i].sprite=(!hidden&&d.tiers.Count==3?engine.Progress(d,d.tiers[i]).completed:i<filled)?service.settings.filledStar:service.settings.emptyStar;stars[i].color=Color.white;}
            if(rewardText!=null)rewardText.text="";
            if(hidden){requirement.text="Keep playing to discover this achievement.";progressText.text="???";tierText.text="HIDDEN";progressFill.fillAmount=0;return;}
            if(rewardText!=null&&service.settings.enableRewardUI)
            {
                foreach(var tier in d.tiers)foreach(var reward in tier.rewards)if(reward.Valid)
                {
                    var state=engine.Progress(d,tier);rewardText.text=(state.completed&&!state.claimedRewardIds.Contains(reward.id)?"REWARD AVAILABLE · ":state.claimedRewardIds.Contains(reward.id)?"CLAIMED · ":"REWARD · ")+(string.IsNullOrEmpty(reward.displayName)?reward.identifier:reward.displayName)+" × "+reward.quantity;break;
                }
            }
            if(active==null)active=d.tiers[d.tiers.Count-1];var p=engine.Progress(d,active);
            requirement.text=active.description;tierText.text=done?"COMPLETED":"TIER "+(d.tiers.IndexOf(active)+1)+" OF "+d.tiers.Count;
            tierText.color=done?new Color(.2f,1,.65f):new Color(.65f,.8f,1);
            double value=p.hasValue?p.value:0;bool minimum=d.metric==AchievementMetric.FastestSeconds;
            progressText.text=done?"COMPLETE":minimum?(p.hasValue?value.ToString("0.#")+"s / "+active.target.ToString("0.#")+"s":"BEST — / "+active.target+"s"):Math.Min(value,active.target).ToString("0.##")+" / "+active.target.ToString("0.##");
            if(!done&&d.calculation==AchievementCalculation.Percentage)progressText.text+="%";
            progressFill.fillAmount=done?1:minimum?(p.hasValue?(float)Math.Min(1,active.target/Math.Max(.001,value)):0):(float)Math.Min(1,value/active.target);
        }
        public static bool IsHidden(AchievementDefinition d,AchievementEngine engine,FloodAchievementSettings settings)
        {if(!d.hidden)return false;foreach(var t in d.tiers)if(engine.Progress(d,t).completed)return false;return true;}
        public void PopStars(){foreach(var star in stars){star.transform.DOKill();star.transform.localScale=Vector3.one;star.transform.DOPunchScale(Vector3.one*.12f,.25f,3,.6f).SetUpdate(true);}}
    }
}
