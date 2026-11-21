using System;
namespace FloodFill.ThreeD.Goals
{
    /// <summary>One update per accepted move. Never decides whether gameplay wins.</summary>
    public sealed class FloodSecondaryGoalTracker3D
    {
        public readonly SecondaryGoalDefinition[] Goals;
        public SecondaryGoalAttempt Attempt { get; }
        public event Action Changed;
        public FloodSecondaryGoalTracker3D(string levelId, SecondaryGoalDefinition[] goals, int initialCaptured, int paletteSize, SecondaryGoalAttempt restored = null)
        {
            Goals = goals ?? Array.Empty<SecondaryGoalDefinition>();
            bool valid = restored != null && restored.levelId == levelId && restored.progress?.Length == Goals.Length && restored.colorSelections?.Length == paletteSize && Array.TrueForAll(restored.progress,p=>p!=null);
            Attempt = valid ? restored : new SecondaryGoalAttempt { levelId=levelId,initialCaptured=initialCaptured,colorSelections=new int[paletteSize],progress=new SecondaryGoalProgress[Goals.Length] };
            for (int i=0;i<Goals.Length;i++)
            {
                if (Attempt.progress[i]==null) Attempt.progress[i]=new SecondaryGoalProgress();
                if (Goals[i].type==SecondaryGoalType.FinalSprint && initialCaptured>=Goals[i].target && !valid) Attempt.progress[i].sprintStart=0;
            }
        }
        public void SelectColor(int color)
        {
            if(color<0||color>=Attempt.colorSelections.Length)return;
            Attempt.colorSelections[color]++;
            for(int i=0;i<Goals.Length;i++) if(Goals[i].type==SecondaryGoalType.LimitedColorUsage)
            {var p=Attempt.progress[i];p.value=Attempt.colorSelections[Goals[i].color];p.failed=p.value>Goals[i].target;}
            Changed?.Invoke();
        }
        public void Move(int number,int newly,int captured)
        {
            if(number<=Attempt.lastMove)return;
            Attempt.lastMove=number;
            for(int i=0;i<Goals.Length;i++)
            {
                var g=Goals[i];var p=Attempt.progress[i];
                switch(g.type)
                {
                    case SecondaryGoalType.BigCapture:p.value=Math.Max(p.value,newly);p.met=p.value>=g.target;break;
                    case SecondaryGoalType.ComboCapture:if(newly>=g.target)p.value++;p.met=p.value>=g.count;break;
                    case SecondaryGoalType.QuickStart:case SecondaryGoalType.FastExpansion:
                        if(number<=g.deadline){p.value=Math.Max(p.value,captured);p.met|=p.value>=g.target;}if(number>=g.deadline&&!p.met)p.failed=true;break;
                    case SecondaryGoalType.FinalSprint:
                        if(p.sprintStart<0&&captured>=g.target)p.sprintStart=number;
                        if(p.sprintStart>=0){p.value=number-p.sprintStart;if(p.value>g.deadline)p.failed=true;}break;
                    case SecondaryGoalType.MoveSaver:case SecondaryGoalType.FinalColorEfficiency:p.value=number;p.failed=number>g.target;break;
                }
            }
            Changed?.Invoke();
        }
        public bool[] Complete(bool won,int moves,int finalColor,double seconds)
        {
            var earned=new bool[Goals.Length];
            for(int i=0;i<Goals.Length;i++)
            {
                var g=Goals[i];var p=Attempt.progress[i];bool meets;
                switch(g.type)
                {
                    case SecondaryGoalType.MoveSaver:meets=moves<=g.target;break;
                    case SecondaryGoalType.FinalColor:meets=finalColor==g.color;break;
                    case SecondaryGoalType.SpeedRun:meets=seconds<=g.seconds;break;
                    case SecondaryGoalType.LimitedColorUsage:meets=Attempt.colorSelections[g.color]<=g.target;break;
                    case SecondaryGoalType.FinalSprint:meets=p.sprintStart>=0&&moves-p.sprintStart<=g.deadline;break;
                    case SecondaryGoalType.FinalColorEfficiency:meets=finalColor==g.color&&moves<=g.target;break;
                    default:meets=p.met;break;
                }
                earned[i]=won&&meets&&!p.failed;p.met=earned[i];p.failed=!earned[i];
            }
            Changed?.Invoke();return earned;
        }
        public SecondaryGoalState State(int i,bool permanent,double seconds,bool won=false)
        {
            if(permanent)return SecondaryGoalState.PreviouslyEarned;
            var p=Attempt.progress[i];var g=Goals[i];
            if(p.failed||(g.type==SecondaryGoalType.SpeedRun&&seconds>g.seconds))return SecondaryGoalState.FailedThisAttempt;
            if(p.met)return SecondaryGoalState.CompletedThisAttempt;
            return Attempt.lastMove>0||p.value>0 ? SecondaryGoalState.InProgress:SecondaryGoalState.NotCompleted;
        }
    }
}
