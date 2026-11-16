using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FloodFill.Development.AutoSolver
{
    internal sealed class AutoSolverAdapter
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string ManagerTypeName = "FloodFill.ThreeD.FloodFillGameManager3D";
        private const string MobileUiTypeName = "FloodFill.ThreeD.FloodFillMobileUI3D";
        private static Type managerType;
        private static Type voxelType;

        public Component Manager { get; private set; }
        public Component Board { get; private set; }

        private AutoSolverAdapter(Component manager, Component board)
        {
            Manager = manager;
            Board = board;
        }

        public static AutoSolverAdapter FindActive()
        {
            if (!EditorApplication.isPlaying) return null;
            managerType = ResolveType(ManagerTypeName);
            if (managerType == null) return null;
            UnityEngine.Object[] managers = Resources.FindObjectsOfTypeAll(managerType);
            for (int i = 0; i < managers.Length; i++)
            {
                Component component = managers[i] as Component;
                if (component == null || component.gameObject == null ||
                    !component.gameObject.activeInHierarchy || !component.gameObject.scene.IsValid()) continue;
                Component board = GetValue(component, "BoardManager") as Component;
                if (board == null) continue;
                voxelType = ResolveType("FloodFill.ThreeD.VoxelCell3D");
                return new AutoSolverAdapter(component, board);
            }
            return null;
        }

        public bool IsAlive => Manager != null && Board != null && Manager.gameObject != null && Board.gameObject != null;
        public string GameState => ReadString(Manager, "State", "Unknown");
        public int MoveCount => ReadInt(Manager, "MoveCount", 0);
        public int MoveLimit => ReadInt(Manager, "CurrentMoveBudget", 0);
        public int CurrentColor => ReadInt(Board, "CurrentPlayerColor", -1);
        public int SelectedColor => ReadInt(Manager, "SelectedColorIndex", -1);
        public int CapturedCount => ReadInt(Board, "CapturedVoxelCount", 0);
        public int TotalVoxelCount => ReadInt(Board, "TotalVoxelCount", 0);
        public float CapturedPercentage => ReadFloat(Board, "CapturedPercentage", 0f);
        public Color[] Palette => GetField(Board, "palette") as Color[];

        public bool TryCaptureSnapshot(out AutoSolverBoardSnapshot snapshot, out string error)
        {
            return TryCaptureSnapshot(false, out snapshot, out error);
        }

        private bool TryCaptureSnapshot(bool allowTerminalState, out AutoSolverBoardSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            if (!IsAlive)
            {
                error = "The active game or voxel board was destroyed.";
                return false;
            }
            if (!allowTerminalState && GameState != "Playing")
            {
                error = "The current game state is " + GameState + ". Start or resume a level before analyzing it.";
                return false;
            }

            Component boardRoot = GetField(Board, "boardRoot") as Component;
            if (boardRoot != null && !boardRoot.gameObject.activeInHierarchy)
            {
                error = "The gameplay board is hidden. Return to the active level before analyzing it.";
                return false;
            }

            IList voxels = GetField(Board, "allVoxels") as IList;
            if (voxels == null || voxels.Count == 0)
            {
                error = "Could not read the board's active voxel collection.";
                return false;
            }

            int width = ReadInt(Board, "Width", 0);
            int height = ReadInt(Board, "Height", 0);
            int depth = ReadInt(Board, "Depth", 0);
            Color[] palette = GetField(Board, "palette") as Color[];
            if (width < 1 || height < 1 || depth < 1 || palette == null || palette.Length < 2)
            {
                error = "Board dimensions or palette could not be read reliably.";
                return false;
            }

            var logicalIndices = new int[voxels.Count];
            var colors = new byte[voxels.Count];
            var captured = new bool[voxels.Count];
            var liveVoxels = new Component[voxels.Count];
            object startingVoxel = GetValue(Board, "StartingVoxel");
            int startingVoxelIndex = -1;
            for (int i = 0; i < voxels.Count; i++)
            {
                Component voxel = voxels[i] as Component;
                if (voxel == null)
                {
                    error = "The active voxel collection contains a destroyed or non-component entry.";
                    return false;
                }
                int x = ReadInt(voxel, "X", -1);
                int y = ReadInt(voxel, "Y", -1);
                int z = ReadInt(voxel, "Z", -1);
                int color = ReadInt(voxel, "ColorIndex", -1);
                if (x < 0 || x >= width || y < 0 || y >= height || z < 0 || z >= depth ||
                    color < 0 || color >= palette.Length)
                {
                    error = "A voxel has coordinates or a color outside the board's valid range.";
                    return false;
                }
                logicalIndices[i] = x + width * (y + height * z);
                colors[i] = (byte)color;
                captured[i] = ReadBool(voxel, "IsCaptured", false);
                liveVoxels[i] = voxel;
                if (ReferenceEquals(voxels[i], startingVoxel)) startingVoxelIndex = i;
            }

            if (startingVoxelIndex < 0)
            {
                error = "Could not identify the board's starting voxel.";
                return false;
            }

            snapshot = new AutoSolverBoardSnapshot
            {
                Width = width,
                Height = height,
                Depth = depth,
                ColorCount = palette.Length,
                MoveCount = MoveCount,
                MoveLimit = MoveLimit,
                SelectedColor = SelectedColor,
                CurrentPlayerColor = CurrentColor,
                GameState = GameState,
                LogicalIndices = logicalIndices,
                Colors = colors,
                Captured = captured,
                Voxels = liveVoxels,
                StartingVoxelIndex = startingVoxelIndex
            };
            return true;
        }

        public bool TryInvokeMove(Component representativeVoxel, int colorIndex, out string error)
        {
            error = null;
            if (!IsAlive || representativeVoxel == null)
            {
                error = "The game or selected voxel no longer exists.";
                return false;
            }
            if (GameState != "Playing")
            {
                error = "The game is no longer in the Playing state.";
                return false;
            }

            try
            {
                MethodInfo selectColor = Manager.GetType().GetMethod("SelectColor", InstanceFlags, null, new[] { typeof(int) }, null);
                if (selectColor == null)
                {
                    error = "Could not find FloodFillGameManager3D.SelectColor(int).";
                    return false;
                }
                selectColor.Invoke(Manager, new object[] { colorIndex });

                Type actualVoxelType = voxelType ?? representativeVoxel.GetType();
                MethodInfo clickHandler = Manager.GetType().GetMethod("HandleVoxelClicked", InstanceFlags, null, new[] { actualVoxelType }, null);
                if (clickHandler == null)
                {
                    error = "Could not find the existing HandleVoxelClicked(VoxelCell3D) gameplay handler.";
                    return false;
                }
                clickHandler.Invoke(Manager, new object[] { representativeVoxel });
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException != null ? exception.InnerException.Message : exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TrySetBoardInput(bool enabled, out bool previous, out string error)
        {
            previous = true;
            error = null;
            if (!IsAlive)
            {
                error = "The active board is no longer available.";
                return false;
            }
            FieldInfo inputField = Board.GetType().GetField("inputEnabled", InstanceFlags);
            MethodInfo setter = Board.GetType().GetMethod("SetInputEnabled", InstanceFlags, null, new[] { typeof(bool) }, null);
            if (inputField == null || setter == null)
            {
                error = "Could not safely lock and restore manual voxel input.";
                return false;
            }
            previous = inputField.GetValue(Board) is bool current && current;
            setter.Invoke(Board, new object[] { enabled });
            return true;
        }

        public void RestoreBoardInput(bool enabled)
        {
            if (!IsAlive) return;
            MethodInfo setter = Board.GetType().GetMethod("SetInputEnabled", InstanceFlags, null, new[] { typeof(bool) }, null);
            setter?.Invoke(Board, new object[] { enabled });
        }

        public void RestartLevel()
        {
            if (!IsAlive) throw new InvalidOperationException("No active game is available.");
            MethodInfo restart = Manager.GetType().GetMethod("RestartGame", InstanceFlags, null, Type.EmptyTypes, null);
            if (restart == null) throw new MissingMethodException("The game's existing RestartGame() method was not found.");
            restart.Invoke(Manager, null);
            HideResultPanel();
        }

        public void StartNextLevel()
        {
            if (!IsAlive) throw new InvalidOperationException("No active game is available.");
            if (GameState != "Won") throw new InvalidOperationException("Next Level is available after a campaign level is won.");
            MethodInfo startCampaign = Manager.GetType().GetMethod("StartCampaignGame", InstanceFlags, null, new[] { typeof(int) }, null);
            if (startCampaign == null) throw new MissingMethodException("The game's existing StartCampaignGame(int) method was not found.");
            int difficulty = Convert.ToInt32(GetValue(Manager, "SelectedDifficulty"));
            startCampaign.Invoke(Manager, new object[] { difficulty });
        }

        public void HideResultPanel()
        {
            if (!IsAlive) return;
            GameObject resultPanel = GetField(Manager, "resultPanel") as GameObject;
            if (resultPanel != null && resultPanel.activeSelf)
                resultPanel.SetActive(false);
        }

        public bool ShowGameplayHud()
        {
            if (!IsAlive || GameState != "Playing") return false;
            Type uiType = ResolveType(MobileUiTypeName);
            if (uiType == null) return false;

            UnityEngine.Object[] uiObjects = Resources.FindObjectsOfTypeAll(uiType);
            for (int i = 0; i < uiObjects.Length; i++)
            {
                Component ui = uiObjects[i] as Component;
                if (ui == null || !ui.gameObject.activeInHierarchy || !ui.gameObject.scene.IsValid() ||
                    !ReferenceEquals(GetField(ui, "manager"), Manager)) continue;

                GameObject hud = GetField(ui, "hud") as GameObject;
                MethodInfo show = uiType.GetMethod("Show", InstanceFlags, null, new[] { typeof(GameObject) }, null);
                if (hud == null || show == null) return false;
                try
                {
                    show.Invoke(ui, new object[] { hud });
                    HideResultPanel();
                    return hud.activeInHierarchy;
                }
                catch (TargetInvocationException exception)
                {
                    Debug.LogException(exception.InnerException ?? exception, ui);
                    return false;
                }
            }
            return false;
        }

        public AutoSolverBoardSnapshot CaptureLiveSnapshot()
        {
            return TryCaptureSnapshot(true, out AutoSolverBoardSnapshot snapshot, out _) ? snapshot : null;
        }

        public bool MatchesExpectedColors(byte[] expectedRegionColors, int[] voxelRegionIds)
        {
            if (!IsAlive || expectedRegionColors == null || voxelRegionIds == null) return false;
            IList voxels = GetField(Board, "allVoxels") as IList;
            if (voxels == null || voxels.Count != voxelRegionIds.Length) return false;
            for (int i = 0; i < voxels.Count; i++)
            {
                if (ReadInt(voxels[i], "ColorIndex", -1) != expectedRegionColors[voxelRegionIds[i]]) return false;
            }
            return true;
        }

        private static object GetValue(object target, string name)
        {
            if (target == null) return null;
            PropertyInfo property = target.GetType().GetProperty(name, InstanceFlags);
            if (property != null) return property.GetValue(target, null);
            return GetField(target, name);
        }

        private static object GetField(object target, string name) =>
            target != null ? target.GetType().GetField(name, InstanceFlags)?.GetValue(target) : null;

        private static int ReadInt(object target, string name, int fallback)
        {
            object value = GetValue(target, name);
            return value is int number ? number : fallback;
        }

        private static float ReadFloat(object target, string name, float fallback)
        {
            object value = GetValue(target, name);
            return value is float number ? number : fallback;
        }

        private static bool ReadBool(object target, string name, bool fallback)
        {
            object value = GetValue(target, name);
            return value is bool flag ? flag : fallback;
        }

        private static string ReadString(object target, string name, string fallback)
        {
            object value = GetValue(target, name);
            return value != null ? value.ToString() : fallback;
        }

        private static Type ResolveType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
