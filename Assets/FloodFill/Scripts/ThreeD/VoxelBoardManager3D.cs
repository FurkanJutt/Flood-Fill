using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FloodFill.ThreeD
{
    public sealed class VoxelBoardManager3D : MonoBehaviour
    {
        public enum VoxelVolumeMode
        {
            HollowCube = 0,
            SolidCube = 1,
            Procedural = 2
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

        private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();

        [Header("Volume")]
        [SerializeField, Min(1)] private int width = 6;
        [SerializeField, Min(1)] private int height = 6;
        [SerializeField, Min(1)] private int depth = 6;
        [SerializeField] private VoxelVolumeMode volumeMode = VoxelVolumeMode.Procedural;

        [Header("Procedural Shape")]
        [SerializeField] private ProceduralVoxelShapeSettings proceduralSettings =
            new ProceduralVoxelShapeSettings();

        [Header("Voxel Layout")]
        [SerializeField, Min(0.05f)] private float voxelSize = 1f;
        [SerializeField, Min(0f)] private float voxelGap = 0.05f;

        [Header("Selection")]
        [SerializeField] private Camera boardCamera;
        [SerializeField, Min(0f)] private float selectionDragThreshold = 12f;

        [Header("Recolor Wave")]
        [SerializeField, Min(0f)] private float waveStepDelay = 0.065f;

        [Header("Win Celebration")]
        [SerializeField, Range(0.1f, 1f)] private float winZoomScale = 0.72f;
        [SerializeField, Min(0.01f)] private float winZoomDuration = 0.80f;
        [SerializeField] private float winIntroTilt = 12f;
        [SerializeField] private float winIntroTurn = 100f;
        [SerializeField, Min(0.1f)] private float winRotationDuration = 7.5f;

        [Header("References")]
        [SerializeField] private Transform boardRoot;
        [SerializeField] private VoxelCell3D voxelPrefab;

        private readonly List<VoxelCell3D> allVoxels = new List<VoxelCell3D>();
        private readonly List<VoxelCell3D> capturedVoxels = new List<VoxelCell3D>();
        private VoxelCell3D[,,] cells;
        private bool[,,] activeMask;
        private VoxelShapeBounds activeBounds = VoxelShapeBounds.Invalid;
        private Color[] palette = Array.Empty<Color>();
        private bool inputEnabled = true;
        private bool pointerGestureActive;
        private bool gestureBeganOnColorButton;
        private bool gestureExceededDragThreshold;
        private int activePointerId = -1;
        private Vector2 pointerDownPosition;
        private Vector3 boardRestingScale = Vector3.one;
        private Quaternion boardRestingRotation = Quaternion.identity;
        private bool hasBoardRestingTransform;
        private Sequence winIntroSequence;
        private Tween winRotationTween;

        private enum PointerPhase
        {
            None,
            Pressed,
            Held,
            Released,
            Canceled
        }

        public event Action<VoxelCell3D> VoxelClicked;

        public int Width => width;
        public int Height => height;
        public int Depth => depth;
        public VoxelVolumeMode VolumeMode => volumeMode;
        public ProceduralVoxelShapeSettings ProceduralSettings => proceduralSettings;
        public VoxelShapeBounds ActiveBounds => activeBounds;
        public int LastGenerationSeed { get; private set; }
        public int LastGenerationAttempt { get; private set; }
        public int CurrentPlayerColor { get; private set; } = -1;
        public int CapturedVoxelCount => capturedVoxels.Count;
        public int TotalVoxelCount => allVoxels.Count;
        public int LastNewlyCapturedCount { get; private set; }
        public float LastRecolorAnimationDuration { get; private set; }
        public float LastWinCelebrationDuration { get; private set; }
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

            CacheBoardRestingTransform();

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            depth = Mathf.Max(1, depth);
            voxelSize = Mathf.Max(0.05f, voxelSize);
            voxelGap = Mathf.Max(0f, voxelGap);
            if (!TryCreateActiveMask())
            {
                return false;
            }

            palette = (Color[])colors.Clone();
            cells = new VoxelCell3D[width, height, depth];
            LastRecolorAnimationDuration = 0f;

            float spacing = voxelSize + voxelGap;
            Vector3 centerOffset = activeBounds.Center * spacing;

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
                $"({StartingVoxel.X},{StartingVoxel.Y},{StartingVoxel.Z}). " +
                $"Seed: {LastGenerationSeed}. Attempt: {LastGenerationAttempt}.",
                this);
            return true;
        }

        public bool RecolorConnectedRegion(VoxelCell3D voxel, int colorIndex)
        {
            if (cells == null || voxel == null || colorIndex < 0 ||
                colorIndex >= palette.Length || GetCell(voxel.X, voxel.Y, voxel.Z) != voxel ||
                voxel.ColorIndex == colorIndex)
            {
                LastNewlyCapturedCount = 0;
                LastRecolorAnimationDuration = 0f;
                return false;
            }

            int originalColorIndex = voxel.ColorIndex;
            var queue = new Queue<VoxelCell3D>();
            var visited = new HashSet<VoxelCell3D>();
            var depthByVoxel = new Dictionary<VoxelCell3D, int>();
            queue.Enqueue(voxel);
            visited.Add(voxel);
            depthByVoxel.Add(voxel, 0);
            LastRecolorAnimationDuration = 0f;

            while (queue.Count > 0)
            {
                VoxelCell3D current = queue.Dequeue();
                int currentDepth = depthByVoxel[current];
                float animationDuration = current.AnimateColor(
                    colorIndex,
                    palette[colorIndex],
                    currentDepth * waveStepDelay);
                LastRecolorAnimationDuration = Mathf.Max(
                    LastRecolorAnimationDuration,
                    animationDuration);
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector3Int direction = Directions[i];
                    VoxelCell3D neighbor = GetCell(
                        current.X + direction.x,
                        current.Y + direction.y,
                        current.Z + direction.z);
                    if (neighbor == null || visited.Contains(neighbor) ||
                        neighbor.ColorIndex != originalColorIndex)
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    depthByVoxel.Add(neighbor, currentDepth + 1);
                    queue.Enqueue(neighbor);
                }
            }

            RecalculateCapturedRegion(voxel);

            Debug.Log(
                $"Recolored {visited.Count} connected voxel(s) from " +
                $"({voxel.X},{voxel.Y},{voxel.Z}) to color {colorIndex}. " +
                $"Captured: {CapturedVoxelCount} / {TotalVoxelCount}.",
                this);
            return true;
        }

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
            if (!inputEnabled)
            {
                CancelPointerGesture();
            }
        }

        public bool TrySelectVoxelAtScreenPosition(Vector2 screenPosition)
        {
            if (!inputEnabled || !TryGetVoxelAtScreenPosition(screenPosition, out VoxelCell3D voxel))
            {
                return false;
            }

            VoxelClicked?.Invoke(voxel);
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
            CancelPointerGesture();
            StopWinCelebration(true);
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
            activeBounds = VoxelShapeBounds.Invalid;
            allVoxels.Clear();
            capturedVoxels.Clear();
            palette = Array.Empty<Color>();
            CurrentPlayerColor = -1;
            LastNewlyCapturedCount = 0;
            LastRecolorAnimationDuration = 0f;
            LastWinCelebrationDuration = 0f;
            LastGenerationSeed = 0;
            LastGenerationAttempt = 0;
            StartingVoxel = null;
        }

        public float PlayWinCelebration()
        {
            if (!IsFullyCaptured || boardRoot == null)
            {
                LastWinCelebrationDuration = Mathf.Max(0f, LastRecolorAnimationDuration);
                return LastWinCelebrationDuration;
            }

            CacheBoardRestingTransform();
            StopWinCelebration(true);

            float waveDelay = Mathf.Max(0f, LastRecolorAnimationDuration);
            float zoomDuration = Mathf.Max(0.01f, winZoomDuration);
            Vector3 zoomedScale = boardRestingScale * Mathf.Clamp(winZoomScale, 0.1f, 1f);

            winIntroSequence = DOTween.Sequence()
                .AppendInterval(waveDelay)
                .Append(boardRoot.DOScale(zoomedScale, zoomDuration).SetEase(Ease.InOutSine))
                .Join(boardRoot.DOLocalRotate(
                        new Vector3(winIntroTilt, winIntroTurn, 0f),
                        zoomDuration,
                        RotateMode.LocalAxisAdd)
                    .SetEase(Ease.InOutCubic))
                .OnComplete(() =>
                {
                    winIntroSequence = null;
                    StartWinBackgroundRotation();
                });

            LastWinCelebrationDuration = waveDelay + zoomDuration;
            return LastWinCelebrationDuration;
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
            hasBoardRestingTransform = false;
            voxelPrefab = prefab;
            width = Mathf.Max(1, boardWidth);
            height = Mathf.Max(1, boardHeight);
            depth = Mathf.Max(1, boardDepth);
            voxelSize = Mathf.Max(0.05f, size);
            voxelGap = Mathf.Max(0f, gap);
            volumeMode = mode;
        }

        private void CacheBoardRestingTransform()
        {
            if (hasBoardRestingTransform || boardRoot == null)
            {
                return;
            }

            boardRestingScale = boardRoot.localScale;
            boardRestingRotation = boardRoot.localRotation;
            hasBoardRestingTransform = true;
        }

        private void StartWinBackgroundRotation()
        {
            if (boardRoot == null)
            {
                return;
            }

            winRotationTween?.Kill();
            winRotationTween = boardRoot.DOLocalRotate(
                    new Vector3(0f, 360f, 0f),
                    Mathf.Max(0.1f, winRotationDuration),
                    RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);
        }

        private void StopWinCelebration(bool restoreTransform)
        {
            winIntroSequence?.Kill();
            winRotationTween?.Kill();
            if (boardRoot != null)
            {
                boardRoot.DOKill();
            }
            winIntroSequence = null;
            winRotationTween = null;

            if (restoreTransform && boardRoot != null && hasBoardRestingTransform)
            {
                boardRoot.localScale = boardRestingScale;
                boardRoot.localRotation = boardRestingRotation;
            }
        }

        public void SetVolumeMode(VoxelVolumeMode mode)
        {
            volumeMode = mode;
        }

        public bool IsActiveCoordinate(int x, int y, int z)
        {
            return activeMask != null && x >= 0 && x < width &&
                y >= 0 && y < height && z >= 0 && z < depth && activeMask[x, y, z];
        }

        private bool ShouldCreateVoxel(int x, int y, int z)
        {
            return IsActiveCoordinate(x, y, z);
        }

        private bool TryCreateActiveMask()
        {
            if (volumeMode == VoxelVolumeMode.SolidCube ||
                volumeMode == VoxelVolumeMode.HollowCube)
            {
                bool solid = volumeMode == VoxelVolumeMode.SolidCube;
                activeMask = CreateCubeMask(solid);
                activeBounds = new VoxelShapeBounds(
                    0,
                    width - 1,
                    0,
                    height - 1,
                    0,
                    depth - 1);
                LastGenerationSeed = 0;
                LastGenerationAttempt = 1;
                return true;
            }

            if (proceduralSettings == null)
            {
                proceduralSettings = new ProceduralVoxelShapeSettings();
            }

            int seed = proceduralSettings.useRandomSeed
                ? Guid.NewGuid().GetHashCode()
                : proceduralSettings.fixedSeed;
            if (!ProceduralVoxelShapeGenerator.TryGenerate(
                    width,
                    height,
                    depth,
                    proceduralSettings,
                    seed,
                    out ProceduralVoxelShapeResult result))
            {
                Debug.LogError(
                    $"Procedural 3D shape generation failed after " +
                    $"{proceduralSettings.maxGenerationAttempts} attempts. " +
                    "Falling back to a hollow cube.",
                    this);
                activeMask = CreateCubeMask(false);
                activeBounds = new VoxelShapeBounds(
                    0,
                    width - 1,
                    0,
                    height - 1,
                    0,
                    depth - 1);
                LastGenerationSeed = seed;
                LastGenerationAttempt = proceduralSettings.maxGenerationAttempts;
                return true;
            }

            activeMask = result.Mask;
            activeBounds = result.Bounds;
            LastGenerationSeed = result.Seed;
            LastGenerationAttempt = result.GenerationAttempt;
            if (proceduralSettings.logGenerationDetails)
            {
                float fill = result.ActiveVoxelCount * 100f / (width * height * depth);
                Debug.Log(
                    $"Procedural 3D shell generated with {result.ActiveVoxelCount} voxels " +
                    $"({fill:0.#}% of logical volume). Bounds: " +
                    $"{activeBounds.Width}x{activeBounds.Height}x{activeBounds.Depth}. " +
                    $"Seed: {result.Seed}. Attempt: {result.GenerationAttempt}.",
                    this);
            }

            return true;
        }

        private bool[,,] CreateCubeMask(bool solid)
        {
            var mask = new bool[width, height, depth];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        mask[x, y, z] = solid || x == 0 || x == width - 1 ||
                            y == 0 || y == height - 1 || z == 0 || z == depth - 1;
                    }
                }
            }

            return mask;
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

        private void RecalculateCapturedRegion(VoxelCell3D originVoxel)
        {
            var previouslyCaptured = new HashSet<VoxelCell3D>(capturedVoxels);
            for (int i = 0; i < capturedVoxels.Count; i++)
            {
                capturedVoxels[i].SetCaptured(false, false);
            }

            capturedVoxels.Clear();
            CurrentPlayerColor = originVoxel.ColorIndex;

            var queue = new Queue<VoxelCell3D>();
            var visited = new HashSet<VoxelCell3D>();
            var depthByVoxel = new Dictionary<VoxelCell3D, int>();
            queue.Enqueue(originVoxel);
            visited.Add(originVoxel);
            depthByVoxel.Add(originVoxel, 0);
            LastRecolorAnimationDuration = Mathf.Max(
                LastRecolorAnimationDuration,
                originVoxel.SetCaptured(true, !previouslyCaptured.Contains(originVoxel)));
            capturedVoxels.Add(originVoxel);

            while (queue.Count > 0)
            {
                VoxelCell3D current = queue.Dequeue();
                int currentDepth = depthByVoxel[current];
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
                    int neighborDepth = currentDepth + 1;
                    depthByVoxel.Add(neighbor, neighborDepth);
                    queue.Enqueue(neighbor);
                    float captureAnimationDuration = neighbor.SetCaptured(
                        true,
                        !previouslyCaptured.Contains(neighbor),
                        neighborDepth * waveStepDelay);
                    LastRecolorAnimationDuration = Mathf.Max(
                        LastRecolorAnimationDuration,
                        captureAnimationDuration);
                    capturedVoxels.Add(neighbor);
                }
            }

            LastNewlyCapturedCount = 0;
            for (int i = 0; i < capturedVoxels.Count; i++)
            {
                if (!previouslyCaptured.Contains(capturedVoxels[i]))
                {
                    LastNewlyCapturedCount++;
                }
            }
        }

        private bool TryGetVoxelAtScreenPosition(
            Vector2 screenPosition,
            out VoxelCell3D voxel)
        {
            voxel = null;
            Camera inputCamera = boardCamera != null ? boardCamera : Camera.main;
            if (cells == null || inputCamera == null)
            {
                return false;
            }

            Ray ray = inputCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit) ||
                !hit.collider.TryGetComponent(out voxel))
            {
                voxel = hit.collider != null
                    ? hit.collider.GetComponentInParent<VoxelCell3D>()
                    : null;
            }

            return voxel != null && GetCell(voxel.X, voxel.Y, voxel.Z) == voxel;
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
                if (Vector2.Distance(pointerDownPosition, screenPosition) > selectionDragThreshold)
                {
                    gestureExceededDragThreshold = true;
                }
                return;
            }

            if (pointerPhase == PointerPhase.Released)
            {
                bool canSelect = !IsPointerOverBlockingUI(screenPosition) &&
                    !IsPointerOverColorButton(screenPosition) &&
                    (!gestureExceededDragThreshold || gestureBeganOnColorButton);
                CancelPointerGesture();
                if (canSelect)
                {
                    TrySelectVoxelAtScreenPosition(screenPosition);
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
            pointerDownPosition = screenPosition;
            gestureExceededDragThreshold = false;
            gestureBeganOnColorButton = IsPointerOverColorButton(screenPosition);
        }

        private void CancelPointerGesture()
        {
            pointerGestureActive = false;
            gestureBeganOnColorButton = false;
            gestureExceededDragThreshold = false;
            activePointerId = -1;
        }

        private static bool IsPointerOverBlockingUI(Vector2 screenPosition)
        {
            PopulateUiRaycastResults(screenPosition);
            for (int i = 0; i < UiRaycastResults.Count; i++)
            {
                RaycastResult result = UiRaycastResults[i];
                if (result.module is GraphicRaycaster &&
                    result.gameObject.GetComponentInParent<ColorButton3D>() == null)
                {
                    UiRaycastResults.Clear();
                    return true;
                }
            }

            UiRaycastResults.Clear();
            return false;
        }

        private static bool IsPointerOverColorButton(Vector2 screenPosition)
        {
            PopulateUiRaycastResults(screenPosition);
            for (int i = 0; i < UiRaycastResults.Count; i++)
            {
                if (UiRaycastResults[i].gameObject.GetComponentInParent<ColorButton3D>() != null)
                {
                    UiRaycastResults.Clear();
                    return true;
                }
            }

            UiRaycastResults.Clear();
            return false;
        }

        private static void PopulateUiRaycastResults(Vector2 screenPosition)
        {
            UiRaycastResults.Clear();
            if (EventSystem.current == null)
            {
                return;
            }

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };
            EventSystem.current.RaycastAll(pointerData, UiRaycastResults);
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
                screenPosition = touch.position;
                pointerId = touch.fingerId;
                pointerPhase = touch.phase switch
                {
                    TouchPhase.Began => PointerPhase.Pressed,
                    TouchPhase.Ended => PointerPhase.Released,
                    TouchPhase.Canceled => PointerPhase.Canceled,
                    _ => PointerPhase.Held
                };
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

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            depth = Mathf.Max(1, depth);
            voxelSize = Mathf.Max(0.05f, voxelSize);
            voxelGap = Mathf.Max(0f, voxelGap);
            selectionDragThreshold = Mathf.Max(0f, selectionDragThreshold);
            waveStepDelay = Mathf.Max(0f, waveStepDelay);
            winZoomScale = Mathf.Clamp(winZoomScale, 0.1f, 1f);
            winZoomDuration = Mathf.Max(0.01f, winZoomDuration);
            winRotationDuration = Mathf.Max(0.1f, winRotationDuration);
            if (proceduralSettings == null)
            {
                proceduralSettings = new ProceduralVoxelShapeSettings();
            }
        }

        private void OnDestroy()
        {
            StopWinCelebration(false);
        }
    }
}
