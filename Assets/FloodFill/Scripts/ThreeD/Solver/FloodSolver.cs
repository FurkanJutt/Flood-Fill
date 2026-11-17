using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace FloodFill.ThreeD.Solver
{
    // One engine for runtime generation and Editor analysis. No Unity API calls.
    internal sealed class FloodSolver
    {
        internal sealed class Node
        {
            internal FloodSolverState State;
            internal Node Parent;
            internal FloodFillSolverMove3D Move;
            internal int Depth, First;
            internal double Rank;
        }
        private readonly FloodFillRegionGraph3D graph;
        private readonly MoveSolverRuntimeSettings settings;
        private readonly CancellationToken token;
        private readonly FloodMoveSimulator simulator;
        private readonly Stopwatch watch = Stopwatch.StartNew();
        private readonly FloodFillSolverResult result = new FloodFillSolverResult();
        private FloodFillSolverMove3D[] best;
        private long phaseDeadline;
        private int phaseNodes;
        private bool interrupted;
        private readonly long memoryLimit;
        private long peak;
        internal FloodSolver(FloodFillRegionGraph3D graph, MoveSolverRuntimeSettings settings, CancellationToken token)
        {
            this.graph = graph; this.settings = settings; this.token = token;
            simulator = new FloodMoveSimulator(graph);
            memoryLimit = (long)settings.MaximumSearchMemoryMegabytes * 1024 * 1024;
        }
        internal FloodFillSolverResult Run(bool fallbackOnly)
        {
            if (!graph.IsConnected) throw new InvalidOperationException("Occupied voxels are disconnected; one captured component cannot cover the board.");
            token.ThrowIfCancellationRequested();
            byte[] initial = (byte[])graph.RegionColors.Clone();
            // One monotone completion is established first, then all phases retain the incumbent.
            Accept(Complete(initial, null, 2, false));
            result.GreedyMoveCount = best.Length;
            double start = watch.Elapsed.TotalMilliseconds;
            phaseDeadline = watch.ElapsedMilliseconds + settings.GreedyMilliseconds;
            for (int strategy = 0; !fallbackOnly && strategy < settings.GreedyRuns && !Stopped(); strategy++)
            {
                Accept(Complete(initial, null, strategy, true));
            }
            result.GreedyMoveCount = best.Length;
            result.GreedyMilliseconds = watch.Elapsed.TotalMilliseconds - start;
            result.BeamMoveCount = best.Length;
            if (!fallbackOnly && !token.IsCancellationRequested)
            {
                start = watch.Elapsed.TotalMilliseconds;
                phaseDeadline = watch.ElapsedMilliseconds + settings.BeamMilliseconds;
                phaseNodes = 0;
                Beam(initial);
                result.BeamMilliseconds = watch.Elapsed.TotalMilliseconds - start;
                result.BeamMoveCount = best.Length;
            }
            if (!fallbackOnly && !token.IsCancellationRequested && graph.RegionCount <= settings.ExactRegionThreshold)
            {
                start = watch.Elapsed.TotalMilliseconds;
                phaseDeadline = watch.ElapsedMilliseconds + settings.ExactMilliseconds;
                phaseNodes = 0; interrupted = false;
                int bound = FloodHeuristics.LowerBound(initial, graph.ColorCount);
                var path = new List<FloodFillSolverMove3D>();
                while (bound < best.Length && !Stopped())
                {
                    var visited = new Dictionary<FloodSolverState, int>();
                    Exact(new FloodSolverState(initial), 0, bound, path, visited);
                    if (interrupted) break;
                    bound++;
                }
                result.IsProvenOptimal = !interrupted && !token.IsCancellationRequested && bound >= best.Length;
                result.ExactMilliseconds = watch.Elapsed.TotalMilliseconds - start;
                if (result.IsProvenOptimal) result.ExactMoveCount = best.Length;
            }
            // The root color lower bound can prove even a large-board incumbent.
            if (best.Length == FloodHeuristics.LowerBound(initial, graph.ColorCount)) result.IsProvenOptimal = true;
            result.BestMoveCount = best.Length;
            result.BestColorSequence = new int[best.Length]; result.BestRegionSequence = new int[best.Length];
            for (int i = 0; i < best.Length; i++) { result.BestColorSequence[i] = best[i].ColorIndex; result.BestRegionSequence[i] = best[i].RegionId; }
            result.SolutionValidated = FloodSolverValidator.Validate(graph, best, out _);
            result.RegionCount = graph.RegionCount; result.PlayableVoxelCount = graph.TotalVoxelCount;
            result.DistinctColorCount = FloodHeuristics.LowerBound(initial, graph.ColorCount) + 1;
            result.ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds + graph.BuildMilliseconds;
            result.GraphBuildMilliseconds = graph.BuildMilliseconds;
            result.EstimatedPeakSearchMemoryBytes = peak;
            result.SearchAlgorithm = "Free-flood portfolio + memoized lookahead + bounded beam + IDA*";
            result.UsedFallback = fallbackOnly;
            result.SearchTerminationReason = token.IsCancellationRequested ? "Cancelled; validated incumbent retained."
                : result.IsProvenOptimal ? "OPTIMAL: admissible bound/exhaustive search proves minimum."
                : fallbackOnly ? "Validated monotone completion."
                : "VALIDATED: configured search limits reached; optimality unproven.";
            return result;
        }
        private bool Stopped()
        {
            return token.IsCancellationRequested || watch.ElapsedMilliseconds >= phaseDeadline || phaseNodes >= settings.ExactNodes;
        }
        private void Accept(FloodFillSolverMove3D[] candidate)
        {
            if (candidate == null || (best != null && candidate.Length >= best.Length)) return;
            if (!FloodSolverValidator.Validate(graph, candidate, out _)) throw new InvalidOperationException("Search produced an invalid candidate.");
            best = candidate;
        }
        private FloodFillSolverMove3D[] Complete(byte[] initial, Node prefix, int strategy, bool bounded)
        {
            byte[] state = initial;
            var path = prefix == null ? new List<FloodFillSolverMove3D>() : new List<FloodFillSolverMove3D>(Trace(prefix));
            while (FloodHeuristics.LowerBound(state, graph.ColorCount) > 0)
            {
                if (bounded && Stopped()) return null;
                if (!bounded) token.ThrowIfCancellationRequested();
                if (best != null && path.Count >= best.Length) return null;
                List<FloodCandidate> moves = FloodHeuristics.Moves(graph, state, strategy);
                if (moves.Count == 0) return null;
                var chosen = moves[0].Move;
                if (bounded && strategy >= 5 && settings.LookaheadDepth > 1)
                {
                    int remaining = settings.LookaheadNodeLimit;
                    var cache = new Dictionary<FloodSolverState, double[]>();
                    double score = double.MaxValue;
                    int count = Math.Min(moves.Count, Math.Max(2, settings.LookaheadBranchLimit));
                    for (int i = 0; i < count && !Stopped(); i++)
                    {
                        byte[] next = simulator.Apply(state, moves[i].Move);
                        double value = Lookahead(next, settings.LookaheadDepth - 1, ref remaining, cache);
                        if (value < score) { score = value; chosen = moves[i].Move; }
                    }
                }
                state = simulator.Apply(state, chosen); path.Add(chosen); result.ExploredStates++;
            }
            return path.ToArray();
        }
        private double Lookahead(byte[] state, int depth, ref int nodes, Dictionary<FloodSolverState, double[]> cache)
        {
            if (FloodHeuristics.LowerBound(state, graph.ColorCount) == 0) return 0;
            if (depth == 0 || nodes-- <= 0 || Stopped()) return FloodHeuristics.Estimate(graph, state);
            var key = new FloodSolverState(state);
            if (cache.TryGetValue(key, out double[] entry) && !double.IsNaN(entry[depth])) { result.DuplicateStatesSkipped++; return entry[depth]; }
            List<FloodCandidate> moves = FloodHeuristics.Moves(graph, state, 5);
            double score = double.MaxValue;
            for (int i = 0; i < Math.Min(moves.Count, settings.LookaheadBranchLimit) && nodes > 0; i++)
                score = Math.Min(score, 1 + Lookahead(simulator.Apply(state, moves[i].Move), depth - 1, ref nodes, cache));
            if (score == double.MaxValue) score = FloodHeuristics.Estimate(graph, state);
            if (entry == null) { entry = new double[settings.LookaheadDepth + 1]; Array.Fill(entry, double.NaN); cache.Add(key, entry); }
            entry[depth] = score; return score;
        }
        private void Beam(byte[] initial)
        {
            long bytesPerNode = graph.RegionCount + 160L;
            // Includes retained parent traces for an entire monotone path, plus candidate buffers.
            int width = (int)Math.Min(settings.BeamWidth, memoryLimit / Math.Max(1, bytesPerNode * (graph.RegionCount + 8L)));
            if (width < 1) return;
            int capacity = Math.Max(width, width * 4);
            var beam = new List<Node> { new Node { State = new FloodSolverState(initial), First = -1 } };
            for (int depth = 0; beam.Count > 0 && depth + 1 < best.Length && !Stopped(); depth++)
            {
                var candidates = new List<Node>(capacity + 1);
                var keys = new HashSet<FloodSolverState>();
                foreach (Node parent in beam)
                {
                    var moves = FloodHeuristics.Moves(graph, parent.State.Colors, 4);
                    for (int i = 0; i < moves.Count; i++)
                    {
                        if (Stopped()) break;
                        result.ExploredStates++; phaseNodes++;
                        var next = new FloodSolverState(simulator.Apply(parent.State.Colors, moves[i].Move));
                        int lower = FloodHeuristics.LowerBound(next.Colors, graph.ColorCount);
                        if (parent.Depth + 1 + lower >= best.Length) continue;
                        if (!keys.Add(next)) { result.DuplicateStatesSkipped++; continue; }
                        result.UniqueStates++;
                        var child = new Node { State = next, Parent = parent, Move = moves[i].Move, Depth = parent.Depth + 1,
                            First = parent.First < 0 ? moves[i].Move.ColorIndex : parent.First };
                        if (lower == 0) { Accept(Trace(child)); continue; }
                        child.Rank = child.Depth + FloodHeuristics.Estimate(graph, next.Colors);
                        candidates.Add(child);
                        if (candidates.Count > capacity)
                        {
                            candidates.Sort(Compare);
                            Node removed = candidates[candidates.Count - 1]; candidates.RemoveAt(candidates.Count - 1); keys.Remove(removed.State);
                        }
                    }
                    if (Stopped()) break;
                }
                if (candidates.Count == 0) break;
                candidates.Sort(Compare);
                var selected = new List<Node>(width); var selectedKeys = new HashSet<FloodSolverState>();
                // Reserve a small quota for each first color, then fill by total-move estimate.
                var colors = new HashSet<int>();
                for (int i = 0; i < candidates.Count && selected.Count < width / 4; i++)
                    if (colors.Add(candidates[i].First)) { selected.Add(candidates[i]); selectedKeys.Add(candidates[i].State); }
                for (int i = 0; i < candidates.Count && selected.Count < width; i++)
                    if (selectedKeys.Add(candidates[i].State)) selected.Add(candidates[i]);
                beam = selected;
                peak = Math.Max(peak, bytesPerNode * (width * (depth + 2L) + capacity));
                // Completions allow improvement long before the beam reaches a terminal depth.
                if (!Stopped()) Accept(Complete(candidates[0].State.Colors, candidates[0], 2, true));
            }
        }
        private static int Compare(Node a, Node b)
        {
            int c = a.Rank.CompareTo(b.Rank); if (c != 0) return c;
            // Stable total ordering, independent of hash-table iteration order.
            for (int i = 0; i < a.State.Colors.Length; i++) { c = a.State.Colors[i].CompareTo(b.State.Colors[i]); if (c != 0) return c; }
            return 0;
        }
        private void Exact(FloodSolverState state, int used, int bound, List<FloodFillSolverMove3D> path, Dictionary<FloodSolverState, int> visited)
        {
            if (Stopped()) { interrupted = true; return; }
            int lower = FloodHeuristics.LowerBound(state.Colors, graph.ColorCount);
            if (used + lower > bound || used + lower >= best.Length) return;
            if (lower == 0) { Accept(path.ToArray()); return; }
            if (visited.TryGetValue(state, out int previous) && previous <= used) { result.DuplicateStatesSkipped++; return; }
            long estimated = (visited.Count + 1L) * (graph.RegionCount + 128L);
            if (estimated > memoryLimit) { interrupted = true; return; }
            peak = Math.Max(peak, estimated); visited[state] = used;
            result.UniqueStates++; result.ExploredStates++; phaseNodes++;
            foreach (FloodCandidate candidate in FloodHeuristics.Moves(graph, state.Colors, 4, true))
            {
                path.Add(candidate.Move);
                Exact(new FloodSolverState(simulator.Apply(state.Colors, candidate.Move)), used + 1, bound, path, visited);
                path.RemoveAt(path.Count - 1);
                if (interrupted) return;
            }
        }
        private static FloodFillSolverMove3D[] Trace(Node node)
        {
            var path = new FloodFillSolverMove3D[node.Depth];
            for (int i = path.Length - 1; i >= 0; i--) { path[i] = node.Move; node = node.Parent; }
            return path;
        }
    }
}
