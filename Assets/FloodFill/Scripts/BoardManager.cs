using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FloodFill
{
    public sealed class BoardManager : MonoBehaviour
    {
        private const float WaveStepDelay = 0.045f;

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
        private Cell selectedCell;
        private Cell lastSelectedCell;
        private int lastSelectionFrame = -1;

        public event Action BoardChanged;
        public event Action<Cell> CellClicked;

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
                    cell.Clicked += HandleCellClicked;
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

        public bool RecolorConnectedRegion(Cell cell, int colorIndex)
        {
            if (cells == null || cell == null || colorIndex < 0 || colorIndex >= colors.Length)
            {
                return false;
            }

            if (!IsInsideBoard(cell.X, cell.Y) || cells[cell.X, cell.Y] != cell || cell.ColorIndex == colorIndex)
            {
                return false;
            }

            int originalColorIndex = cell.ColorIndex;
            int recoloredCellCount = RecolorMatchingComponent(cell, originalColorIndex, colorIndex);
            CurrentPlayerColor = colorIndex;

            var expansionQueue = new Queue<Cell>();
            for (int i = 0; i < capturedCells.Count; i++)
            {
                expansionQueue.Enqueue(capturedCells[i]);
            }

            ExpandCapturedRegion(expansionQueue, colorIndex, true);
            BoardChanged?.Invoke();

            Debug.Log(
                $"Recolored {recoloredCellCount} connected cell(s) from ({cell.X}, {cell.Y}) " +
                $"to color {colorIndex}. " +
                $"Captured cells: {CapturedCellCount} / {TotalCellCount}",
                this);
            return true;
        }

        private int RecolorMatchingComponent(Cell startingCell, int originalColorIndex, int newColorIndex)
        {
            var queue = new Queue<Cell>();
            var visited = new HashSet<Cell>();
            var depthByCell = new Dictionary<Cell, int>();
            queue.Enqueue(startingCell);
            visited.Add(startingCell);
            depthByCell.Add(startingCell, 0);

            while (queue.Count > 0)
            {
                Cell current = queue.Dequeue();
                int currentDepth = depthByCell[current];
                current.AnimateColor(
                    newColorIndex,
                    colors[newColorIndex],
                    currentDepth * WaveStepDelay);

                for (int i = 0; i < NeighborDirections.Length; i++)
                {
                    int neighborX = current.X + NeighborDirections[i].x;
                    int neighborY = current.Y + NeighborDirections[i].y;
                    if (!IsInsideBoard(neighborX, neighborY))
                    {
                        continue;
                    }

                    Cell neighbor = cells[neighborX, neighborY];
                    if (visited.Contains(neighbor) || neighbor.ColorIndex != originalColorIndex)
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    depthByCell.Add(neighbor, currentDepth + 1);
                    queue.Enqueue(neighbor);
                }
            }

            return visited.Count;
        }

        public void ClearBoard()
        {
            if (boardRoot != null)
            {
                for (int i = boardRoot.childCount - 1; i >= 0; i--)
                {
                    GameObject child = boardRoot.GetChild(i).gameObject;
                    Cell cell = child.GetComponent<Cell>();
                    if (cell != null)
                    {
                        cell.Clicked -= HandleCellClicked;
                    }

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
            selectedCell = null;
            lastSelectedCell = null;
            lastSelectionFrame = -1;
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

        private void ExpandCapturedRegion(
            Queue<Cell> queue,
            int targetColorIndex,
            bool animate)
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

        public bool TrySelectCellAtScreenPosition(Vector2 screenPosition)
        {
            if (cells == null || boardCamera == null)
            {
                return false;
            }

            float distanceFromCamera = Mathf.Abs(boardCamera.transform.position.z);
            Vector3 worldPosition = boardCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, distanceFromCamera));
            Collider2D hitCollider = Physics2D.OverlapPoint(worldPosition);
            if (hitCollider == null || !hitCollider.TryGetComponent(out Cell cell))
            {
                return false;
            }

            if (!IsInsideBoard(cell.X, cell.Y) || cells[cell.X, cell.Y] != cell)
            {
                return false;
            }

            SelectCell(cell);
            return true;
        }

        private void HandleCellClicked(Cell cell)
        {
            if (cell != null && IsInsideBoard(cell.X, cell.Y) && cells[cell.X, cell.Y] == cell)
            {
                SelectCell(cell);
            }
        }

        private void SelectCell(Cell cell)
        {
            if (lastSelectedCell == cell && lastSelectionFrame == Time.frameCount)
            {
                return;
            }

            if (selectedCell != null && selectedCell != cell)
            {
                selectedCell.SetSelected(false);
            }

            selectedCell = cell;
            lastSelectedCell = cell;
            lastSelectionFrame = Time.frameCount;
            selectedCell.SetSelected(true);
            Debug.Log($"Cell selected: ({cell.X}, {cell.Y})", this);
            CellClicked?.Invoke(cell);
        }

        private void Update()
        {
            if (!TryGetPointerPress(out Vector2 screenPosition, out int pointerId))
            {
                return;
            }

            if (IsPointerOverUI(pointerId))
            {
                return;
            }

            TrySelectCellAtScreenPosition(screenPosition);
        }

        private static bool IsPointerOverUI(int pointerId)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return pointerId < 0
                ? EventSystem.current.IsPointerOverGameObject()
                : EventSystem.current.IsPointerOverGameObject(pointerId);
        }

        private static bool TryGetPointerPress(out Vector2 screenPosition, out int pointerId)
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                var primaryTouch = Touchscreen.current.primaryTouch;
                if (primaryTouch.press.wasReleasedThisFrame)
                {
                    screenPosition = primaryTouch.position.ReadValue();
                    pointerId = primaryTouch.touchId.ReadValue();
                    return true;
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                pointerId = -1;
                return true;
            }
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended)
                {
                    screenPosition = touch.position;
                    pointerId = touch.fingerId;
                    return true;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                screenPosition = Input.mousePosition;
                pointerId = -1;
                return true;
            }
#endif

            screenPosition = default;
            pointerId = -1;
            return false;
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
