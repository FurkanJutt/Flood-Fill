using System;
using System.Collections.Generic;
using System.Threading;
using FloodFill.ThreeD.Solver;
namespace FloodFill.ThreeD.PowerUps
{
    public sealed class FloodHintPlan3D
    {
        public int[] logicalTargets,colors,logicalIndices;
        public byte[] currentColors,nextColors;
    }
    public sealed class FloodHintRecommendation3D
    {
        public int targetLogical,color,expectedCaptured,expectedNew,solutionLength;
        public int[] primary,merging;
        public bool winningContinuation,optimal;
        public FloodHintPlan3D plan;
    }
    public sealed class FloodHintResult3D
    {
        public readonly List<FloodHintRecommendation3D> recommendations=new List<FloodHintRecommendation3D>();
        public string error;
    }
    public static class FloodHintService3D
    {
        private static readonly SemaphoreSlim Gate=new SemaphoreSlim(1,1);
        private static bool Same<T>(T[] a,T[] b)
        {if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!EqualityComparer<T>.Default.Equals(a[i],b[i]))return false;return true;}
        public static FloodHintResult3D Calculate(FloodFillBoardSnapshot3D snapshot,int[] captured,MoveSolverSettings search,FloodHintPlan3D cache,CancellationToken token)
        {
            var result=new FloodHintResult3D();Gate.Wait(token);
            try
            {
                if(!FloodFillRegionGraph3D.TryBuild(snapshot,out var graph,token))throw new ArgumentException("The current board could not be analyzed.");
                var logical=snapshot.LogicalIndices;var colors=snapshot.Colors;var index=new Dictionary<int,int>(logical.Length);
                for(int i=0;i<logical.Length;i++)index.Add(logical[i],i);
                var current=new HashSet<int>(captured);FloodFillSolverMove3D[] plan=null;bool optimal=false;
                if(cache!=null&&Same(logical,cache.logicalIndices))
                {
                    int offset=Same(colors,cache.currentColors)?0:Same(colors,cache.nextColors)?1:-1;
                    if(offset>=0&&offset<cache.colors.Length)
                    {
                        var candidate=new FloodFillSolverMove3D[cache.colors.Length-offset];
                        for(int i=0;i<candidate.Length;i++)candidate[i]=new FloodFillSolverMove3D(graph.VoxelRegionIds[index[cache.logicalTargets[i+offset]]],cache.colors[i+offset]);
                        if(FloodSolverValidator.Validate(graph,candidate,out _))plan=candidate;
                    }
                }
                if(plan==null)
                {
                    try
                    {
                        var solution=FloodFillMoveSolver3D.Solve(graph,search,false,token);
                        if(solution.SolutionValidated&&solution.BestMoveCount>0)
                        {
                            plan=new FloodFillSolverMove3D[solution.BestMoveCount];
                            for(int i=0;i<plan.Length;i++)plan[i]=new FloodFillSolverMove3D(solution.BestRegionSequence[i],solution.BestColorSequence[i]);
                            optimal=solution.IsProvenOptimal;
                        }
                    }
                    catch(OperationCanceledException){if(token.IsCancellationRequested)throw;}
                }
                if(plan!=null&&plan.Length>0){result.recommendations.Add(Build(graph,logical,colors,current,plan[0],plan,optimal));if(token.IsCancellationRequested)return result;}
                token.ThrowIfCancellationRequested();
                // Visibility alternatives are validated single recolors, not claimed winning continuations.
                var candidates=FloodHeuristics.Moves(graph,graph.RegionColors,5);
                for(int i=0;i<candidates.Count&&result.recommendations.Count<12;i++)
                {
                    if(token.IsCancellationRequested){if(result.recommendations.Count>0)return result;token.ThrowIfCancellationRequested();}var move=candidates[i].Move;
                    if(plan!=null&&plan.Length>0&&move.RegionId==plan[0].RegionId&&move.ColorIndex==plan[0].ColorIndex)continue;
                    result.recommendations.Add(Build(graph,logical,colors,current,move,null,false));
                }
                if(result.recommendations.Count==0)result.error="No useful legal hint is available.";
                return result;
            }
            finally{Gate.Release();}
        }
        public static FloodHintResult3D LegalFallback(FloodFillBoardSnapshot3D snapshot,int[] captured,CancellationToken token)
        {
            var result=new FloodHintResult3D();
            if(!FloodFillRegionGraph3D.TryBuild(snapshot,out var graph,token))return result;
            var logical=snapshot.LogicalIndices;var colors=snapshot.Colors;var old=new HashSet<int>(captured);
            var moves=FloodHeuristics.Moves(graph,graph.RegionColors,5);
            for(int i=0;i<Math.Min(8,moves.Count);i++){token.ThrowIfCancellationRequested();result.recommendations.Add(Build(graph,logical,colors,old,moves[i].Move,null,false));}
            return result;
        }
        private static FloodHintRecommendation3D Build(FloodFillRegionGraph3D graph,int[] logical,byte[] colors,HashSet<int> captured,FloodFillSolverMove3D move,FloodFillSolverMove3D[] plan,bool optimal)
        {
            var simulator=new FloodMoveSimulator(graph);byte[] after=simulator.Apply(graph.RegionColors,move);
            int count=simulator.CountComponent(after,move.RegionId);var membership=new bool[graph.RegionCount];simulator.CopyLastComponent(membership);
            var primary=new List<int>();var merging=new List<int>();int newly=0;var nextColors=new byte[logical.Length];
            for(int i=0;i<logical.Length;i++)
            {
                int region=graph.VoxelRegionIds[i];nextColors[i]=after[region];
                if(region==move.RegionId)primary.Add(logical[i]);else if(membership[region])merging.Add(logical[i]);
                if(membership[region]&&!captured.Contains(logical[i]))newly++;
            }
            FloodHintPlan3D cache=null;
            if(plan!=null)
            {
                cache=new FloodHintPlan3D { logicalTargets=new int[plan.Length],colors=new int[plan.Length],logicalIndices=logical,currentColors=colors,nextColors=nextColors };
                for(int i=0;i<plan.Length;i++){cache.logicalTargets[i]=graph.RepresentativeLogicalIndices[plan[i].RegionId];cache.colors[i]=plan[i].ColorIndex;}
            }
            return new FloodHintRecommendation3D {targetLogical=graph.RepresentativeLogicalIndices[move.RegionId],color=move.ColorIndex,primary=primary.ToArray(),merging=merging.ToArray(),expectedCaptured=count,expectedNew=newly,solutionLength=plan?.Length??0,winningContinuation=plan!=null,optimal=optimal,plan=cache};
        }
    }
}
