using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace FloodFill.ThreeD
{
    public sealed class FloodFillGameManager3D : MonoBehaviour
    {
        public enum GameState
        {
            Playing,
            Won,
            Lost
        }

        private static readonly VoxelBoardManager3D.VoxelVolumeMode[] BoardModeOptions =
        {
            VoxelBoardManager3D.VoxelVolumeMode.Procedural,
            VoxelBoardManager3D.VoxelVolumeMode.HollowCube
        };

        private static readonly int[] BoardSizeOptions =
        {
            6, 8, 10, 12, 15, 18, 20
        };

        [Header("Game")]
        [SerializeField, Min(1)] private int maxMoves = 25;
        [SerializeField, Min(0f)] private float resultRevealDelay = 0.45f;
        [SerializeField] private VoxelBoardManager3D boardManager;
        [SerializeField] private OrbitCameraController3D orbitCamera;

        [Header("Palette")]
        [SerializeField] private Color[] colors =
        {
            new Color(0.25f, 0.78f, 0.38f),
            new Color(0.62f, 0.32f, 0.86f),
            new Color(0.98f, 0.82f, 0.20f),
            new Color(1.00f, 0.52f, 0.16f),
            new Color(0.20f, 0.48f, 0.95f),
            new Color(0.30f, 0.82f, 0.80f)
        };

        [Header("UI")]
        [SerializeField] private TMP_Text movesText;
        [SerializeField] private TMP_Text capturedText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Dropdown boardModeDropdown;
        [SerializeField] private TMP_Dropdown boardSizeDropdown;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private ColorButton3D[] colorButtons;

        private Coroutine resultCoroutine;

        public int MoveCount { get; private set; }
        public int Score { get; private set; }
        public GameState State { get; private set; }
        public int SelectedColorIndex { get; private set; } = -1;
        public VoxelBoardManager3D.VoxelVolumeMode SelectedBoardMode { get; private set; } =
            VoxelBoardManager3D.VoxelVolumeMode.Procedural;
        public int SelectedBoardSize { get; private set; } = 6;
        public int CurrentPlayerColor => boardManager != null ? boardManager.CurrentPlayerColor : -1;

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            boardManager.VoxelClicked += HandleVoxelClicked;
            EnsureBoardModeDropdown();
            EnsureBoardSizeDropdown();
            InitializeBoardModeDropdown();
            InitializeBoardSizeDropdown();
            RestartGame();
        }

        public void SelectColor(int colorIndex)
        {
            if (State != GameState.Playing || boardManager == null ||
                colorIndex < 0 || colors == null || colorIndex >= colors.Length ||
                colorIndex == SelectedColorIndex)
            {
                return;
            }

            SelectedColorIndex = colorIndex;
            RefreshUI();
            Debug.Log($"Selected 3D paint color: {colorIndex}. Click a cube to recolor it.", this);
        }

        private void HandleVoxelClicked(VoxelCell3D voxel)
        {
            if (State != GameState.Playing || boardManager == null ||
                SelectedColorIndex < 0)
            {
                return;
            }

            if (!boardManager.RecolorConnectedRegion(voxel, SelectedColorIndex))
            {
                return;
            }

            MoveCount++;
            AwardScore(boardManager.LastNewlyCapturedCount);
            RefreshUI();
            if (boardManager.IsFullyCaptured)
            {
                ScheduleResult(GameState.Won);
            }
            else if (MoveCount >= maxMoves)
            {
                ScheduleResult(GameState.Lost);
            }
        }

        public void RestartGame()
        {
            Stopwatch restartWatch = Stopwatch.StartNew();
            if (resultCoroutine != null)
            {
                StopCoroutine(resultCoroutine);
                resultCoroutine = null;
            }

            MoveCount = 0;
            Score = 0;
            State = GameState.Playing;
            SelectedColorIndex = -1;
            SyncBoardModeFromDropdown();
            SyncBoardSizeFromDropdown();
            boardManager.SetVolumeMode(SelectedBoardMode);
            boardManager.SetDimensions(SelectedBoardSize);
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            if (!boardManager.GenerateBoard(colors))
            {
                SetColorInputEnabled(false);
                restartWatch.Stop();
                boardManager.LogLastGenerationPerformance(0d, restartWatch.Elapsed.TotalMilliseconds);
                return;
            }

            double cameraFramingMilliseconds = 0d;
            if (boardManager.TryGetWorldBounds(out Bounds bounds))
            {
                Stopwatch cameraWatch = Stopwatch.StartNew();
                orbitCamera.FrameBounds(bounds);
                cameraWatch.Stop();
                cameraFramingMilliseconds = cameraWatch.Elapsed.TotalMilliseconds;
            }

            RefreshUI();
            if (boardManager.IsFullyCaptured)
            {
                ScheduleResult(GameState.Won);
            }

            restartWatch.Stop();
            boardManager.LogLastGenerationPerformance(
                cameraFramingMilliseconds,
                restartWatch.Elapsed.TotalMilliseconds);
        }

        public void Configure(
            VoxelBoardManager3D board,
            OrbitCameraController3D cameraController,
            int moveLimit,
            TMP_Text movesLabel,
            TMP_Text capturedLabel,
            TMP_Text scoreLabel,
            TMP_Dropdown modeDropdown,
            TMP_Dropdown sizeDropdown,
            GameObject endPanel,
            TMP_Text endLabel,
            ColorButton3D[] buttons,
            Color[] palette)
        {
            boardManager = board;
            orbitCamera = cameraController;
            maxMoves = Mathf.Max(1, moveLimit);
            movesText = movesLabel;
            capturedText = capturedLabel;
            scoreText = scoreLabel;
            boardModeDropdown = modeDropdown;
            boardSizeDropdown = sizeDropdown;
            resultPanel = endPanel;
            resultText = endLabel;
            colorButtons = buttons;
            colors = palette;
        }

        private void InitializeBoardModeDropdown()
        {
            SelectedBoardMode = boardManager != null
                ? boardManager.VolumeMode
                : VoxelBoardManager3D.VoxelVolumeMode.Procedural;
            if (boardModeDropdown == null)
            {
                return;
            }

            boardModeDropdown.ClearOptions();
            boardModeDropdown.AddOptions(new List<string>
            {
                "Random Shape",
                "Full Grid"
            });
            int selectedIndex = System.Array.IndexOf(BoardModeOptions, SelectedBoardMode);
            boardModeDropdown.SetValueWithoutNotify(Mathf.Max(0, selectedIndex));
            boardModeDropdown.RefreshShownValue();
            boardModeDropdown.onValueChanged.RemoveListener(HandleBoardModeDropdownChanged);
            boardModeDropdown.onValueChanged.AddListener(HandleBoardModeDropdownChanged);
        }

        private void InitializeBoardSizeDropdown()
        {
            SelectedBoardSize = boardManager != null ? boardManager.Width : BoardSizeOptions[0];
            if (boardSizeDropdown == null)
            {
                return;
            }

            var labels = new List<string>(BoardSizeOptions.Length);
            for (int i = 0; i < BoardSizeOptions.Length; i++)
            {
                labels.Add($"{BoardSizeOptions[i]}x{BoardSizeOptions[i]}");
            }

            boardSizeDropdown.ClearOptions();
            boardSizeDropdown.AddOptions(labels);
            int selectedIndex = System.Array.IndexOf(BoardSizeOptions, SelectedBoardSize);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
                SelectedBoardSize = BoardSizeOptions[selectedIndex];
            }

            boardSizeDropdown.SetValueWithoutNotify(selectedIndex);
            boardSizeDropdown.RefreshShownValue();
            boardSizeDropdown.onValueChanged.RemoveListener(HandleBoardSizeDropdownChanged);
            boardSizeDropdown.onValueChanged.AddListener(HandleBoardSizeDropdownChanged);
        }

        private void EnsureBoardModeDropdown()
        {
            boardModeDropdown = EnsureRuntimeDropdown(
                boardModeDropdown,
                "BoardModeDropdown",
                new Vector2(120f, -62f),
                120f);
        }

        private void EnsureBoardSizeDropdown()
        {
            boardSizeDropdown = EnsureRuntimeDropdown(
                boardSizeDropdown,
                "BoardSizeDropdown",
                new Vector2(120f, -148f),
                360f);
        }

        private TMP_Dropdown EnsureRuntimeDropdown(
            TMP_Dropdown dropdown,
            string objectName,
            Vector2 position,
            float templateHeight)
        {
            if (dropdown != null)
            {
                return dropdown;
            }

            Canvas canvas = movesText != null
                ? movesText.GetComponentInParent<Canvas>()
                : FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning($"The 3D {objectName} could not find a Canvas.", this);
                return null;
            }

            Transform existingDropdown = canvas.transform.Find(objectName);
            if (existingDropdown != null &&
                existingDropdown.TryGetComponent(out TMP_Dropdown existingComponent))
            {
                return existingComponent;
            }

            GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(
                new TMP_DefaultControls.Resources());
            dropdownObject.name = objectName;
            dropdownObject.transform.SetParent(canvas.transform, false);
            SetLayerRecursively(dropdownObject, canvas.gameObject.layer);

            RectTransform dropdownRect = dropdownObject.GetComponent<RectTransform>();
            dropdownRect.anchorMin = new Vector2(0f, 1f);
            dropdownRect.anchorMax = new Vector2(0f, 1f);
            dropdownRect.pivot = new Vector2(0.5f, 0.5f);
            dropdownRect.anchoredPosition = position;
            dropdownRect.sizeDelta = new Vector2(190f, 76f);

            Transform restartButton = canvas.transform.Find("RestartButton");
            if (restartButton != null)
            {
                dropdownObject.transform.SetSiblingIndex(restartButton.GetSiblingIndex() + 1);
            }

            dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.navigation = new Navigation { mode = Navigation.Mode.None };
            dropdown.colors = CreateDropdownColors();

            Image background = dropdownObject.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.18f, 0.20f, 0.28f, 1f);
            }

            TMP_FontAsset uiFont = FindSceneFont(canvas.transform);
            StyleDropdownText(dropdown.captionText, uiFont, true);
            StyleDropdownText(dropdown.itemText, uiFont, false);
            if (dropdown.itemText != null &&
                dropdown.itemText.transform.parent is RectTransform itemRect)
            {
                itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, 50f);
            }

            if (dropdown.template != null)
            {
                dropdown.template.sizeDelta = new Vector2(0f, templateHeight);
                Image templateBackground = dropdown.template.GetComponent<Image>();
                if (templateBackground != null)
                {
                    templateBackground.color = new Color(0.12f, 0.14f, 0.21f, 1f);
                }

                Toggle itemToggle = dropdown.template.GetComponentInChildren<Toggle>(true);
                if (itemToggle != null)
                {
                    itemToggle.colors = CreateDropdownColors();
                    if (itemToggle.targetGraphic is Image itemBackground)
                    {
                        itemBackground.color = new Color(0.18f, 0.20f, 0.28f, 1f);
                    }
                }
            }

            AddDropdownArrow(dropdownObject, uiFont);
            return dropdown;
        }

        private static void StyleDropdownText(
            TMP_Text text,
            TMP_FontAsset font,
            bool caption)
        {
            if (text == null)
            {
                return;
            }

            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = 24f;
            text.fontStyle = caption ? FontStyles.Bold : FontStyles.Normal;
            text.color = new Color(0.95f, 0.96f, 1f);
            if (caption)
            {
                text.alignment = TextAlignmentOptions.Center;
                text.rectTransform.offsetMin = new Vector2(8f, 0f);
                text.rectTransform.offsetMax = new Vector2(-38f, 0f);
            }
        }

        private static void AddDropdownArrow(GameObject dropdownObject, TMP_FontAsset font)
        {
            Transform existingArrow = dropdownObject.transform.Find("Arrow");
            if (existingArrow != null)
            {
                existingArrow.gameObject.SetActive(false);
            }

            var arrowObject = new GameObject(
                "ArrowLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            arrowObject.transform.SetParent(dropdownObject.transform, false);
            arrowObject.layer = dropdownObject.layer;
            TextMeshProUGUI arrowText = arrowObject.GetComponent<TextMeshProUGUI>();
            arrowText.text = "▼";
            if (font != null)
            {
                arrowText.font = font;
            }

            arrowText.fontSize = 22f;
            arrowText.fontStyle = FontStyles.Bold;
            arrowText.color = new Color(0.95f, 0.96f, 1f);
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.raycastTarget = false;
            RectTransform arrowRect = arrowText.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-21f, 0f);
            arrowRect.sizeDelta = new Vector2(34f, 50f);
        }

        private static ColorBlock CreateDropdownColors()
        {
            return new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.08f, 1.08f, 1.08f),
                pressedColor = new Color(0.88f, 0.90f, 0.96f),
                selectedColor = Color.white,
                disabledColor = new Color(0.55f, 0.57f, 0.64f, 0.65f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private static TMP_FontAsset FindSceneFont(Transform canvas)
        {
            TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null)
                {
                    return texts[i].font;
                }
            }

            return null;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            for (int i = 0; i < target.transform.childCount; i++)
            {
                SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
            }
        }

        private void HandleBoardModeDropdownChanged(int optionIndex)
        {
            if (optionIndex >= 0 && optionIndex < BoardModeOptions.Length)
            {
                SelectedBoardMode = BoardModeOptions[optionIndex];
            }
        }

        private void HandleBoardSizeDropdownChanged(int optionIndex)
        {
            if (optionIndex >= 0 && optionIndex < BoardSizeOptions.Length)
            {
                SelectedBoardSize = BoardSizeOptions[optionIndex];
            }
        }

        private void SyncBoardModeFromDropdown()
        {
            if (boardModeDropdown != null)
            {
                HandleBoardModeDropdownChanged(boardModeDropdown.value);
            }
        }

        private void SyncBoardSizeFromDropdown()
        {
            if (boardSizeDropdown != null)
            {
                HandleBoardSizeDropdownChanged(boardSizeDropdown.value);
            }
        }

        private void AwardScore(int newlyCapturedCount)
        {
            if (newlyCapturedCount <= 0)
            {
                return;
            }

            int baseScore = Random.Range(21, 50);
            int multiplier;
            if (newlyCapturedCount < 4)
            {
                multiplier = 1;
            }
            else if (newlyCapturedCount <= 6)
            {
                multiplier = 3;
            }
            else
            {
                multiplier = Mathf.Clamp(newlyCapturedCount - 3, 4, 7);
            }

            Score += baseScore * multiplier;
        }

        private void ScheduleResult(GameState state)
        {
            State = state;
            SetColorInputEnabled(false);
            if (resultCoroutine != null)
            {
                StopCoroutine(resultCoroutine);
            }

            float animationDuration = state == GameState.Won
                ? boardManager.PlayWinCelebration()
                : boardManager.LastRecolorAnimationDuration;
            float revealDelay = animationDuration + resultRevealDelay;
            resultCoroutine = StartCoroutine(RevealResult(state, revealDelay));
        }

        private IEnumerator RevealResult(GameState state, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            resultCoroutine = null;
            if (resultText != null)
            {
                resultText.text = state == GameState.Won
                    ? $"YOU WIN!\n\nMoves used: {MoveCount}\nScore: {Score:N0}"
                    : $"OUT OF MOVES\n\nMoves used: {MoveCount} / {maxMoves}\nScore: {Score:N0}";
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            Debug.Log(
                state == GameState.Won
                    ? $"3D game won in {MoveCount} moves."
                    : $"3D game lost after {MoveCount} moves.",
                this);
        }

        private void RefreshUI()
        {
            movesText.text = $"Moves: {MoveCount} / {maxMoves}";
            int displayedPercentage = MoveCount == 0
                ? 0
                : Mathf.RoundToInt(boardManager.CapturedPercentage);
            capturedText.text = $"Captured: {displayedPercentage}%";
            scoreText.text = $"Score: {Score:N0}";
            SetColorInputEnabled(State == GameState.Playing);
        }

        private void SetColorInputEnabled(bool enabledInput)
        {
            if (boardManager != null)
            {
                boardManager.SetInputEnabled(enabledInput);
            }

            if (colorButtons == null)
            {
                return;
            }

            for (int i = 0; i < colorButtons.Length; i++)
            {
                ColorButton3D colorButton = colorButtons[i];
                if (colorButton == null)
                {
                    continue;
                }

                bool selected = colorButton.ColorIndex == SelectedColorIndex;
                colorButton.SetSelected(selected);
                colorButton.SetInteractable(enabledInput && !selected);
            }
        }

        private bool ValidateReferences()
        {
            bool valid = boardManager != null && orbitCamera != null &&
                movesText != null && capturedText != null && scoreText != null &&
                resultPanel != null && resultText != null &&
                colorButtons != null && colorButtons.Length >= 2 &&
                colors != null && colors.Length >= colorButtons.Length;
            if (!valid)
            {
                Debug.LogError("Flood Fill 3D GameManager has missing references.", this);
            }

            return valid;
        }

        private void OnValidate()
        {
            maxMoves = Mathf.Max(1, maxMoves);
            resultRevealDelay = Mathf.Max(0f, resultRevealDelay);
        }

        private void OnDestroy()
        {
            if (boardModeDropdown != null)
            {
                boardModeDropdown.onValueChanged.RemoveListener(HandleBoardModeDropdownChanged);
            }

            if (boardSizeDropdown != null)
            {
                boardSizeDropdown.onValueChanged.RemoveListener(HandleBoardSizeDropdownChanged);
            }

            if (boardManager != null)
            {
                boardManager.VoxelClicked -= HandleVoxelClicked;
            }
        }
    }
}
