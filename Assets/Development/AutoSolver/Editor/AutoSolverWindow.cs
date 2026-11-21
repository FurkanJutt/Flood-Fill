using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FloodFill.Development.AutoSolver
{
    public sealed class AutoSolverWindow : EditorWindow
    {
        private readonly AutoSolverPlaybackController playback = new AutoSolverPlaybackController();
        private readonly AutoSolverSummaryRecorder summaryRecorder = new AutoSolverSummaryRecorder();
        private AutoSolverAdapter adapter;
        private AutoSolverBoardSnapshot boardSnapshot;
        private AutoSolverGraph graph;
        private AutoSolverResult solution;
        private Task<AutoSolverResult> searchTask;
        private CancellationTokenSource searchCancellation;
        private Vector2 scroll;
        private string status = "Enter Play Mode to use Auto Solver.";
        private string analysisReport = string.Empty;
        private bool logAnalysisWhenReady;
        private bool autoPlayWhenReady;
        private bool waitingForRestart;
        private bool solverSessionStarted;
        [SerializeField] private bool autoNextLevel;
        [SerializeField] private bool autoStartSolver;
        private int observedGeneration = -1, autoSolveGeneration = -1;
        private UnityEngine.Object observedManager;
        private bool autoNextAttempted;
        private double advanceAfter = -1;

        private bool showSolution = true;
        private float playbackSpeed = 1f;
        private float animationDuration = 0.2f;
        private bool enableCaptureAnimation = true;
        private bool enableColorTransition = true;
        private bool enableCubePop = true;
        private int searchTimeBudgetMs = 3000;
        private int expandedStateBudget = 60000;
        private int beamWidth = 32;
        private double lastRepaintTime;
        private double nextConnectionSearch;

        [MenuItem("Tools/Flood Fill/Auto Solver")]
        private static void OpenWindow()
        {
            AutoSolverWindow window = GetWindow<AutoSolverWindow>("Auto Solver");
            window.minSize = new Vector2(360f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += EditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            if (EditorApplication.isPlaying) FindActiveGame();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            summaryRecorder.Observe(adapter); summaryRecorder.Finish("Window closed");
            solverSessionStarted = false;
            CancelSearch();
            playback.Stop("Auto Solver window closed. Playback stopped safely.");
            adapter = null;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawConnection();
            if (GUILayout.Button("Solver Summary", GUILayout.Height(25f))) AutoSolverSummaryWindow.OpenWindow();
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use Auto Solver.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawSolverControls();
            DrawPlaybackSettings();
            DrawLiveStatistics();
            DrawSearchResult();
            EditorGUILayout.EndScrollView();
        }

        private void DrawConnection()
        {
            EditorGUILayout.LabelField("GAME CONNECTION", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("Find Active Game", GUILayout.Height(26f))) FindActiveGame();
                }
                EditorGUILayout.LabelField(adapter != null && adapter.IsAlive ? "Connected" : "Not connected", GUILayout.Width(110f));
            }

            if (!EditorApplication.isPlaying) return;
            if (adapter == null || !adapter.IsAlive)
            {
                EditorGUILayout.HelpBox("No active Flood Fill 3D game was found. Enter Play Mode and load a level.", MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Current Level", adapter.CurrentLevelLabel);
            EditorGUILayout.LabelField("Current Game Status", adapter.GameState);
            string boardStatus = boardSnapshot != null
                ? $"{boardSnapshot.Width}x{boardSnapshot.Height}x{boardSnapshot.Depth} · {adapter.TotalVoxelCount} playable voxels"
                : adapter.TotalVoxelCount + " playable voxels · analyze to inspect dimensions";
            EditorGUILayout.LabelField("Current Board Status", boardStatus);
        }

        private void DrawSolverControls()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("SOLVER CONTROLS", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(adapter == null || !adapter.IsAlive || searchTask != null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Analyze Current Level")) BeginSearch(false, true);
                    if (GUILayout.Button("Calculate Solution")) BeginSearch(false, false);
                }
                if (GUILayout.Button("Start Auto Solve", GUILayout.Height(28f))) StartAutoSolve();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!playback.IsPlaying || playback.IsPaused))
                    if (GUILayout.Button("Pause")) playback.Pause();
                using (new EditorGUI.DisabledScope(!playback.IsPlaying || !playback.IsPaused))
                    if (GUILayout.Button("Resume")) playback.Resume();
                using (new EditorGUI.DisabledScope(playback.IsPlaying
                           ? playback.NextMoveIndex >= playback.TotalMoves
                           : solution == null || solution.Conclusion != AutoSolverConclusion.Solved || !solution.ValidationPassed))
                {
                    if (GUILayout.Button("Step"))
                    {
                        if (playback.IsPlaying) playback.Step();
                        else StartPlayback(true);
                    }
                }
                using (new EditorGUI.DisabledScope(!solverSessionStarted && !playback.IsPlaying && searchTask == null))
                    if (GUILayout.Button("Stop"))
                    {
                        StopAutoSolve();
                    }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(adapter == null || !adapter.IsAlive || searchTask != null))
                    if (GUILayout.Button("Restart Level", GUILayout.Height(24f))) RestartLevel();
                using (new EditorGUI.DisabledScope(adapter == null || !adapter.IsAlive || !adapter.CanStartNextLevel || searchTask != null))
                    if (GUILayout.Button("Next Level", GUILayout.Height(24f))) StartNextLevel();
            }
            autoNextLevel = EditorGUILayout.Toggle("Auto Next Level", autoNextLevel);
            autoStartSolver = EditorGUILayout.Toggle("Auto Start Solver", autoStartSolver);
            EditorGUILayout.HelpBox(status, playback.IsPaused ? MessageType.Warning : MessageType.Info);
        }

        private void DrawPlaybackSettings()
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("PLAYBACK SETTINGS", EditorStyles.boldLabel);
            float speed = EditorGUILayout.Slider("Playback Speed", playbackSpeed, 0.5f, 4f);
            if (!Mathf.Approximately(speed, playbackSpeed))
            {
                playbackSpeed = speed;
                playback.SetSpeed(playbackSpeed);
            }
            EditorGUILayout.LabelField("Speed preset", SpeedName(playbackSpeed));
            animationDuration = EditorGUILayout.Slider("Animation Duration", animationDuration, 0.02f, 0.5f);
            enableCaptureAnimation = EditorGUILayout.Toggle("Enable Capture Animation", enableCaptureAnimation);
            enableColorTransition = EditorGUILayout.Toggle("Enable Color Transition", enableColorTransition);
            enableCubePop = EditorGUILayout.Toggle("Enable Cube Pop Animation", enableCubePop);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("SEARCH BUDGET", EditorStyles.boldLabel);
            searchTimeBudgetMs = EditorGUILayout.IntSlider("Time Budget (ms)", searchTimeBudgetMs, 250, 10000);
            expandedStateBudget = EditorGUILayout.IntSlider("Expanded States", expandedStateBudget, 1000, 250000);
            beamWidth = EditorGUILayout.IntSlider("Beam Width", beamWidth, 4, 128);
        }

        private string PlaybackProgressLabel
        {
            get
            {
                int total = solution != null && solution.Moves != null ? solution.Moves.Length : 0;
                int played = solution != null && ReferenceEquals(playback.CurrentSolution, solution) ? playback.NextMoveIndex : 0;
                return played + " / " + total;
            }
        }

        private void DrawLiveStatistics()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("LIVE STATISTICS", EditorStyles.boldLabel);
            if (adapter == null || !adapter.IsAlive)
            {
                EditorGUILayout.LabelField("Solver Status", playback.Status);
                return;
            }

            EditorGUILayout.LabelField("Solver Status", playback.IsPlaying
                ? playback.IsPaused ? "AUTO SOLVER – PAUSED" : "AUTO SOLVER – PLAYING"
                : playback.Status);
            EditorGUILayout.LabelField("Elapsed time", adapter.ElapsedTimeText);
            EditorGUILayout.LabelField("Solver moves played", PlaybackProgressLabel);
            EditorGUILayout.LabelField("Captured cubes", adapter.CapturedCount + " / " + adapter.TotalVoxelCount);
            float captured = Mathf.Clamp01(adapter.CapturedPercentage / 100f);
            Rect progressRect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(progressRect, captured, "Captured: " + adapter.CapturedPercentageLabel);
            EditorGUILayout.LabelField("Remaining moves", Mathf.Max(0, adapter.MoveLimit - adapter.MoveCount) + " / " + adapter.MoveLimit);
            EditorGUILayout.LabelField("Current selected color", DescribeColor(adapter.SelectedColor, adapter.Palette));
            EditorGUILayout.LabelField("Playback speed", SpeedName(playbackSpeed));
        }

        private void DrawSearchResult()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("LEVEL ANALYSIS", EditorStyles.boldLabel);
            if (searchTask != null)
            {
                EditorGUILayout.LabelField("Calculation", "Searching on a background thread…");
                return;
            }
            if (solution == null || boardSnapshot == null || graph == null)
            {
                EditorGUILayout.LabelField("Calculation", "No solution calculated yet.");
                return;
            }

            EditorGUILayout.LabelField("Result", AutoSolverDiagnostics.Classify(boardSnapshot, solution));
            EditorGUILayout.LabelField("Budget source length", boardSnapshot.OriginalSolutionLength + (boardSnapshot.OriginalOptimalityProven ? " (optimal)" : " (best known, unproven)"));
            EditorGUILayout.LabelField("Budget calculation source", boardSnapshot.OriginalSolutionSource);
            if (solution.CoreResult != null)
            {
                EditorGUILayout.LabelField("Greedy / beam", solution.CoreResult.GreedyMoveCount + " / " + solution.CoreResult.BeamMoveCount);
                EditorGUILayout.LabelField("Duplicates / estimated peak MiB", solution.CoreResult.DuplicateStatesSkipped + " / " + (solution.CoreResult.EstimatedPeakSearchMemoryBytes / 1048576d).ToString("0.##"));
            }
            EditorGUILayout.LabelField("Planned moves", solution.Conclusion == AutoSolverConclusion.Solved ? solution.Moves.Length.ToString() : "—");
            EditorGUILayout.LabelField("Optimality proven", solution.IsProvenOptimal ? "Yes" : "No");
            EditorGUILayout.LabelField("Explored states", solution.ExploredStates.ToString("N0"));
            EditorGUILayout.LabelField("Calculation time", solution.CalculationMilliseconds.ToString("0.###") + " ms");
            EditorGUILayout.LabelField("Details", solution.Message ?? string.Empty);
            showSolution = EditorGUILayout.Foldout(showSolution, "Planned color sequence", true);
            if (showSolution && solution.Moves != null)
            {
                Color[] palette = adapter != null ? adapter.Palette : null;
                for (int i = 0; i < solution.Moves.Length; i++)
                {
                    int colorIndex = solution.Moves[i].ColorIndex;
                    int count = solution.CapturedPerMove != null && i < solution.CapturedPerMove.Length
                        ? solution.CapturedPerMove[i]
                        : 0;
                    EditorGUILayout.LabelField((i + 1) + ". " + DescribeColor(colorIndex, palette) + " · captures " + count);
                }
            }
            if (!string.IsNullOrEmpty(analysisReport))
            {
                EditorGUILayout.Space(3f);
                EditorGUILayout.TextArea(analysisReport, GUILayout.MinHeight(100f));
            }
        }

        private void StartAutoSolve()
        {
            BeginSearch(true, false);
            solverSessionStarted = searchTask != null;
        }

        private void StopAutoSolve()
        {
            summaryRecorder.Stop(adapter, "Stopped");
            solverSessionStarted = false;
            autoSolveGeneration = adapter != null ? adapter.Generation : -1;
            CancelSearch();
            playback.Stop("Stopped. Press Start Auto Solve to start again.");
        }

        private void BeginSearch(bool autoPlay, bool logAnalysis)
        {
            if (!EditorApplication.isPlaying) return;
            autoSolveGeneration = adapter != null ? adapter.Generation : -1;
            CancelSearch();
            if (playback.IsPlaying) playback.Stop("Previous playback stopped before analysis.");
            if (adapter == null || !adapter.IsAlive) FindActiveGame();
            if (adapter == null || !adapter.IsAlive)
            {
                status = "No active Flood Fill 3D game was found.";
                return;
            }
            if (!adapter.TryCaptureSnapshot(out boardSnapshot, out string captureError))
            {
                status = captureError;
                solution = null;
                return;
            }
            if (!AutoSolverGraphBuilder.TryBuild(boardSnapshot, out graph, out string graphError))
            {
                status = graphError;
                solution = null;
                return;
            }

            if (autoPlay) summaryRecorder.Begin(adapter, boardSnapshot);
            solution = null;
            analysisReport = string.Empty;
            logAnalysisWhenReady = logAnalysis;
            autoPlayWhenReady = autoPlay;
            searchCancellation = new CancellationTokenSource();
            CancellationToken token = searchCancellation.Token;
            AutoSolverGraph graphForWorker = graph;
            int remainingMoves = boardSnapshot.RemainingMoves;
            int timeBudget = searchTimeBudgetMs;
            int stateBudget = expandedStateBudget;
            int searchBeamWidth = beamWidth;
            searchTask = Task.Run(
                () => AutoSolverSearch.Solve(graphForWorker, remainingMoves, timeBudget, stateBudget, searchBeamWidth, token),
                token);
            status = autoPlay ? "Calculating and validating a solution before playback…" : "Calculating a solution on a background thread…";
            Repaint();
        }

        private void StartPlayback(bool oneStep)
        {
            if (adapter == null || boardSnapshot == null || graph == null || solution == null) return;
            if (!playback.IsPlaying)
            {
                playback.Start(adapter, boardSnapshot, graph, solution, playbackSpeed, animationDuration,
                    enableCaptureAnimation, enableColorTransition, enableCubePop, oneStep, out string error);
                if (playback.IsPlaying) summaryRecorder.EnsureRun(adapter, boardSnapshot, solution);
                else if (!string.IsNullOrEmpty(error)) playback.Stop(error);
                status = string.IsNullOrEmpty(error) ? playback.Status : error;
            }
            else if (oneStep)
            {
                playback.Step();
                status = playback.Status;
            }
        }

        private void FindActiveGame()
        {
            if (!EditorApplication.isPlaying)
            {
                adapter = null;
                status = "Enter Play Mode to use Auto Solver.";
                return;
            }
            adapter = AutoSolverAdapter.FindActive();
            nextConnectionSearch = EditorApplication.timeSinceStartup + 0.5d;
            status = adapter != null
                ? "Connected to the active 3D Flood Fill manager and voxel board."
                : "No active Flood Fill 3D game was found.";
            Repaint();
        }

        private void RestartLevel()
        {
            summaryRecorder.Observe(adapter); summaryRecorder.Finish("Restarted");
            CancelSearch();
            playback.Stop("Restarting through the game's own restart method.");
            if (adapter == null || !adapter.IsAlive) FindActiveGame();
            if (adapter == null || !adapter.IsAlive) return;
            try
            {
                adapter.RestartLevel();
                solution = null;
                graph = null;
                boardSnapshot = null;
                waitingForRestart = true;
                status = "Level restarted. Waiting for the game's move-limit calculation…";
            }
            catch (Exception exception)
            {
                status = "Restart failed: " + exception.Message;
            }
        }

        private void StartNextLevel()
        {
            summaryRecorder.Observe(adapter); summaryRecorder.Finish("Next level");
            CancelSearch();
            playback.Stop("Starting the next level in the current category.");
            if (adapter == null || !adapter.IsAlive) FindActiveGame();
            if (adapter == null || !adapter.IsAlive) return;
            try
            {
                adapter.StartNextLevel();
                solution = null;
                graph = null;
                boardSnapshot = null;
                waitingForRestart = true;
                status = "Next level started. Waiting for the game's move-limit calculation…";
            }
            catch (Exception exception)
            {
                status = "Could not start the next level: " + exception.Message;
            }
        }

        private void EditorUpdate()
        {
            if (!EditorApplication.isPlaying) return;
            if ((adapter == null || !adapter.IsAlive) && EditorApplication.timeSinceStartup >= nextConnectionSearch)
            {
                adapter = AutoSolverAdapter.FindActive();
                nextConnectionSearch = EditorApplication.timeSinceStartup + 0.5d;
            }

            summaryRecorder.Observe(adapter);
            if (adapter != null && adapter.IsAlive && (observedManager != adapter.Manager || observedGeneration != adapter.Generation))
            {
                bool hadBoard = observedGeneration >= 0;
                if (hadBoard && observedManager != adapter.Manager) solverSessionStarted = false;
                observedManager = adapter.Manager; observedGeneration = adapter.Generation;
                autoSolveGeneration = -1; autoNextAttempted = false; advanceAfter = -1;
                if (hadBoard)
                {
                    CancelSearch(); playback.Stop("Level changed. Previous solution cleared.");
                    boardSnapshot = null; graph = null; solution = null;
                }
            }
            if (waitingForRestart && adapter != null && adapter.IsAlive)
            {
                adapter.HideResultPanel();
                if (adapter.ReadyToSolve)
                {
                    adapter.ShowGameplayHud();
                    waitingForRestart = false;
                    if (!adapter.TryCaptureSnapshot(out boardSnapshot, out string captureError))
                    {
                        status = "Level restarted, but snapshot failed: " + captureError;
                    }
                    else if (AutoSolverGraphBuilder.TryBuild(boardSnapshot, out graph, out string graphError))
                    {
                        status = "Level restarted. Fresh board snapshot captured; calculate a solution when ready.";
                    }
                    else status = "Level restarted, but snapshot failed: " + graphError;
                }
            }

            if (searchTask != null && searchTask.IsCompleted) CompleteSearch();
            bool playbackWasRunning = playback.IsPlaying;
            int previousMoveIndex = playback.NextMoveIndex;
            playback.Tick(EditorApplication.timeSinceStartup);
            summaryRecorder.Observe(adapter, Math.Max(0, playback.NextMoveIndex - previousMoveIndex));
            TickAutomation();
            double repaintInterval = playback.IsPlaying || searchTask != null || waitingForRestart ? 0.08d : 0.15d;
            if (adapter != null && adapter.IsAlive && EditorApplication.timeSinceStartup - lastRepaintTime > repaintInterval)
            {
                lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
            else if (playbackWasRunning && !playback.IsPlaying) Repaint();
        }

        private void TickAutomation()
        {
            if (!solverSessionStarted || adapter == null || !adapter.IsAlive || !adapter.BoardVisible || waitingForRestart || searchTask != null || playback.IsPlaying) return;
            if (autoStartSolver && adapter.ReadyToSolve && autoSolveGeneration != adapter.Generation)
            {
                autoSolveGeneration = adapter.Generation;
                BeginSearch(true, false);
                return;
            }
            if (adapter.GameState != "Won") { advanceAfter = -1; return; }
            if (!autoNextLevel || autoNextAttempted) return;
            if (!adapter.CanStartNextLevel)
            {
                autoNextAttempted = true;
                status = "Category complete: level 100 reached. Auto Next Level stopped.";
                return;
            }
            if (advanceAfter < 0) { advanceAfter = EditorApplication.timeSinceStartup + 1; return; }
            if (EditorApplication.timeSinceStartup < advanceAfter) return;
            autoNextAttempted = true;
            StartNextLevel();
        }

        private void CompleteSearch()
        {
            Task<AutoSolverResult> completed = searchTask;
            searchTask = null;
            searchCancellation?.Dispose();
            searchCancellation = null;
            if (completed.IsCanceled)
            {
                summaryRecorder.SearchCancelled();
                status = "Search cancelled.";
                autoPlayWhenReady = false;
                Repaint();
                return;
            }
            if (completed.IsFaulted)
            {
                summaryRecorder.SearchFailed();
                Exception exception = completed.Exception != null ? completed.Exception.GetBaseException() : null;
                status = "Solver error: " + (exception != null ? exception.Message : "Unknown background search failure.");
                Debug.LogException(exception ?? new Exception(status));
                autoPlayWhenReady = false;
                Repaint();
                return;
            }

            solution = completed.Result;
            if (solution.Conclusion == AutoSolverConclusion.Solved && solution.ValidationPassed)
            {
                solution.ValidationPassed = AutoSolverSearch.TryValidate(graph, graph.RegionColors, solution.Moves, out solution.CapturedPerMove);
                if (!solution.ValidationPassed)
                {
                    solution.Conclusion = AutoSolverConclusion.Invalid;
                    solution.Message = "Independent pre-playback validation failed; playback was blocked.";
                }
            }
            if (autoPlayWhenReady && solution.ValidationPassed && !solution.FitsMoveBudget && adapter != null &&
                adapter.TryGetBudgetSolution(boardSnapshot, graph, out var budgetSolution))
            {
                budgetSolution.CalculationMilliseconds = solution.CalculationMilliseconds;
                solution = budgetSolution;
            }
            if (autoPlayWhenReady) summaryRecorder.SearchFinished(adapter, solution);
            status = "Search finished: " + AutoSolverDiagnostics.Classify(boardSnapshot, solution) + ". " + solution.Message;
            if (logAnalysisWhenReady)
            {
                analysisReport = AutoSolverDiagnostics.FormatReport(boardSnapshot, graph, solution, adapter != null ? adapter.Palette : null);
                Debug.Log(analysisReport, adapter != null ? adapter.Manager : null);
            }
            if (autoPlayWhenReady && solution.Conclusion == AutoSolverConclusion.Solved && solution.ValidationPassed)
                StartPlayback(false);
            autoPlayWhenReady = false;
            logAnalysisWhenReady = false;
            Repaint();
        }

        private void CancelSearch()
        {
            if (searchCancellation != null) summaryRecorder.SearchCancelled();
            if (searchCancellation != null)
            {
                searchCancellation.Cancel();
                searchCancellation.Dispose();
                searchCancellation = null;
            }
            searchTask = null;
            autoPlayWhenReady = false;
            logAnalysisWhenReady = false;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
            {
                summaryRecorder.Observe(adapter); summaryRecorder.Finish("Play Mode ended");
                CancelSearch();
                solverSessionStarted = false;
                playback.Stop("Play Mode ended. Playback cleaned up safely.");
                adapter = null;
                boardSnapshot = null;
                graph = null;
                solution = null;
                waitingForRestart = false;
                observedManager = null; observedGeneration = autoSolveGeneration = -1;
                autoNextAttempted = false; advanceAfter = -1;
                status = "Enter Play Mode to use Auto Solver.";
                Repaint();
            }
            else if (change == PlayModeStateChange.EnteredPlayMode) FindActiveGame();
        }

        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            summaryRecorder.Observe(adapter); summaryRecorder.Finish("Scene changed");
            CancelSearch();
            solverSessionStarted = false;
            playback.Stop("Scene changed. Playback stopped and temporary animation settings were restored.");
            adapter = EditorApplication.isPlaying ? AutoSolverAdapter.FindActive() : null;
            boardSnapshot = null;
            graph = null;
            solution = null;
            waitingForRestart = false;
            observedManager = null; observedGeneration = autoSolveGeneration = -1;
            Repaint();
        }

        private static string DescribeColor(int colorIndex, Color[] palette)
        {
            if (colorIndex < 0) return "None";
            if (palette == null || colorIndex >= palette.Length) return "Color " + colorIndex;
            Color color = palette[colorIndex];
            return "Color " + colorIndex + " (#" + ColorUtility.ToHtmlStringRGB(color) + ")";
        }

        private static string SpeedName(float value)
        {
            if (Mathf.Approximately(value, 0.5f)) return "0.5x (0.80s per move)";
            if (Mathf.Approximately(value, 1f)) return "1x (0.40s per move)";
            if (Mathf.Approximately(value, 2f)) return "2x (0.20s per move)";
            if (Mathf.Approximately(value, 4f)) return "4x (0.10s per move)";
            return value.ToString("0.0") + "x";
        }
    }
}
