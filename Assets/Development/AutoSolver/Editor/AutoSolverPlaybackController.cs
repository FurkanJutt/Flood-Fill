using System;
using UnityEditor;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    internal sealed class AutoSolverPlaybackController
    {
        private readonly AutoSolverVisualTuner visualTuner = new AutoSolverVisualTuner();
        private AutoSolverAdapter adapter;
        private AutoSolverBoardSnapshot snapshot;
        private AutoSolverGraph graph;
        private AutoSolverResult result;
        private byte[] expectedState;
        private int nextMoveIndex;
        private int expectedMoveCount;
        private int expectedSelectedColor;
        private bool inputWasEnabled;
        private bool inputLocked;
        private bool stepMode;
        private bool stepMovePendingCompletion;
        private bool isPaused;
        private bool isPlaying;
        private float playbackSpeed = 1f;
        private float animationDuration = 0.2f;
        private double nextMoveTime;
        private double pausedWait;
        private double currentMoveDuration;
        private double originalTimeScale = 1d;

        public bool IsPlaying => isPlaying;
        public bool IsPaused => isPaused;
        public AutoSolverResult CurrentSolution => result;
        public int NextMoveIndex => nextMoveIndex;
        public int TotalMoves => result != null ? result.Moves.Length : 0;
        public string Status { get; private set; } = "Idle.";

        public void Start(
            AutoSolverAdapter game,
            AutoSolverBoardSnapshot boardSnapshot,
            AutoSolverGraph regionGraph,
            AutoSolverResult solution,
            float speed,
            float animationTime,
            bool captureAnimation,
            bool colorTransition,
            bool cubePop,
            bool stepOnce,
            out string error)
        {
            error = null;
            if (solution == null || solution.Conclusion != AutoSolverConclusion.Solved ||
                !solution.ValidationPassed || solution.Moves == null)
            {
                error = "There is no validated solution to play.";
                return;
            }
            if (game == null || !game.IsAlive || boardSnapshot == null || regionGraph == null)
            {
                error = "The active game or its board snapshot is no longer available.";
                return;
            }
            if (solution.Moves.Length > boardSnapshot.RemainingMoves)
            {
                error = "Playback blocked: the plan needs " + solution.Moves.Length + " moves, but only " + boardSnapshot.RemainingMoves + " remain. No moves were played.";
                return;
            }
            if (game.GameState != "Playing" || game.MoveCount != boardSnapshot.MoveCount ||
                game.SelectedColor != boardSnapshot.SelectedColor)
            {
                error = "The live game changed after analysis. Analyze the current board again.";
                return;
            }
            if (!AutoSolverSearch.TryValidate(regionGraph, regionGraph.RegionColors, solution.Moves, out _))
            {
                error = "The planned moves failed validation immediately before playback.";
                return;
            }
            AutoSolverBoardSnapshot current = game.CaptureLiveSnapshot();
            if (current == null || !SameColors(boardSnapshot, current))
            {
                error = "Voxel colors changed after analysis. Analyze the current board again.";
                return;
            }

            adapter = game;
            snapshot = boardSnapshot;
            graph = regionGraph;
            result = solution;
            expectedState = (byte[])graph.RegionColors.Clone();
            nextMoveIndex = 0;
            expectedMoveCount = snapshot.MoveCount;
            expectedSelectedColor = snapshot.SelectedColor;
            playbackSpeed = Mathf.Clamp(speed, 0.5f, 4f);
            animationDuration = Mathf.Clamp(animationTime, 0.01f, 2f);
            stepMode = stepOnce;
            stepMovePendingCompletion = false;
            isPaused = false;
            originalTimeScale = Time.timeScale;
            if (!adapter.TrySetBoardInput(false, out inputWasEnabled, out error)) return;
            inputLocked = true;
            visualTuner.Begin(adapter, snapshot, animationDuration, captureAnimation, colorTransition, cubePop);
            ApplySpeed();
            isPlaying = true;
            nextMoveTime = EditorApplication.timeSinceStartup;
            Status = stepMode ? "Stepping through the validated solution." : "Auto solve playing.";
        }

        public void Tick(double now)
        {
            if (!isPlaying || isPaused) return;
            if (adapter == null || !adapter.IsAlive)
            {
                Stop("Stopped because the active game was destroyed.");
                return;
            }
            if (now < nextMoveTime) return;

            if (stepMode && stepMovePendingCompletion)
            {
                stepMovePendingCompletion = false;
                Pause("Step finished. Click Step again to continue.");
                return;
            }

            if (nextMoveIndex >= result.Moves.Length)
            {
                FinishPlayback();
                return;
            }
            if (adapter.GameState != "Playing")
            {
                string ending = adapter.GameState == "Lost"
                    ? "The game reached its move limit before the planned solution finished."
                    : "The game left the Playing state before the next planned move.";
                Stop(ending);
                return;
            }
            if (!ValidateLiveState(out string mismatch))
            {
                Pause(mismatch);
                return;
            }

            AutoSolverMove move = result.Moves[nextMoveIndex];
            int voxelIndex = graph.RepresentativeVoxelIndices[move.RegionId];
            if (voxelIndex < 0 || voxelIndex >= snapshot.Voxels.Length || snapshot.Voxels[voxelIndex] == null)
            {
                Stop("The representative voxel was destroyed. Playback stopped safely.");
                return;
            }
            if (!adapter.TryInvokeMove(snapshot.Voxels[voxelIndex], move.ColorIndex, out string error))
            {
                Stop("Could not invoke the game's existing move handler: " + error);
                return;
            }

            expectedState = AutoSolverSearch.ApplyMove(graph, expectedState, move);
            expectedMoveCount++;
            expectedSelectedColor = move.ColorIndex;
            nextMoveIndex++;
            stepMovePendingCompletion = stepMode;
            if (adapter.MoveCount != expectedMoveCount)
            {
                Stop("The gameplay handler did not record exactly one move; playback stopped.");
                return;
            }
            if (adapter.GameState == "Lost")
            {
                Stop("The level's move limit ended the game before the full solution could play.");
                return;
            }

            double speedScale = Math.Max(0.1d, originalTimeScale * playbackSpeed);
            currentMoveDuration = (animationDuration * 2d) / speedScale;
            nextMoveTime = now + currentMoveDuration;
            Status = "Playing move " + nextMoveIndex + " of " + result.Moves.Length + ".";
        }

        public void Pause(string reason = null)
        {
            if (!isPlaying || isPaused) return;
            pausedWait = Math.Max(0d, nextMoveTime - EditorApplication.timeSinceStartup);
            isPaused = true;
            visualTuner.PauseTime();
            Status = string.IsNullOrEmpty(reason) ? "Paused." : "Paused: " + reason;
        }

        public void Resume()
        {
            if (!isPlaying || !isPaused) return;
            isPaused = false;
            ApplySpeed();
            nextMoveTime = EditorApplication.timeSinceStartup + pausedWait;
            Status = stepMode ? "Stepping." : "Resumed.";
        }

        public void Step()
        {
            if (!isPlaying)
            {
                Status = "Calculate a validated solution before stepping.";
                return;
            }
            stepMode = true;
            if (isPaused) Resume();
            Status = "Executing one solver move.";
        }

        public void SetSpeed(float speed)
        {
            float previousSpeed = playbackSpeed;
            playbackSpeed = Mathf.Clamp(speed, 0.5f, 4f);
            if (!isPlaying) return;
            if (!isPaused) ApplySpeed();

            double remaining = isPaused
                ? pausedWait
                : Math.Max(0d, nextMoveTime - EditorApplication.timeSinceStartup);
            double progress = currentMoveDuration > 0d
                ? 1d - Math.Min(1d, remaining / currentMoveDuration)
                : 0d;
            double oldScale = Math.Max(0.1d, originalTimeScale * previousSpeed);
            double newScale = Math.Max(0.1d, originalTimeScale * playbackSpeed);
            double newDuration = currentMoveDuration > 0d
                ? currentMoveDuration * oldScale / newScale
                : (animationDuration * 2d) / newScale;
            double newRemaining = newDuration * (1d - progress);
            currentMoveDuration = newDuration;
            if (isPaused) pausedWait = newRemaining;
            else nextMoveTime = EditorApplication.timeSinceStartup + newRemaining;
        }

        public void Stop(string message = "Stopped. ")
        {
            if (isPlaying && snapshot != null) visualTuner.CompleteVoxelAnimations(snapshot);
            isPlaying = false;
            isPaused = false;
            stepMode = false;
            stepMovePendingCompletion = false;
            visualTuner.Restore();
            if (inputLocked && adapter != null)
                adapter.RestoreBoardInput(inputWasEnabled && adapter.GameState == "Playing");
            inputLocked = false;
            if (!string.IsNullOrEmpty(message)) Status = message;
        }

        private void FinishPlayback()
        {
            AutoSolverBoardSnapshot live = adapter.CaptureLiveSnapshot();
            if (live != null && live.CapturedCount == live.VoxelCount)
                Stop("Solution playback complete. The live game captured every voxel.");
            else
                Stop("Planned sequence ended, but the live game did not report a complete capture.");
        }

        private bool ValidateLiveState(out string error)
        {
            error = null;
            if (adapter.MoveCount != expectedMoveCount)
            {
                error = "Manual input changed the move count. Playback paused.";
                return false;
            }
            if (adapter.SelectedColor != expectedSelectedColor)
            {
                error = "The selected color changed outside the solver. Playback paused.";
                return false;
            }
            if (!adapter.MatchesExpectedColors(expectedState, graph.VoxelRegionIds))
            {
                error = "Voxel colors changed or the live board changed outside the solver. Playback paused.";
                return false;
            }
            return true;
        }

        private void ApplySpeed()
        {
            visualTuner.SetPlaybackSpeed(playbackSpeed);
        }

        private static bool SameColors(AutoSolverBoardSnapshot a, AutoSolverBoardSnapshot b)
        {
            if (a.Width != b.Width || a.Height != b.Height || a.Depth != b.Depth ||
                a.VoxelCount != b.VoxelCount) return false;
            for (int i = 0; i < a.VoxelCount; i++)
                if (a.LogicalIndices[i] != b.LogicalIndices[i] || a.Colors[i] != b.Colors[i]) return false;
            return true;
        }
    }
}
