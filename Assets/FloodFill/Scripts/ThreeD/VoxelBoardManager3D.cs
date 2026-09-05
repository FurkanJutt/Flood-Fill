using System;
using System.Collections.Generic;
using UnityEngine;

namespace FloodFill.ThreeD
{
    public sealed class VoxelBoardManager3D : MonoBehaviour
    {
        public enum VoxelVolumeMode
        {
            HollowCube,
            SolidCube
        }

        private static readonly Vector3Int[] Directions =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1)
        };

        [Header("Volume")]
        [SerializeField, Min(1)] private int width = 6;
        [SerializeField, Min(1)] private int height = 6;
        [SerializeField, Min(1)] private int depth = 6;
        [SerializeField] private VoxelVolumeMode volumeMode = VoxelVolumeMode.HollowCube;

        [Header("Voxel Layout")]
        [SerializeField, Min(0.05f)] private float voxelSize = 1f;
        [SerializeField, Min(0f)] private float voxelGap = 0.05f;

        [Header("References")]
        [SerializeField] private Transform boardRoot;
        [SerializeField] private VoxelCell3D voxelPrefab;

        private readonly List<VoxelCell3D> allVoxels = new List<VoxelCell3D>();
        private readonly List<VoxelCell3D> capturedVoxels = new List<VoxelCell3D>();
        private VoxelCell3D[,,] cells;
        private Color[] palette = Array.Empty<Color>();

        public int Width => width;
        public int Height => height;
        public int Depth => depth;
        public VoxelVolumeMode VolumeMode => volumeMode;
        public int CurrentPlayerColor { get; private set; } = -1;
        public int CapturedVoxelCount => capturedVoxels.Count;
        public int TotalVoxelCount => allVoxels.Count;
        public int LastNewlyCapturedCount { get; private set; }
        public float CapturedPercentage => TotalVoxelCount > 0
            ? CapturedVoxelCount * 100f / TotalVoxelCount
            : 0f;
        public bool IsFullyCaptured => TotalVoxelCount > 0 && CapturedVoxelCount == TotalVoxelCount;
        public VoxelCell3D StartingVoxel { get; private set; }

        public bool GenerateBoard(Color[] colors)
        {
            ClearBoard();
            if (boardRoot == null || voxelPrefab == null || colors == null || colors.Length < 2)
            {
                Debug.LogError(
                    "3D Flood Fill board needs a Board Root, VoxelCell3D prefab, and at least two colors.",
                    this);
                return false;
            }

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            depth = Mathf.Max(1, depth);
            voxelSize = Mathf.Max(0.05f, voxelSize);
            voxelGap = Mathf.Max(0f, voxelGap);
            palette = (Color[])colors.Clone();
            cells = new VoxelCell3D[width, height, depth];

            float spacing = voxelSize + voxelGap;
            Vector3 centerOffset = new Vector3(
                (width - 1) * spacing * 0.5f,
                (height - 1) * spacing * 0.5f,
                (depth - 1) * spacing * 0.5f);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        if (!ShouldCreateVoxel(x, y, z))
                        {
                            continue;
                        }

                        int colorIndex = UnityEngine.Random.Range(0, palette.Length);
                        VoxelCell3D voxel = Instantiate(voxelPrefab, boardRoot);
                        voxel.name = $"Voxel_{x}_{y}_{z}";
                        voxel.transform.localPosition = new Vector3(
                            x * spacing,
                            y * spacing,
                            z * spacing) - centerOffset;
                        voxel.transform.localRotation = Quaternion.identity;
                        voxel.transform.localScale = Vector3.one * voxelSize;
                        voxel.Initialize(x, y, z, colorIndex, palette[colorIndex]);
                        cells[x, y, z] = voxel;
                        allVoxels.Add(voxel);
                    }
                }
            }

            StartingVoxel = FindStartingVoxel();
            if (StartingVoxel == null)
            {
                Debug.LogError("3D Flood Fill could not find an active starting voxel.", this);
                ClearBoard();
                return false;
            }

            CurrentPlayerColor = StartingVoxel.ColorIndex;
            CaptureInitialRegion();
            Debug.Log(
                $"3D board generated: {width}x{height}x{depth}. Mode: {volumeMode}. " +
                $"Voxels generated: {TotalVoxelCount}. Starting voxel: " +
                $"({StartingVoxel.X},{StartingVoxel.Y},{StartingVoxel.Z}).",
                this);
            return true;
        }

        public bool ChangePlayerColor(int colorIndex)
        {
            if (cells == null || colorIndex < 0 || colorIndex >= palette.Length ||
                colorIndex == CurrentPlayerColor)
            {
                LastNewlyCapturedCount = 0;
                return false;
            }

            CurrentPlayerColor = colorIndex;
            Color selectedColor = palette[colorIndex];
            var queue = new Queue<VoxelCell3D>();
            var visited = new HashSet<VoxelCell3D>();
            for (int i = 0; i < capturedVoxels.Count; i++)
            {
                VoxelCell3D captured = capturedVoxels[i];
                captured.SetColor(colorIndex, selectedColor);
                queue.Enqueue(captured);
                visited.Add(captured);
            }

            LastNewlyCapturedCount = 0;
            while (queue.Count > 0)
            {
                VoxelCell3D current = queue.Dequeue();
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector3Int direction = Directions[i];
                    VoxelCell3D neighbor = GetCell(
                        current.X + direction.x,
                        current.Y + direction.y,
                        current.Z + direction.z);
                    if (neighbor == null || visited.Contains(neighbor) ||
                        neighbor.ColorIndex != colorIndex)
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                    if (!neighbor.IsCaptured)
                    {
                        neighbor.SetCaptured(true, true);
                        capturedVoxels.Add(neighbor);
                        LastNewlyCapturedCount++;
                    }
                }
            }

            Debug.Log(
                $"Selected color: {colorIndex}. Captured: " +
                $"{CapturedVoxelCount} / {TotalVoxelCount}.",
                this);
            return true;
        }

        public VoxelCell3D GetCell(int x, int y, int z)
        {
            if (cells == null || x < 0 || x >= width || y < 0 || y >= height ||
                z < 0 || z >= depth)
            {
                return null;
            }

            return cells[x, y, z];
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            bool foundBounds = false;
            for (int i = 0; i < allVoxels.Count; i++)
            {
                VoxelCell3D voxel = allVoxels[i];
                if (voxel == null || !voxel.TryGetWorldBounds(out Bounds voxelBounds))
                {
                    continue;
                }

                if (!foundBounds)
                {
                    bounds = voxelBounds;
                    foundBounds = true;
                }
                else
                {
                    bounds.Encapsulate(voxelBounds);
                }
            }

            return foundBounds;
        }

        public void ClearBoard()
        {
            if (boardRoot != null)
            {
                for (int i = boardRoot.childCount - 1; i >= 0; i--)
                {
                    GameObject child = boardRoot.GetChild(i).gameObject;
                    child.SetActive(false);
                    if (Application.isPlaying)
                    {
                        Destroy(child);
                    }
                    else
                    {
                        DestroyImmediate(child);
                    }
                }
            }

            cells = null;
            allVoxels.Clear();
            capturedVoxels.Clear();
            palette = Array.Empty<Color>();
            CurrentPlayerColor = -1;
            LastNewlyCapturedCount = 0;
            StartingVoxel = null;
        }

        public void Configure(
            Transform root,
            VoxelCell3D prefab,
            int boardWidth,
            int boardHeight,
            int boardDepth,
            float size,
            float gap,
            VoxelVolumeMode mode)
        {
            boardRoot = root;
            voxelPrefab = prefab;
            width = Mathf.Max(1, boardWidth);
            height = Mathf.Max(1, boardHeight);
            depth = Mathf.Max(1, boardDepth);
            voxelSize = Mathf.Max(0.05f, size);
            voxelGap = Mathf.Max(0f, gap);
            volumeMode = mode;
        }

        private bool ShouldCreateVoxel(int x, int y, int z)
        {
            return volumeMode == VoxelVolumeMode.SolidCube ||
                x == 0 || x == width - 1 ||
                y == 0 || y == height - 1 ||
                z == 0 || z == depth - 1;
        }

        private VoxelCell3D FindStartingVoxel()
        {
            VoxelCell3D best = null;
            int bestScore = int.MaxValue;
            for (int i = 0; i < allVoxels.Count; i++)
            {
                VoxelCell3D candidate = allVoxels[i];
                int score = candidate.X + candidate.Y + candidate.Z;
                if (best == null || score < bestScore ||
                    score == bestScore && IsLexicographicallyEarlier(candidate, best))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static bool IsLexicographicallyEarlier(VoxelCell3D left, VoxelCell3D right)
        {
            return left.X < right.X ||
                left.X == right.X && left.Y < right.Y ||
                left.X == right.X && left.Y == right.Y && left.Z < right.Z;
        }

        private void CaptureInitialRegion()
        {
            var queue = new Queue<VoxelCell3D>();
            var visited = new HashSet<VoxelCell3D>();
            queue.Enqueue(StartingVoxel);
            visited.Add(StartingVoxel);
            StartingVoxel.SetCaptured(true, false);
            capturedVoxels.Add(StartingVoxel);

            while (queue.Count > 0)
            {
                VoxelCell3D current = queue.Dequeue();
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector3Int direction = Directions[i];
                    VoxelCell3D neighbor = GetCell(
                        current.X + direction.x,
                        current.Y + direction.y,
                        current.Z + direction.z);
                    if (neighbor == null || visited.Contains(neighbor) ||
                        neighbor.ColorIndex != CurrentPlayerColor)
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                    neighbor.SetCaptured(true, false);
                    capturedVoxels.Add(neighbor);
                }
            }

            LastNewlyCapturedCount = 0;
        }

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            depth = Mathf.Max(1, depth);
            voxelSize = Mathf.Max(0.05f, voxelSize);
            voxelGap = Mathf.Max(0f, voxelGap);
        }
    }
}
