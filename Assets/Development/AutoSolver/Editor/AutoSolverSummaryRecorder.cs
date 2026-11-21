using System;
using System.Linq;
using System.Globalization;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    internal sealed class AutoSolverSummaryRecorder
    {
        private readonly AutoSolverSummaryStore store;
        private AutoSolverAttempt current;
        private UnityEngine.Object manager;
        private int generation;
        private double startedAt;
        public AutoSolverSummaryRecorder() : this(AutoSolverSummaryStore.Shared) { }
        internal AutoSolverSummaryRecorder(AutoSolverSummaryStore target) { store = target; }

        public void Begin(AutoSolverAdapter adapter, AutoSolverBoardSnapshot snapshot)
        {
            Observe(adapter);
            if (current == null)
            {
                string runId = AutoSolverSummarySession.RunId(adapter);
                current = store.Data.attempts.LastOrDefault(a => a.boardRunId == runId);
                manager = adapter.Manager; generation = adapter.Generation;
                if (current != null)
                {
                    double elapsed = DateTime.TryParse(current.startedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start)
                        ? Math.Max(0, (DateTime.UtcNow - start.ToUniversalTime()).TotalSeconds) : current.elapsedSeconds;
                    startedAt = UnityEditor.EditorApplication.timeSinceStartup - elapsed;
                }
            }
            if (current == null)
            {
                var game = (FloodFill.ThreeD.FloodFillGameManager3D)adapter.Manager;
                current = new AutoSolverAttempt
                {
                    id = Guid.NewGuid().ToString("N"), boardRunId = AutoSolverSummarySession.RunId(adapter), kind = game.NumberedLevel > 0 ? "Numbered" : game.IsCampaignGame ? "Campaign" : "Custom",
                    mode = game.NumberedLevel > 0 ? game.NumberedMode : game.SelectedBoardMode == FloodFill.ThreeD.VoxelBoardManager3D.VoxelVolumeMode.HollowCube ? 0 : game.SelectedBoardMode == FloodFill.ThreeD.VoxelBoardManager3D.VoxelVolumeMode.Items ? 2 : 1,
                    difficulty = (int)game.SelectedDifficulty, level = game.NumberedLevel > 0 ? game.NumberedLevel : game.IsCampaignGame ? game.ActiveCampaignLevel : 0,
                    seed = game.BoardManager.LastGenerationSeed, width = snapshot.Width, height = snapshot.Height, depth = snapshot.Depth,
                    voxels = snapshot.VoxelCount, capturedPercentage = adapter.CapturedPercentage, moveBudget = snapshot.MoveLimit, startingMoves = snapshot.MoveCount, moves = snapshot.MoveCount,
                    startedUtc = DateTime.UtcNow.ToString("O"), outcome = "Running", unityVersion = Application.unityVersion
                };
                startedAt = UnityEditor.EditorApplication.timeSinceStartup;
                manager = adapter.Manager; generation = adapter.Generation;
                store.Data.attempts.Add(current);
            }
            current.searches++; current.searchResult = "Searching";
            if (!Terminal(current)) current.outcome = "Running";
            SaveCurrent();
        }
        public void EnsureRun(AutoSolverAdapter adapter, AutoSolverBoardSnapshot snapshot, AutoSolverResult solution)
        {
            if (!Matches(adapter)) { Begin(adapter, snapshot); SearchFinished(adapter, solution); }
            else if (!Terminal(current) && current.outcome != "Running") { current.outcome = "Running"; SaveCurrent(); }
        }
        public void SearchFinished(AutoSolverAdapter adapter, AutoSolverResult result)
        {
            if (!Matches(adapter) || result == null) return;
            current.plannedMoves = result.Moves != null ? result.Moves.Length : 0;
            current.searchResult = result.Conclusion == AutoSolverConclusion.Solved && result.ValidationPassed
                ? (result.FitsMoveBudget ? "Within budget" : "Over budget") : result.Conclusion.ToString();
            current.searchMilliseconds += result.CalculationMilliseconds;
            if (!Terminal(current) && !result.FitsMoveBudget) current.outcome = current.searchResult;
            current.provenOptimal = result.IsProvenOptimal;
            current.algorithm = result.CoreResult != null ? result.CoreResult.SearchAlgorithm : "Auto Solver";
            SaveCurrent();
        }
        public void SearchCancelled()
        {
            if (current == null || current.searchResult != "Searching") return;
            current.searchResult = "Cancelled";
            if (current.outcome == "Running") current.outcome = "Cancelled";
            SaveCurrent();
        }
        public void SearchFailed()
        {
            if (current == null) return;
            current.searchResult = "Solver error";
            if (!Terminal(current)) current.outcome = "Solver error";
            SaveCurrent();
        }
        public void Observe(AutoSolverAdapter adapter, int appliedMoves = 0)
        {
            if (current == null) return;
            if (!Matches(adapter)) { Finish("Restarted / level changed"); return; }
            string outcome = adapter.GameState == "Won" ? "Won" : adapter.GameState == "Lost" ? "Lost" : current.outcome;
            bool changed = current.moves != adapter.MoveCount || current.score != adapter.Score || current.outcome != outcome || appliedMoves > 0;
            current.moves = adapter.MoveCount; current.score = adapter.Score; current.capturedPercentage = adapter.CapturedPercentage;
            current.solverMoves += Math.Max(0, appliedMoves);
            if (!Terminal(current))
            {
                current.elapsedSeconds = Math.Max(0, UnityEditor.EditorApplication.timeSinceStartup - startedAt);
                current.outcome = outcome;
                if (Terminal(current)) current.finishedUtc = DateTime.UtcNow.ToString("O");
            }
            if (changed) SaveCurrent();
        }
        public void Stop(AutoSolverAdapter adapter, string reason)
        {
            Observe(adapter);
            if (current == null || Terminal(current)) return;
            current.outcome = reason; current.elapsedSeconds = Math.Max(0, UnityEditor.EditorApplication.timeSinceStartup - startedAt);
            SaveCurrent();
        }
        public void Finish(string reason)
        {
            if (current == null) return;
            bool changed = false;
            if (current.searchResult == "Searching") { current.searchResult = "Cancelled"; changed = true; }
            if (!Terminal(current))
            {
                current.outcome = reason; current.finishedUtc = DateTime.UtcNow.ToString("O");
                current.elapsedSeconds = Math.Max(0, UnityEditor.EditorApplication.timeSinceStartup - startedAt);
                changed = true;
            }
            if (changed) SaveCurrent();
            current = null; manager = null;
        }
        private void SaveCurrent()
        {
            if (current != null) current.updatedUtc = DateTime.UtcNow.ToString("O");
            store.Save();
        }
        private bool Matches(AutoSolverAdapter adapter) => adapter != null && adapter.IsAlive && manager == adapter.Manager && generation == adapter.Generation;
        private static bool Terminal(AutoSolverAttempt attempt) => attempt.outcome == "Won" || attempt.outcome == "Lost";
    }
}
