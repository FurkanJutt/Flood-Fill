using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    public sealed class AutoSolverSummaryWindow : EditorWindow
    {
        private Vector2 scroll;
        private int view;
        private string filter = "";
        private readonly bool[] expanded = { true, true, true, true };
        private static readonly string[] Difficulties = { "EASY", "MEDIUM", "HARD", "EXPERT" };
        private static readonly string[] Modes = { "Full Grid", "Random Shape", "Items" };
        private static readonly float[] LevelWidths = { 115, 85, 55, 155, 80, 80, 65, 55, 50, 85, 105, 85, 85, 110, 85, 65, 85, 90, 110, 110, 155 };
        private static readonly string[] LevelHeaders = { "Mode", "Type", "Level", "Latest status", "Completed", "Tries to win", "Attempts", "Losses", "Wins", "Moves used", "Moves required", "Given moves", "Best moves", "Level seed", "Board size", "Voxels", "Captured", "Search ms", "Level time", "Solver time", "Updated (local)" };
        private static readonly float[] AttemptWidths = { 115, 85, 55, 155, 120, 105, 85, 85, 85, 95, 110, 85, 65, 70, 90, 110, 110, 155 };
        private static readonly string[] AttemptHeaders = { "Mode", "Type", "Level", "Outcome", "Search result", "Moves required", "Moves used", "Given moves", "Start moves", "Solver moves", "Level seed", "Board size", "Voxels", "Searches", "Search ms", "Level time", "Solver time", "Started (local)" };

        [MenuItem("Tools/Flood Fill/Solver Summary")]
        public static void OpenWindow()
        {
            var window = GetWindow<AutoSolverSummaryWindow>("Solver Summary");
            window.minSize = new Vector2(850, 350); window.Show();
        }
        private void OnEnable() => AutoSolverSummaryStore.Changed += Repaint;
        private void OnDisable() => AutoSolverSummaryStore.Changed -= Repaint;
        private void OnGUI()
        {
            var store = AutoSolverSummaryStore.Shared;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                view = EditorGUILayout.Popup(view, new[] { "All levels", "Attempt history", "Completed levels" }, EditorStyles.toolbarPopup, GUILayout.Width(150));
                GUILayout.FlexibleSpace();
                filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.Width(220));
                using (new EditorGUI.DisabledScope(!System.IO.File.Exists(store.FilePath)))
                    if (GUILayout.Button("Show Save File", EditorStyles.toolbarButton, GUILayout.Width(100))) EditorUtility.RevealInFinder(store.FilePath);
            }
            if (!string.IsNullOrEmpty(store.Error)) EditorGUILayout.HelpBox(store.Error, MessageType.Error);
            var all = store.Data.attempts;
            int completed = all.Where(a => a.Won).Select(a => a.LevelKey).Distinct().Count();
            int tracked = all.Select(a => a.LevelKey).Distinct().Count();
            EditorGUILayout.LabelField(tracked + " levels   ·   " + completed + " completed   ·   " + (tracked - completed) + " unfinished   ·   " + all.Count + " attempts", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("One try is one board run where Auto Solve starts. Restarting creates a new try; stopping and resuming the same board does not. Tries to win counts attempts up to the first recorded win. Analysis alone is excluded. All levels includes in-progress and failed runs. Rows show the latest attempt; Completed means the level has been won at least once. Win-only statistics stay blank until a win. Moves required is the latest solver plan length from its analysis position, not a proven minimum. Level time matches the gameplay clock (active play, including time before Auto Solve). Solver time is wall time since starting the solver, including search and pauses. Times freeze on win/loss; older entries without a recorded level clock show —.", MessageType.Info);
            if (all.Count == 0) EditorGUILayout.HelpBox("No solver runs recorded yet. Press Start Auto Solve to begin recording. Earlier unlogged attempts cannot be reconstructed.", MessageType.Info);
            float[] widths = view != 1 ? LevelWidths : AttemptWidths;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(widths.Sum())))
            {
                for (int difficulty = 0; difficulty < 4; difficulty++)
                {
                    var category = all.Where(a => a.difficulty == difficulty).ToList();
                    int count = category.Where(a => a.Won).Select(a => a.LevelKey).Distinct().Count();
                    expanded[difficulty] = EditorGUILayout.Foldout(expanded[difficulty], Difficulties[difficulty] + "   ·   " + category.Select(a => a.LevelKey).Distinct().Count() + " levels   ·   " + count + " completed   ·   " + category.Count + " attempts", true, EditorStyles.foldoutHeader);
                    if (!expanded[difficulty]) continue;
                    Row(view != 1 ? LevelHeaders : AttemptHeaders, widths, true, false);
                    int row = 0;
                    if (view != 1)
                    {
                        foreach (var entry in AutoSolverLevelSummary.Build(category).Where(entry => (view != 2 || entry.Completed) && entry.Attempts.Any(Matches)))
                        {
                            var latest = entry.LatestAttempt;
                            string updated = !string.IsNullOrEmpty(latest.updatedUtc) ? latest.updatedUtc : !string.IsNullOrEmpty(latest.finishedUtc) ? latest.finishedUtc : latest.startedUtc;
                            Row(new[] { Mode(latest), latest.kind, Level(latest), latest.outcome == "Running" ? "In progress" : latest.outcome,
                                entry.Completed ? "Yes" : "No", entry.Completed ? entry.TriesToWin.ToString() : "—", entry.Attempts.Count.ToString(), entry.Losses.ToString(), entry.Wins.ToString(),
                                latest.moves.ToString(), MovesRequired(latest), latest.moveBudget.ToString(), entry.Completed ? entry.BestMoves.ToString() : "—", latest.seed.ToString(), Size(latest), latest.voxels.ToString(),
                                (latest.Won ? 100f : Mathf.Min(99.99f, latest.capturedPercentage)).ToString("0.##") + "%", latest.searchMilliseconds.ToString("0.0"), LevelTime(latest), Duration(latest.elapsedSeconds), LocalDate(updated) }, widths, false, row++ % 2 == 0);
                        }
                    }
                    else foreach (var attempt in category.Where(Matches).OrderByDescending(a => a.startedUtc, StringComparer.Ordinal))
                        Row(new[] { Mode(attempt), attempt.kind, Level(attempt), attempt.outcome, attempt.searchResult == "Solved" ? "Plan found (legacy)" : attempt.searchResult, MovesRequired(attempt), attempt.moves.ToString(), attempt.moveBudget.ToString(), attempt.startingMoves.ToString(), attempt.solverMoves.ToString(), attempt.seed.ToString(), Size(attempt), attempt.voxels.ToString(), attempt.searches.ToString(), attempt.searchMilliseconds.ToString("0.0"), LevelTime(attempt), Duration(attempt.elapsedSeconds), LocalDate(attempt.startedUtc) }, widths, false, row++ % 2 == 0);
                    if (row == 0) EditorGUILayout.LabelField(view == 2 ? "No recorded completions in this difficulty." : "No attempts in this difficulty.", EditorStyles.miniLabel);
                    GUILayout.Space(12);
                }
            }
            EditorGUILayout.EndScrollView();
        }
        private bool Matches(AutoSolverAttempt a) => string.IsNullOrWhiteSpace(filter) || (Mode(a) + " " + a.kind + " " + a.level + " " + a.seed + " " + a.outcome).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        internal static string Duration(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss\.f");
        internal static string LevelTime(AutoSolverAttempt attempt) => attempt.hasLevelElapsedTime ? Duration(attempt.levelElapsedSeconds) : "—";
        private static string MovesRequired(AutoSolverAttempt attempt) => attempt.plannedMoves > 0 ? attempt.plannedMoves.ToString() : "—";
        private static string Mode(AutoSolverAttempt a) => a.mode >= 0 && a.mode < Modes.Length ? Modes[a.mode] : "Unknown";
        private static string Level(AutoSolverAttempt a) => a.level > 0 ? a.level.ToString() : "—";
        private static string Size(AutoSolverAttempt a) => a.width + "×" + a.height + "×" + a.depth;
        private static string LocalDate(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "—";
        private static void Row(string[] values, float[] widths, bool header, bool alternate)
        {
            Rect rect = GUILayoutUtility.GetRect(widths.Sum(), 24, GUILayout.ExpandWidth(false));
            if (header || alternate) EditorGUI.DrawRect(rect, new Color(1,1,1,header ? .10f : .035f));
            float x = rect.x;
            for (int i = 0; i < values.Length; i++)
            {
                Rect cell = new Rect(x + 5, rect.y + 2, widths[i] - 9, 20);
                if (header) GUI.Label(cell, new GUIContent(values[i], values[i]), EditorStyles.boldLabel);
                else EditorGUI.SelectableLabel(cell, values[i] ?? "—", EditorStyles.label);
                x += widths[i];
            }
        }
    }
}
