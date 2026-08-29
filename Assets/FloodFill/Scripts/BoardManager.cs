using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FloodFill
{
    public sealed class BoardManager : MonoBehaviour
    {
        private const float WaveStepDelay = 0.055f;

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

        [Header("References")]
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Cell cellPrefab;
        [SerializeField] private Camera boardCamera;

        private readonly List<Cell> capturedCells = new List<Cell>();
        private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();
        private Color[] activeColors = Array.Empty<Color>();
        private Cell[,] cells;
        private Cell selectedCell;
        private Cell lastSelectedCell;
        private int lastSelectionFrame = -1;
        private bool inputEnabled = true;
        private bool pointerGestureActive;
        private int activePointerId = -1;
        private Cell pointerPreviewCell;

        private enum PointerPhase
        {
            None,
            Pressed,
            Held,
            Released,
            Canceled
        }

        public event Action BoardChanged;
        public event Action<Cell> CellClicked;

        public int CurrentPlayerColor { get; private set; } = -1;
        public int CapturedCellCount => capturedCells.Count;
        public int TotalCellCount => width * height;
        public float CapturedPercentage => TotalCellCount > 0
            ? CapturedCellCount * 100f / TotalCellCount
            : 0f;
        public bool IsFullyCaptured => CapturedCellCount == TotalCellCount && TotalCellCount > 0;
        public float LastRecolorAnimationDuration { get; private set; }
        public int LastNewlyCapturedCellCount { get; private set; }

        public bool GenerateBoard()
        {
            if (!ValidateConfiguration())
            {
                return false;
            }

            ClearBoard();
            cells = new Cell[width, height];
            capturedCells.Clear();
            LastRecolorAnimationDuration = 0f;
            LastNewlyCapturedCellCount = 0;

            float step = cellSize + cellSpacing;
            float startX = -(width - 1) * step * 0.5f;
            float startY = -(height - 1) * step * 0.5f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int colorIndex = UnityEngine.Random.Range(0, activeColors.Length);
                    Cell cell = Instantiate(cellPrefab, boardRoot);
                    cell.name = $"Cell_{x}_{y}";
                    cell.transform.localPosition = new Vector3(startX + x * step, startY + y * step, 0f);
                    cell.transform.localRotation = Quaternion.identity;
                    cell.transform.localScale = Vector3.one * cellSize;
                    cell.Initialize(x, y, colorIndex, activeColors[colorIndex]);
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
            if (cells == null || cell == null || colorIndex < 0 || colorIndex >= activeColors.Length)
            {
                return false;
            }

            if (!IsInsideBoard(cell.X, cell.Y) || cells[cell.X, cell.Y] != cell || cell.ColorIndex == colorIndex)
            {
                return false;
            }

            int originalColorIndex = cell.ColorIndex;
            LastRecolorAnimationDuration = 0f;
            LastNewlyCapturedCellCount = 0;
            int recoloredCellCount = RecolorMatchingComponent(cell, originalColorIndex, colorIndex);
            RecalculateCapturedRegion(cell);
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
                float animationDuration = current.AnimateColor(
                    newColorIndex,
                    activeColors[newColorIndex],
                    currentDepth * WaveStepDelay);
                LastRecolorAnimationDuration = Mathf.Max(
                    LastRecolorAnimationDuration,
                    animationDuration);

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
            CancelPointerGesture();

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
            selectedCell = null;
            lastSelectedCell = null;
            lastSelectionFrame = -1;
            CurrentPlayerColor = -1;
            LastRecolorAnimationDuration = 0f;
            LastNewlyCapturedCellCount = 0;
        }

        public void SetInputEnabled(bool enabledInput)
        {
            inputEnabled = enabledInput;
            if (!inputEnabled)
            {
                CancelPointerGesture();
            }
        }

        public void Configure(
            Transform root,
            Cell prefab,
            Camera targetCamera,
            int boardWidth,
            int boardHeight,
            float size,
            float spacing)
        {
            boardRoot = root;
            cellPrefab = prefab;
            boardCamera = targetCamera;
            width = Mathf.Max(1, boardWidth);
            height = Mathf.Max(1, boardHeight);
            cellSize = Mathf.Max(0.05f, size);
            cellSpacing = Mathf.Max(0f, spacing);
        }

        public void SetActiveColors(IReadOnlyList<Color> palette)
        {
            if (palette == null)
            {
                activeColors = Array.Empty<Color>();
                return;
            }

            activeColors = new Color[palette.Count];
            for (int i = 0; i < palette.Count; i++)
            {
                activeColors[i] = palette[i];
            }
        }

        private void CaptureInitialRegion(Cell startingCell)
        {
            var queue = new Queue<Cell>();
            startingCell.SetCaptured(true, false);
            capturedCells.Add(startingCell);
            queue.Enqueue(startingCell);
            ExpandCapturedRegion(queue, CurrentPlayerColor, false, null);
        }

        private void RecalculateCapturedRegion(Cell originCell)
        {
            var previouslyCaptured = new HashSet<Cell>(capturedCells);
            for (int i = 0; i < capturedCells.Count; i++)
            {
                capturedCells[i].SetCaptured(false, false);
            }

            capturedCells.Clear();
            CurrentPlayerColor = originCell.ColorIndex;

            var queue = new Queue<Cell>();
            originCell.SetCaptured(true, !previouslyCaptured.Contains(originCell));
            capturedCells.Add(originCell);
            queue.Enqueue(originCell);
            ExpandCapturedRegion(queue, CurrentPlayerColor, true, previouslyCaptured);

            for (int i = 0; i < capturedCells.Count; i++)
            {
                if (!previouslyCaptured.Contains(capturedCells[i]))
                {
                    LastNewlyCapturedCellCount++;
                }
            }
        }

        private void ExpandCapturedRegion(
            Queue<Cell> queue,
            int targetColorIndex,
            bool animate,
            HashSet<Cell> previouslyCaptured)
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

                    bool shouldAnimate = animate &&
                        (previouslyCaptured == null || !previouslyCaptured.Contains(neighbor));
                    neighbor.SetCaptured(true, shouldAnimate);
                    capturedCells.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        public bool TrySelectCellAtScreenPosition(Vector2 screenPosition)
        {
            if (!inputEnabled || !TryGetCellAtScreenPosition(screenPosition, out Cell cell))
            {
                return false;
            }

            SelectCell(cell);
            return true;
        }

        public bool TryPreviewCellAtScreenPosition(Vector2 screenPosition)
        {
            if (!inputEnabled || !TryGetCellAtScreenPosition(screenPosition, out Cell cell))
            {
                SetPointerPreviewCell(null);
                return false;
            }

            SetPointerPreviewCell(cell);
            return true;
        }

        private void SetPointerPreviewCell(Cell cell)
        {
            if (pointerPreviewCell == cell)
            {
                return;
            }

            if (pointerPreviewCell != null)
            {
                pointerPreviewCell.StopPointerPop();
            }

            pointerPreviewCell = cell;
            if (pointerPreviewCell != null)
            {
                pointerPreviewCell.StartPointerPopLoop();
            }
        }

        private bool TryGetCellAtScreenPosition(Vector2 screenPosition, out Cell cell)
        {
            cell = null;
            if (cells == null || boardCamera == null)
            {
                return false;
            }

            float distanceFromCamera = Mathf.Abs(boardCamera.transform.position.z);
            Vector3 worldPosition = boardCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, distanceFromCamera));
            Collider2D hitCollider = Physics2D.OverlapPoint(worldPosition);
            if (hitCollider == null || !hitCollider.TryGetComponent(out cell))
            {
                return false;
            }

            if (!IsInsideBoard(cell.X, cell.Y) || cells[cell.X, cell.Y] != cell)
            {
                return false;
            }

            return true;
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
            if (!inputEnabled || !TryGetPointerState(
                    out Vector2 screenPosition,
                    out int pointerId,
                    out PointerPhase pointerPhase))
            {
                return;
            }

            if (pointerPhase == PointerPhase.Pressed)
            {
                BeginPointerGesture(screenPosition, pointerId);
                return;
            }

            if (!pointerGestureActive || pointerId != activePointerId)
            {
                return;
            }

            if (pointerPhase == PointerPhase.Held)
            {
                if (!IsPointerOverBlockingUI(screenPosition))
                {
                    TryPreviewCellAtScreenPosition(screenPosition);
                }
                return;
            }

            if (pointerPhase == PointerPhase.Released)
            {
                bool releasedOverUI = IsPointerOverBlockingUI(screenPosition);
                CancelPointerGesture();
                if (!releasedOverUI)
                {
                    TrySelectCellAtScreenPosition(screenPosition);
                }

                return;
            }

            if (pointerPhase == PointerPhase.Canceled)
            {
                CancelPointerGesture();
            }
        }

        private void BeginPointerGesture(Vector2 screenPosition, int pointerId)
        {
            if (IsPointerOverBlockingUI(screenPosition))
            {
                return;
            }

            pointerGestureActive = true;
            activePointerId = pointerId;
            TryPreviewCellAtScreenPosition(screenPosition);
        }

        private void CancelPointerGesture()
        {
            SetPointerPreviewCell(null);
            pointerGestureActive = false;
            activePointerId = -1;
        }

        private static bool IsPointerOverBlockingUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            var pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };
            UiRaycastResults.Clear();
            EventSystem.current.RaycastAll(pointerEventData, UiRaycastResults);

            for (int i = 0; i < UiRaycastResults.Count; i++)
            {
                RaycastResult result = UiRaycastResults[i];
                if (result.module is GraphicRaycaster &&
                    result.gameObject.GetComponentInParent<ColorButton>() == null)
                {
                    UiRaycastResults.Clear();
                    return true;
                }
            }

            UiRaycastResults.Clear();
            return false;
        }

        private static bool TryGetPointerState(
            out Vector2 screenPosition,
            out int pointerId,
            out PointerPhase pointerPhase)
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                var primaryTouch = Touchscreen.current.primaryTouch;
                if (primaryTouch.press.wasPressedThisFrame)
                {
                    screenPosition = primaryTouch.position.ReadValue();
                    pointerId = primaryTouch.touchId.ReadValue();
                    pointerPhase = PointerPhase.Pressed;
                    return true;
                }

                if (primaryTouch.press.wasReleasedThisFrame)
                {
                    screenPosition = primaryTouch.position.ReadValue();
                    pointerId = primaryTouch.touchId.ReadValue();
                    pointerPhase = PointerPhase.Released;
                    return true;
                }

                if (primaryTouch.press.isPressed)
                {
                    screenPosition = primaryTouch.position.ReadValue();
                    pointerId = primaryTouch.touchId.ReadValue();
                    pointerPhase = PointerPhase.Held;
                    return true;
                }
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                pointerId = -1;
                pointerPhase = PointerPhase.Pressed;
                return true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                pointerId = -1;
                pointerPhase = PointerPhase.Released;
                return true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                screenPosition = Mouse.current.position.ReadValue();
                pointerId = -1;
                pointerPhase = PointerPhase.Held;
                return true;
            }
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    screenPosition = touch.position;
                    pointerId = touch.fingerId;
                    pointerPhase = PointerPhase.Pressed;
                    return true;
                }

                if (touch.phase == TouchPhase.Ended)
                {
                    screenPosition = touch.position;
                    pointerId = touch.fingerId;
                    pointerPhase = PointerPhase.Released;
                    return true;
                }

                if (touch.phase == TouchPhase.Canceled)
                {
                    screenPosition = touch.position;
                    pointerId = touch.fingerId;
                    pointerPhase = PointerPhase.Canceled;
                    return true;
                }

                screenPosition = touch.position;
                pointerId = touch.fingerId;
                pointerPhase = PointerPhase.Held;
                return true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                screenPosition = Input.mousePosition;
                pointerId = -1;
                pointerPhase = PointerPhase.Pressed;
                return true;
            }

            if (Input.GetMouseButtonUp(0))
            {
                screenPosition = Input.mousePosition;
                pointerId = -1;
                pointerPhase = PointerPhase.Released;
                return true;
            }

            if (Input.GetMouseButton(0))
            {
                screenPosition = Input.mousePosition;
                pointerId = -1;
                pointerPhase = PointerPhase.Held;
                return true;
            }
#endif

            screenPosition = default;
            pointerId = -1;
            pointerPhase = PointerPhase.None;
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

            if (activeColors == null || activeColors.Length < 2)
            {
                Debug.LogError("Flood Fill requires at least two active colors from GameManager.", this);
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
