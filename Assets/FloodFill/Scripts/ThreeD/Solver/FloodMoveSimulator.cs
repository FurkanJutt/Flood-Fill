using System;
using System.Collections.Generic;

namespace FloodFill.ThreeD.Solver
{
    public sealed class FloodMoveSimulator
    {
        private readonly FloodFillRegionGraph3D graph;
        private readonly int[] queue;
        private readonly bool[] seen;
        public FloodMoveSimulator(FloodFillRegionGraph3D graph)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
            queue = new int[graph.RegionCount];
            seen = new bool[graph.RegionCount];
        }
        // Thread confined. Never mutates the supplied state.
        public byte[] Apply(byte[] state, FloodFillSolverMove3D move)
        {
            if (state == null || state.Length != graph.RegionCount || move.RegionId < 0 ||
                move.RegionId >= state.Length || move.ColorIndex < 0 || move.ColorIndex >= graph.ColorCount ||
                state[move.RegionId] == move.ColorIndex) throw new ArgumentException("Invalid recoloring move.");
            var next = (byte[])state.Clone();
            Array.Clear(seen, 0, seen.Length);
            int head = 0, tail = 0;
            byte source = state[move.RegionId];
            queue[tail++] = move.RegionId;
            seen[move.RegionId] = true;
            while (head < tail)
            {
                int r = queue[head++];
                next[r] = (byte)move.ColorIndex;
                for (int e = graph.NeighborOffsets[r]; e < graph.NeighborOffsets[r + 1]; e++)
                {
                    int n = graph.Neighbors[e];
                    if (!seen[n] && state[n] == source) { seen[n] = true; queue[tail++] = n; }
                }
            }
            return next;
        }
        public int CountComponent(byte[] state, int representative)
        {
            Array.Clear(seen, 0, seen.Length);
            int head = 0, tail = 0, voxels = 0;
            queue[tail++] = representative;
            seen[representative] = true;
            while (head < tail)
            {
                int r = queue[head++]; voxels += graph.RegionVoxelCounts[r];
                for (int e = graph.NeighborOffsets[r]; e < graph.NeighborOffsets[r + 1]; e++)
                {
                    int n = graph.Neighbors[e];
                    if (!seen[n] && state[n] == state[r]) { seen[n] = true; queue[tail++] = n; }
                }
            }
            return voxels;
        }
    }

    internal sealed class FloodComponentAnalysis
    {
        internal readonly int[] Ids, Representatives, Mass, Sizes;
        internal readonly byte[] Colors;
        internal readonly HashSet<int>[] Adjacent;
        internal readonly int Count, LargestMass, DistinctColors;
        internal FloodComponentAnalysis(FloodFillRegionGraph3D graph, byte[] state)
        {
            int n = state.Length;
            Ids = new int[n]; Array.Fill(Ids, -1);
            Representatives = new int[n]; Mass = new int[n]; Sizes = new int[n]; Colors = new byte[n];
            var queue = new int[n];
            var distinct = new bool[graph.ColorCount];
            int count = 0, largest = 0, colorCount = 0;
            for (int start = 0; start < n; start++)
            {
                if (Ids[start] >= 0) continue;
                int c = count++, head = 0, tail = 0;
                Representatives[c] = start; Colors[c] = state[start];
                if (!distinct[state[start]]) { distinct[state[start]] = true; colorCount++; }
                queue[tail++] = start; Ids[start] = c;
                while (head < tail)
                {
                    int r = queue[head++]; Mass[c] += graph.RegionVoxelCounts[r]; Sizes[c]++;
                    for (int e = graph.NeighborOffsets[r]; e < graph.NeighborOffsets[r + 1]; e++)
                    {
                        int next = graph.Neighbors[e];
                        if (Ids[next] < 0 && state[next] == state[start]) { Ids[next] = c; queue[tail++] = next; }
                    }
                }
                largest = Math.Max(largest, Mass[c]);
            }
            Count = count; LargestMass = largest; DistinctColors = colorCount;
            Adjacent = new HashSet<int>[count];
            for (int i = 0; i < count; i++) Adjacent[i] = new HashSet<int>();
            for (int r = 0; r < n; r++)
                for (int e = graph.NeighborOffsets[r]; e < graph.NeighborOffsets[r + 1]; e++)
                { int a = Ids[r], b = Ids[graph.Neighbors[e]]; if (a != b) Adjacent[a].Add(b); }
        }
    }
}
