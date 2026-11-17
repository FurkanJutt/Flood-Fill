using System;
using System.Collections.Generic;

namespace FloodFill.ThreeD.Solver
{
    // A restricted search that grows a component from several promising origins.
    // It complements free-flood search; its distance estimate is NEVER used in free-flood proofs.
    internal sealed class FloodAnchoredSearch
    {
        private sealed class Node
        {
            internal FloodSolverState State;
            internal Node Parent;
            internal FloodFillSolverMove3D Move;
            internal int Anchor, Depth, Lower, Mass;
            internal double Rank;
            internal int[] Targets;
        }
        private readonly FloodFillRegionGraph3D graph;
        private readonly FloodMoveSimulator simulator;
        private readonly Func<bool> stopped;
        private readonly Func<int> bestLength;
        private readonly Action<FloodFillSolverMove3D[]> accept;
        private readonly Action<bool> countState;
        private readonly int[] queue, distance;
        private readonly bool[] colors;
        internal long PeakBytes { get; private set; }
        internal FloodAnchoredSearch(FloodFillRegionGraph3D graph, Func<bool> stopped, Func<int> bestLength,
            Action<FloodFillSolverMove3D[]> accept, Action<bool> countState)
        {
            this.graph=graph;this.stopped=stopped;this.bestLength=bestLength;this.accept=accept;this.countState=countState;
            simulator=new FloodMoveSimulator(graph);queue=new int[graph.RegionCount];distance=new int[graph.RegionCount];colors=new bool[graph.ColorCount];
        }
        private Node Evaluate(byte[] state,int anchor,Node parent,int color)
        {
            Array.Fill(distance,-1);Array.Clear(colors,0,colors.Length);
            int head=0,tail=0,mass=0;byte source=state[anchor];queue[tail++]=anchor;distance[anchor]=0;
            while(head<tail)
            {
                int r=queue[head++];mass+=graph.RegionVoxelCounts[r];
                for(int e=graph.NeighborOffsets[r];e<graph.NeighborOffsets[r+1];e++)
                {int n=graph.Neighbors[e];if(distance[n]<0&&state[n]==source){distance[n]=0;queue[tail++]=n;}}
            }
            // Multi-source shortest transitions from the complete captured component.
            head=0;int max=0,sum=0;
            var targets=new bool[graph.ColorCount];
            while(head<tail)
            {
                int r=queue[head++];
                for(int e=graph.NeighborOffsets[r];e<graph.NeighborOffsets[r+1];e++)
                {
                    int n=graph.Neighbors[e];if(distance[n]>=0)continue;
                    distance[n]=distance[r]+1;queue[tail++]=n;
                    max=Math.Max(max,distance[n]);sum+=distance[n];colors[state[n]]=true;
                    if(distance[n]==1)targets[state[n]]=true;
                }
            }
            int distinct=0,targetCount=0;
            for(int c=0;c<colors.Length;c++){if(colors[c])distinct++;if(targets[c])targetCount++;}
            int[] available=new int[targetCount];int cursor=0;for(int c=0;c<targets.Length;c++)if(targets[c])available[cursor++]=c;
            int depth=parent==null?0:parent.Depth+1;
            return new Node{State=new FloodSolverState(state),Parent=parent,Move=new FloodFillSolverMove3D(anchor,color),Anchor=anchor,
                Depth=depth,Lower=Math.Max(max,distinct),Mass=mass,Targets=available,
                Rank=depth+Math.Max(max,distinct)+0.25d*sum/graph.RegionCount-0.05d*mass/graph.TotalVoxelCount};
        }
        private static int Compare(Node a,Node b)
        {int d=a.Rank.CompareTo(b.Rank);if(d!=0)return d;d=b.Mass.CompareTo(a.Mass);if(d!=0)return d;d=a.Anchor.CompareTo(b.Anchor);return d!=0?d:a.Move.ColorIndex.CompareTo(b.Move.ColorIndex);}
        private static FloodFillSolverMove3D[] Trace(Node node)
        {var path=new FloodFillSolverMove3D[node.Depth];for(int i=path.Length-1;i>=0;i--){path[i]=node.Move;node=node.Parent;}return path;}
        private void Complete(Node node)
        {
            while(node.Lower>0&&node.Depth+node.Lower<bestLength()&&!stopped())
            {
                Node best=null;
                foreach(int color in node.Targets)
                {
                    var next=Evaluate(simulator.Apply(node.State.Colors,new FloodFillSolverMove3D(node.Anchor,color)),node.Anchor,node,color);countState(false);
                    if(best==null||Compare(next,best)<0)best=next;
                }
                if(best==null)return;node=best;
            }
            if(node.Lower==0)accept(Trace(node));
        }
        internal void Run(int requestedWidth,long memoryLimit)
        {
            int width=(int)Math.Min(requestedWidth,memoryLimit/Math.Max(1,(graph.RegionCount+256L)*(bestLength()+8L)*Math.Max(6L,graph.ColorCount+2L)));
            if(width<1)return;
            var origins=new List<int>();for(int r=0;r<graph.RegionCount;r++)origins.Add(r);
            origins.Sort((a,b)=>{long sa=(long)graph.RegionVoxelCounts[a]*10+graph.NeighborOffsets[a+1]-graph.NeighborOffsets[a];long sb=(long)graph.RegionVoxelCounts[b]*10+graph.NeighborOffsets[b+1]-graph.NeighborOffsets[b];int c=sb.CompareTo(sa);return c!=0?c:a.CompareTo(b);});
            var beam=new List<Node>();var used=new HashSet<int>();
            for(int i=-1;i<Math.Min(3,origins.Count);i++)
            {
                int anchor=i<0?graph.StartingRegion:origins[i];if(!used.Add(anchor))continue;
                var root=Evaluate((byte[])graph.RegionColors.Clone(),anchor,null,-1);beam.Add(root);Complete(root);
                if(stopped())return;
            }
            for(int depth=0;beam.Count>0&&depth<bestLength()-1&&!stopped();depth++)
            {
                var next=new List<Node>();var keys=new HashSet<(FloodSolverState,int)>();
                foreach(Node parent in beam)
                {
                    foreach(int color in parent.Targets)
                    {
                        if(stopped())break;
                        Node child=Evaluate(simulator.Apply(parent.State.Colors,new FloodFillSolverMove3D(parent.Anchor,color)),parent.Anchor,parent,color);
                        if(!keys.Add((child.State,child.Anchor))){countState(true);continue;}countState(false);
                        if(child.Depth+child.Lower>=bestLength())continue;
                        if(child.Lower==0){accept(Trace(child));continue;}
                        next.Add(child);
                    }
                    if(stopped())break;
                }
                if(next.Count==0)break;next.Sort(Compare);
                int keep=Math.Min(width,next.Count);beam=next.GetRange(0,keep);
                PeakBytes=Math.Max(PeakBytes,(graph.RegionCount+256L)*(width*(depth+2L)+next.Count));
                // A completion every few layers makes this an anytime search.
                if(depth%3==0&&!stopped())Complete(beam[0]);
            }
        }
    }
}
