using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    [UnityEditor.InitializeOnLoad]
    internal static class AutoSolverSummarySession
    {
        private const string Key = "FloodFill.AutoSolverSummary.PlaySession";
        static AutoSolverSummarySession()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
                    UnityEditor.SessionState.SetString(Key, Guid.NewGuid().ToString("N"));
            };
        }
        public static string RunId(AutoSolverAdapter adapter)
        {
            string session = UnityEditor.SessionState.GetString(Key, "");
            if (session.Length == 0) { session = Guid.NewGuid().ToString("N"); UnityEditor.SessionState.SetString(Key, session); }
            return session + ":" + adapter.Manager.GetEntityId() + ":" + adapter.Generation;
        }
    }

    [Serializable]
    internal sealed class AutoSolverAttempt
    {
        public string id, boardRunId, kind, startedUtc, finishedUtc, updatedUtc, outcome, searchResult, algorithm, unityVersion;
        public int mode, difficulty, level, seed, width, height, depth, voxels, moveBudget, startingMoves, moves, solverMoves, score, searches, plannedMoves;
        public double searchMilliseconds, elapsedSeconds;
        public float capturedPercentage;
        public bool provenOptimal;
        public string LevelKey => kind + ":" + mode + ":" + difficulty + ":" + level + (kind == "Custom" ? ":" + seed + ":" + width + ":" + height + ":" + depth + ":" + id : "");
        public bool Won => outcome == "Won";
    }

    [Serializable]
    internal sealed class AutoSolverSummaryData
    {
        public int version = 1;
        public List<AutoSolverAttempt> attempts = new List<AutoSolverAttempt>();
    }

    internal sealed class AutoSolverLevelSummary
    {
        public readonly List<AutoSolverAttempt> Attempts;
        public readonly AutoSolverAttempt LatestAttempt, LatestWin;
        public readonly int TriesToWin, Wins, Losses, BestMoves;
        public bool Completed => Wins > 0;
        private AutoSolverLevelSummary(IEnumerable<AutoSolverAttempt> attempts)
        {
            Attempts = attempts.OrderBy(a => a.startedUtc, StringComparer.Ordinal).ToList();
            LatestAttempt = Attempts.Last();
            var wins = Attempts.Where(a => a.Won).ToList();
            LatestWin = wins.LastOrDefault(); Wins = wins.Count;
            Losses = Attempts.Count(a => a.outcome == "Lost");
            BestMoves = Completed ? wins.Min(a => a.moves) : 0;
            TriesToWin = Completed ? Attempts.IndexOf(wins.First()) + 1 : 0;
        }
        public static IEnumerable<AutoSolverLevelSummary> Build(IEnumerable<AutoSolverAttempt> attempts) => attempts
            .GroupBy(a => a.LevelKey).Select(g => new AutoSolverLevelSummary(g))
            .OrderBy(row => row.LatestAttempt.mode).ThenBy(row => row.LatestAttempt.level);
    }

    internal sealed class AutoSolverSummaryStore
    {
        private static AutoSolverSummaryStore shared;
        public static AutoSolverSummaryStore Shared => shared ?? (shared = new AutoSolverSummaryStore(Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserSettings/FloodFillSolverSummary.json")));
        public static event Action Changed;
        public readonly string FilePath;
        public AutoSolverSummaryData Data { get; private set; } = new AutoSolverSummaryData();
        public string Error { get; private set; }
        private bool writable = true;
        public AutoSolverSummaryStore(string path)
        {
            FilePath = path;
            if (!File.Exists(path)) return;
            try
            {
                Data = JsonUtility.FromJson<AutoSolverSummaryData>(File.ReadAllText(path));
                if (Data == null || Data.version != 1 || Data.attempts == null || Data.attempts.Exists(a => a == null))
                    throw new InvalidDataException("Unsupported or damaged summary file.");
                foreach (var attempt in Data.attempts)
                    if (attempt.outcome == "Running") attempt.outcome = "Interrupted";
            }
            catch (Exception exception)
            {
                writable = false; Data = new AutoSolverSummaryData();
                Error = "Summary could not be loaded. The existing file is preserved and saving is disabled: " + exception.Message;
                Debug.LogWarning(Error);
            }
        }
        public void Save()
        {
            if (!writable) { Changed?.Invoke(); return; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(Data, true));
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
                else File.Move(temporary, FilePath);
                Error = null;
            }
            catch (Exception exception)
            {
                Error = "Summary is available in memory but could not be saved: " + exception.Message;
                Debug.LogWarning(Error);
            }
            Changed?.Invoke();
        }
    }
}
