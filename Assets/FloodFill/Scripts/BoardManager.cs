using System;
using System.Collections.Generic;
using UnityEngine;

namespace FloodFill
{
    public sealed class BoardManager : MonoBehaviour
    {
        private static readonly Vector2Int[] NeighborDirections =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        [Header("Board")]
        [SerializeField, Min(1)] private int width = 10;
        [SerializeField, Min(1)] private int height = 10;
        [SerializeField, Min(0.05f)] private float cellSize = 0.8f;
        [SerializeField, Min(0f)] private float cellSpacing = 0.06f;
        [SerializeField] private Color[] colors =
        {
            new Color(0.93f, 0.25f, 0.25f),
            new Color(0.25f, 0.78f, 0.38f),
            new Color(0.20f, 0.48f, 0.95f),
            new Color(0.98f, 0.82f, 0.20f),
            new Color(0.62f, 0.32f, 0.86f),
            new Color(1.00f, 0.52f, 0.16f)
        };

        [Header("References")]
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Cell cellPrefab;
        [SerializeField] private Camera boardCamera;

        private readonly List<Cell> capturedCells = new List<Cell>();
        private Cell[,] cells;

        public event Action BoardChanged;

        public int CurrentPlayerColor { get; private set; } = -1;
        public int CapturedCellCount => capturedCells.Count;
        public int TotalCellCount => width * height;
        public float CapturedPercentage => TotalCellCount > 0
            ? CapturedCellCount * 100f / TotalCellCount
            : 0f;
        public bool IsFullyCaptured => CapturedCellCount == TotalCellCount && TotalCellCount > 0;
        public IReadOnlyList<Color> Colors => colors;

        public bool GenerateBoard()
        {
            if (!ValidateConfiguration())
            {
                return false;
            }

            ClearBoard();
            cells = new Cell[width, height];
            capturedCells.Clear();

            float step = cellSize + cellSpacing;
            float startX = -(width - 1) * step * 0.5f;
            float startY = -(height - 1) * step * 0.5f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int colorIndex = UnityEngine.Random.Range(0, colors.Length);
                    Cell cell = Instantiate(cellPrefab, boardRoot);
                    cell.name = $"Cell_{x}_{y}";
                    cell.transform.localPosition = new Vector3(startX + x * step, startY + y * step, 0f);
                    cell.transform.localRotation = Quaternion.identity;
                    cell.transform.localScale = Vector3.one * cellSize;
                    cell.Initialize(x, y, colorIndex, colors[colorIndex]);
                    cells[x, y] = cell;
                }
            }

            Cell startingCell = cells[0, 0];
            CurrentPlayerColor = startingCell.ColorIndex;
            CaptureInitialRegion(startingCell);
            FitCameraToBoard();
            BoardChanged?.Invoke();

            Debug.Log($"Flood Fill board generated: {width}x{height}", this);
            return true;
        }

        public bool ChangePlayerColor(int colorIndex)
        {
            if (cells == null || colorIndex < 0 || colorIndex >= colors.Length || colorIndex == CurrentPlayerColor)
            {
                return false;
            }

            CurrentPlayerColor = colorIndex;
            Color selectedColor = colors[colorIndex];
            var expansionQueue = new Queue<Cell>();

            for (int i = 0; i < capturedCells.Count; i++)
            {
                Cell capturedCell = capturedCells[i];
                capturedCell.SetColor(colorIndex, selectedColor);
                expansionQueue.Enqueue(capturedCell);
            }

            ExpandCapturedRegion(expansionQueue, colorIndex, true);
            BoardChanged?.Invoke();

            Debug.Log($"Selected color: {colorIndex}. Captured cells: {CapturedCellCount} / {TotalCellCount}", this);
            return true;
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
            capturedCells.Clear();
            CurrentPlayerColor = -1;
        }

        public void Configure(
            Transform root,
            Cell prefab,
            Camera targetCamera,
            int boardWidth,
            int boardHeight,
            float size,
            float spacing,
            Color[] palette)
        {
            boardRoot = root;
            cellPrefab = prefab;
            boardCamera = targetCamera;
            width = Mathf.Max(1, boardWidth);
            height = Mathf.Max(1, boardHeight);
            cellSize = Mathf.Max(0.05f, size);
            cellSpacing = Mathf.Max(0f, spacing);
            colors = palette;
        }

        private void CaptureInitialRegion(Cell startingCell)
        {
            var queue = new Queue<Cell>();
            startingCell.SetCaptured(true, false);
            capturedCells.Add(startingCell);
            queue.Enqueue(startingCell);
            ExpandCapturedRegion(queue, CurrentPlayerColor, false);
        }

        private void ExpandCapturedRegion(Queue<Cell> queue, int targetColorIndex, bool animate)
        {
            while (queue.Count > 0)
            {
                Cell current = queue.Dequeue();

                for (int i = 0; i < NeighborDirections.Length; i++)
                {
                    int neighborX = current.X + NeighborDirections[i].x;
                    int neighborY = current.Y + NeighborDirections[i].y;

                    if (!IsInsideBoard(neighborX, neighborY))
                    {
                        continue;
                    }

                    Cell neighbor = cells[neighborX, neighborY];
                    if (neighbor.IsCaptured || neighbor.ColorIndex != targetColorIndex)
                    {
                        continue;
                    }

                    neighbor.SetCaptured(true, animate);
                    capturedCells.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        private bool IsInsideBoard(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        private bool ValidateConfiguration()
        {
            if (width < 1 || height < 1)
            {
                Debug.LogError("Flood Fill requires a board width and height of at least 1.", this);
                return false;
            }

            if (colors == null || colors.Length < 2)
            {
                Debug.LogError("Flood Fill requires at least two configured colors.", this);
                return false;
            }

            if (boardRoot == null || cellPrefab == null)
            {
                Debug.LogError("Flood Fill BoardManager is missing its board root or Cell prefab reference.", this);
                return false;
            }

            return true;
        }

        private void FitCameraToBoard()
        {
            if (boardCamera == null)
            {
                Debug.LogWarning("Flood Fill BoardManager has no camera assigned, so automatic board framing was skipped.", this);
                return;
            }

            float boardWidth = width * cellSize + Mathf.Max(0, width - 1) * cellSpacing;
            float boardHeight = height * cellSize + Mathf.Max(0, height - 1) * cellSpacing;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 9f / 16f;
            aspect = Mathf.Max(0.1f, aspect);

            float verticalSize = boardHeight * 0.5f;
            float horizontalSize = boardWidth * 0.5f / aspect;
            boardCamera.orthographic = true;
            boardCamera.orthographicSize = Mathf.Max(verticalSize, horizontalSize) * 1.12f + 0.35f;
            boardCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellSize = Mathf.Max(0.05f, cellSize);
            cellSpacing = Mathf.Max(0f, cellSpacing);
        }
    }
}
