using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FloodFill.ThreeD.Solver;
using Stopwatch=System.Diagnostics.Stopwatch;

namespace FloodFill.ThreeD.Goals
{
    public static class FloodSecondaryGoalGenerator3D
    {
        // Uses the existing simulator and its exact component membership, including free-flood moves
        // which may move away from the previously captured component.
        public static SecondarySolutionTrace Trace(FloodFillRegionGraph3D graph,int[] regions,int[] colors,int moveBudget,int moveLimit,CancellationToken token=default)
        {
            if(graph==null||regions==null||colors==null||regions.Length!=colors.Length||colors.Length>moveBudget||colors.Length>moveLimit)throw new ArgumentException("No within-budget witness.");
            var state=(byte[])graph.RegionColors.Clone();var sim=new FloodMoveSimulator(graph);
            var previous=new bool[graph.RegionCount];var next=new bool[graph.RegionCount];
            var trace=new SecondarySolutionTrace{total=graph.TotalVoxelCount,regions=graph.RegionCount,colors=(int[])colors.Clone(),newlyCaptured=new int[colors.Length],captured=new int[colors.Length],colorSelections=new int[graph.ColorCount],initialColorCounts=new int[graph.ColorCount]};
            trace.initialCaptured=sim.CountComponent(state,graph.StartingRegion);sim.CopyLastComponent(previous);
            for(int r=0;r<graph.RegionCount;r++)trace.initialColorCounts[graph.RegionColors[r]]+=graph.RegionVoxelCounts[r];
            int selected=-1;
            for(int i=0;i<colors.Length;i++)
            {
                token.ThrowIfCancellationRequested();state=sim.Apply(state,new FloodFillSolverMove3D(regions[i],colors[i]));
                trace.captured[i]=sim.CountComponent(state,regions[i]);sim.CopyLastComponent(next);
                if(trace.captured[i]==graph.TotalVoxelCount&&i<colors.Length-1)throw new ArgumentException("Witness continues after the game would already have won.");
                for(int r=0;r<next.Length;r++)if(next[r]&&!previous[r])trace.newlyCaptured[i]+=graph.RegionVoxelCounts[r];
                var swap=previous;previous=next;next=swap;
                if(selected!=colors[i])trace.colorSelections[colors[i]]++;selected=colors[i];
            }
            if(colors.Length==0||trace.captured.Last()!=graph.TotalVoxelCount)throw new ArgumentException("Witness does not win.");
            trace.finalColor=colors.Last();return trace;
        }
        public static bool Demonstrated(SecondaryGoalDefinition goal,SecondarySolutionTrace trace)
        {
            var tracker=new FloodSecondaryGoalTracker3D("validation",new[]{goal},trace.initialCaptured,trace.colorSelections.Length);
            int selected=-1;
            for(int i=0;i<trace.Length;i++){if(selected!=trace.colors[i])tracker.SelectColor(trace.colors[i]);selected=trace.colors[i];tracker.Move(i+1,trace.newlyCaptured[i],trace.captured[i]);}
            // Timing is an explicitly conservative human estimate, never a mathematical proof.
            return tracker.Complete(true,trace.Length,trace.finalColor,goal.type==SecondaryGoalType.SpeedRun?goal.seconds:0)[0];
        }
        public static SecondaryGoalDefinition[] Generate(SecondarySolutionTrace t,int budget,int mode,int difficulty,int seed,FloodSecondaryGoalSettings3D settings,CancellationToken token,out double milliseconds)
        {
            var watch=Stopwatch.StartNew();var tuning=settings.ForDifficulty(difficulty);var candidates=new List<SecondaryGoalDefinition>();
            int minimum=Math.Max(settings.minimumNewCapture,(int)Math.Ceiling(t.total*settings.minimumCaptureBoardFraction));
            int efficiency=t.Length+(int)Math.Floor((budget-t.Length)*tuning.moveSlackFraction);
            void Add(SecondaryGoalDefinition g)
            {
                token.ThrowIfCancellationRequested();
                if(candidates.Count>=settings.candidateLimit||watch.Elapsed.TotalMilliseconds>settings.generationBudgetMilliseconds||settings.Weight(mode,difficulty,g.type)<=0)return;
                if(Demonstrated(g,t)){g.id="v1_"+(int)g.type+"_"+g.target+"_"+g.deadline+"_"+g.color+"_"+g.count;candidates.Add(g);}
            }
            if(efficiency<=budget-settings.minimumMoveSavings)
            {Add(new SecondaryGoalDefinition{type=SecondaryGoalType.MoveSaver,target=efficiency});Add(new SecondaryGoalDefinition{type=SecondaryGoalType.FinalColorEfficiency,target=efficiency,color=t.finalColor});}
            Add(new SecondaryGoalDefinition{type=SecondaryGoalType.FinalColor,color=t.finalColor});
            int peak=t.newlyCaptured.Max();int capture=Math.Max(minimum,(int)Math.Ceiling(peak*tuning.captureFraction));
            if(capture<=peak&&capture<t.total-t.initialCaptured)Add(new SecondaryGoalDefinition{type=SecondaryGoalType.BigCapture,target=capture});
            foreach(var type in new[]{SecondaryGoalType.QuickStart,SecondaryGoalType.FastExpansion})
            {
                int deadline=Math.Max(1,(int)Math.Floor(t.Length*(type==SecondaryGoalType.QuickStart?tuning.earlyMoveFraction:Math.Min(.7,tuning.earlyMoveFraction+.25))));
                if(deadline>=t.Length)continue;
                int pct=(int)Math.Floor(100.0*t.captured[deadline-1]/t.total/5)*5;
                int target=(int)Math.Ceiling(pct*t.total/100.0);
                if(pct>0&&pct<100&&target>t.initialCaptured+minimum)Add(new SecondaryGoalDefinition{type=type,target=target,count=pct,deadline=deadline});
            }
            int comboCount=Math.Max(settings.minimumComboMoves,(int)Math.Ceiling(t.Length*(.15+difficulty*.05)));
            var ordered=t.newlyCaptured.OrderByDescending(v=>v).ToArray();
            if(comboCount<=ordered.Length)
            {int threshold=Math.Max(minimum,(int)Math.Ceiling(ordered[comboCount-1]*tuning.captureFraction));if(ordered[comboCount-1]>=threshold)Add(new SecondaryGoalDefinition{type=SecondaryGoalType.ComboCapture,target=threshold,count=comboCount});}
            for(int c=0;c<t.colorSelections.Length;c++)
                if(t.initialColorCounts[c]>=t.total/(double)t.colorSelections.Length&&t.colorSelections[c]>0&&t.colorSelections[c]<t.colorSelections.Max()&&t.colorSelections[c]<=t.Length/3)
                    Add(new SecondaryGoalDefinition{type=SecondaryGoalType.LimitedColorUsage,color=c,target=t.colorSelections[c]});
            int startPct=(int)Math.Round(tuning.sprintStartFraction*100);int startTarget=(int)Math.Ceiling(startPct*t.total/100.0);
            int crossing=Array.FindIndex(t.captured,v=>v>=startTarget);
            if(crossing>=0&&t.Length-crossing-1>=2&&t.initialCaptured<startTarget)
                Add(new SecondaryGoalDefinition{type=SecondaryGoalType.FinalSprint,target=startTarget,count=startPct,deadline=t.Length-crossing-1});
            double time=Math.Max(settings.minimumSpeedSeconds,t.Length*tuning.humanSecondsPerMove+Math.Pow(t.total,1.0/3)*settings.cameraSecondsPerAxis+t.regions*settings.regionInspectionSeconds);
            time*=mode==1?settings.randomShapeTimeMultiplier:mode==2?settings.itemsTimeMultiplier:1;
            Add(new SecondaryGoalDefinition{type=SecondaryGoalType.SpeedRun,seconds=Math.Ceiling(time/5)*5,estimatedTiming=true});
            var random=new Random(seed^0x53474F41);var selected=new List<SecondaryGoalDefinition>();
            while(selected.Count<3&&candidates.Count>0)
            {
                double pick=random.NextDouble()*candidates.Sum(g=>settings.Weight(mode,difficulty,g.type));int index=0;
                for(;index<candidates.Count-1;index++){pick-=settings.Weight(mode,difficulty,candidates[index].type);if(pick<0)break;}
                var chosen=candidates[index];selected.Add(chosen);candidates.RemoveAll(g=>Family(g.type)==Family(chosen.type));
            }
            milliseconds=watch.Elapsed.TotalMilliseconds;return selected.ToArray();
        }
        private static int Family(SecondaryGoalType type)
        {
            if(type==SecondaryGoalType.FinalColorEfficiency||type==SecondaryGoalType.FinalColor||type==SecondaryGoalType.MoveSaver)return 0;
            if(type==SecondaryGoalType.QuickStart||type==SecondaryGoalType.FastExpansion||type==SecondaryGoalType.FinalSprint)return 1;
            if(type==SecondaryGoalType.BigCapture||type==SecondaryGoalType.ComboCapture)return 2;
            return (int)type+3;
        }
    }
}
