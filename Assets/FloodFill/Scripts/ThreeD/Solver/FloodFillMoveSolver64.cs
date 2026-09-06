using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace FloodFill.ThreeD.Solver
{
    internal sealed class SolverSearchOutcome3D
    {
        public FloodFillSolverMove3D[] BestMoves = Array.Empty<FloodFillSolverMove3D>();
        public int GreedyMoves;
        public int BeamMoves;
        public int ExactMoves = -1;
        public bool ProvenOptimal;
        public bool UsedFallback;
        public int ExpandedStates;
        public double GreedyMilliseconds;
        public double BeamMilliseconds;
        public double ExactMilliseconds;
    }

    internal sealed class FloodFillMoveSolver64
    {
        private readonly FloodFillRegionGraph3D graph;
        private readonly MoveSolverRuntimeSettings settings;
        private readonly CancellationToken cancellationToken;
        private readonly ulong[] adjacency;
        private readonly ulong allRegions;
        private readonly List<Move64> moves = new List<Move64>(256);
        private readonly List<TraceNode> traces = new List<TraceNode>(2048);

        private struct State64 : IEquatable<State64>
        {
            public ulong C0;
            public ulong C1;
            public ulong C2;
            public ulong C3;
            public ulong C4;
            public ulong C5;
            public ulong C6;
            public ulong C7;

            public ulong Get(int color)
            {
                switch (color)
                {
                    case 0: return C0;
                    case 1: return C1;
                    case 2: return C2;
                    case 3: return C3;
                    case 4: return C4;
                    case 5: return C5;
                    case 6: return C6;
                    default: return C7;
                }
            }

            public void Set(int color, ulong value)
            {
                switch (color)
                {
                    case 0: C0 = value; break;
                    case 1: C1 = value; break;
                    case 2: C2 = value; break;
                    case 3: C3 = value; break;
                    case 4: C4 = value; break;
                    case 5: C5 = value; break;
                    case 6: C6 = value; break;
                    default: C7 = value; break;
                }
            }

            public bool Equals(State64 other)
            {
                return C0 == other.C0 && C1 == other.C1 && C2 == other.C2 &&
                    C3 == other.C3 && C4 == other.C4 && C5 == other.C5 &&
                    C6 == other.C6 && C7 == other.C7;
            }

            public override bool Equals(object obj)
            {
                return obj is State64 other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + C0.GetHashCode();
                    hash = hash * 31 + C1.GetHashCode();
                    hash = hash * 31 + C2.GetHashCode();
                    hash = hash * 31 + C3.GetHashCode();
                    hash = hash * 31 + C4.GetHashCode();
                    hash = hash * 31 + C5.GetHashCode();
                    hash = hash * 31 + C6.GetHashCode();
                    hash = hash * 31 + C7.GetHashCode();
                    return hash;
                }
            }
        }

        private readonly struct Move64
        {
            public Move64(ulong component, int representative, int source, int target)
            {
                Component = component;
                Representative = representative;
                Source = source;
                Target = target;
            }

            public ulong Component { get; }
            public int Representative { get; }
            public int Source { get; }
            public int Target { get; }
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

        private readonly struct BeamNode
        {
            public BeamNode(State64 state, int trace)
            {
                State = state;
                Trace = trace;
            }
            public State64 State { get; }
            public int Trace { get; }
        }

        private readonly struct BeamCandidate
        {
            public BeamCandidate(State64 state, int parentTrace, Move64 move, long score)
            {
                State = state;
                ParentTrace = parentTrace;
                Move = move;
                Score = score;
            }
            public State64 State { get; }
            public int ParentTrace { get; }
            public Move64 Move { get; }
            public long Score { get; }
        }

        public FloodFillMoveSolver64(
            FloodFillRegionGraph3D graph,
            MoveSolverRuntimeSettings settings,
            CancellationToken cancellationToken)
        {
            this.graph = graph;
            this.settings = settings;
            this.cancellationToken = cancellationToken;
            adjacency = new ulong[graph.RegionCount];
            for (int region = 0; region < graph.RegionCount; region++)
            {
                ulong mask = 0;
                for (int i = graph.NeighborOffsets[region];
                     i < graph.NeighborOffsets[region + 1]; i++)
                {
                    mask |= 1UL << graph.Neighbors[i];
                }
                adjacency[region] = mask;
            }
            allRegions = graph.RegionCount == 64
                ? ulong.MaxValue
                : (1UL << graph.RegionCount) - 1UL;
        }

        public SolverSearchOutcome3D Solve()
        {
            State64 initial = CreateInitialState();
            var outcome = new SolverSearchOutcome3D();

            Stopwatch watch = Stopwatch.StartNew();
            FloodFillSolverMove3D[] greedy = RunGreedyVariants(initial);
            watch.Stop();
            outcome.GreedyMilliseconds = watch.Elapsed.TotalMilliseconds;
            outcome.GreedyMoves = greedy.Length;
            outcome.BestMoves = greedy;

            watch.Restart();
            FloodFillSolverMove3D[] beam = RunBeam(initial, greedy.Length, out int beamExpanded);
            watch.Stop();
            outcome.BeamMilliseconds = watch.Elapsed.TotalMilliseconds;
            outcome.ExpandedStates += beamExpanded;
            outcome.BeamMoves = beam.Length > 0 ? beam.Length : greedy.Length;
            if (beam.Length > 0 && beam.Length < outcome.BestMoves.Length)
            {
                outcome.BestMoves = beam;
            }

            if (graph.RegionCount <= settings.ExactRegionThreshold &&
                !cancellationToken.IsCancellationRequested)
            {
                watch.Restart();
                FloodFillSolverMove3D[] exact = RunExact(
                    initial, outcome.BestMoves, out bool proven, out int exactExpanded);
                watch.Stop();
                outcome.ExactMilliseconds = watch.Elapsed.TotalMilliseconds;
                outcome.ExpandedStates += exactExpanded;
                outcome.ProvenOptimal = proven;
                if (exact.Length > 0)
                {
                    outcome.ExactMoves = exact.Length;
                    outcome.BestMoves = exact;
                }
                else if (proven)
                {
                    outcome.ExactMoves = outcome.BestMoves.Length;
                }
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
            State64 state = CreateInitialState();
            for (int i = 0; i < sequence.Length; i++)
            {
                FloodFillSolverMove3D step = sequence[i];
                int source = FindColor(state, step.RegionId);
                if (source < 0 || step.ColorIndex < 0 || step.ColorIndex >= graph.ColorCount ||
                    source == step.ColorIndex)
                {
                    return false;
                }
                ulong component = FloodComponent(1UL << step.RegionId, state.Get(source));
                ulong boundary = CollectAdjacent(component) & state.Get(step.ColorIndex);
                if (boundary == 0)
                {
                    return false;
                }
                state = Apply(state, new Move64(
                    component, step.RegionId, source, step.ColorIndex));
            }
            return IsSolved(state);
        }

        public FloodFillSolverMove3D[] CreateFallback()
        {
            return RunSingleGreedy(CreateInitialState(), 0);
        }

        private State64 CreateInitialState()
        {
            var state = new State64();
            for (int region = 0; region < graph.RegionCount; region++)
            {
                int color = graph.RegionColors[region];
                state.Set(color, state.Get(color) | (1UL << region));
            }
            return state;
        }

        private FloodFillSolverMove3D[] RunGreedyVariants(State64 initial)
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

        private FloodFillSolverMove3D[] RunSingleGreedy(State64 initial, int variant)
        {
            State64 state = initial;
            var path = new List<FloodFillSolverMove3D>(graph.RegionCount);
            while (!IsSolved(state) && path.Count < graph.RegionCount)
            {
                CollectMoves(state, moves);
                if (moves.Count == 0)
                {
                    return Array.Empty<FloodFillSolverMove3D>();
                }

                int bestIndex = 0;
                long bestScore = long.MinValue;
                for (int i = 0; i < moves.Count; i++)
                {
                    State64 child = Apply(state, moves[i]);
                    int componentCount = CountComponents(child, out int largestVoxels);
                    long score = -(long)componentCount * (variant % 2 == 0 ? 1000000L : 500000L) +
                        (long)largestVoxels * (variant % 3 + 1) * 100L -
                        ((moves[i].Target + variant) % graph.ColorCount);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                Move64 selected = moves[bestIndex];
                state = Apply(state, selected);
                path.Add(new FloodFillSolverMove3D(selected.Representative, selected.Target));
            }
            return IsSolved(state) ? path.ToArray() : Array.Empty<FloodFillSolverMove3D>();
        }

        private FloodFillSolverMove3D[] RunBeam(
            State64 initial, int upperBound, out int expanded)
        {
            expanded = 0;
            if (upperBound <= 1)
            {
                return Array.Empty<FloodFillSolverMove3D>();
            }

            traces.Clear();
            var current = new List<BeamNode>(settings.BeamWidth) { new BeamNode(initial, -1) };
            var next = new List<BeamNode>(settings.BeamWidth);
            var unique = new Dictionary<State64, BeamCandidate>(settings.BeamWidth * 4);
            var ranked = new List<BeamCandidate>(settings.BeamWidth * 4);
            Stopwatch watch = Stopwatch.StartNew();
            for (int depth = 0; depth < upperBound - 1; depth++)
            {
                unique.Clear();
                for (int nodeIndex = 0; nodeIndex < current.Count; nodeIndex++)
                {
                    if ((expanded & 127) == 0 &&
                        (cancellationToken.IsCancellationRequested ||
                         watch.ElapsedMilliseconds >= settings.BeamMilliseconds))
                    {
                        return Array.Empty<FloodFillSolverMove3D>();
                    }

                    BeamNode node = current[nodeIndex];
                    CollectMoves(node.State, moves);
                    for (int moveIndex = 0; moveIndex < moves.Count; moveIndex++)
                    {
                        Move64 move = moves[moveIndex];
                        State64 child = Apply(node.State, move);
                        expanded++;
                        if (IsSolved(child))
                        {
                            return Reconstruct(node.Trace, move);
                        }

                        int componentCount = CountComponents(child, out int largestVoxels);
                        long score = -(long)componentCount * 1000000L +
                            (long)largestVoxels * 100L;
                        var candidate = new BeamCandidate(
                            child, node.Trace, move, score);
                        if (!unique.TryGetValue(child, out BeamCandidate existing) ||
                            candidate.Score > existing.Score)
                        {
                            unique[child] = candidate;
                        }
                    }
                }

                ranked.Clear();
                foreach (BeamCandidate candidate in unique.Values)
                {
                    ranked.Add(candidate);
                }
                ranked.Sort((left, right) => right.Score.CompareTo(left.Score));
                next.Clear();
                int retained = Math.Min(settings.BeamWidth, ranked.Count);
                for (int i = 0; i < retained; i++)
                {
                    BeamCandidate candidate = ranked[i];
                    int trace = traces.Count;
                    traces.Add(new TraceNode(candidate.ParentTrace, new FloodFillSolverMove3D(
                        candidate.Move.Representative, candidate.Move.Target)));
                    next.Add(new BeamNode(candidate.State, trace));
                }
                if (next.Count == 0)
                {
                    break;
                }
                List<BeamNode> swap = current;
                current = next;
                next = swap;
            }
            return Array.Empty<FloodFillSolverMove3D>();
        }

        private FloodFillSolverMove3D[] RunExact(
            State64 initial,
            FloodFillSolverMove3D[] upperSolution,
            out bool proven,
            out int expanded)
        {
            proven = false;
            expanded = 0;
            if (upperSolution.Length == 0)
            {
                return Array.Empty<FloodFillSolverMove3D>();
            }

            int lowerBound = Math.Max(1, CountDistinctColors(initial) - 1);
            var transposition = new Dictionary<State64, int>(8192);
            var path = new FloodFillSolverMove3D[upperSolution.Length];
            var moveBuffers = new List<Move64>[upperSolution.Length + 1];
            for (int i = 0; i < moveBuffers.Length; i++)
            {
                moveBuffers[i] = new List<Move64>(256);
            }
            Stopwatch watch = Stopwatch.StartNew();
            for (int bound = lowerBound; bound < upperSolution.Length; bound++)
            {
                transposition.Clear();
                bool aborted = false;
                if (DepthFirstSearch(
                    initial, 0, bound, path, moveBuffers, transposition,
                    watch, ref expanded, ref aborted, out int solvedDepth))
                {
                    var result = new FloodFillSolverMove3D[solvedDepth];
                    Array.Copy(path, result, solvedDepth);
                    proven = true;
                    return result;
                }
                if (aborted)
                {
                    return Array.Empty<FloodFillSolverMove3D>();
                }
            }

            proven = true;
            return Array.Empty<FloodFillSolverMove3D>();
        }

        private bool DepthFirstSearch(
            State64 state,
            int depth,
            int bound,
            FloodFillSolverMove3D[] path,
            List<Move64>[] moveBuffers,
            Dictionary<State64, int> transposition,
            Stopwatch watch,
            ref int expanded,
            ref bool aborted,
            out int solvedDepth)
        {
            solvedDepth = 0;
            if (IsSolved(state))
            {
                solvedDepth = depth;
                return true;
            }
            int heuristic = CountDistinctColors(state) - 1;
            if (depth + heuristic > bound)
            {
                return false;
            }
            if (expanded >= settings.ExactNodes ||
                watch.ElapsedMilliseconds >= settings.ExactMilliseconds ||
                cancellationToken.IsCancellationRequested)
            {
                aborted = true;
                return false;
            }

            int remaining = bound - depth;
            if (transposition.TryGetValue(state, out int previousRemaining) &&
                previousRemaining >= remaining)
            {
                return false;
            }
            transposition[state] = remaining;
            expanded++;

            List<Move64> buffer = moveBuffers[depth];
            CollectMoves(state, buffer);
            buffer.Sort((left, right) =>
            {
                State64 leftState = Apply(state, left);
                State64 rightState = Apply(state, right);
                return CountComponents(leftState, out _).CompareTo(
                    CountComponents(rightState, out _));
            });
            for (int i = 0; i < buffer.Count; i++)
            {
                Move64 move = buffer[i];
                path[depth] = new FloodFillSolverMove3D(move.Representative, move.Target);
                if (DepthFirstSearch(
                    Apply(state, move), depth + 1, bound, path, moveBuffers,
                    transposition, watch, ref expanded, ref aborted, out solvedDepth))
                {
                    return true;
                }
                if (aborted)
                {
                    return false;
                }
            }
            return false;
        }

        private void CollectMoves(State64 state, List<Move64> output)
        {
            output.Clear();
            for (int source = 0; source < graph.ColorCount; source++)
            {
                ulong remaining = state.Get(source);
                while (remaining != 0)
                {
                    int representative = LowestBitIndex(remaining);
                    ulong component = FloodComponent(1UL << representative, remaining);
                    remaining &= ~component;
                    ulong boundary = CollectAdjacent(component) & ~component;
                    for (int target = 0; target < graph.ColorCount; target++)
                    {
                        if (target != source && (boundary & state.Get(target)) != 0)
                        {
                            output.Add(new Move64(
                                component, representative, source, target));
                        }
                    }
                }
            }
        }

        private State64 Apply(State64 state, Move64 move)
        {
            state.Set(move.Source, state.Get(move.Source) & ~move.Component);
            state.Set(move.Target, state.Get(move.Target) | move.Component);
            return state;
        }

        private ulong FloodComponent(ulong frontier, ulong allowed)
        {
            ulong component = frontier;
            while (frontier != 0)
            {
                ulong adjacentMask = CollectAdjacent(frontier);
                frontier = adjacentMask & allowed & ~component;
                component |= frontier;
            }
            return component;
        }

        private ulong CollectAdjacent(ulong regions)
        {
            ulong result = 0;
            while (regions != 0)
            {
                ulong bit = regions & (~regions + 1UL);
                result |= adjacency[LowestBitIndex(bit)];
                regions &= regions - 1UL;
            }
            return result;
        }

        private int CountComponents(State64 state, out int largestVoxels)
        {
            int count = 0;
            largestVoxels = 0;
            for (int color = 0; color < graph.ColorCount; color++)
            {
                ulong remaining = state.Get(color);
                while (remaining != 0)
                {
                    ulong component = FloodComponent(
                        remaining & (~remaining + 1UL), remaining);
                    remaining &= ~component;
                    count++;
                    int voxels = 0;
                    ulong members = component;
                    while (members != 0)
                    {
                        int region = LowestBitIndex(members);
                        voxels += graph.RegionVoxelCounts[region];
                        members &= members - 1UL;
                    }
                    largestVoxels = Math.Max(largestVoxels, voxels);
                }
            }
            return count;
        }

        private int CountDistinctColors(State64 state)
        {
            int count = 0;
            for (int color = 0; color < graph.ColorCount; color++)
            {
                if (state.Get(color) != 0)
                {
                    count++;
                }
            }
            return count;
        }

        private bool IsSolved(State64 state)
        {
            for (int color = 0; color < graph.ColorCount; color++)
            {
                if (state.Get(color) == allRegions)
                {
                    return true;
                }
            }
            return false;
        }

        private int FindColor(State64 state, int region)
        {
            if (region < 0 || region >= graph.RegionCount)
            {
                return -1;
            }
            ulong bit = 1UL << region;
            for (int color = 0; color < graph.ColorCount; color++)
            {
                if ((state.Get(color) & bit) != 0)
                {
                    return color;
                }
            }
            return -1;
        }

        private FloodFillSolverMove3D[] Reconstruct(int parentTrace, Move64 finalMove)
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

        private static int LowestBitIndex(ulong value)
        {
            int index = 0;
            while ((value & 1UL) == 0)
            {
                value >>= 1;
                index++;
            }
            return index;
        }
    }
}
