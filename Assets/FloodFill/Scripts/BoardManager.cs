using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using FloodFill.Shapes;
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

        public enum BoardShapeMode
        {
            Rectangle,
            Procedural
        }

        [Header("Board")]
        [SerializeField, HideInInspector, Min(1)] private int width = 10;
        [SerializeField, HideInInspector, Min(1)] private int height = 10;
        [SerializeField, Min(0.05f)] private float cellSize = 0.8f;
        [SerializeField, Min(0f)] private float cellSpacing = 0.06f;

        [Header("Shape Generation")]
        [SerializeField] private BoardShapeMode shapeMode = BoardShapeMode.Procedural;
        [SerializeField] private ProceduralShapeSettings proceduralSettings = new ProceduralShapeSettings();

        [Header("Shape Border")]
        [SerializeField] private bool showShapeBorder = true;
        [SerializeField] private Color shapeBorderColor = Color.white;
        [SerializeField, Min(0.005f)] private float shapeBorderWidth = 0.055f;

        [Header("Win Celebration")]
        [SerializeField, Min(1f)] private float winPopScale = 1.09f;
        [SerializeField, Min(0.01f)] private float winPopUpDuration = 0.35f;
        [SerializeField, Min(0.01f)] private float winPopSettleDuration = 0.55f;

        [Header("References")]
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Cell cellPrefab;
        [SerializeField] private Camera boardCamera;

        private readonly List<Cell> capturedCells = new List<Cell>();
        private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();
        private Color[] activeColors = Array.Empty<Color>();
        private bool[,] activeMask;
        private ShapeBounds activeBounds = ShapeBounds.Invalid;
        private int totalCellCount;
        private GameObject completionBackingObject;
        private Cell[,] cells;
        private Cell selectedCell;
        private Cell lastSelectedCell;
        private int lastSelectionFrame = -1;
        private bool inputEnabled = true;
        private bool pointerGestureActive;
        private int activePointerId = -1;
        private Cell pointerPreviewCell;
        private Sequence winCelebrationSequence;
        private Vector3 boardRestingScale = Vector3.one;
        private bool hasBoardRestingScale;

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
        public int TotalCellCount => totalCellCount;
        public float CapturedPercentage => TotalCellCount > 0
            ? CapturedCellCount * 100f / TotalCellCount
            : 0f;
        public bool IsFullyCaptured => CapturedCellCount == TotalCellCount && TotalCellCount > 0;
        public float LastRecolorAnimationDuration { get; private set; }
        public int LastNewlyCapturedCellCount { get; private set; }
        public float LastWinCelebrationDuration { get; private set; }
        public Cell StartingCell { get; private set; }
        public BoardShapeMode ShapeMode => shapeMode;
        public ShapeBounds ActiveBounds => activeBounds;
        public int LastGenerationSeed { get; private set; }
        public int LastGenerationAttempt { get; private set; }
        public ProceduralShapeSettings ProceduralSettings => proceduralSettings;

        public bool GenerateBoard()
        {
            if (!ValidateConfiguration())
            {
                return false;
            }

            CacheBoardRestingScale();
            ClearBoard();
            if (!TryCreateActiveMask())
            {
                return false;
            }

            cells = new Cell[width, height];
            capturedCells.Clear();
            LastRecolorAnimationDuration = 0f;
            LastNewlyCapturedCellCount = 0;

            float step = cellSize + cellSpacing;
            float shapeCenterX = (activeBounds.MinX + activeBounds.MaxX) * 0.5f;
            float shapeCenterY = (activeBounds.MinY + activeBounds.MaxY) * 0.5f;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!activeMask[x, y])
                    {
                        continue;
                    }

                    int colorIndex = UnityEngine.Random.Range(0, activeColors.Length);
                    Cell cell = Instantiate(cellPrefab, boardRoot);
                    cell.name = $"Cell_{x}_{y}";
                    cell.transform.localPosition = new Vector3(
                        (x - shapeCenterX) * step,
                        (y - shapeCenterY) * step,
                        0f);
                    cell.transform.localRotation = Quaternion.identity;
                    cell.transform.localScale = Vector3.one * cellSize;
                    cell.Initialize(x, y, colorIndex, activeColors[colorIndex]);
                    cells[x, y] = cell;
                }
            }

            CreateShapeBorder();

            StartingCell = FindBottomLeftStartingCell();
            if (StartingCell == null)
            {
                Debug.LogError("Flood Fill could not find an active starting cell.", this);
                ClearBoard();
                return false;
            }

            CurrentPlayerColor = StartingCell.ColorIndex;
            CaptureInitialRegion(StartingCell);
            FitCameraToBoard();
            BoardChanged?.Invoke();

            Debug.Log(
                $"Flood Fill board generated. Mode: {shapeMode}. Logical size: {width}x{height}. " +
                $"Active cells: {TotalCellCount}. Bounds: {activeBounds.Width}x{activeBounds.Height}.",
                this);
            return true;
        }

        public bool RecolorConnectedRegion(Cell cell, int colorIndex)
        {
            if (cells == null || cell == null || colorIndex < 0 || colorIndex >= activeColors.Length)
            {
                return false;
            }

            if (GetCell(cell.X, cell.Y) != cell || cell.ColorIndex == colorIndex)
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
                    Cell neighbor = GetCell(neighborX, neighborY);
                    if (neighbor == null || visited.Contains(neighbor) ||
                        neighbor.ColorIndex != originalColorIndex)
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
            StopWinCelebration();
            completionBackingObject = null;

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
            activeMask = null;
            activeBounds = ShapeBounds.Invalid;
            totalCellCount = 0;
            capturedCells.Clear();
            selectedCell = null;
            lastSelectedCell = null;
            lastSelectionFrame = -1;
            CurrentPlayerColor = -1;
            LastRecolorAnimationDuration = 0f;
            LastNewlyCapturedCellCount = 0;
            LastWinCelebrationDuration = 0f;
            StartingCell = null;
            LastGenerationSeed = 0;
            LastGenerationAttempt = 0;
        }

        public float PlayWinCelebration()
        {
            if (!IsFullyCaptured || boardRoot == null || CurrentPlayerColor < 0 ||
                CurrentPlayerColor >= activeColors.Length)
            {
                LastWinCelebrationDuration = Mathf.Max(0f, LastRecolorAnimationDuration);
                return LastWinCelebrationDuration;
            }

            CacheBoardRestingScale();
            StopWinCelebration();
            float waveDelay = Mathf.Max(0f, LastRecolorAnimationDuration);
            float popUpDuration = Mathf.Max(0.01f, winPopUpDuration);
            float settleDuration = Mathf.Max(0.01f, winPopSettleDuration);
            Vector3 poppedScale = boardRestingScale * Mathf.Max(1f, winPopScale);

            CreateCompletionBacking();
            winCelebrationSequence = DOTween.Sequence()
                .AppendInterval(waveDelay)
                .Append(boardRoot.DOScale(poppedScale, popUpDuration).SetEase(Ease.OutBack))
                .Append(boardRoot.DOScale(boardRestingScale, settleDuration).SetEase(Ease.OutSine))
                .OnComplete(() => winCelebrationSequence = null);
            LastWinCelebrationDuration = waveDelay + popUpDuration + settleDuration;
            return LastWinCelebrationDuration;
        }

        private void CreateCompletionBacking()
        {
            if (completionBackingObject != null || cells == null || boardRoot == null ||
                CurrentPlayerColor < 0 || CurrentPlayerColor >= activeColors.Length)
            {
                return;
            }

            SpriteRenderer templateRenderer = cellPrefab != null
                ? cellPrefab.GetComponent<SpriteRenderer>()
                : null;
            if (templateRenderer == null || templateRenderer.sprite == null)
            {
                return;
            }

            completionBackingObject = new GameObject("CompletionBacking");
            completionBackingObject.transform.SetParent(boardRoot, false);
            float backingSize = cellSize + cellSpacing + 0.002f;
            Color winningColor = activeColors[CurrentPlayerColor];

            for (int x = 0; x < cells.GetLength(0); x++)
            {
                for (int y = 0; y < cells.GetLength(1); y++)
                {
                    Cell cell = cells[x, y];
                    if (cell == null)
                    {
                        continue;
                    }

                    var tileObject = new GameObject($"Backing_{x}_{y}");
                    tileObject.transform.SetParent(completionBackingObject.transform, false);
                    tileObject.transform.localPosition = cell.transform.localPosition;
                    tileObject.transform.localRotation = cell.transform.localRotation;
                    tileObject.transform.localScale = Vector3.one * backingSize;

                    var backingRenderer = tileObject.AddComponent<SpriteRenderer>();
                    backingRenderer.sprite = templateRenderer.sprite;
                    backingRenderer.sharedMaterial = templateRenderer.sharedMaterial;
                    backingRenderer.color = winningColor;
                    backingRenderer.flipX = templateRenderer.flipX;
                    backingRenderer.flipY = templateRenderer.flipY;
                    backingRenderer.sortingLayerID = templateRenderer.sortingLayerID;
                    backingRenderer.sortingOrder = templateRenderer.sortingOrder - 2;
                    backingRenderer.maskInteraction = templateRenderer.maskInteraction;
                }
            }
        }

        private void CacheBoardRestingScale()
        {
            if (!hasBoardRestingScale && boardRoot != null)
            {
                boardRestingScale = boardRoot.localScale;
                hasBoardRestingScale = true;
            }
        }

        private void StopWinCelebration()
        {
            winCelebrationSequence?.Kill();
            winCelebrationSequence = null;
            if (boardRoot != null)
            {
                boardRoot.DOKill();
                if (hasBoardRestingScale)
                {
                    boardRoot.localScale = boardRestingScale;
                }
            }
        }

        private void CreateShapeBorder()
        {
            if (!showShapeBorder || activeMask == null || boardRoot == null)
            {
                return;
            }

            IReadOnlyList<Vector3[]> contours = ShapeBorderContourBuilder.BuildContours(
                activeMask,
                activeBounds,
                cellSize,
                cellSpacing);
            if (contours.Count == 0)
            {
                return;
            }

            var borderObject = new GameObject("ShapeBorder");
            borderObject.transform.SetParent(boardRoot, false);
            SpriteRenderer cellRenderer = cellPrefab != null
                ? cellPrefab.GetComponent<SpriteRenderer>()
                : null;
            for (int contourIndex = 0; contourIndex < contours.Count; contourIndex++)
            {
                var contourObject = new GameObject($"Contour_{contourIndex}");
                contourObject.transform.SetParent(borderObject.transform, false);
                var lineRenderer = contourObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.loop = true;
                lineRenderer.alignment = LineAlignment.TransformZ;
                lineRenderer.textureMode = LineTextureMode.Stretch;
                lineRenderer.widthMultiplier = shapeBorderWidth * 2f;
                lineRenderer.numCornerVertices = 3;
                lineRenderer.numCapVertices = 0;
                lineRenderer.startColor = shapeBorderColor;
                lineRenderer.endColor = shapeBorderColor;
                lineRenderer.positionCount = contours[contourIndex].Length;
                lineRenderer.SetPositions(contours[contourIndex]);

                if (cellRenderer != null)
                {
                    lineRenderer.sharedMaterial = cellRenderer.sharedMaterial;
                    lineRenderer.sortingLayerID = cellRenderer.sortingLayerID;
                    lineRenderer.sortingOrder = cellRenderer.sortingOrder - 1;
                }
            }
        }

        public void SetInputEnabled(bool enabledInput)
        {
            inputEnabled = enabledInput;
            if (!inputEnabled)
            {
                CancelPointerGesture();
            }
        }

        public void SetGridSize(int boardWidth, int boardHeight)
        {
            width = Mathf.Max(1, boardWidth);
            height = Mathf.Max(1, boardHeight);
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
            hasBoardRestingScale = false;
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

                    Cell neighbor = GetCell(neighborX, neighborY);
                    if (neighbor == null || neighbor.IsCaptured ||
                        neighbor.ColorIndex != targetColorIndex)
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

            if (GetCell(cell.X, cell.Y) != cell)
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

        public Cell GetCell(int x, int y)
        {
            return cells != null && IsInsideBoard(x, y) ? cells[x, y] : null;
        }

        public bool IsActiveCoordinate(int x, int y)
        {
            return activeMask != null && IsInsideBoard(x, y) && activeMask[x, y];
        }

        public void SetShapeMode(BoardShapeMode mode)
        {
            shapeMode = mode;
        }

        private bool TryCreateActiveMask()
        {
            if (shapeMode == BoardShapeMode.Rectangle)
            {
                activeMask = new bool[width, height];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        activeMask[x, y] = true;
                    }
                }

                activeBounds = new ShapeBounds(0, width - 1, 0, height - 1);
                totalCellCount = width * height;
                LastGenerationSeed = 0;
                LastGenerationAttempt = 1;
                return true;
            }

            int seed = proceduralSettings.useRandomSeed
                ? CreateRandomGenerationSeed()
                : proceduralSettings.fixedSeed;
            if (!ProceduralShapeGenerator.TryGenerate(
                    width,
                    height,
                    proceduralSettings,
                    seed,
                    out ProceduralShapeResult result))
            {
                Debug.LogError(
                    $"Procedural shape generation failed after " +
                    $"{proceduralSettings.maxGenerationAttempts} attempts. " +
                    "Falling back to a full rectangle.",
                    this);
                activeMask = new bool[width, height];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        activeMask[x, y] = true;
                    }
                }

                activeBounds = new ShapeBounds(0, width - 1, 0, height - 1);
                totalCellCount = width * height;
                LastGenerationSeed = seed;
                LastGenerationAttempt = proceduralSettings.maxGenerationAttempts;
                return true;
            }

            activeMask = result.Mask;
            activeBounds = result.Bounds;
            totalCellCount = result.ActiveCellCount;
            LastGenerationSeed = result.Seed;
            LastGenerationAttempt = result.GenerationAttempt;

            float fillPercent = totalCellCount * 100f / (width * height);
            Debug.Log(
                $"Procedural shape generated. Logical size: {width}x{height}. " +
                $"Active cells: {totalCellCount}. Fill: {fillPercent:0.#}%. " +
                $"Bounds: {activeBounds.Width}x{activeBounds.Height}. " +
                $"Generation attempt: {LastGenerationAttempt}. Seed: {LastGenerationSeed}.",
                this);
            if (proceduralSettings.logGeneratedMask)
            {
                Debug.Log(BuildMaskLog(), this);
            }

            return true;
        }

        private static int CreateRandomGenerationSeed()
        {
            return Guid.NewGuid().GetHashCode();
        }

        private Cell FindBottomLeftStartingCell()
        {
            if (cells == null || !activeBounds.IsValid)
            {
                return null;
            }

            for (int y = activeBounds.MinY; y <= activeBounds.MaxY; y++)
            {
                for (int x = activeBounds.MinX; x <= activeBounds.MaxX; x++)
                {
                    Cell cell = GetCell(x, y);
                    if (cell != null)
                    {
                        return cell;
                    }
                }
            }

            return null;
        }

        private string BuildMaskLog()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Procedural board mask (X = active, . = inactive):");
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    builder.Append(IsActiveCoordinate(x, y) ? 'X' : '.');
                }

                if (y > 0)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
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

            if (shapeMode == BoardShapeMode.Procedural && proceduralSettings == null)
            {
                proceduralSettings = new ProceduralShapeSettings();
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

            int visibleWidth = activeBounds.IsValid ? activeBounds.Width : width;
            int visibleHeight = activeBounds.IsValid ? activeBounds.Height : height;
            float boardWidth = visibleWidth * cellSize +
                Mathf.Max(0, visibleWidth - 1) * cellSpacing;
            float boardHeight = visibleHeight * cellSize +
                Mathf.Max(0, visibleHeight - 1) * cellSpacing;
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
            shapeBorderWidth = Mathf.Max(0.005f, shapeBorderWidth);
            winPopScale = Mathf.Max(1f, winPopScale);
            winPopUpDuration = Mathf.Max(0.01f, winPopUpDuration);
            winPopSettleDuration = Mathf.Max(0.01f, winPopSettleDuration);
            if (proceduralSettings == null)
            {
                proceduralSettings = new ProceduralShapeSettings();
            }
        }

        private void OnDestroy()
        {
            StopWinCelebration();
        }

        [ContextMenu("Generate Board")]
        private void GenerateBoardFromContextMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Generate Board is available while the game is running.", this);
                return;
            }

            GenerateBoard();
        }
    }
}
