using System;

namespace FloodFill.ThreeD.Solver
{
    /// <summary>
    /// Bounded lookahead over legal moves. A move selects any connected
    /// same-color region and recolors that component, matching board clicks.
    /// Its score orders moves only and is never used as an admissible bound.
    /// </summary>
    public sealed class FloodFillMoveLookahead3D
    {
        private readonly struct Candidate
        {
            public Candidate(int representative, int target, long immediateScore)
            {
                Representative = representative;
                Target = target;
                ImmediateScore = immediateScore;
            }
            public int Representative { get; }
            public int Target { get; }
            public long ImmediateScore { get; }
        }

        private readonly FloodFillRegionGraph3D graph;
        private readonly int depth;
        private readonly int branchLimit;
        private readonly int nodeLimit;
        private readonly Candidate[][] candidatesByDepth;
        private readonly byte[][] statesByDepth;
        private readonly int[] queue;
        private readonly int[] visitStamp;
        private readonly int[] boundaryStamp;
        private readonly int[] targetVoxelCounts;
        private readonly int[] targetRegionCounts;
        private readonly int[] colorComponentIds;
        private readonly int[] colorComponentVoxelCounts;
        private readonly int[] colorComponentRegionCounts;
        private readonly int[] colorComponentHeads;
        private readonly int[] nextComponentMember;
        private readonly int[] targetComponentStamp;
        private readonly bool[] componentVisited;
        private int stamp;
        private int nodesExpanded;

        public FloodFillMoveLookahead3D(
            FloodFillRegionGraph3D graph,
            int depth,
            int branchLimit,
            int nodeLimit = 128)
        {
            this.graph = graph;
            this.depth = Math.Max(1, Math.Min(5, depth));
            this.branchLimit = Math.Max(1, Math.Min(graph.ColorCount, branchLimit));
            this.nodeLimit = Math.Max(8, nodeLimit);
            candidatesByDepth = new Candidate[this.depth][];
            statesByDepth = new byte[this.depth][];
            for (int i = 0; i < this.depth; i++)
            {
                candidatesByDepth[i] = new Candidate[this.branchLimit];
                statesByDepth[i] = new byte[graph.RegionCount];
            }
            queue = new int[graph.RegionCount];
            visitStamp = new int[graph.RegionCount];
            boundaryStamp = new int[graph.RegionCount];
            targetVoxelCounts = new int[graph.ColorCount];
            targetRegionCounts = new int[graph.ColorCount];
            colorComponentIds = new int[graph.RegionCount];
            colorComponentVoxelCounts = new int[graph.RegionCount];
            colorComponentRegionCounts = new int[graph.RegionCount];
            colorComponentHeads = new int[graph.RegionCount];
            nextComponentMember = new int[graph.RegionCount];
            targetComponentStamp = new int[graph.RegionCount];
            componentVisited = new bool[graph.RegionCount];
        }

