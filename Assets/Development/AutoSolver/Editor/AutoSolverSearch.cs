using System;
using System.Threading;
using FloodFill.ThreeD.Solver;

namespace FloodFill.Development.AutoSolver
{
    internal static class AutoSolverSearch
    {
        public static AutoSolverResult Solve(AutoSolverGraph graph, int remainingMoveLimit,
            int timeBudgetMilliseconds, int expandedStateBudget, int beamWidth, CancellationToken cancellationToken)
        {
            var result = new AutoSolverResult { RegionCount = graph?.RegionCount ?? 0,
                PlayableVoxels = graph?.TotalVoxels ?? 0, RemainingMoveLimit = remainingMoveLimit };
            if (graph?.Core == null) { result.Conclusion = AutoSolverConclusion.Invalid; result.Message = "Missing board graph."; return result; }
            if (!graph.Core.IsConnected) { result.Conclusion = AutoSolverConclusion.ProvenNoSolution; result.Message = "Disconnected occupied voxels cannot form a single captured component."; return result; }
            var settings = new MoveSolverSettings
            {
                beamWidth = Math.Max(1, beamWidth), exactMaxExpandedStates = Math.Max(1, expandedStateBudget),
                developmentGreedySearchTimeBudgetMs = Math.Max(1, timeBudgetMilliseconds / 4),
                developmentBeamSearchTimeBudgetMs = Math.Max(1, timeBudgetMilliseconds / 2),
                developmentExactSearchTimeBudgetMs = Math.Max(1, timeBudgetMilliseconds / 4),
                exactSolverRegionThreshold = 256
            };
            try
            {
                var core = FloodFillMoveSolver3D.Solve(graph.Core, settings, true, cancellationToken);
                result.CoreResult = core;
                result.Moves = new AutoSolverMove[core.BestMoveCount];
                for (int i = 0; i < result.Moves.Length; i++) result.Moves[i] = new AutoSolverMove(core.BestRegionSequence[i], core.BestColorSequence[i]);
                result.ValidationPassed = TryValidate(graph, graph.RegionColors, result.Moves, out result.CapturedPerMove);
                result.Conclusion = result.ValidationPassed ? AutoSolverConclusion.Solved : AutoSolverConclusion.Invalid;
                result.IsProvenOptimal = core.IsProvenOptimal; result.ExploredStates = core.ExploredStates;
                result.CalculationMilliseconds = core.ElapsedMilliseconds; result.Message = core.SearchTerminationReason;
                if (result.ValidationPassed && result.Moves.Length > remainingMoveLimit)
                    result.Message += " The solution exceeds the current remaining move budget.";
            }
            catch (OperationCanceledException) { result.Conclusion = AutoSolverConclusion.Cancelled; result.Message = "Cancelled before a valid incumbent was available."; }
            return result;
        }
        public static byte[] ApplyMove(AutoSolverGraph graph, byte[] state, AutoSolverMove move)
            => new FloodMoveSimulator(graph.Core).Apply(state, new FloodFillSolverMove3D(move.RegionId, move.ColorIndex));
        public static bool TryValidate(AutoSolverGraph graph, byte[] initial, AutoSolverMove[] moves, out int[] capturedPerMove)
        {
            capturedPerMove = Array.Empty<int>();
            if (graph?.Core == null || initial == null || initial.Length != graph.RegionCount || moves == null) return false;
            for (int i = 0; i < initial.Length; i++) if (initial[i] != graph.RegionColors[i]) return false;
            var converted = new FloodFillSolverMove3D[moves.Length];
            for (int i = 0; i < moves.Length; i++) converted[i] = new FloodFillSolverMove3D(moves[i].RegionId, moves[i].ColorIndex);
            return FloodSolverValidator.Validate(graph.Core, converted, out capturedPerMove);
        }
    }
}
