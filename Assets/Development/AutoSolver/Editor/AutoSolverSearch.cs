using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace FloodFill.Development.AutoSolver
{
    internal static class AutoSolverSearch
    {
        private sealed class Node
        {
            public byte[] Colors;
            public Node Parent;
            public AutoSolverMove Move;
            public int Depth;
            public long Score;
        }

        private sealed class StateKey : IEquatable<StateKey>
        {
            private readonly byte[] state;
            private readonly int hash;

            public StateKey(byte[] state)
            {
                this.state = state;
                unchecked
                {
                    uint value = 2166136261;
                    for (int i = 0; i < state.Length; i++) value = (value ^ state[i]) * 16777619;
                    hash = (int)value;
                }
            }

            public bool Equals(StateKey other)
            {
                if (ReferenceEquals(this, other)) return true;
                if (other == null || hash != other.hash || state.Length != other.state.Length) return false;
                for (int i = 0; i < state.Length; i++) if (state[i] != other.state[i]) return false;
                return true;
            }
            public override bool Equals(object obj) => Equals(obj as StateKey);
            public override int GetHashCode() => hash;
        }

        public static AutoSolverResult Solve(
            AutoSolverGraph graph,
            int remainingMoveLimit,
            int timeBudgetMilliseconds,
            int expandedStateBudget,
            int beamWidth,
            CancellationToken cancellationToken)
        {
            var result = new AutoSolverResult
            {
                Conclusion = AutoSolverConclusion.Inconclusive,
                RegionCount = graph != null ? graph.RegionCount : 0,
                PlayableVoxels = graph != null ? graph.TotalVoxels : 0,
                RemainingMoveLimit = remainingMoveLimit
            };
            if (graph == null || graph.RegionCount == 0 || graph.Neighbors == null)
            {
                result.Conclusion = AutoSolverConclusion.Invalid;
                result.Message = "The color-region graph is empty.";
                return result;
            }

            Stopwatch watch = Stopwatch.StartNew();
            int maxTime = Math.Max(100, timeBudgetMilliseconds);
            int maxStates = Math.Max(100, expandedStateBudget);
            int width = Math.Max(4, beamWidth);
            byte[] initial = (byte[])graph.RegionColors.Clone();

            if (graph.ConnectedComponentCount != 1)
            {
                result.Conclusion = AutoSolverConclusion.ProvenNoSolution;
                result.SearchWasExhaustive = true;
                result.Message = "The playable voxels are disconnected, so the game cannot capture the entire board.";
                result.CalculationMilliseconds = watch.Elapsed.TotalMilliseconds;
                return result;
            }
            if (IsSolved(initial))
            {
                result.Conclusion = AutoSolverConclusion.Solved;
                result.IsProvenOptimal = true;
                result.ValidationPassed = true;
                result.Message = "The current board is already solved.";
                result.CalculationMilliseconds = watch.Elapsed.TotalMilliseconds;
                return result;
            }

            AutoSolverResult greedy = GreedySearch(graph, initial);

            bool exactComplete = false;
            if (graph.RegionCount <= 18)
            {
                AutoSolverResult exact = ExactBreadthFirst(
                    graph, initial, remainingMoveLimit, maxTime, maxStates, cancellationToken, watch);
                result.ExploredStates = exact.ExploredStates;
                if (exact.Conclusion == AutoSolverConclusion.Solved ||
                    exact.Conclusion == AutoSolverConclusion.ProvenNoSolution ||
                    exact.Conclusion == AutoSolverConclusion.Cancelled)
                {
                    exact.RegionCount = graph.RegionCount;
                    exact.PlayableVoxels = graph.TotalVoxels;
                    exact.RemainingMoveLimit = remainingMoveLimit;
                    exact.CalculationMilliseconds = watch.Elapsed.TotalMilliseconds;
                    return exact;
                }
                exactComplete = exact.SearchWasExhaustive;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                result.Conclusion = AutoSolverConclusion.Cancelled;
                result.Message = "Search cancelled.";
                result.CalculationMilliseconds = watch.Elapsed.TotalMilliseconds;
                return result;
            }

            AutoSolverResult beam = BeamSearch(
                graph, initial, remainingMoveLimit, maxTime, maxStates,
                width, result.ExploredStates, cancellationToken, watch);
            beam.RegionCount = graph.RegionCount;
            beam.PlayableVoxels = graph.TotalVoxels;
            beam.RemainingMoveLimit = remainingMoveLimit;
            beam.CalculationMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (beam.Conclusion == AutoSolverConclusion.Inconclusive && exactComplete)
            {
                beam.Conclusion = AutoSolverConclusion.ProvenNoSolution;
                beam.SearchWasExhaustive = true;
                beam.Message = "All reachable states were exhausted.";
            }
            else if (beam.Conclusion == AutoSolverConclusion.Inconclusive &&
                     greedy.Conclusion == AutoSolverConclusion.Solved && greedy.ValidationPassed)
            {
                greedy.RegionCount = graph.RegionCount;
                greedy.PlayableVoxels = graph.TotalVoxels;
                greedy.RemainingMoveLimit = remainingMoveLimit;
                greedy.ExploredStates += beam.ExploredStates;
                greedy.CalculationMilliseconds = watch.Elapsed.TotalMilliseconds;
                greedy.Message = "A validated greedy path was found; bounded search did not prove optimality.";
                return greedy;
            }
            return beam;
        }

        private static AutoSolverResult GreedySearch(AutoSolverGraph graph, byte[] initial)
        {
            var result = new AutoSolverResult { Conclusion = AutoSolverConclusion.Inconclusive };
            var path = new List<AutoSolverMove>(Math.Max(1, graph.RegionCount - 1));
            byte[] state = (byte[])initial.Clone();
            int maximumMoves = Math.Max(1, graph.RegionCount);
            while (!IsSolved(state) && path.Count < maximumMoves)
            {
                List<AutoSolverMove> moves = CollectMoves(graph, state);
                if (moves.Count == 0)
                {
                    result.Message = "No legal adjacent color merge was available.";
                    return result;
                }
                AutoSolverMove move = moves[0];
                state = ApplyMove(graph, state, move);
                path.Add(move);
            }

            if (!IsSolved(state))
            {
                result.Message = "The greedy path exceeded the region reduction bound.";
                return result;
            }
            result.Moves = path.ToArray();
            result.ValidationPassed = TryValidate(graph, initial, result.Moves, out result.CapturedPerMove);
            result.Conclusion = result.ValidationPassed ? AutoSolverConclusion.Solved : AutoSolverConclusion.Invalid;
            result.Message = result.ValidationPassed
                ? "A valid path was found; optimality is not proven."
                : "The greedy path failed independent simulation validation.";
            result.ExploredStates = path.Count;
            return result;
        }

        private static AutoSolverResult ExactBreadthFirst(
            AutoSolverGraph graph,
            byte[] initial,
            int moveLimit,
            int timeBudget,
            int stateBudget,
            CancellationToken cancellationToken,
            Stopwatch watch)
        {
            var result = new AutoSolverResult { Conclusion = AutoSolverConclusion.Inconclusive };
            var root = new Node { Colors = initial, Depth = 0 };
            var queue = new Queue<Node>();
            queue.Enqueue(root);
            var visited = new HashSet<StateKey> { new StateKey(initial) };
            int maxDepth = Math.Max(0, graph.RegionCount - 1);
            while (queue.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Conclusion = AutoSolverConclusion.Cancelled;
                    result.Message = "Search cancelled.";
                    return result;
                }
                if (result.ExploredStates >= stateBudget || watch.ElapsedMilliseconds >= timeBudget)
                {
                    result.Message = "Exact search reached its configured budget.";
                    return result;
                }

                Node node = queue.Dequeue();
                result.ExploredStates++;
                if (node.Depth >= maxDepth) continue;
                List<AutoSolverMove> moves = CollectMoves(graph, node.Colors);
                for (int i = 0; i < moves.Count; i++)
                {
                    byte[] next = ApplyMove(graph, node.Colors, moves[i]);
                    var key = new StateKey(next);
                    if (!visited.Add(key)) continue;
                    var child = new Node
                    {
                        Colors = next,
                        Parent = node,
                        Move = moves[i],
                        Depth = node.Depth + 1
                    };
                    if (IsSolved(next))
                    {
                        result.Conclusion = AutoSolverConclusion.Solved;
                        result.IsProvenOptimal = true;
                        result.ValidationPassed = TryValidate(graph, initial, Reconstruct(child), out int[] captured);
                        result.Moves = Reconstruct(child);
                        result.CapturedPerMove = captured;
                        result.Message = result.ValidationPassed
                            ? "Exact breadth-first search found an optimal solution."
                            : "Exact search found a path that failed simulation validation.";
                        return result;
                    }
                    queue.Enqueue(child);
                }
            }

            result.Conclusion = AutoSolverConclusion.ProvenNoSolution;
            result.SearchWasExhaustive = true;
            result.Message = "Exact search exhausted every reachable state.";
            return result;
        }

        private static AutoSolverResult BeamSearch(
            AutoSolverGraph graph,
            byte[] initial,
            int moveLimit,
            int timeBudget,
            int stateBudget,
            int beamWidth,
            int previousExpanded,
            CancellationToken cancellationToken,
            Stopwatch watch)
        {
            var result = new AutoSolverResult { Conclusion = AutoSolverConclusion.Inconclusive };
            var beam = new List<Node> { new Node { Colors = initial, Depth = 0, Score = Evaluate(graph, initial) } };
            int candidateLimit = Math.Max(64, beamWidth * 8);
            int totalExpanded = previousExpanded;
            while (beam.Count > 0)
            {
                var candidates = new List<Node>(candidateLimit);
                var nextKeys = new HashSet<StateKey>();
                bool budgetReached = false;
                for (int b = 0; b < beam.Count && !budgetReached; b++)
                {
                    Node parent = beam[b];
                    List<AutoSolverMove> moves = CollectMoves(graph, parent.Colors);
                    for (int m = 0; m < moves.Count; m++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            result.Conclusion = AutoSolverConclusion.Cancelled;
                            result.ExploredStates = totalExpanded;
                            result.Message = "Search cancelled.";
                            return result;
                        }
                        if (totalExpanded >= stateBudget || watch.ElapsedMilliseconds >= timeBudget)
                        {
                            budgetReached = true;
                            break;
                        }

                        AutoSolverMove move = moves[m];
                        byte[] next = ApplyMove(graph, parent.Colors, move);
                        totalExpanded++;
                        if (IsSolved(next))
                        {
                            Node solved = new Node { Colors = next, Parent = parent, Move = move, Depth = parent.Depth + 1 };
                            result.Moves = Reconstruct(solved);
                            result.ValidationPassed = TryValidate(graph, initial, result.Moves, out result.CapturedPerMove);
                            result.ExploredStates = totalExpanded;
                            result.Conclusion = result.ValidationPassed
                                ? AutoSolverConclusion.Solved
                                : AutoSolverConclusion.Invalid;
                            result.Message = result.ValidationPassed
                                ? "Beam search found a valid solution; optimality is not proven."
                                : "The candidate solution failed independent simulation validation.";
                            return result;
                        }

                        var key = new StateKey(next);
                        if (!nextKeys.Add(key)) continue;
                        candidates.Add(new Node
                        {
                            Colors = next,
                            Parent = parent,
                            Move = move,
                            Depth = parent.Depth + 1,
                            Score = Evaluate(graph, next)
                        });
                        if (candidates.Count >= candidateLimit)
                        {
                            TrimCandidates(candidates, nextKeys, beamWidth * 4);
                        }
                    }
                }

                result.ExploredStates = totalExpanded;
                if (candidates.Count == 0) break;
                candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
                int keep = Math.Min(beamWidth, candidates.Count);
                beam = candidates.GetRange(0, keep);
                if (budgetReached) break;
            }

            result.Message = "Search budget expired before a solution was found.";
            return result;
        }

        private static void TrimCandidates(List<Node> candidates, HashSet<StateKey> keys, int keep)
        {
            candidates.Sort((a, b) => a.Score.CompareTo(b.Score));
            if (candidates.Count <= keep) return;
            candidates.RemoveRange(keep, candidates.Count - keep);
            keys.Clear();
            for (int i = 0; i < candidates.Count; i++) keys.Add(new StateKey(candidates[i].Colors));
        }

        private static List<AutoSolverMove> CollectMoves(AutoSolverGraph graph, byte[] colors)
        {
            var moves = new List<AutoSolverMove>();
            var visited = new bool[colors.Length];
            var queue = new int[colors.Length];
            var boundaryColors = new bool[graph.ColorCount];
            for (int start = 0; start < colors.Length; start++)
            {
                if (visited[start]) continue;
                byte source = colors[start];
                Array.Clear(boundaryColors, 0, boundaryColors.Length);
                int head = 0, tail = 0;
                queue[tail++] = start;
                visited[start] = true;
                while (head < tail)
                {
                    int current = queue[head++];
                    int[] adjacent = graph.Neighbors[current];
                    for (int i = 0; i < adjacent.Length; i++)
                    {
                        int next = adjacent[i];
                        if (colors[next] == source)
                        {
                            if (!visited[next])
                            {
                                visited[next] = true;
                                queue[tail++] = next;
                            }
                        }
                        else
                        {
                            boundaryColors[colors[next]] = true;
                        }
                    }
                }
                for (int color = 0; color < boundaryColors.Length; color++)
                    if (boundaryColors[color]) moves.Add(new AutoSolverMove(start, color));
            }
            return moves;
        }

        public static byte[] ApplyMove(AutoSolverGraph graph, byte[] state, AutoSolverMove move)
        {
            var next = (byte[])state.Clone();
            if (move.RegionId < 0 || move.RegionId >= state.Length || move.ColorIndex < 0 ||
                move.ColorIndex >= graph.ColorCount) return next;
            byte source = state[move.RegionId];
            if (source == move.ColorIndex) return next;
            var queue = new int[state.Length];
            var visited = new bool[state.Length];
            int head = 0, tail = 0;
            queue[tail++] = move.RegionId;
            visited[move.RegionId] = true;
            while (head < tail)
            {
                int current = queue[head++];
                next[current] = (byte)move.ColorIndex;
                int[] adjacent = graph.Neighbors[current];
                for (int i = 0; i < adjacent.Length; i++)
                {
                    int neighbor = adjacent[i];
                    if (!visited[neighbor] && state[neighbor] == source)
                    {
                        visited[neighbor] = true;
                        queue[tail++] = neighbor;
                    }
                }
            }
            return next;
        }

        public static bool TryValidate(
            AutoSolverGraph graph,
            byte[] initial,
            AutoSolverMove[] moves,
            out int[] capturedPerMove)
        {
            capturedPerMove = Array.Empty<int>();
            if (graph == null || initial == null || moves == null || initial.Length != graph.RegionCount) return false;
            var captures = new int[moves.Length];
            byte[] state = (byte[])initial.Clone();
            for (int i = 0; i < moves.Length; i++)
            {
                AutoSolverMove move = moves[i];
                if (move.RegionId < 0 || move.RegionId >= state.Length || move.ColorIndex < 0 ||
                    move.ColorIndex >= graph.ColorCount || state[move.RegionId] == move.ColorIndex ||
                    !HasAdjacentTarget(graph, state, move)) return false;
                state = ApplyMove(graph, state, move);
                captures[i] = CountCapturedFrom(graph, state, move.RegionId);
            }
            capturedPerMove = captures;
            return IsSolved(state) && (captures.Length == 0 || captures[captures.Length - 1] == graph.TotalVoxels);
        }

        public static int CountCapturedFrom(AutoSolverGraph graph, byte[] state, int region)
        {
            if (region < 0 || region >= state.Length) return 0;
            byte color = state[region];
            var visited = new bool[state.Length];
            var queue = new int[state.Length];
            int head = 0, tail = 0, captured = 0;
            queue[tail++] = region;
            visited[region] = true;
            while (head < tail)
            {
                int current = queue[head++];
                captured += graph.RegionVoxelCounts[current];
                for (int i = 0; i < graph.Neighbors[current].Length; i++)
                {
                    int next = graph.Neighbors[current][i];
                    if (visited[next] || state[next] != color) continue;
                    visited[next] = true;
                    queue[tail++] = next;
                }
            }
            return captured;
        }

        private static bool HasAdjacentTarget(AutoSolverGraph graph, byte[] state, AutoSolverMove move)
        {
            byte source = state[move.RegionId];
            var queue = new int[state.Length];
            var visited = new bool[state.Length];
            int head = 0, tail = 0;
            queue[tail++] = move.RegionId;
            visited[move.RegionId] = true;
            while (head < tail)
            {
                int current = queue[head++];
                for (int i = 0; i < graph.Neighbors[current].Length; i++)
                {
                    int next = graph.Neighbors[current][i];
                    if (state[next] == move.ColorIndex) return true;
                    if (!visited[next] && state[next] == source)
                    {
                        visited[next] = true;
                        queue[tail++] = next;
                    }
                }
            }
            return false;
        }

        private static bool IsSolved(byte[] state)
        {
            if (state.Length == 0) return false;
            byte color = state[0];
            for (int i = 1; i < state.Length; i++) if (state[i] != color) return false;
            return true;
        }

        private static long Evaluate(AutoSolverGraph graph, byte[] state)
        {
            var visited = new bool[state.Length];
            var queue = new int[state.Length];
            var colors = new bool[graph.ColorCount];
            int components = 0, distinctColors = 0, largestVoxelComponent = 0;
            for (int start = 0; start < state.Length; start++)
            {
                if (!colors[state[start]]) { colors[state[start]] = true; distinctColors++; }
                if (visited[start]) continue;
                components++;
                int head = 0, tail = 0, voxels = 0;
                byte color = state[start];
                queue[tail++] = start;
                visited[start] = true;
                while (head < tail)
                {
                    int current = queue[head++];
                    voxels += graph.RegionVoxelCounts[current];
                    for (int i = 0; i < graph.Neighbors[current].Length; i++)
                    {
                        int next = graph.Neighbors[current][i];
                        if (visited[next] || state[next] != color) continue;
                        visited[next] = true;
                        queue[tail++] = next;
                    }
                }
                largestVoxelComponent = Math.Max(largestVoxelComponent, voxels);
            }
            return components * 100000L + distinctColors * 1000L - largestVoxelComponent;
        }

        private static AutoSolverMove[] Reconstruct(Node node)
        {
            int count = node.Depth;
            var moves = new AutoSolverMove[count];
            for (int i = count - 1; i >= 0; i--)
            {
                moves[i] = node.Move;
                node = node.Parent;
            }
            return moves;
        }
    }
}
