using System.Text;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    internal static class AutoSolverDiagnostics
    {
        public static string Classify(AutoSolverBoardSnapshot snapshot, AutoSolverResult result)
        {
            if (snapshot == null || result == null) return "INCONCLUSIVE";
            if (result.Conclusion == AutoSolverConclusion.Invalid || result.Conclusion == AutoSolverConclusion.ProvenNoSolution) return "FAILED";
            if (!result.ValidationPassed) return "INCONCLUSIVE";
            string validity = result.IsProvenOptimal ? "OPTIMAL" : "VALIDATED";
            return validity + (result.Moves.Length <= snapshot.RemainingMoves ? " — within budget" : " — OVER BUDGET");
        }

        public static string FormatReport(
            AutoSolverBoardSnapshot snapshot,
            AutoSolverGraph graph,
            AutoSolverResult result,
            Color[] palette)
        {
            string classification = Classify(snapshot, result);
            var builder = new StringBuilder(512);
            builder.AppendLine("[AutoSolver] LEVEL ANALYSIS");
            builder.AppendLine($"Board Dimensions: {snapshot.Width}x{snapshot.Height}x{snapshot.Depth}");
            builder.AppendLine($"Playable Voxels: {snapshot.VoxelCount}");
            builder.AppendLine($"Connected Color Regions: {graph.RegionCount}");
            builder.AppendLine($"Solution Length: {(result.Conclusion == AutoSolverConclusion.Solved ? result.Moves.Length.ToString() : "Not found")}");
            builder.AppendLine($"Original budget solution length: {snapshot.OriginalSolutionLength} ({(snapshot.OriginalOptimalityProven ? "proven optimal" : "best known, unproven")})");
            builder.AppendLine($"Analysis position: {snapshot.MoveCount} moves already played; analysis uses current colors.");
            builder.AppendLine($"Starting Region: {graph.Core.StartingRegion}; Distinct Colors: {result.CoreResult?.DistinctColorCount}");
            builder.AppendLine($"Greedy: {result.CoreResult?.GreedyMoveCount}; Beam: {result.CoreResult?.BeamMoveCount}; Duplicates: {result.CoreResult?.DuplicateStatesSkipped}; Estimated peak search bytes: {result.CoreResult?.EstimatedPeakSearchMemoryBytes}");
            builder.AppendLine($"Maximum Moves: {snapshot.MoveLimit} (remaining: {snapshot.RemainingMoves})");
            builder.AppendLine($"Optimality Proven: {(result.IsProvenOptimal ? "Yes" : "No")}");
            builder.AppendLine($"Explored States: {result.ExploredStates}");
            builder.AppendLine($"Calculation Time: {result.CalculationMilliseconds:0.###}ms");
            builder.AppendLine("Result: " + classification);
            if (!string.IsNullOrEmpty(result.Message)) builder.AppendLine("Details: " + result.Message);
            if (result.Moves != null && result.Moves.Length > 0)
            {
                builder.AppendLine("Solution:");
                for (int i = 0; i < result.Moves.Length; i++)
                {
                    int colorIndex = result.Moves[i].ColorIndex;
                    string color = palette != null && colorIndex >= 0 && colorIndex < palette.Length
                        ? ColorUtility.ToHtmlStringRGB(palette[colorIndex])
                        : "?";
                    int captured = result.CapturedPerMove != null && i < result.CapturedPerMove.Length
                        ? result.CapturedPerMove[i]
                        : 0;
                    builder.AppendLine($"  {i + 1}. Color #{colorIndex} (#{color}) — captures {captured} cubes");
                }
            }
            return builder.ToString();
        }
    }
}
