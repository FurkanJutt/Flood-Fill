using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace FloodFill.ThreeD.Solver
{
    internal sealed class FloodFillMoveSolverLarge
    {
        private readonly FloodFillRegionGraph3D graph;
        private readonly MoveSolverRuntimeSettings settings;
        private readonly CancellationToken cancellationToken;
        private readonly int wordCount;
        private readonly int stateWords;
        private readonly int[] queue;
        private readonly int[] visitStamp;
        private readonly int[] boundaryStamp;
        private readonly bool[] componentVisited;
        private readonly int[] targetMergeCounts;
        private readonly int[] targetVoxelEstimates;
        private readonly List<LargeMove> moves = new List<LargeMove>(4096);
        private readonly List<TraceNode> traces = new List<TraceNode>(4096);
        private int stamp;

        private readonly struct LargeMove
        {
            public LargeMove(
                int representative,
                int source,
                int target,
                int mergeEstimate,
                int voxelEstimate)
            {
                Representative = representative;
                Source = source;
                Target = target;
                MergeEstimate = mergeEstimate;
                VoxelEstimate = voxelEstimate;
            }
            public int Representative { get; }
            public int Source { get; }
            public int Target { get; }
            public int MergeEstimate { get; }
            public int VoxelEstimate { get; }
        }

        private readonly struct TraceNode
        {
            public TraceNode(int parent, FloodFillSolverMove3D move)
            {
                Parent = parent;
                Move = move;
            }
            public int Parent { get; }
            public FloodFillSolverMove3D Move { get; }
        }

        public FloodFillMoveSolverLarge(
            FloodFillRegionGraph3D graph,
            MoveSolverRuntimeSettings settings,
            CancellationToken cancellationToken)
        {
            this.graph = graph;
            this.settings = settings;
            this.cancellationToken = cancellationToken;
            wordCount = (graph.RegionCount + 63) >> 6;
            stateWords = wordCount * graph.ColorCount;
            queue = new int[graph.RegionCount];
            visitStamp = new int[graph.RegionCount];
            boundaryStamp = new int[graph.RegionCount];
            componentVisited = new bool[graph.RegionCount];
            targetMergeCounts = new int[graph.ColorCount];
            targetVoxelEstimates = new int[graph.ColorCount];
        }

        public SolverSearchOutcome3D Solve()
        {
            ulong[] initial = CreateInitialState();
            var outcome = new SolverSearchOutcome3D();
            Stopwatch watch = Stopwatch.StartNew();
            FloodFillSolverMove3D[] greedy = RunGreedyVariants(initial);
            watch.Stop();
            outcome.GreedyMilliseconds = watch.Elapsed.TotalMilliseconds;
            outcome.GreedyMoves = greedy.Length;
            outcome.BestMoves = greedy;

            watch.Restart();
            FloodFillSolverMove3D[] beam = RunBeam(initial, greedy.Length, out int expanded);
            watch.Stop();
            outcome.BeamMilliseconds = watch.Elapsed.TotalMilliseconds;
            outcome.ExpandedStates = expanded;
            outcome.BeamMoves = beam.Length > 0 ? beam.Length : greedy.Length;
            if (beam.Length > 0 && beam.Length < outcome.BestMoves.Length)
            {
                outcome.BestMoves = beam;
            }

            outcome.UsedFallback = outcome.BestMoves.Length == 0;
            if (outcome.UsedFallback)
            {
                outcome.BestMoves = RunSingleGreedy(initial, 0);
                outcome.GreedyMoves = outcome.BestMoves.Length;
            }
            return outcome;
        }

        public bool Validate(FloodFillSolverMove3D[] sequence)
        {
            ulong[] state = CreateInitialState();
            ulong[] scratch = new ulong[stateWords];
            for (int i = 0; i < sequence.Length; i++)
            {
                FloodFillSolverMove3D step = sequence[i];
                int source = GetColor(state, 0, step.RegionId);
                if (source < 0 || step.ColorIndex < 0 || step.ColorIndex >= graph.ColorCount ||
                    source == step.ColorIndex ||
                    !HasAdjacentTarget(state, 0, step.RegionId, source, step.ColorIndex))
                {
                    return false;
                }
                Apply(state, 0, scratch, step.RegionId, source, step.ColorIndex);
                ulong[] swap = state;
                state = scratch;
                scratch = swap;
            }
            return IsSolved(state, 0);
        }

        public FloodFillSolverMove3D[] CreateFallback()
        {
            return RunSingleGreedy(CreateInitialState(), 0);
        }

        private ulong[] CreateInitialState()
        {
            var state = new ulong[stateWords];
            for (int region = 0; region < graph.RegionCount; region++)
            {
                SetRegionColor(state, 0, region, graph.RegionColors[region]);
            }
            return state;
        }

        private FloodFillSolverMove3D[] RunGreedyVariants(ulong[] initial)
        {
            FloodFillSolverMove3D[] best = Array.Empty<FloodFillSolverMove3D>();
            for (int variant = 0; variant < settings.GreedyRuns; variant++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FloodFillSolverMove3D[] candidate = RunSingleGreedy(initial, variant);
                if (candidate.Length > 0 && (best.Length == 0 || candidate.Length < best.Length))
                {
                    best = candidate;
                }
            }
            return best;
        }

        private FloodFillSolverMove3D[] RunSingleGreedy(ulong[] initial, int variant)
        {
            var current = new ulong[stateWords];
            var next = new ulong[stateWords];
            Array.Copy(initial, current, stateWords);
            var path = new List<FloodFillSolverMove3D>(graph.RegionCount);
            while (!IsSolved(current, 0) && path.Count < graph.RegionCount)
            {
                CollectMoves(current, 0, moves);
                if (moves.Count == 0)
                {
                    return Array.Empty<FloodFillSolverMove3D>();
                }

                int bestIndex = 0;
                long bestScore = long.MinValue;
                for (int i = 0; i < moves.Count; i++)
                {
                    LargeMove move = moves[i];
                    long mergeWeight = variant % 3 == 0 ? 1000000L :
                        variant % 3 == 1 ? 350000L : 650000L;
                    long voxelWeight = variant % 3 == 1 ? 1000L : 100L;
                    long score = move.MergeEstimate * mergeWeight +
                        move.VoxelEstimate * voxelWeight -
                        ((move.Target + variant) % graph.ColorCount);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                LargeMove selected = moves[bestIndex];
                Apply(current, 0, next, selected.Representative,
                    selected.Source, selected.Target);
                ulong[] swap = current;
                current = next;
                next = swap;
                path.Add(new FloodFillSolverMove3D(
                    selected.Representative, selected.Target));
            }
            return IsSolved(current, 0) ? path.ToArray() : Array.Empty<FloodFillSolverMove3D>();
        }

        private FloodFillSolverMove3D[] RunBeam(
            ulong[] initial,
            int upperBound,
            out int expanded)
        {
            expanded = 0;
            if (upperBound <= 1)
            {
                return Array.Empty<FloodFillSolverMove3D>();
            }

            int beamWidth = Math.Min(settings.BeamWidth, graph.RegionCount > 256 ? 32 : 64);
            var currentStates = new ulong[beamWidth * stateWords];
            var nextStates = new ulong[beamWidth * stateWords];
            var scratch = new ulong[stateWords];
            Array.Copy(initial, currentStates, stateWords);
            var currentComponents = new int[beamWidth];
            var nextComponents = new int[beamWidth];
            var currentLargest = new int[beamWidth];
            var nextLargest = new int[beamWidth];
            var currentTraces = new int[beamWidth];
            var nextTraces = new int[beamWidth];
            var nextParents = new int[beamWidth];
            var nextMoves = new LargeMove[beamWidth];
            var nextScores = new long[beamWidth];
            var nextHashes = new ulong[beamWidth];
            currentComponents[0] = graph.RegionCount;
            currentLargest[0] = FindLargestInitialRegion();
            currentTraces[0] = -1;
            int currentCount = 1;
            traces.Clear();
            var hashes = new HashSet<ulong>(beamWidth * 8);
            Stopwatch watch = Stopwatch.StartNew();

            for (int depth = 0; depth < upperBound - 1; depth++)
            {
                int nextCount = 0;
                hashes.Clear();
                for (int node = 0; node < currentCount; node++)
                {
                    if ((expanded & 63) == 0 &&
                        (cancellationToken.IsCancellationRequested ||
                         watch.ElapsedMilliseconds >= settings.BeamMilliseconds))
                    {
                        return Array.Empty<FloodFillSolverMove3D>();
                    }

                    int currentOffset = node * stateWords;
                    CollectMoves(currentStates, currentOffset, moves);
                    for (int moveIndex = 0; moveIndex < moves.Count; moveIndex++)
                    {
                        if ((expanded & 255) == 0 &&
                            (cancellationToken.IsCancellationRequested ||
                             watch.ElapsedMilliseconds >= settings.BeamMilliseconds))
                        {
                            return Array.Empty<FloodFillSolverMove3D>();
                        }
                        LargeMove move = moves[moveIndex];
                        Apply(currentStates, currentOffset, scratch, 0,
                            move.Representative, move.Source, move.Target);
                        expanded++;
                        if (IsSolved(scratch, 0))
                        {
                            return Reconstruct(currentTraces[node], move);
                        }

                        int estimatedComponents = Math.Max(
                            1, currentComponents[node] - move.MergeEstimate);
                        int estimatedLargest = Math.Max(
                            currentLargest[node], move.VoxelEstimate);
                        long score = -(long)estimatedComponents * 1000000L +
                            (long)estimatedLargest * 100L;
                        int slot = FindCandidateSlot(nextScores, nextCount, beamWidth, score);
                        if (slot < 0)
                        {
                            continue;
                        }

                        ulong hash = HashState(scratch, 0);
                        if (hashes.Contains(hash))
                        {
                            continue;
                        }
                        if (slot < nextCount)
                        {
                            hashes.Remove(nextHashes[slot]);
                        }
                        hashes.Add(hash);
                        nextHashes[slot] = hash;
                        Array.Copy(scratch, 0, nextStates, slot * stateWords, stateWords);
                        nextComponents[slot] = estimatedComponents;
                        nextLargest[slot] = estimatedLargest;
                        nextParents[slot] = currentTraces[node];
                        nextMoves[slot] = move;
                        nextScores[slot] = score;
                        if (slot == nextCount)
                        {
                            nextCount++;
                        }
                    }
                }

                if (nextCount == 0)
                {
                    break;
                }
                for (int i = 0; i < nextCount; i++)
                {
                    int trace = traces.Count;
                    traces.Add(new TraceNode(nextParents[i], new FloodFillSolverMove3D(
                        nextMoves[i].Representative, nextMoves[i].Target)));
                    nextTraces[i] = trace;
                }

                Swap(ref currentStates, ref nextStates);
                Swap(ref currentComponents, ref nextComponents);
                Swap(ref currentLargest, ref nextLargest);
                Swap(ref currentTraces, ref nextTraces);
                currentCount = nextCount;
            }
            return Array.Empty<FloodFillSolverMove3D>();
        }

        private void CollectMoves(ulong[] state, int stateOffset, List<LargeMove> output)
        {
            output.Clear();
            Array.Clear(componentVisited, 0, componentVisited.Length);
            for (int start = 0; start < graph.RegionCount; start++)
            {
                if (componentVisited[start])
                {
                    continue;
                }

                int source = GetColor(state, stateOffset, start);
                Array.Clear(targetMergeCounts, 0, targetMergeCounts.Length);
                Array.Clear(targetVoxelEstimates, 0, targetVoxelEstimates.Length);
                int boundaryGeneration = NextStamp();
                int head = 0;
                int tail = 0;
                int componentVoxels = 0;
                queue[tail++] = start;
                componentVisited[start] = true;
                while (head < tail)
                {
                    int region = queue[head++];
                    componentVoxels += graph.RegionVoxelCounts[region];
                    for (int edge = graph.NeighborOffsets[region];
                         edge < graph.NeighborOffsets[region + 1]; edge++)
                    {
                        int neighbor = graph.Neighbors[edge];
                        int neighborColor = GetColor(state, stateOffset, neighbor);
                        if (neighborColor == source)
                        {
                            if (!componentVisited[neighbor])
                            {
                                componentVisited[neighbor] = true;
                                queue[tail++] = neighbor;
                            }
                        }
                        else if (boundaryStamp[neighbor] != boundaryGeneration)
                        {
                            boundaryStamp[neighbor] = boundaryGeneration;
                            targetMergeCounts[neighborColor]++;
                            targetVoxelEstimates[neighborColor] +=
                                graph.RegionVoxelCounts[neighbor];
                        }
                    }
                }

                for (int target = 0; target < graph.ColorCount; target++)
                {
                    if (target != source && targetMergeCounts[target] > 0)
                    {
                        output.Add(new LargeMove(
                            start,
                            source,
                            target,
                            targetMergeCounts[target],
                            componentVoxels + targetVoxelEstimates[target]));
                    }
                }
            }
        }

        private void Apply(
            ulong[] sourceState,
            int sourceOffset,
            ulong[] destination,
            int representative,
            int sourceColor,
            int targetColor)
        {
            Apply(sourceState, sourceOffset, destination, 0,
                representative, sourceColor, targetColor);
        }

        private void Apply(
            ulong[] sourceState,
            int sourceOffset,
            ulong[] destination,
            int destinationOffset,
            int representative,
            int sourceColor,
            int targetColor)
        {
            Array.Copy(sourceState, sourceOffset, destination, destinationOffset, stateWords);
            int generation = NextStamp();
            int head = 0;
            int tail = 0;
            queue[tail++] = representative;
            visitStamp[representative] = generation;
            while (head < tail)
            {
                int region = queue[head++];
                ClearRegionColor(destination, destinationOffset, region, sourceColor);
                SetRegionColor(destination, destinationOffset, region, targetColor);
                for (int edge = graph.NeighborOffsets[region];
                     edge < graph.NeighborOffsets[region + 1]; edge++)
                {
                    int neighbor = graph.Neighbors[edge];
                    if (visitStamp[neighbor] == generation ||
                        !HasColor(sourceState, sourceOffset, neighbor, sourceColor))
                    {
                        continue;
                    }
                    visitStamp[neighbor] = generation;
                    queue[tail++] = neighbor;
                }
            }
        }

        private bool HasAdjacentTarget(
            ulong[] state,
            int offset,
            int representative,
            int sourceColor,
            int targetColor)
        {
            int generation = NextStamp();
            int head = 0;
            int tail = 0;
            queue[tail++] = representative;
            visitStamp[representative] = generation;
            while (head < tail)
            {
                int region = queue[head++];
                for (int edge = graph.NeighborOffsets[region];
                     edge < graph.NeighborOffsets[region + 1]; edge++)
                {
                    int neighbor = graph.Neighbors[edge];
                    if (HasColor(state, offset, neighbor, targetColor))
                    {
                        return true;
                    }
                    if (visitStamp[neighbor] != generation &&
                        HasColor(state, offset, neighbor, sourceColor))
                    {
                        visitStamp[neighbor] = generation;
                        queue[tail++] = neighbor;
                    }
                }
            }
            return false;
        }

        private bool IsSolved(ulong[] state, int offset)
        {
            for (int color = 0; color < graph.ColorCount; color++)
            {
                int colorOffset = offset + color * wordCount;
                bool all = true;
                for (int word = 0; word < wordCount; word++)
                {
                    ulong expected = ulong.MaxValue;
                    if (word == wordCount - 1 && (graph.RegionCount & 63) != 0)
                    {
                        expected = (1UL << (graph.RegionCount & 63)) - 1UL;
                    }
                    if (state[colorOffset + word] != expected)
                    {
                        all = false;
                        break;
                    }
                }
                if (all)
                {
                    return true;
                }
            }
            return false;
        }

        private int GetColor(ulong[] state, int offset, int region)
        {
            for (int color = 0; color < graph.ColorCount; color++)
            {
                if (HasColor(state, offset, region, color))
                {
                    return color;
                }
            }
            return -1;
        }

        private bool HasColor(ulong[] state, int offset, int region, int color)
        {
            int word = region >> 6;
            ulong bit = 1UL << (region & 63);
            return (state[offset + color * wordCount + word] & bit) != 0;
        }

        private void SetRegionColor(ulong[] state, int offset, int region, int color)
        {
            state[offset + color * wordCount + (region >> 6)] |=
                1UL << (region & 63);
        }

        private void ClearRegionColor(ulong[] state, int offset, int region, int color)
        {
            state[offset + color * wordCount + (region >> 6)] &=
                ~(1UL << (region & 63));
        }

        private int FindLargestInitialRegion()
        {
            int largest = 0;
            for (int i = 0; i < graph.RegionVoxelCounts.Length; i++)
            {
                largest = Math.Max(largest, graph.RegionVoxelCounts[i]);
            }
            return largest;
        }

        private int NextStamp()
        {
            stamp++;
            if (stamp == int.MaxValue)
            {
                Array.Clear(visitStamp, 0, visitStamp.Length);
                Array.Clear(boundaryStamp, 0, boundaryStamp.Length);
                stamp = 1;
            }
            return stamp;
        }

        private FloodFillSolverMove3D[] Reconstruct(int parentTrace, LargeMove finalMove)
        {
            int length = 1;
            for (int trace = parentTrace; trace >= 0; trace = traces[trace].Parent)
            {
                length++;
            }
            var result = new FloodFillSolverMove3D[length];
            result[length - 1] = new FloodFillSolverMove3D(
                finalMove.Representative, finalMove.Target);
            int index = length - 2;
            for (int trace = parentTrace; trace >= 0; trace = traces[trace].Parent)
            {
                result[index--] = traces[trace].Move;
            }
            return result;
        }

        private static int FindCandidateSlot(
            long[] scores, int count, int capacity, long score)
        {
            if (count < capacity)
            {
                return count;
            }
            int worst = 0;
            for (int i = 1; i < count; i++)
            {
                if (scores[i] < scores[worst])
                {
                    worst = i;
                }
            }
            return score > scores[worst] ? worst : -1;
        }

        private ulong HashState(ulong[] state, int offset)
        {
            ulong hash = 1469598103934665603UL;
            for (int i = 0; i < stateWords; i++)
            {
                hash ^= state[offset + i];
                hash *= 1099511628211UL;
            }
            return hash;
        }

        private static void Swap<T>(ref T[] left, ref T[] right)
        {
            T[] temporary = left;
            left = right;
            right = temporary;
        }
    }
}
