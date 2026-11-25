using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
namespace FloodFill.ThreeD
{
    public sealed class FloodVictoryPopup3D : MonoBehaviour
    {
        public TMP_Text level, moves, best, primaryLabel, coinReward, gemReward;
        [FormerlySerializedAs("captured")] public TMP_Text elapsedTime;
        public Image[] stars;
        public RectTransform leftLaurel, rightLaurel;
        public GameObject levelsButton;
        public Image[] confetti;
        public Image[] rewardSparkles;
        public Image heroGlow;
        [Min(0)] public float laurelDuration=.65f, starPopDuration=.42f, confettiDuration=3.5f;
        [Min(0)] public float statisticsDuration=1.2f, statisticsDelay=.2f;
        private readonly List<Tween> animations=new List<Tween>();
        private Vector2 leftPosition,rightPosition;
        private bool cached;
        private int finalMoves, finalBudget;
        private double finalElapsedSeconds;
        private Vector3 movesScale, elapsedScale;
        private Color[] sparkleColors;
        private Quaternion[] sparkleRotations;
        private float[] sparkleTimers, sparkleTargets, sparkleFadeSpeeds, sparkleSpinSpeeds;
        private readonly System.Random sparkleRandom=new System.Random();
        private Color glowColor;
        private Quaternion glowRotation;
        private Vector3 glowScale;
        private float glowTime;
        public bool IsCountingStatistics { get; private set; }
        public int AnimationStartCount { get; private set; }
        public bool IsAnimating => animations.Exists(t=>t!=null&&t.IsActive()&&!t.IsComplete());
        public void Present(FloodFillGameManager3D game)
        {
            int number=game.NumberedLevel>0?game.NumberedLevel:game.IsCampaignGame?game.ActiveCampaignLevel:0;
            level.text=number>0?"LEVEL "+number:"CUSTOM LEVEL";
            finalMoves=game.MoveCount;finalBudget=game.CurrentMoveBudget;finalElapsedSeconds=game.ElapsedSeconds;
            if(!IsCountingStatistics)SetStatistics(finalMoves,finalElapsedSeconds);
            int bestMoves=game.NumberedLevel>0?FloodLevelProgression3D.Get(game.NumberedMode,(int)game.SelectedDifficulty,game.NumberedLevel).bestMoves:game.MoveCount;
            best.text=(bestMoves>0?bestMoves:game.MoveCount).ToString();
            int earned=FloodLevelProgression3D.Stars(game.MoveCount,game.CurrentMoveBudget);
            for(int i=0;i<stars.Length;i++)stars[i].GetComponent<FloodFillUIState3D>()?.Apply(i<earned);
            bool last=game.NumberedLevel==FloodLevelProgression3D.LevelsPerCategory;
            primaryLabel.text=game.NumberedLevel<=0&&!game.IsCampaignGame?"PLAY AGAIN":last?"LEVELS":"NEXT LEVEL";primaryLabel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,primaryLabel.GetPreferredValues(primaryLabel.text).x+8);levelsButton.SetActive(!last);
            // Display placeholders only. No wallet, persistence or award calls.
            coinReward.text="+100";gemReward.text="+5";
        }
        private void Cache()
        {
            if(cached)return;leftPosition=leftLaurel.anchoredPosition;rightPosition=rightLaurel.anchoredPosition;movesScale=moves.rectTransform.localScale;elapsedScale=elapsedTime.rectTransform.localScale;
            int count=rewardSparkles==null?0:rewardSparkles.Length;
            sparkleColors=new Color[count];sparkleRotations=new Quaternion[count];
            sparkleTimers=new float[count];sparkleTargets=new float[count];sparkleFadeSpeeds=new float[count];sparkleSpinSpeeds=new float[count];
            for(int i=0;i<count;i++){sparkleColors[i]=rewardSparkles[i].color;sparkleRotations[i]=rewardSparkles[i].rectTransform.localRotation;}
            if(heroGlow!=null){glowColor=heroGlow.color;glowRotation=heroGlow.rectTransform.localRotation;glowScale=heroGlow.rectTransform.localScale;}
            cached=true;
        }
        private void OnEnable()
        {
            if(!Application.isPlaying)return;
            Cache();StopAnimations();AnimationStartCount++;
            glowTime=0;
            for(int i=0;i<sparkleColors.Length;i++)
            {
                sparkleTimers[i]=0;sparkleSpinSpeeds[i]=SparkleRange(8,16)*(i%2==0?1:-1);
            }
            AnimateStatistics();
            AnimateLaurel(leftLaurel,leftPosition,-1);AnimateLaurel(rightLaurel,rightPosition,1);
            for(int i=0;i<stars.Length;i++)
            {
                var rect=stars[i].rectTransform;rect.localScale=Vector3.zero;
                animations.Add(rect.DOScale(1,starPopDuration).SetDelay(.48f+i*.16f).SetEase(Ease.OutBack).SetUpdate(true));
            }
            for(int i=0;i<confetti.Length;i++)Launch(confetti[i],i);
        }
        private float SparkleRange(float min,float max)=>Mathf.Lerp(min,max,(float)sparkleRandom.NextDouble());
        private void Update()
        {
            if(!Application.isPlaying||!cached)return;
            if(heroGlow!=null)
            {
                glowTime+=Time.unscaledDeltaTime;
                float pulse=(1-Mathf.Cos(glowTime*Mathf.PI*.5f))*.5f;
                heroGlow.rectTransform.localRotation=glowRotation*Quaternion.Euler(0,0,glowTime*6);
                heroGlow.rectTransform.localScale=glowScale*Mathf.Lerp(1,1.08f,pulse);
                var color=glowColor;color.a*=Mathf.Lerp(1,.72f,pulse);heroGlow.color=color;
            }
            for(int i=0;i<sparkleColors.Length;i++)
            {
                var sparkle=rewardSparkles[i];float delta=Time.unscaledDeltaTime;
                sparkle.rectTransform.Rotate(0,0,sparkleSpinSpeeds[i]*delta,Space.Self);
                sparkleTimers[i]-=delta;
                if(sparkleTimers[i]<=0)
                {
                    float duration=SparkleRange(.15f,.55f);
                    // Alternate bright and dim, with a fresh intensity and pause each time.
                    sparkleTargets[i]=sparkleColors[i].a*(sparkle.color.a>sparkleColors[i].a*.6f?SparkleRange(.15f,.4f):SparkleRange(.8f,1));
                    sparkleFadeSpeeds[i]=Mathf.Abs(sparkleTargets[i]-sparkle.color.a)/duration;
                    sparkleTimers[i]=duration+SparkleRange(.08f,.5f);
                }
                var tint=sparkle.color;tint.a=Mathf.MoveTowards(tint.a,sparkleTargets[i],sparkleFadeSpeeds[i]*delta);sparkle.color=tint;
            }
        }
        private void SetStatistics(int moveCount,double seconds)
        {
            moves.text=$"<color=#FFD341>{moveCount}</color> / {finalBudget}";
            elapsedTime.text=FloodFillGameManager3D.FormatElapsedTime(seconds);
        }
        private void AnimateStatistics()
        {
            IsCountingStatistics=true;SetStatistics(0,0);float progress=0;
            animations.Add(DOTween.To(()=>progress,value=>
            {
                progress=value;SetStatistics(Mathf.FloorToInt(finalMoves*value),finalElapsedSeconds*value);
            },1,statisticsDuration).SetDelay(statisticsDelay).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(()=>
            {
                IsCountingStatistics=false;SetStatistics(finalMoves,finalElapsedSeconds);
                animations.Add(moves.rectTransform.DOPunchScale(movesScale*.07f,.22f,4,.5f).SetUpdate(true));
                animations.Add(elapsedTime.rectTransform.DOPunchScale(elapsedScale*.07f,.22f,4,.5f).SetUpdate(true));
            }));
        }
        private void AnimateLaurel(RectTransform leaf,Vector2 target,int side)
        {
            leaf.anchoredPosition=target+new Vector2(-side*80,-90);leaf.localScale=Vector3.one*.08f;leaf.localRotation=Quaternion.Euler(0,0,side*22);
            var sequence=DOTween.Sequence().SetUpdate(true).SetDelay(.10f);
            sequence.Append(leaf.DOAnchorPos(target,laurelDuration).SetEase(Ease.OutCubic));
            sequence.Join(leaf.DOScale(1,laurelDuration).SetEase(Ease.OutBack));sequence.Join(leaf.DOLocalRotate(Vector3.zero,laurelDuration).SetEase(Ease.OutCubic));animations.Add(sequence);
        }
        private void Launch(Image image,int index)
        {
            var rect=image.rectTransform;int side=index%2==0?-1:1;
            var random=new System.Random(7483+index*431);
            float R(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
            Vector2 start=new Vector2(side*465,325),control=new Vector2(side*R(-50,230),R(1000,1250)),end=new Vector2(R(-420,420),R(260,500));
            float turn=R(-720,720),duration=confettiDuration*R(.8f,1.15f),scale=R(.65f,1.1f);
            image.gameObject.SetActive(true);image.color=Color.white;rect.anchoredPosition=start;rect.localScale=Vector3.zero;
            float progress=0;
            var tween=DOTween.To(()=>progress,value=>
            {
                progress=value;float inverse=1-value;rect.anchoredPosition=inverse*inverse*start+2*inverse*value*control+value*value*end;
                rect.localRotation=Quaternion.Euler(0,0,turn*value);
                rect.localScale=new Vector3(scale*Mathf.Lerp(.35f,1,Mathf.Abs(Mathf.Sin(value*18+index))),scale,1)*Mathf.Clamp01(value*12);
                var tint=image.color;tint.a=Mathf.Clamp01((1-value)*5);image.color=tint;
            },1,duration).SetDelay(.12f+(index/2)*.035f).SetEase(Ease.Linear).SetUpdate(true).OnComplete(()=>image.gameObject.SetActive(false));
            animations.Add(tween);
        }
        private void OnDisable()
        {
            StopAnimations();IsCountingStatistics=false;
            if(!cached)return;
            if(heroGlow!=null){heroGlow.color=glowColor;heroGlow.rectTransform.localRotation=glowRotation;heroGlow.rectTransform.localScale=glowScale;}
            for(int i=0;i<sparkleColors.Length;i++){rewardSparkles[i].color=sparkleColors[i];rewardSparkles[i].rectTransform.localRotation=sparkleRotations[i];}
            moves.rectTransform.localScale=movesScale;elapsedTime.rectTransform.localScale=elapsedScale;
            leftLaurel.anchoredPosition=leftPosition;rightLaurel.anchoredPosition=rightPosition;
            leftLaurel.localScale=rightLaurel.localScale=Vector3.one;leftLaurel.localRotation=rightLaurel.localRotation=Quaternion.identity;
            foreach(var star in stars)if(star!=null)star.rectTransform.localScale=Vector3.one;
            foreach(var particle in confetti)if(particle!=null)particle.gameObject.SetActive(false);
        }
        private void StopAnimations(){foreach(var tween in animations)if(tween!=null&&tween.IsActive())tween.Kill();animations.Clear();}
    }
}
