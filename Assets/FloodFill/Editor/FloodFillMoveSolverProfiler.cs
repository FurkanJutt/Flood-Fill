using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Solver;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace FloodFill.EditorTools
{
    internal sealed class FloodFillMoveSolverProfiler : EditorWindow
    {
        private sealed class TestCase
        {
            public string Name;
            public FloodFillRegionGraph3D Graph;
            public int ReferenceMoves = -1;
            public bool IsPurpleYellowScenario;
        }

        private sealed class TestResult
        {
            public string Name;
            public string Error;
            public FloodFillSolverResult SolverResult;
            public double SolveMilliseconds;
            public long AllocatedBytes;
            public string StrategySweep;
        }

        private static readonly int[] Sizes = { 6, 8, 10, 12, 16 };
        private static readonly int[] Seeds = { 1103, 2207 };
        private readonly List<string> reports = new List<string>();
        private Vector2 scroll;
        private Task<TestResult[]> task;
        private CancellationTokenSource cancellation;
        private string status = "Ready. Builds fixed-seed Full Grid and procedural irregular test boards.";

        [MenuItem("Tools/Flood Fill/Profile Move Solver")]
        private static void Open()
        {
            GetWindow<FloodFillMoveSolverProfiler>("Move Solver Profile");
        }

        private void OnEnable() => EditorApplication.update += Poll;

        private void OnDisable()
        {
            EditorApplication.update -= Poll;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Flood Fill Move Solver", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(status, task == null ? MessageType.Info : MessageType.None);
            using (new EditorGUI.DisabledScope(task != null))
                if (GUILayout.Button("Run Fixed Seed Profile Suite", GUILayout.Height(28f))) RunSuite();
            using (new EditorGUI.DisabledScope(task == null))
                if (GUILayout.Button("Cancel", GUILayout.Height(22f))) cancellation?.Cancel();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < reports.Count; i++)
                EditorGUILayout.HelpBox(reports[i], reports[i].StartsWith("PASS", StringComparison.Ordinal)
                    ? MessageType.Info
                    : MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        private void RunSuite()
        {
            reports.Clear();
            var testCases = new List<TestCase>(Sizes.Length * Seeds.Length * 2 + 1);
            var shapeSettings = new ProceduralVoxelShapeSettings
            {
                useRandomSeed = false,
                logGenerationDetails = false,
                logGenerationPerformance = false
            };

            for (int sizeIndex = 0; sizeIndex < Sizes.Length; sizeIndex++)
            for (int seedIndex = 0; seedIndex < Seeds.Length; seedIndex++)
            {
                int size = Sizes[sizeIndex];
                int seed = Seeds[seedIndex];
                if (!ProceduralVoxelShapeGenerator.TryGenerate(
                        size, size, size, shapeSettings, seed, out ProceduralVoxelShapeResult shape))
                {
                    reports.Add($"FAIL {size}^3 procedural seed={seed}: shape generation failed.");
                    continue;
                }
                AddCase(testCases, $"{size}^3 irregular seed={seed}", shape.Mask, size, size, size, seed);
                AddCase(testCases, $"{size}^3 Full Grid seed={seed}", null, size, size, size, seed);
            }

            AddPurpleYellowScenario(testCases, false);
            AddPurpleYellowScenario(testCases, true);

            byte[,,] smallPattern = new byte[2, 2, 2];
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++) smallPattern[x, y, z] = (byte)((x + y + z) & 1);
            AddCase(testCases, "2^3 exact reference checkerboard", null, 2, 2, 2, 73, smallPattern);

            if (testCases.Count == 0)
            {
                status = "No valid test boards were generated.";
                return;
            }

            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            task = Task.Run(() => SolveCases(testCases, token), token);
            status = $"Running {testCases.Count} deterministic cases on a worker thread…";
        }

        private static void AddCase(
            List<TestCase> cases,
            string name,
            bool[,,] mask,
            int width,
            int height,
            int depth,
            int seed,
            byte[,,] explicitColors = null)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            for (int z = 0; z < depth; z++)
                if ((mask == null || mask[x, y, z]) && (explicitColors == null || explicitColors[x, y, z] < 6)) count++;

            int[] logicalIndices = new int[count];
            byte[] colors = new byte[count];
            var random = new System.Random(unchecked(seed ^ 0x5F3759DF));
            int cursor = 0;
            int startingVoxel = -1;
            int bestScore = int.MaxValue;
            int bestX = int.MaxValue, bestY = int.MaxValue, bestZ = int.MaxValue;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            for (int z = 0; z < depth; z++)
            {
                if (mask != null && !mask[x, y, z]) continue;
                int logical = x + width * (y + height * z);
                logicalIndices[cursor] = logical;
                colors[cursor] = explicitColors != null ? explicitColors[x, y, z] : (byte)random.Next(6);
                int score = x + y + z;
                if (score < bestScore || score == bestScore &&
                    (x < bestX || x == bestX && (y < bestY || y == bestY && z < bestZ)))
                {
                    bestScore = score;
                    bestX = x; bestY = y; bestZ = z;
                    startingVoxel = cursor;
                }
                cursor++;
            }

            var snapshot = new FloodFillBoardSnapshot3D(
                width, height, depth, 6, logicalIndices, colors, startingVoxel);
            if (!FloodFillRegionGraph3D.TryBuild(snapshot, out FloodFillRegionGraph3D graph))
            {
                cases.Add(new TestCase { Name = name + " (graph build failed)" });
                return;
            }
            if (!ValidateGraph(snapshot, graph, out string graphError))
            {
                cases.Add(new TestCase { Name = name + " (graph invalid: " + graphError + ")" });
                return;
            }

            int referenceMoves = graph.RegionCount <= 12 ? ReferenceExactMoveCount(graph) : -1;
            cases.Add(new TestCase { Name = name, Graph = graph, ReferenceMoves = referenceMoves });
        }

        private static bool ValidateGraph(
            FloodFillBoardSnapshot3D snapshot,
            FloodFillRegionGraph3D graph,
            out string error)
        {
            error = null;
            int volume = snapshot.Width * snapshot.Height * snapshot.Depth;
            int[] voxelAt = new int[volume];
            Array.Fill(voxelAt, -1);
            for (int i = 0; i < snapshot.VoxelCount; i++) voxelAt[snapshot.LogicalIndices[i]] = i;
            int[] regions = new int[snapshot.VoxelCount];
            Array.Fill(regions, -1);
            int[] counts = new int[snapshot.VoxelCount];
            byte[] regionColors = new byte[snapshot.VoxelCount];
            int[] representatives = new int[snapshot.VoxelCount];
            var queue = new int[snapshot.VoxelCount];
            int regionCount = 0;
            for (int start = 0; start < snapshot.VoxelCount; start++)
            {
                if (regions[start] >= 0) continue;
                int head = 0, tail = 0;
                byte color = snapshot.Colors[start];
                regions[start] = regionCount;
                queue[tail++] = start;
                while (head < tail)
                {
                    int voxel = queue[head++];
                    counts[regionCount]++;
                    int logical = snapshot.LogicalIndices[voxel];
                    int x = logical % snapshot.Width;
                    int yz = logical / snapshot.Width;
                    int y = yz % snapshot.Height;
                    int z = yz / snapshot.Height;
                    Visit(x + 1, y, z); Visit(x - 1, y, z);
                    Visit(x, y + 1, z); Visit(x, y - 1, z);
                    Visit(x, y, z + 1); Visit(x, y, z - 1);
                }
                regionColors[regionCount] = color;
                representatives[regionCount] = snapshot.LogicalIndices[start];
                regionCount++;

                void Visit(int nx, int ny, int nz)
                {
                    if (nx < 0 || nx >= snapshot.Width || ny < 0 || ny >= snapshot.Height || nz < 0 || nz >= snapshot.Depth) return;
                    int adjacent = voxelAt[nx + snapshot.Width * (ny + snapshot.Height * nz)];
                    if (adjacent < 0 || regions[adjacent] >= 0 || snapshot.Colors[adjacent] != color) return;
                    regions[adjacent] = regionCount;
                    queue[tail++] = adjacent;
                }
            }

            if (graph.RegionCount != regionCount)
            {
                error = "Connected region count differs from independent flood fill.";
                return false;
            }
            for (int region = 0; region < regionCount; region++)
            {
                if (graph.RegionColors[region] == regionColors[region] &&
                    graph.RegionVoxelCounts[region] == counts[region] &&
                    graph.RepresentativeLogicalIndices[region] == representatives[region]) continue;
                error = "Region color, size, or representative disagrees with independent flood fill.";
                return false;
            }
            var expectedEdges = new HashSet<ulong>();
            for (int voxel = 0; voxel < snapshot.VoxelCount; voxel++)
            {
                int logical = snapshot.LogicalIndices[voxel];
                int x = logical % snapshot.Width;
                int yz = logical / snapshot.Width;
                int y = yz % snapshot.Height;
                int z = yz / snapshot.Height;
                AddEdge(voxel, x + 1, y, z);
                AddEdge(voxel, x, y + 1, z);
                AddEdge(voxel, x, y, z + 1);
            }
            var actualEdges = new HashSet<ulong>();
            for (int region = 0; region < graph.RegionCount; region++)
            for (int edge = graph.NeighborOffsets[region]; edge < graph.NeighborOffsets[region + 1]; edge++)
            {
                int neighbor = graph.Neighbors[edge];
                if (region < neighbor) actualEdges.Add(Edge(region, neighbor));
            }
            if (!expectedEdges.SetEquals(actualEdges))
            {
                error = "Six-direction region adjacency differs from independently derived voxel adjacency.";
                return false;
            }
            return true;

            void AddEdge(int currentVoxel, int nx, int ny, int nz)
            {
                if (nx >= snapshot.Width || ny >= snapshot.Height || nz >= snapshot.Depth) return;
                int neighbor = voxelAt[nx + snapshot.Width * (ny + snapshot.Height * nz)];
                if (neighbor >= 0 && regions[currentVoxel] != regions[neighbor])
                    expectedEdges.Add(Edge(regions[currentVoxel], regions[neighbor]));
            }
        }

        private static ulong Edge(int left, int right)
        {
            uint low = (uint)Math.Min(left, right);
            uint high = (uint)Math.Max(left, right);
            return ((ulong)low << 32) | high;
        }

        private static int ReferenceExactMoveCount(FloodFillRegionGraph3D graph)
        {
            byte[] initial = (byte[])graph.RegionColors.Clone();
            var queue = new Queue<byte[]>();
            var depth = new Dictionary<string, int>();
            string initialKey = Convert.ToBase64String(initial);
            queue.Enqueue(initial);
            depth.Add(initialKey, 0);
            while (queue.Count > 0)
            {
                byte[] state = queue.Dequeue();
                string key = Convert.ToBase64String(state);
                int currentDepth = depth[key];
                bool solved = true;
                for (int i = 1; i < state.Length; i++) if (state[i] != state[0]) { solved = false; break; }
                if (solved) return currentDepth;

                var visited = new bool[state.Length];
                var componentQueue = new int[state.Length];
                for (int start = 0; start < state.Length; start++)
                {
                    if (visited[start]) continue;
                    byte source = state[start];
                    int head = 0, tail = 0;
                    componentQueue[tail++] = start;
                    visited[start] = true;
                    var targets = new bool[graph.ColorCount];
                    while (head < tail)
                    {
                        int current = componentQueue[head++];
                        for (int edge = graph.NeighborOffsets[current]; edge < graph.NeighborOffsets[current + 1]; edge++)
                        {
                            int neighbor = graph.Neighbors[edge];
                            if (state[neighbor] == source)
                            {
                                if (!visited[neighbor]) { visited[neighbor] = true; componentQueue[tail++] = neighbor; }
                            }
                            else targets[state[neighbor]] = true;
                        }
                    }
                    for (int target = 0; target < targets.Length; target++)
                    {
                        if (!targets[target]) continue;
                        byte[] next = (byte[])state.Clone();
                        for (int i = 0; i < tail; i++) next[componentQueue[i]] = (byte)target;
                        string nextKey = Convert.ToBase64String(next);
                        if (depth.ContainsKey(nextKey)) continue;
                        depth.Add(nextKey, currentDepth + 1);
                        queue.Enqueue(next);
                    }
                }
                }
            return -1;
        }

        private static void AddPurpleYellowScenario(List<TestCase> cases, bool reverse)
        {
            const int width = 6;
            const int height = 6;
            var occupied = new Dictionary<int, byte>();
            for (int y = 0; y < 5; y++) occupied[2 + width * y] = 0;
            for (int y = 0; y < 5; y++) occupied[3 + width * y] = 1;
            occupied[4 + width * 2] = 1;
            for (int y = 1; y <= 3; y++) occupied[1 + width * y] = 2;
            occupied[2 + width * 5] = 3;
            occupied[3 + width * 5] = 3;
            for (int y = 2; y <= 4; y++) occupied[5 + width * y] = 4;

            var logical = new List<int>(occupied.Keys);
            logical.Sort();
            if (reverse) logical.Reverse();
            var colors = new byte[logical.Count];
            int startingVoxel = -1;
            for (int i = 0; i < logical.Count; i++)
            {
                colors[i] = occupied[logical[i]];
                if (logical[i] == 2) startingVoxel = i;
            }

            var snapshot = new FloodFillBoardSnapshot3D(
                width, height, 1, 5, logical.ToArray(), colors, startingVoxel);
            string name = "Purple/yellow strategic case" + (reverse ? " (reversed voxel order)" : string.Empty);
            if (!FloodFillRegionGraph3D.TryBuild(snapshot, out FloodFillRegionGraph3D graph))
            {
                cases.Add(new TestCase { Name = name + " (graph build failed)" });
                return;
            }
            cases.Add(new TestCase { Name = name, Graph = graph, IsPurpleYellowScenario = true });
        }

        private static int CountCapturedVoxels(
            FloodFillRegionGraph3D graph, byte[] state, int representative)
        {
            var visited = new bool[graph.RegionCount];
            var queue = new int[graph.RegionCount];
            int head = 0, tail = 0, capturedVoxels = 0;
            int source = state[representative];
            queue[tail++] = representative;
            visited[representative] = true;
            while (head < tail)
            {
                int region = queue[head++];
                capturedVoxels += graph.RegionVoxelCounts[region];
                for (int edge = graph.NeighborOffsets[region]; edge < graph.NeighborOffsets[region + 1]; edge++)
                {
                    int neighbor = graph.Neighbors[edge];
                    if (visited[neighbor] || state[neighbor] != source) continue;
                    visited[neighbor] = true;
                    queue[tail++] = neighbor;
                }
            }
            return capturedVoxels;
        }

        private static MoveSolverSettings CopySettings(MoveSolverSettings source)
        {
            return new MoveSolverSettings
            {
                exactSolverRegionThreshold = source.exactSolverRegionThreshold,
                beamWidth = source.beamWidth,
                greedyRuns = source.greedyRuns,
                runtimeExactSearchTimeBudgetMs = source.runtimeExactSearchTimeBudgetMs,
                developmentExactSearchTimeBudgetMs = source.developmentExactSearchTimeBudgetMs,
                exactMaxExpandedStates = source.exactMaxExpandedStates,
                beamSearchTimeBudgetMs = source.beamSearchTimeBudgetMs,
                greedySearchTimeBudgetMs = source.greedySearchTimeBudgetMs,
                maximumSearchMemoryMegabytes = source.maximumSearchMemoryMegabytes,
                lookaheadDepth = source.lookaheadDepth,
                lookaheadBranchLimit = source.lookaheadBranchLimit,
                lookaheadNodeLimit = source.lookaheadNodeLimit,
                allowAsyncForLargeBoards = source.allowAsyncForLargeBoards,
                asyncRegionThreshold = source.asyncRegionThreshold,
                logSolverPerformance = source.logSolverPerformance,
                complexityThreshold1 = source.complexityThreshold1,
                complexityThreshold2 = source.complexityThreshold2,
                complexityThreshold3 = source.complexityThreshold3,
                complexityThreshold4 = source.complexityThreshold4,
                complexityMultiplier1 = source.complexityMultiplier1,
                complexityMultiplier2 = source.complexityMultiplier2,
                complexityMultiplier3 = source.complexityMultiplier3,
                complexityMultiplier4 = source.complexityMultiplier4,
                maximumMoveBudget = source.maximumMoveBudget
            };
        }

        private static TestResult[] SolveCases(List<TestCase> cases, CancellationToken token)
        {
            var results = new TestResult[cases.Count];
            var settings = new MoveSolverSettings();
            for (int i = 0; i < cases.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                TestCase test = cases[i];
                var item = new TestResult { Name = test.Name };
                if (test.Graph == null)
                {
                    item.Error = "Board graph validation failed.";
                    results[i] = item;
                    continue;
                }
                if (test.IsPurpleYellowScenario)
                {
                    var simulator = new FloodMoveSimulator(test.Graph);
                    byte[] state = simulator.Apply(test.Graph.RegionColors,
                        new FloodFillSolverMove3D(test.Graph.StartingRegion, 1));
                    int captured = simulator.CountComponent(state, test.Graph.StartingRegion);
                    if (captured != 11) item.Error = "Purple/yellow merge must contain 11 voxels.";
                }
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                Stopwatch watch = Stopwatch.StartNew();
                item.SolverResult = FloodFillMoveSolver3D.Solve(test.Graph, settings, true, token);
                watch.Stop();
                item.SolveMilliseconds = watch.Elapsed.TotalMilliseconds;
                item.AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                if (!item.SolverResult.SolutionValidated) item.Error = "Solver did not validate its complete sequence.";
                if (test.ReferenceMoves >= 0 && item.SolverResult.IsProvenOptimal && item.SolverResult.BestMoveCount != test.ReferenceMoves)
                    item.Error = $"Optimality mismatch: reference={test.ReferenceMoves}, solver={item.SolverResult.BestMoveCount}.";
                if (test.ReferenceMoves >= 0 && item.SolverResult.BestMoveCount < test.ReferenceMoves)
                    item.Error = $"Solver path is shorter than independent exact reference ({test.ReferenceMoves}).";
                if (!test.IsPurpleYellowScenario)
                {
                    var comparison = new System.Text.StringBuilder();
                    for (int depth = 1; depth <= 5; depth++)
                    {
                        token.ThrowIfCancellationRequested();
                        MoveSolverSettings variant = CopySettings(settings);
                        variant.lookaheadDepth = depth;
                        variant.lookaheadBranchLimit = depth == 1 ? 1 : 2;
                        variant.greedyRuns = 1;
                        Stopwatch variantWatch = Stopwatch.StartNew();
                        FloodFillSolverResult variantResult = FloodFillMoveSolver3D.Solve(
                            test.Graph, variant, true, token);
                        variantWatch.Stop();
                        if (depth > 1) comparison.Append(" | ");
                        comparison.Append($"d{depth}: greedy={variantResult.GreedyMoveCount}, best={variantResult.BestMoveCount}, {variantWatch.Elapsed.TotalMilliseconds:0.0}ms, {(variantResult.SolutionValidated ? "valid" : "INVALID")}");
                        if (!variantResult.SolutionValidated)
                            item.Error = $"Lookahead depth {depth} returned an invalid solution.";
                    }
                    item.StrategySweep = comparison.ToString();
                }
                results[i] = item;
            }
            return results;
        }

        private void Poll()
        {
            if (task == null || !task.IsCompleted) return;
            Task<TestResult[]> completed = task;
            task = null;
            cancellation?.Dispose();
            cancellation = null;
            if (completed.IsCanceled)
            {
                status = "Profile suite cancelled.";
                return;
            }
            if (completed.IsFaulted)
            {
                status = "Profile suite failed: " + completed.Exception.GetBaseException().Message;
                Debug.LogException(completed.Exception.GetBaseException());
                return;
            }

            TestResult[] results = completed.Result;
            int failures = 0;
            for (int i = 0; i < results.Length; i++)
            {
                TestResult item = results[i];
                FloodFillSolverResult r = item.SolverResult;
                if (item.Error != null) failures++;
                string report = item.Error != null
                    ? $"FAIL {item.Name}: {item.Error}"
                    : $"PASS {item.Name}: voxels={r.PlayableVoxelCount}, regions={r.RegionCount}, colors={r.DistinctColorCount}, greedy={r.GreedyMoveCount}, beam={r.BeamMoveCount}, best={r.BestMoveCount}, status={(r.IsProvenOptimal ? "OPTIMAL" : "VALIDATED")}, time={item.SolveMilliseconds:0.0}ms, expanded={r.ExploredStates}, unique={r.UniqueStates}, duplicateSkipped={r.DuplicateStatesSkipped}, estimatedPeak={r.EstimatedPeakSearchMemoryBytes / (1024d * 1024d):0.##}MiB, allocated={item.AllocatedBytes / 1024d:0.##}KiB, termination={r.SearchTerminationReason}" + (string.IsNullOrEmpty(item.StrategySweep) ? string.Empty : "\n  quality sweep: " + item.StrategySweep);
                reports.Add(report);
                Debug.Log("[Move Solver Profile] " + report);
            }
            status = failures == 0
                ? $"Completed {results.Length} fixed-seed cases with no validation failures."
                : $"Completed {results.Length} cases; {failures} failed validation.";
            Repaint();
        }
    }
}
