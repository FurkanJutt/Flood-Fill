using System;
using System.IO;
using FloodFill.ThreeD.Solver;
using UnityEngine;

namespace FloodFill.ThreeD
{
    [Serializable]
    public sealed class FloodFillSessionSaveData3D
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int selectedBoardMode;
        public int selectedBoardSize;
        public int difficulty;
        public int moveCount;
        public int score;
        public int currentMoveBudget;
        public int selectedColorIndex = -1;
        public int gameState;
        public MoveBudgetProfile moveBudgetProfile;
        public VoxelBoardSaveData3D board;
        public OrbitCameraSaveData3D camera;
    }

    [Serializable]
    public sealed class VoxelBoardSaveData3D
    {
        public int width;
        public int height;
        public int depth;
        public int volumeMode;
        public int currentPlayerColor = -1;
        public int generationSeed;
        public VoxelSaveData3D[] voxels;
    }

    [Serializable]
    public sealed class VoxelSaveData3D
    {
        public int x;
        public int y;
        public int z;
        public int colorIndex;
        public bool captured;
        public bool starting;
    }

    [Serializable]
    public sealed class OrbitCameraSaveData3D
    {
        public Vector3 targetPosition;
        public float yaw;
        public float pitch;
        public float distance;
        public float defaultDistance;
    }

    public static class FloodFillSessionPersistence3D
    {
        private const string SaveFileName = "flood-fill-3d-session-v1.json";

        public static string SavePath => Path.Combine(
            Application.persistentDataPath,
            SaveFileName);

        public static bool TryLoad(out FloodFillSessionSaveData3D data)
        {
            data = null;
            try
            {
                if (!File.Exists(SavePath))
                {
                    return false;
                }

                string json = File.ReadAllText(SavePath);
                data = JsonUtility.FromJson<FloodFillSessionSaveData3D>(json);
                return data != null && data.version == FloodFillSessionSaveData3D.CurrentVersion;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not load the saved 3D Flood Fill session: {exception.Message}");
                data = null;
                return false;
            }
        }

        public static bool Save(FloodFillSessionSaveData3D data)
        {
            if (data == null)
            {
                return false;
            }

            string temporaryPath = SavePath + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(data);
                File.WriteAllText(temporaryPath, json);
                File.Copy(temporaryPath, SavePath, true);
                File.Delete(temporaryPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not save the 3D Flood Fill session: {exception.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }

                string temporaryPath = SavePath + ".tmp";
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not clear the saved 3D Flood Fill session: {exception.Message}");
            }
        }
    }
}