        public int SelectMove(byte[] state, out int representative)
        {
            representative = -1;
            if (state == null || state.Length != graph.RegionCount) return -1;
            nodesExpanded = 0;
            int count = CollectCandidates(state, 0);
            if (count == 0) return -1;

            long bestScore = long.MinValue;
            Candidate best = candidatesByDepth[0][0];
            for (int i = 0; i < count; i++)
            {
                Candidate candidate = candidatesByDepth[0][i];
                Apply(state, candidate, statesByDepth[0]);
                long score = depth == 1
                    ? Evaluate(statesByDepth[0], candidate.Representative, 1)
                    : Search(statesByDepth[0], 1, 1, candidate.Representative);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            representative = best.Representative;
            return best.Target;
        }

        public void ApplyMoveInPlace(byte[] state, int representative, int targetColor)
        {
            Apply(state, new Candidate(representative, targetColor, 0), statesByDepth[0]);
            Array.Copy(statesByDepth[0], state, graph.RegionCount);
        }

        public bool TryEvaluateMoveCapture(
            byte[] state, int representative, int targetColor, out int capturedVoxels)
        {
            capturedVoxels = 0;
            if (state == null || state.Length != graph.RegionCount ||
                representative < 0 || representative >= graph.RegionCount ||
                targetColor < 0 || targetColor >= graph.ColorCount ||
                state[representative] == targetColor)
                return false;

            int sourceColor = state[representative];
            int generation = NextStamp();
            int head = 0, tail = 0;
            queue[tail++] = representative;
            visitStamp[representative] = generation;
            bool touchesTarget = false;
            while (head < tail)
            {
                int region = queue[head++];
                for (int edge = graph.NeighborOffsets[region];
                     edge < graph.NeighborOffsets[region + 1]; edge++)
                {
                    int neighbor = graph.Neighbors[edge];
                    if (state[neighbor] == targetColor) touchesTarget = true;
                    if (visitStamp[neighbor] == generation || state[neighbor] != sourceColor)
                        continue;
                    visitStamp[neighbor] = generation;
                    queue[tail++] = neighbor;
                }
            }
            if (!touchesTarget) return false;

            var candidate = new Candidate(representative, targetColor, 0);
            Apply(state, candidate, statesByDepth[0]);
            capturedVoxels = CountCapturedVoxels(
                statesByDepth[0], representative, out _, out _);
            return true;
        }

        public long Evaluate(byte[] state, int representative)
        {
            return Evaluate(state, representative, 0);
        }

        private long Search(byte[] state, int level, int movesUsed, int lastRepresentative)
        {
            if (movesUsed >= depth || level >= depth || nodesExpanded >= nodeLimit)
                return Evaluate(state, lastRepresentative, movesUsed);
            nodesExpanded++;

            int count = CollectCandidates(state, level);
            if (count == 0) return Evaluate(state, lastRepresentative, movesUsed);

            long best = long.MinValue;
            for (int i = 0; i < count; i++)
            {
                Candidate candidate = candidatesByDepth[level][i];
                Apply(state, candidate, statesByDepth[level]);
                long score = movesUsed + 1 >= depth
                    ? Evaluate(statesByDepth[level], candidate.Representative, movesUsed + 1)
                    : Search(statesByDepth[level], level + 1, movesUsed + 1,
                        candidate.Representative);
                if (score > best) best = score;
            }
            return best;
        }

        private int CollectCandidates(byte[] state, int level)
        {
            BuildColorComponents(state);
            int retained = 0;
            Candidate[] candidates = candidatesByDepth[level];
            for (int start = 0; start < graph.RegionCount; start++)
            {
                if (colorComponentIds[start] != start) continue;
                int sourceColor = state[start];
                Array.Clear(targetVoxelCounts, 0, targetVoxelCounts.Length);
                Array.Clear(targetRegionCounts, 0, targetRegionCounts.Length);
                int boundaryGeneration = NextStamp();
                for (int region = colorComponentHeads[start]; region >= 0;
                     region = nextComponentMember[region])
                {
                    for (int edge = graph.NeighborOffsets[region];
                         edge < graph.NeighborOffsets[region + 1]; edge++)
                    {
                        int neighbor = graph.Neighbors[edge];
                        int neighborColor = state[neighbor];
                        if (neighborColor == sourceColor) continue;
                        int targetComponent = colorComponentIds[neighbor];
                        if (targetComponentStamp[targetComponent] == boundaryGeneration) continue;
                        targetComponentStamp[targetComponent] = boundaryGeneration;
                        targetRegionCounts[neighborColor] +=
                            colorComponentRegionCounts[targetComponent];
                        targetVoxelCounts[neighborColor] +=
                            colorComponentVoxelCounts[targetComponent];
                    }
                }

                for (int target = 0; target < graph.ColorCount; target++)
                {
                    if (target == sourceColor || targetRegionCounts[target] == 0) continue;
                    long immediate = (long)(colorComponentVoxelCounts[start] + targetVoxelCounts[target]) * 100000L +
                        (long)targetRegionCounts[target] * 100L;
                    var candidate = new Candidate(start, target, immediate);
                    InsertCandidate(candidates, ref retained, candidate);
                }
            }
            return retained;
        }

        private void BuildColorComponents(byte[] state)
        {
            Array.Fill(colorComponentIds, -1);
            Array.Fill(colorComponentHeads, -1);
            for (int start = 0; start < graph.RegionCount; start++)
            {
                if (colorComponentIds[start] >= 0) continue;
                int componentId = start;
                int color = state[start];
                int head = 0, tail = 0, voxelCount = 0;
                queue[tail++] = start;
                colorComponentIds[start] = componentId;
                while (head < tail)
                {
                    int region = queue[head++];
                    voxelCount += graph.RegionVoxelCounts[region];
                    nextComponentMember[region] = colorComponentHeads[componentId];
                    colorComponentHeads[componentId] = region;
                    for (int edge = graph.NeighborOffsets[region];
                         edge < graph.NeighborOffsets[region + 1]; edge++)
                    {
                        int neighbor = graph.Neighbors[edge];
                        if (colorComponentIds[neighbor] >= 0 || state[neighbor] != color) continue;
                        colorComponentIds[neighbor] = componentId;
                        queue[tail++] = neighbor;
                    }
                }
                colorComponentVoxelCounts[componentId] = voxelCount;
                colorComponentRegionCounts[componentId] = tail;
            }
        }

        private void InsertCandidate(Candidate[] candidates, ref int count, Candidate candidate)
        {
            int position = count;
            while (position > 0 && IsBetter(candidate, candidates[position - 1])) position--;
            if (position >= branchLimit) return;
            int last = Math.Min(count, branchLimit - 1);
            for (int i = last; i > position; i--) candidates[i] = candidates[i - 1];
            candidates[position] = candidate;
            if (count < branchLimit) count++;
        }

        private static bool IsBetter(Candidate left, Candidate right)
        {
            // Preserve graph traversal order for exact ties. Color and region IDs
            // carry no gameplay value and must not introduce a hidden preference.
            return left.ImmediateScore > right.ImmediateScore;
        }

        private void Apply(byte[] sourceState, Candidate candidate, byte[] destination)
        {
            Array.Copy(sourceState, destination, graph.RegionCount);
            int sourceColor = sourceState[candidate.Representative];
            int generation = NextStamp();
            int head = 0, tail = 0;
            queue[tail++] = candidate.Representative;
            visitStamp[candidate.Representative] = generation;
            while (head < tail)
            {
                int region = queue[head++];
                destination[region] = (byte)candidate.Target;
                for (int edge = graph.NeighborOffsets[region];
                     edge < graph.NeighborOffsets[region + 1]; edge++)
                {
                    int neighbor = graph.Neighbors[edge];
                    if (visitStamp[neighbor] == generation || sourceState[neighbor] != sourceColor)
                        continue;
                    visitStamp[neighbor] = generation;
                    queue[tail++] = neighbor;
                }
            }
        }

        private long Evaluate(byte[] state, int representative, int movesUsed)
        {
            int componentCount = 0;
            int largestGroupVoxels = 0;
            Array.Clear(componentVisited, 0, componentVisited.Length);
            for (int start = 0; start < graph.RegionCount; start++)
            {
                if (componentVisited[start]) continue;
                componentCount++;
                int color = state[start];
                int head = 0, tail = 0, voxelCount = 0;
                queue[tail++] = start;
                componentVisited[start] = true;
                while (head < tail)
                {
                    int region = queue[head++];
                    voxelCount += graph.RegionVoxelCounts[region];
                    for (int edge = graph.NeighborOffsets[region];
                         edge < graph.NeighborOffsets[region + 1]; edge++)
                    {
                        int neighbor = graph.Neighbors[edge];
                        if (componentVisited[neighbor] || state[neighbor] != color) continue;
                        componentVisited[neighbor] = true;
                        queue[tail++] = neighbor;
                    }
                }
                largestGroupVoxels = Math.Max(largestGroupVoxels, voxelCount);
            }

            int capturedVoxels = CountCapturedVoxels(state, representative, out int frontierColors,
                out int largestFrontierGroup);
            if (capturedVoxels == graph.TotalVoxelCount) return 1000000000000L - movesUsed;
            return -(long)componentCount * 1000000000L +
                (long)largestGroupVoxels * 100000L +
                (long)capturedVoxels * 100L +
                (long)largestFrontierGroup * 10L + frontierColors;
        }

        private int CountCapturedVoxels(
            byte[] state,
            int representative,
            out int frontierColors,
            out int largestFrontierGroup)
        {
            int source = state[representative];
            int generation = NextStamp();
            int boundaryGeneration = NextStamp();
            int head = 0, tail = 0, capturedVoxels = 0;
            int colorMask = 0;
            int[] boundaryByColor = targetVoxelCounts;
            Array.Clear(boundaryByColor, 0, boundaryByColor.Length);
            queue[tail++] = representative;
            visitStamp[representative] = generation;
            while (head < tail)
            {
                int region = queue[head++];
                capturedVoxels += graph.RegionVoxelCounts[region];
                for (int edge = graph.NeighborOffsets[region];
                     edge < graph.NeighborOffsets[region + 1]; edge++)
                {
                    int neighbor = graph.Neighbors[edge];
                    if (visitStamp[neighbor] == generation) continue;
                    if (state[neighbor] == source)
                    {
                        visitStamp[neighbor] = generation;
                        queue[tail++] = neighbor;
                    }
                    else if (boundaryStamp[neighbor] != boundaryGeneration)
                    {
                        boundaryStamp[neighbor] = boundaryGeneration;
                        boundaryByColor[state[neighbor]] += graph.RegionVoxelCounts[neighbor];
                        colorMask |= 1 << state[neighbor];
                    }
                }
            }
            frontierColors = CountBits((uint)colorMask);
            largestFrontierGroup = 0;
            for (int color = 0; color < graph.ColorCount; color++)
                largestFrontierGroup = Math.Max(largestFrontierGroup, boundaryByColor[color]);
            return capturedVoxels;
        }

        private int NextStamp()
        {
            if (++stamp == int.MaxValue)
            {
                Array.Clear(visitStamp, 0, visitStamp.Length);
                Array.Clear(boundaryStamp, 0, boundaryStamp.Length);
                Array.Clear(targetComponentStamp, 0, targetComponentStamp.Length);
                stamp = 1;
            }
            return stamp;
        }

        private static int CountBits(uint value)
        {
            int count = 0;
            while (value != 0) { value &= value - 1; count++; }
            return count;
        }
    }
}
