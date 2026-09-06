using System;
using System.Diagnostics;
using System.Threading;

namespace FloodFill.ThreeD.Solver
{
    public static class FloodFillMoveSolver3D
    {
        public static FloodFillSolverResult SolveGuaranteedFallback(
            FloodFillRegionGraph3D graph,
            MoveSolverSettings serializedSettings)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }
            serializedSettings ??= new MoveSolverSettings();
            MoveSolverRuntimeSettings runtime =
                serializedSettings.CreateRuntimeSettings(false);
            FloodFillSolverMove3D[] moves;
            bool valid;
            if (graph.RegionCount <= 64 && graph.ColorCount <= 8)
            {
                var solver = new FloodFillMoveSolver64(
                    graph, runtime, CancellationToken.None);
                moves = solver.CreateFallback();
                valid = solver.Validate(moves);
            }
            else
            {
                var solver = new FloodFillMoveSolverLarge(
                    graph, runtime, CancellationToken.None);
                moves = solver.CreateFallback();
                valid = solver.Validate(moves);
            }
            if (!valid || moves.Length == 0)
            {
                throw new InvalidOperationException(
                    "The guaranteed logical fallback did not replay successfully.");
            }

            int[] colors = new int[moves.Length];
            int[] regions = new int[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                colors[i] = moves[i].ColorIndex;
                regions[i] = moves[i].RegionId;
            }
            MoveBudgetProfile budget = MoveBudgetCalculator3D.Calculate(
                moves.Length, graph.RegionCount, false, serializedSettings);
            return new FloodFillSolverResult
            {
                BestMoveCount = moves.Length,
                BestColorSequence = colors,
                BestRegionSequence = regions,
                RegionCount = graph.RegionCount,
                PlayableVoxelCount = graph.TotalVoxelCount,
                GreedyMoveCount = moves.Length,
                BeamMoveCount = moves.Length,
                UsedFallback = true,
                SolutionValidated = true,
                GraphBuildMilliseconds = graph.BuildMilliseconds,
                BudgetProfile = budget
            };
        }

        public static FloodFillSolverResult Solve(
            FloodFillRegionGraph3D graph,
            MoveSolverSettings serializedSettings,
            bool useDevelopmentBudget,
            CancellationToken cancellationToken = default)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }
            if (serializedSettings == null)
            {
                serializedSettings = new MoveSolverSettings();
            }

            Stopwatch totalWatch = Stopwatch.StartNew();
            MoveSolverRuntimeSettings settings =
                serializedSettings.CreateRuntimeSettings(useDevelopmentBudget);
            SolverSearchOutcome3D search;
            bool validated;
            if (graph.RegionCount <= 64 && graph.ColorCount <= 8)
            {
                var solver = new FloodFillMoveSolver64(graph, settings, cancellationToken);
                search = solver.Solve();
                validated = solver.Validate(search.BestMoves);
                if (!validated)
                {
                    search.BestMoves = solver.CreateFallback();
                    search.UsedFallback = true;
                    search.ProvenOptimal = false;
                    validated = solver.Validate(search.BestMoves);
                }
            }
            else
            {
                var solver = new FloodFillMoveSolverLarge(graph, settings, cancellationToken);
                search = solver.Solve();
                validated = solver.Validate(search.BestMoves);
                if (!validated)
                {
                    search.BestMoves = solver.CreateFallback();
                    search.UsedFallback = true;
                    search.ProvenOptimal = false;
                    validated = solver.Validate(search.BestMoves);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!validated || search.BestMoves == null || search.BestMoves.Length == 0)
            {
                throw new InvalidOperationException(
                    "The move solver could not produce a replay-valid solution.");
            }

            int[] colors = new int[search.BestMoves.Length];
            int[] regions = new int[search.BestMoves.Length];
            for (int i = 0; i < search.BestMoves.Length; i++)
            {
                colors[i] = search.BestMoves[i].ColorIndex;
                regions[i] = search.BestMoves[i].RegionId;
            }
            MoveBudgetProfile budget = MoveBudgetCalculator3D.Calculate(
                search.BestMoves.Length,
                graph.RegionCount,
                search.ProvenOptimal,
                serializedSettings);
            totalWatch.Stop();
            return new FloodFillSolverResult
            {
                BestMoveCount = search.BestMoves.Length,
                IsProvenOptimal = search.ProvenOptimal,
                BestColorSequence = colors,
                BestRegionSequence = regions,
                ElapsedMilliseconds = totalWatch.Elapsed.TotalMilliseconds +
                    graph.BuildMilliseconds,
                GraphBuildMilliseconds = graph.BuildMilliseconds,
                GreedyMilliseconds = search.GreedyMilliseconds,
                BeamMilliseconds = search.BeamMilliseconds,
                ExactMilliseconds = search.ExactMilliseconds,
                ExploredStates = search.ExpandedStates,
                RegionCount = graph.RegionCount,
                PlayableVoxelCount = graph.TotalVoxelCount,
                GreedyMoveCount = search.GreedyMoves,
                BeamMoveCount = search.BeamMoves,
                ExactMoveCount = search.ExactMoves,
                UsedFallback = search.UsedFallback,
                SolutionValidated = true,
                BudgetProfile = budget
            };
        }
    }
}
