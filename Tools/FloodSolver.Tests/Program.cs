using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using FloodFill.ThreeD.Solver;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string message)
    { assertions++; if (!value) throw new Exception(message); }
    private static FloodFillBoardSnapshot3D Board(int n, int seed, string shape)
    {
        var random = new Random(seed); var logical = new List<int>(); var colors = new List<byte>();
        for (int z = 0; z < n; z++) for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            if (shape == "shell" && x > 0 && y > 0 && z > 0 && x < n-1 && y < n-1 && z < n-1) continue;
            // Connected irregular volume, with holes and varying height.
            if (shape == "irregular" && y > 0 && (y > (x + z) % n || (x % 3 == 1 && z % 3 == 1))) continue;
            logical.Add(x + n * (y + n * z));
            colors.Add(shape == "groups" ? (byte)((x * 3 / n + y * 2 / n) % 6) : (byte)random.Next(6));
        }
        return new FloodFillBoardSnapshot3D(n,n,n,6,logical.ToArray(),colors.ToArray(),0);
    }
    private static FloodFillRegionGraph3D Graph(FloodFillBoardSnapshot3D snapshot)
    {
        Check(FloodFillRegionGraph3D.TryBuild(snapshot, out var graph), "Graph construction");
        int[] indices = snapshot.LogicalIndices; byte[] colors = snapshot.Colors;
        Check(graph.RegionVoxelCounts.Sum() == snapshot.VoxelCount, "Graph voxel mass");
        var at = new Dictionary<int,int>();
        for(int i=0;i<indices.Length;i++) at.Add(indices[i],i);
        var edges = new HashSet<long>();
        var counts = new int[graph.RegionCount];
        for(int i=0;i<indices.Length;i++)
        {
            int r=graph.VoxelRegionIds[i];counts[r]++;Check(graph.RegionColors[r]==colors[i],"Region color");
            foreach(int neighbor in Adjacent(indices[i],snapshot)) if(at.TryGetValue(neighbor,out int j))
            {
                int other=graph.VoxelRegionIds[j];
                Check((colors[i]==colors[j])==(r==other),"Maximal same-color partition");
                if(r!=other) edges.Add(((long)r<<32)|(uint)other);
            }
        }
        Check(counts.SequenceEqual(graph.RegionVoxelCounts),"Region mass");
        var actual=new HashSet<long>();
        for(int r=0;r<graph.RegionCount;r++) for(int e=graph.NeighborOffsets[r];e<graph.NeighborOffsets[r+1];e++)
            actual.Add(((long)r<<32)|(uint)graph.Neighbors[e]);
        Check(edges.SetEquals(actual),"All six-neighbor graph edges");
        return graph;
    }
    private static IEnumerable<int> Adjacent(int logical,FloodFillBoardSnapshot3D s)
    {
        int x=logical%s.Width, y=logical/s.Width%s.Height,z=logical/(s.Width*s.Height);
        if(x>0)yield return logical-1;if(x+1<s.Width)yield return logical+1;
        if(y>0)yield return logical-s.Width;if(y+1<s.Height)yield return logical+s.Width;
        if(z>0)yield return logical-s.Width*s.Height;if(z+1<s.Depth)yield return logical+s.Width*s.Height;
    }
    // Independent voxel oracle, equivalent to the game's BFS but separate from region search.
    private static int Replay(FloodFillBoardSnapshot3D s,FloodFillRegionGraph3D g,FloodFillSolverResult result)
    {
        byte[] voxels=s.Colors, state=(byte[])g.RegionColors.Clone();int[] indices=s.LogicalIndices;
        var at=new Dictionary<int,int>();for(int i=0;i<indices.Length;i++)at.Add(indices[i],i);
        var simulator=new FloodMoveSimulator(g);int captured=0;
        for(int m=0;m<result.BestMoveCount;m++)
        {
            int region=result.BestRegionSequence[m],target=result.BestColorSequence[m];
            int start=at[g.RepresentativeLogicalIndices[region]];byte source=voxels[start];
            Check(source!=target,"Move counts exactly once");
            var queue=new Queue<int>();var seen=new HashSet<int>();queue.Enqueue(start);seen.Add(start);
            while(queue.Count>0){int v=queue.Dequeue();foreach(int p in Adjacent(indices[v],s))if(at.TryGetValue(p,out int next)&&voxels[next]==source&&seen.Add(next))queue.Enqueue(next);}
            foreach(int v in seen)voxels[v]=(byte)target;
            state=simulator.Apply(state,new FloodFillSolverMove3D(region,target));
            for(int i=0;i<indices.Length;i++)Check(voxels[i]==state[g.VoxelRegionIds[i]],"Per-move voxel/region equivalence");
            seen.Clear();seen.Add(start);queue.Enqueue(start);
            while(queue.Count>0){int v=queue.Dequeue();foreach(int p in Adjacent(indices[v],s))if(at.TryGetValue(p,out int next)&&voxels[next]==target&&seen.Add(next))queue.Enqueue(next);}
            captured=seen.Count;Check(captured==simulator.CountComponent(state,region),"Capture mass equivalence");
        }
        Check(captured==s.VoxelCount||result.BestMoveCount==0&&g.RegionCount==1,"Fully captured");
        return captured;
    }
    private static int Exhaustive(FloodFillRegionGraph3D graph)
    {
        var initial=(byte[])graph.RegionColors.Clone();var q=new Queue<(byte[],int)>();var seen=new HashSet<string>();
        q.Enqueue((initial,0));seen.Add(Convert.ToBase64String(initial));var sim=new FloodMoveSimulator(graph);
        while(q.Count>0)
        {
            var (state,depth)=q.Dequeue();if(state.All(x=>x==state[0]))return depth;
            // All regions and all colors, including nonmerging legal recolorings.
            for(int r=0;r<state.Length;r++)for(int c=0;c<graph.ColorCount;c++)
            {if(state[r]==c)continue;byte[] next=sim.Apply(state,new FloodFillSolverMove3D(r,c));if(seen.Add(Convert.ToBase64String(next)))q.Enqueue((next,depth+1));}
        }
        throw new Exception("No exhaustive solution");
    }
    private sealed class CollidingComparer : IEqualityComparer<FloodSolverState>
    {
        public bool Equals(FloodSolverState a,FloodSolverState b)=>a.Equals(b);
        public int GetHashCode(FloodSolverState state)=>0;
    }
    private static void UnitTests()
    {
        for(int seed=0;seed<30;seed++)
        {
            var rnd=new Random(seed);var colors=new byte[8];for(int i=0;i<8;i++)colors[i]=(byte)rnd.Next(3);
            var s=new FloodFillBoardSnapshot3D(2,2,2,3,Enumerable.Range(0,8).ToArray(),colors,0);var g=Graph(s);
            int exact=Exhaustive(g);var r=FloodFillMoveSolver3D.Solve(g,new MoveSolverSettings{developmentExactSearchTimeBudgetMs=2000,developmentBeamSearchTimeBudgetMs=2,developmentGreedySearchTimeBudgetMs=10},true);
            Check(r.BestMoveCount==exact&&r.IsProvenOptimal,"IDA* vs exhaustive BFS");Replay(s,g,r);
        }
        var merging=Graph(new FloodFillBoardSnapshot3D(11,1,1,2,Enumerable.Range(0,11).ToArray(),new byte[]{0,0,0,0,0,1,1,1,1,1,1},0));
        var mergeSim=new FloodMoveSimulator(merging);var merged=mergeSim.Apply(merging.RegionColors,new FloodFillSolverMove3D(merging.StartingRegion,1));
        Check(mergeSim.CountComponent(merged,merging.StartingRegion)==11,"5 purple plus 6 yellow gives 11 captured");
        var data=new byte[1025];var equal=(byte[])data.Clone();var different=(byte[])data.Clone();different[1024]=1;
        var collisionA=new FloodSolverState(new byte[]{1,5,5,2,1,3,2,3,0,0,2,3});
        var collisionB=new FloodSolverState(new byte[]{3,2,0,1,1,2,5,4,5,4,3,1});
        Check(collisionA.GetHashCode()==collisionB.GetHashCode()&&!collisionA.Equals(collisionB),"Real FNV32 collision compares complete state");
        Check(new HashSet<FloodSolverState>{collisionA,collisionB}.Count==2,"Transposition table preserves actual hash collision");
        var set=new HashSet<FloodSolverState>(new CollidingComparer()){new FloodSolverState(data),new FloodSolverState(equal),new FloodSolverState(different)};
        Check(set.Count==2,"Collision safe equality beyond 512 regions");
        var immutable=Board(6,1103,"shell");var original=immutable.Colors;original[0]=255;Check(immutable.Colors[0]!=255,"Snapshot defensive copy");
        var graph=Graph(immutable);var cts=new CancellationTokenSource();cts.Cancel();bool cancelled=false;
        try{FloodFillMoveSolver3D.Solve(graph,new MoveSolverSettings(),true,cts.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"Pre-cancellation");
        var one=new FloodFillBoardSnapshot3D(1,1,1,2,new[]{0},new byte[]{0},0);
        var done=FloodFillMoveSolver3D.Solve(Graph(one),new MoveSolverSettings(),true);Check(done.BestMoveCount==0&&done.IsProvenOptimal,"Already solved zero moves");
        Check(!FloodFillRegionGraph3D.TryBuild(new FloodFillBoardSnapshot3D(1,1,1,2,new[]{0,0},new byte[]{0,1},0),out _),"Reject duplicate coordinates");
        Check(!FloodFillRegionGraph3D.TryBuild(new FloodFillBoardSnapshot3D(int.MaxValue,int.MaxValue,2,2,new[]{0},new byte[]{0},0),out _),"Reject dimension overflow");
        var disconnected=Graph(new FloodFillBoardSnapshot3D(3,1,1,2,new[]{0,2},new byte[]{0,1},0));Check(!disconnected.IsConnected,"Disconnected graph");
        var reversed=immutable.LogicalIndices;var reversedColors=immutable.Colors;Array.Reverse(reversed);Array.Reverse(reversedColors);
        var reverseGraph=Graph(new FloodFillBoardSnapshot3D(6,6,6,6,reversed,reversedColors,reversed.Length-1));
        Check(graph.RegionColors.SequenceEqual(reverseGraph.RegionColors)&&graph.Neighbors.SequenceEqual(reverseGraph.Neighbors),"Canonical graph independent of voxel order");
        foreach(int moves in new[]{1,24,35,64})foreach(int regions in new[]{10,80,150,250,400})
        {
            var settings=new MoveSolverSettings();var profile=MoveBudgetCalculator3D.Calculate(moves,regions,false,settings);
            int Round(double x)=>(int)Math.Round(x,MidpointRounding.AwayFromZero);
            int Bonus(int min,double fraction)=>Math.Max(1,Round(Math.Max(min,Round(moves*fraction))*profile.complexityMultiplier));
            Check(profile.perfectMoves==moves&&profile.hardMoves==moves+Bonus(1,.08)&&profile.normalMoves==moves+Bonus(3,.23)&&profile.easyMoves==moves+Bonus(5,.38),"Difficulty percentages and rounding");
        }
        // A long enough request guarantees cancellation after its initial completion.
        var cancelGraph=Graph(Board(12,1103,"shell"));var delayed=new CancellationTokenSource();delayed.CancelAfter(100);
        var kept=FloodFillMoveSolver3D.Solve(cancelGraph,new MoveSolverSettings{developmentGreedySearchTimeBudgetMs=10000,developmentBeamSearchTimeBudgetMs=10000},true,delayed.Token);
        Check(kept.SolutionValidated&&kept.SearchTerminationReason.Contains("Cancelled"),"Cancellation preserves incumbent");
        Console.WriteLine("UNIT TESTS PASS: "+assertions+" assertions");
    }
    public static void Main(string[] args)
    {
        if(args.Length>0&&args[0]=="--sweep")
        {
            foreach(int size in new[]{8,12})foreach(int depth in new[]{1,2,3,4,5})
            {
                var s=Board(size,1103,"shell");var g=Graph(s);
                var r=FloodFillMoveSolver3D.Solve(g,new MoveSolverSettings{lookaheadDepth=depth,developmentGreedySearchTimeBudgetMs=2000,developmentBeamSearchTimeBudgetMs=1,developmentExactSearchTimeBudgetMs=1},true);
                Console.WriteLine($"{size},{depth},{r.GreedyMoveCount},{r.ElapsedMilliseconds:F1}");
            }
            return;
        }
        if(args.Length>0&&args[0]=="--runtime")
        {
            Console.WriteLine("size,seed,voxels,regions,best,ms");
            foreach(int size in new[]{6,8,10,12,16})foreach(int seed in new[]{1103,2207})
            {
                var s=Board(size,seed,"shell");var g=Graph(s);var r=FloodFillMoveSolver3D.Solve(g,new MoveSolverSettings(),false);
                Replay(s,g,r);Console.WriteLine($"{size},{seed},{g.TotalVoxelCount},{g.RegionCount},{r.BestMoveCount},{r.ElapsedMilliseconds:F1}");
            }
            return;
        }
        UnitTests();
        if(args.Length>0&&args[0]=="--unit")return;
        var rows=new List<string>{"size,shape,seed,voxels,regions,greedy,best,ms,expanded,duplicates,estimatedPeakBytes,optimal,withinNormalBudget"};
        foreach(int size in new[]{6,8,10,12,16})foreach(string shape in new[]{"shell","solid","irregular","groups"})foreach(int seed in new[]{1103,2207})
        {
            var s=Board(size,seed,shape);var g=Graph(s);var settings=new MoveSolverSettings();
            var r=FloodFillMoveSolver3D.Solve(g,settings,true);Check(r.SolutionValidated,"Complete validated result");Replay(s,g,r);
            Check(r.BestMoveCount==r.BestColorSequence.Length&&r.BestMoveCount==r.BudgetProfile.bestSolutionMoves,"Authoritative solution length");
            var constrained=FloodFillMoveSolver3D.Solve(g,new MoveSolverSettings{maximumSearchMemoryMegabytes=1,developmentBeamSearchTimeBudgetMs=10,developmentGreedySearchTimeBudgetMs=1,developmentExactSearchTimeBudgetMs=5},true);
            Check(constrained.SolutionValidated&&constrained.EstimatedPeakSearchMemoryBytes<=1048576,"Bounded search under 1 MiB configured retained memory");
            string row=$"{size},{shape},{seed},{g.TotalVoxelCount},{g.RegionCount},{r.GreedyMoveCount},{r.BestMoveCount},{r.ElapsedMilliseconds:F1},{r.ExploredStates},{r.DuplicateStatesSkipped},{r.EstimatedPeakSearchMemoryBytes},{r.IsProvenOptimal},{r.BestMoveCount<=r.BudgetProfile.normalMoves}";
            rows.Add(row);Console.WriteLine(row);
        }
        if(args.Length>0)File.WriteAllLines(args[0],rows);
        Console.WriteLine("ALL TESTS PASS: "+assertions+" assertions; 40 benchmark cases. Within generated budgets compares to this engine's own budget, not an independent quality benchmark.");
    }
}
