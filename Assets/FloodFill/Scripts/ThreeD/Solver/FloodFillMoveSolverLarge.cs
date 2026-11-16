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
        private int beamUniqueStates;
        private int beamDuplicatesSkipped;
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
            outcome.SearchAlgorithm = "Multiword greedy + bounded beam search";

            watch.Restart();
            FloodFillSolverMove3D[] beam = RunBeam(
                initial, greedy.Length, out int expanded, out int uniqueStates,
                out int duplicatesSkipped, out string termination, out long peakBytes);
            watch.Stop();
            outcome.BeamMilliseconds = watch.Elapsed.TotalMilliseconds;
            outcome.ExpandedStates = expanded;
            outcome.UniqueStates = uniqueStates;
            outcome.DuplicateStatesSkipped = duplicatesSkipped;
            outcome.SearchTerminationReason = termination;
            outcome.EstimatedPeakSearchMemoryBytes = peakBytes;
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
            out int expanded,
            out int uniqueStates,
            out int duplicatesSkipped,
            out string terminationReason,
            out long peakMemoryBytes)
        {
            expanded = 0;
            uniqueStates = 0;
            duplicatesSkipped = 0;
            terminationReason = "Beam search did not run.";
            peakMemoryBytes = 0;
            if (upperBound <= 1)
            {
                terminationReason = "Greedy solution is already one move or shorter.";
                return Array.Empty<FloodFillSolverMove3D>();
            }

            long stateBytes = (long)stateWords * sizeof(ulong);
            long bytesPerBeamSlot = Math.Max(1L, stateBytes * 2L + 64L);
            long memoryBudget = (long)settings.MaximumSearchMemoryMegabytes * 1024L * 1024L;
            int memoryLimitedWidth = (int)Math.Max(1L,
                Math.Min(int.MaxValue, memoryBudget / bytesPerBeamSlot));
            int practicalWidthLimit = graph.RegionCount > 256 ? 32 : 64;
            int beamWidth = Math.Max(1,
                Math.Min(Math.Min(settings.BeamWidth, practicalWidthLimit), memoryLimitedWidth));
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
            var hashCounts = new Dictionary<ulong, int>(beamWidth * 2);
            Stopwatch watch = Stopwatch.StartNew();
            peakMemoryBytes = EstimateBeamMemoryBytes(beamWidth, stateBytes);
            terminationReason = "Beam frontier exhausted.";

            for (int depth = 0; depth < upperBound - 1; depth++)
            {
                int nextCount = 0;
                hashCounts.Clear();
                for (int node = 0; node < currentCount; node++)
                {
                    if ((expanded & 63) == 0 && cancellationToken.IsCancellationRequested)
                    {
                        terminationReason = "Beam search cancelled.";
                        uniqueStates = beamUniqueStates;
                        duplicatesSkipped = beamDuplicatesSkipped;
                        return Array.Empty<FloodFillSolverMove3D>();
                    }
                    if ((expanded & 63) == 0 && watch.ElapsedMilliseconds >= settings.BeamMilliseconds)
                    {
                        terminationReason = "Beam search reached its time budget.";
                        uniqueStates = beamUniqueStates;
                        duplicatesSkipped = beamDuplicatesSkipped;
                        return Array.Empty<FloodFillSolverMove3D>();
                    }

                    int currentOffset = node * stateWords;
                    CollectMoves(currentStates, currentOffset, moves);
                    for (int moveIndex = 0; moveIndex < moves.Count; moveIndex++)
                    {
                        if ((expanded & 255) == 0 && cancellationToken.IsCancellationRequested)
                        {
                            terminationReason = "Beam search cancelled.";
                            uniqueStates = beamUniqueStates;
                            duplicatesSkipped = beamDuplicatesSkipped;
                            return Array.Empty<FloodFillSolverMove3D>();
                        }
                        if ((expanded & 255) == 0 && watch.ElapsedMilliseconds >= settings.BeamMilliseconds)
                        {
                            terminationReason = "Beam search reached its time budget.";
                            uniqueStates = beamUniqueStates;
                            duplicatesSkipped = beamDuplicatesSkipped;
                            return Array.Empty<FloodFillSolverMove3D>();
                        }
                        LargeMove move = moves[moveIndex];
                        Apply(currentStates, currentOffset, scratch, 0,
                            move.Representative, move.Source, move.Target);
                        expanded++;
                        if (IsSolved(scratch, 0))
                        {
                            terminationReason = "Beam search found a complete solution.";
                            uniqueStates = beamUniqueStates;
                            duplicatesSkipped = beamDuplicatesSkipped;
                            return Reconstruct(currentTraces[node], move);
                        }

                        int estimatedComponents = Math.Max(
                            1, currentComponents[node] - move.MergeEstimate);
                        int estimatedLargest = Math.Max(
                            currentLargest[node], move.VoxelEstimate);
                        long score = -(long)estimatedComponents * 1000000L +
                            (long)estimatedLargest * 100L;
                        ulong hash = HashState(scratch, 0);
                        int duplicateSlot = hashCounts.ContainsKey(hash)
                            ? FindEqualState(nextStates, nextHashes, nextCount, hash, scratch)
                            : -1;
                        if (duplicateSlot >= 0)
                        {
                            beamDuplicatesSkipped++;
                            if (score > nextScores[duplicateSlot])
                            {
                                nextComponents[duplicateSlot] = estimatedComponents;
                                nextLargest[duplicateSlot] = estimatedLargest;
                                nextParents[duplicateSlot] = currentTraces[node];
                                nextMoves[duplicateSlot] = move;
                                nextScores[duplicateSlot] = score;
                            }
                            continue;
                        }
                        beamUniqueStates++;
                        int slot = FindCandidateSlot(nextScores, nextCount, beamWidth, score);
                        if (slot < 0) continue;
                        if (slot < nextCount)
                        {
                            ulong oldHash = nextHashes[slot];
                            int oldCount = hashCounts[oldHash] - 1;
                            if (oldCount == 0) hashCounts.Remove(oldHash);
                            else hashCounts[oldHash] = oldCount;
                        }
                        nextHashes[slot] = hash;
                        if (hashCounts.TryGetValue(hash, out int hashCount)) hashCounts[hash] = hashCount + 1;
                        else hashCounts.Add(hash, 1);
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
                    terminationReason = "Beam frontier exhausted.";
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
                peakMemoryBytes = Math.Max(peakMemoryBytes,
                    EstimateBeamMemoryBytes(beamWidth, stateBytes) + (long)traces.Capacity * 16L);
            }
            uniqueStates = beamUniqueStates;
            duplicatesSkipped = beamDuplicatesSkipped;
            if (terminationReason == "Beam frontier exhausted.")
                terminationReason = "Beam search exhausted its greedy solution bound.";
            return Array.Empty<FloodFillSolverMove3D>();
        }

        private int FindEqualState(
            ulong[] states,
            ulong[] hashes,
            int count,
            ulong hash,
            ulong[] candidate)
        {
            for (int slot = 0; slot < count; slot++)
            {
                if (hashes[slot] != hash) continue;
                int offset = slot * stateWords;
                bool equal = true;
                for (int word = 0; word < stateWords; word++)
                {
                    if (states[offset + word] == candidate[word]) continue;
                    equal = false;
                    break;
                }
                if (equal) return slot;
            }
            return -1;
        }

        private long EstimateBeamMemoryBytes(int beamWidth, long stateBytes)
        {
            long stateBuffers = (long)beamWidth * stateBytes * 2L + stateBytes;
            long metadata = (long)beamWidth * (sizeof(int) * 7L + sizeof(long) + sizeof(ulong) + 32L);
            long reusableWorkspace = (long)graph.RegionCount * (sizeof(int) * 3L + 8L);
            return stateBuffers + metadata + reusableWorkspace;
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
