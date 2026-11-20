using System;
using UnityEngine;

namespace FloodFill.ThreeD
{
    /// <summary>Versioned additive catalog. Legacy campaign and session keys remain intact.</summary>
    public static class FloodLevelProgression3D
    {
        public const int LevelsPerCategory = 100, PageSize = 30, PageCount = 4;
        private const string Key = "FloodFill3D.LevelCatalog.v1";
        [Serializable] public sealed class Result { public bool completed; public int stars; public int bestMoves; }
        [Serializable] public sealed class Catalog { public int version = 1; public Result[] levels = new Result[1200]; }
        private static Catalog data;
        private static Catalog Data
        {
            get
            {
                if (data != null) return data;
                try { data = JsonUtility.FromJson<Catalog>(PlayerPrefs.GetString(Key, "")); }
                catch (Exception e) { Debug.LogWarning("Level catalog could not be read: " + e.Message); }
                if (data == null || data.version != 1 || data.levels == null || data.levels.Length != 1200) data = new Catalog();
                return data;
            }
        }
        public static int Index(int mode, int difficulty, int level)
        {
            if (mode < 0 || mode > 2 || difficulty < 0 || difficulty > 3 || level < 1 || level > 100) throw new ArgumentOutOfRangeException("Invalid level identity");
            return (mode * 4 + difficulty) * 100 + level - 1;
        }
        public static Result Get(int mode, int difficulty, int level) => Data.levels[Index(mode, difficulty, level)] ?? new Result();
        public static bool Unlocked(int mode, int difficulty, int level) => level >= 1 && level <= 100 && (level == 1 || Get(mode, difficulty, level).completed || Get(mode, difficulty, level - 1).completed);
        public static int Completed(int mode, int difficulty)
        {
            int count = 0; for (int l = 1; l <= 100; l++) if (Get(mode, difficulty, l).completed) count++; return count;
        }
        public static int Next(int mode, int difficulty)
        {
            for (int l = 1; l <= 100; l++) if (!Get(mode, difficulty, l).completed) return l;
            return 100;
        }
        public static int Seed(int mode, int difficulty, int level) => unchecked(0x46463344 + Index(mode, difficulty, level) * 104729);
        public static void Record(int mode, int difficulty, int level, int moves, int stars)
        {
            int index = Index(mode, difficulty, level); Result result = Get(mode, difficulty, level);
            result.bestMoves = result.completed ? Mathf.Min(result.bestMoves, moves) : moves;
            result.completed = true; result.stars = Mathf.Max(result.stars, Mathf.Clamp(stars, 1, 3));
            Data.levels[index] = result;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data)); PlayerPrefs.Save();
        }
        public static int Stars(int moves, int budget)
        {
            var rules = Resources.Load<FloodLevelRules3D>("FloodLevelRules");
            float three = rules != null ? rules.threeStarBudgetRatio : .7f;
            float two = rules != null ? rules.twoStarBudgetRatio : .9f;
            float ratio = moves / (float)Mathf.Max(1, budget);
            return ratio <= Mathf.Min(three, two) ? 3 : ratio <= Mathf.Max(three, two) ? 2 : 1;
        }
    }
}
