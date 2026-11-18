using System;
using System.Threading;

namespace FloodFill.ThreeD.Solver
{
    // Compatibility facade: runtime and Editor callers share exactly the same engine.
    public static class FloodFillMoveSolver3D
    {
        public static FloodFillSolverResult SolveGuaranteedFallback(FloodFillRegionGraph3D graph, MoveSolverSettings settings, CancellationToken cancellationToken = default)
            => SolveCore(graph, settings, false, cancellationToken, true);
        public static FloodFillSolverResult Solve(FloodFillRegionGraph3D graph, MoveSolverSettings settings,
            bool useDevelopmentBudget, CancellationToken cancellationToken = default)
            => SolveCore(graph, settings, useDevelopmentBudget, cancellationToken, false);
        private static FloodFillSolverResult SolveCore(FloodFillRegionGraph3D graph, MoveSolverSettings settings,
            bool development, CancellationToken token, bool fallback)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            settings ??= new MoveSolverSettings();
            FloodFillSolverResult result = new FloodSolver(graph, settings.CreateRuntimeSettings(development), token).Run(fallback);
            result.BudgetProfile = MoveBudgetCalculator3D.Calculate(result.BestMoveCount, graph.RegionCount, result.IsProvenOptimal, settings);
            return result;
        }
    }
}
