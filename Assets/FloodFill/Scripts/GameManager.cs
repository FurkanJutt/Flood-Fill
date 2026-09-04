using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FloodFill
{
    public sealed class GameManager : MonoBehaviour
    {
        public enum GameState
        {
            Playing,
            Won,
            Lost
        }

        public enum GridSize
        {
            Size10x10 = 10,
            Size12x12 = 12,
            Size15x15 = 15,
            Size16x16 = 16,
            Size18x18 = 18,
            Size20x20 = 20,
            Size24x24 = 24
        }

        private static readonly GridSize[] GridSizeOptions =
        {
            GridSize.Size10x10,
            GridSize.Size12x12,
            GridSize.Size15x15,
            GridSize.Size16x16,
            GridSize.Size18x18,
            GridSize.Size20x20,
            GridSize.Size24x24
        };

        [Header("Game")]
        [SerializeField, Min(1)] private int maxMoves = 25;
        [SerializeField, Min(0f)] private float resultRevealDelay = 0.75f;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private GridSize gridSize = GridSize.Size16x16;

        [Header("Scoring")]
        [SerializeField, Min(1)] private int minimumRandomScore = 21;
        [SerializeField, Min(1)] private int maximumRandomScore = 49;
        [SerializeField, Min(1)] private int minimumCellsForScoreMultiplier = 4;
        [SerializeField, Min(2)] private int minimumScoreMultiplier = 3;
        [SerializeField, Min(2)] private int maximumScoreMultiplier = 7;

        [Header("Colors")]
        [SerializeField] private Color[] colors =
        {
            new Color(0.93f, 0.25f, 0.25f),
            new Color(0.25f, 0.78f, 0.38f),
            new Color(0.20f, 0.48f, 0.95f),
            new Color(0.98f, 0.82f, 0.20f),
            new Color(0.62f, 0.32f, 0.86f),
            new Color(1.00f, 0.52f, 0.16f),
            new Color(1.00f, 0.00f, 1.00f),
            new Color(0.30f, 0.82f, 0.80f)
        };

        [Header("UI")]
        [SerializeField] private TMP_Text movesText;
        [SerializeField] private TMP_Text capturedText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Dropdown boardSizeDropdown;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private ColorButton[] colorButtons;

        public int MoveCount { get; private set; }
        public int MaxMoves => maxMoves;
        public int SelectedGridSize => (int)gridSize;
        public int Score { get; private set; }
        public int LastScoreGain { get; private set; }
        public int LastRandomBaseScore { get; private set; }
        public int LastScoreMultiplier { get; private set; } = 1;
        public GameState State { get; private set; }
        public int SelectedColorIndex { get; private set; } = -1;
        public IReadOnlyList<Color> ActiveColors => activeColors;

        private bool awaitingResult;
        private Coroutine resultRevealCoroutine;
        private Color[] activeColors = System.Array.Empty<Color>();

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            boardManager.CellClicked += HandleCellClicked;
            InitializeBoardSizeDropdown();
            RestartGame();
        }

        public void SelectColor(int colorIndex)
        {
            if (State != GameState.Playing || awaitingResult || boardManager == null)
            {
                return;
            }

            if (colorIndex < 0 || colorIndex >= activeColors.Length || colorIndex == SelectedColorIndex)
            {
                return;
            }

            SelectedColorIndex = colorIndex;
            RefreshUI();
            Debug.Log($"Selected paint color: {colorIndex}. Tap a cell to recolor it.", this);
        }

        private void HandleCellClicked(Cell cell)
        {
            if (State != GameState.Playing || awaitingResult || SelectedColorIndex < 0)
            {
                return;
            }

            if (!boardManager.RecolorConnectedRegion(cell, SelectedColorIndex))
            {
                return;
            }

            AwardScore(boardManager.LastNewlyCapturedCellCount);
            MoveCount++;
            if (boardManager.IsFullyCaptured)
            {
                ScheduleResult(GameState.Won);
            }
            else if (MoveCount >= maxMoves)
            {
                ScheduleResult(GameState.Lost);
            }
            else
            {
                RefreshUI();
            }
        }

        public void RestartGame()
        {
            if (boardManager == null)
            {
                Debug.LogError("Flood Fill GameManager cannot restart without a BoardManager reference.", this);
                return;
            }

            SyncGridSizeFromDropdown();
            ApplySelectedGridSize();

            if (resultRevealCoroutine != null)
            {
                StopCoroutine(resultRevealCoroutine);
                resultRevealCoroutine = null;
            }

            awaitingResult = false;
            MoveCount = 0;
            Score = 0;
            LastScoreGain = 0;
            LastRandomBaseScore = 0;
            LastScoreMultiplier = 1;
            State = GameState.Playing;
            SelectedColorIndex = -1;

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            if (!SelectRandomActiveColors())
            {
                SetColorInputEnabled(false);
                return;
            }

            boardManager.SetActiveColors(activeColors);
            ApplyActiveColorsToButtons();

            if (!boardManager.GenerateBoard())
            {
                SetColorInputEnabled(false);
                return;
            }

            if (boardManager.IsFullyCaptured)
            {
                ScheduleResult(GameState.Won);
                return;
            }

            RefreshUI();
        }

        public void SetGrid()
        {
            ApplySelectedGridSize();
            if (Application.isPlaying)
            {
                RestartGame();
            }
        }

        private void ScheduleResult(GameState finalState)
        {
            awaitingResult = true;
            RefreshUI();
            SetColorInputEnabled(false);
            float animationDuration = finalState == GameState.Won
                ? boardManager.PlayWinCelebration()
                : boardManager.LastRecolorAnimationDuration;
            float delay = animationDuration + resultRevealDelay;
            resultRevealCoroutine = StartCoroutine(RevealResultAfterDelay(finalState, delay));
        }

        private IEnumerator RevealResultAfterDelay(GameState finalState, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            resultRevealCoroutine = null;
            awaitingResult = false;
            FinishGame(finalState);
        }

        public void Configure(
            BoardManager board,
            int moveLimit,
            TMP_Text movesLabel,
            TMP_Text capturedLabel,
            TMP_Text scoreLabel,
            TMP_Dropdown sizeDropdown,
            GameObject endPanel,
            TMP_Text endLabel,
            ColorButton[] buttons,
            Color[] colorPool)
        {
            boardManager = board;
            maxMoves = Mathf.Max(1, moveLimit);
            movesText = movesLabel;
            capturedText = capturedLabel;
            scoreText = scoreLabel;
            boardSizeDropdown = sizeDropdown;
            resultPanel = endPanel;
            resultText = endLabel;
            colorButtons = buttons;
            colors = colorPool;
        }

        private void FinishGame(GameState finalState)
        {
            State = finalState;
            RefreshUI();
            SetColorInputEnabled(false);

            if (resultText != null)
            {
                resultText.text = finalState == GameState.Won
                    ? $"YOU WIN!\n\nMoves used: {MoveCount}\nScore: {Score:N0}"
                    : $"OUT OF MOVES\n\nMoves used: {MoveCount} / {maxMoves}\nScore: {Score:N0}";
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            if (finalState == GameState.Won)
            {
                Debug.Log($"Game won in {MoveCount} moves", this);
            }
            else
            {
                Debug.Log($"Game lost after {MoveCount} moves", this);
            }
        }

        private void RefreshUI()
        {
            if (movesText != null)
            {
                movesText.text = $"Moves: {MoveCount} / {maxMoves}";
            }

            if (capturedText != null)
            {
                int displayedPercentage = MoveCount == 0
                    ? 0
                    : Mathf.RoundToInt(boardManager.CapturedPercentage);
                capturedText.text = $"Captured: {displayedPercentage}%";
            }

            if (scoreText != null)
            {
                scoreText.text = $"Score: {Score:N0}";
            }

            bool canPlay = State == GameState.Playing;
            boardManager.SetInputEnabled(canPlay && !awaitingResult);
            if (colorButtons == null)
            {
                return;
            }

            for (int i = 0; i < colorButtons.Length; i++)
            {
                ColorButton colorButton = colorButtons[i];
                if (colorButton != null)
                {
                    if (colorButton.ColorIndex >= 0 && colorButton.ColorIndex < activeColors.Length)
                    {
                        colorButton.SetVisualColor(activeColors[colorButton.ColorIndex]);
                    }

                    colorButton.SetSelected(colorButton.ColorIndex == SelectedColorIndex);
                    colorButton.SetInteractable(canPlay && colorButton.ColorIndex != SelectedColorIndex);
                }
            }
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
                if (colorButtons[i] != null)
                {
                    colorButtons[i].SetInteractable(enabledInput);
                }
            }
        }

        private bool SelectRandomActiveColors()
        {
            int requiredColorCount = colorButtons != null ? colorButtons.Length : 0;
            if (colors == null || requiredColorCount < 2 || colors.Length < requiredColorCount)
            {
                Debug.LogError(
                    $"Flood Fill needs at least {requiredColorCount} colors in GameManager's color pool.",
                    this);
                activeColors = System.Array.Empty<Color>();
                return false;
            }

            var availableColors = new List<Color>(colors);
            activeColors = new Color[requiredColorCount];
            for (int i = 0; i < requiredColorCount; i++)
            {
                int randomIndex = Random.Range(0, availableColors.Count);
                activeColors[i] = availableColors[randomIndex];
                availableColors.RemoveAt(randomIndex);
            }

            return true;
        }

        private void AwardScore(int newlyCapturedCellCount)
        {
            if (newlyCapturedCellCount <= 0)
            {
                LastScoreGain = 0;
                LastRandomBaseScore = 0;
                LastScoreMultiplier = 1;
                return;
            }

            LastRandomBaseScore = Random.Range(minimumRandomScore, maximumRandomScore + 1);
            if (newlyCapturedCellCount < minimumCellsForScoreMultiplier)
            {
                LastScoreMultiplier = 1;
            }
            else
            {
                int cellsAtMinimumMultiplier =
                    minimumCellsForScoreMultiplier + minimumScoreMultiplier - 1;
                LastScoreMultiplier = newlyCapturedCellCount <= cellsAtMinimumMultiplier
                    ? minimumScoreMultiplier
                    : Mathf.Min(
                        maximumScoreMultiplier,
                        minimumScoreMultiplier + newlyCapturedCellCount - cellsAtMinimumMultiplier);
            }

            LastScoreGain = LastRandomBaseScore * LastScoreMultiplier;
            Score += LastScoreGain;
        }

        private void ApplySelectedGridSize()
        {
            if (boardManager != null)
            {
                int dimension = (int)gridSize;
                boardManager.SetGridSize(dimension, dimension);
            }
        }

        private void InitializeBoardSizeDropdown()
        {
            if (boardSizeDropdown == null)
            {
                return;
            }

            var options = new List<string>(GridSizeOptions.Length);
            for (int i = 0; i < GridSizeOptions.Length; i++)
            {
                int dimension = (int)GridSizeOptions[i];
                options.Add($"{dimension} x {dimension}");
            }

            boardSizeDropdown.ClearOptions();
            boardSizeDropdown.AddOptions(options);
            int selectedIndex = System.Array.IndexOf(GridSizeOptions, gridSize);
            boardSizeDropdown.SetValueWithoutNotify(Mathf.Max(0, selectedIndex));
            boardSizeDropdown.RefreshShownValue();
            boardSizeDropdown.onValueChanged.RemoveListener(HandleBoardSizeDropdownChanged);
            boardSizeDropdown.onValueChanged.AddListener(HandleBoardSizeDropdownChanged);
        }

        private void HandleBoardSizeDropdownChanged(int optionIndex)
        {
            if (optionIndex >= 0 && optionIndex < GridSizeOptions.Length)
            {
                gridSize = GridSizeOptions[optionIndex];
            }
        }

        private void SyncGridSizeFromDropdown()
        {
            if (boardSizeDropdown != null)
            {
                HandleBoardSizeDropdownChanged(boardSizeDropdown.value);
            }
        }

        private void ApplyActiveColorsToButtons()
        {
            for (int i = 0; i < colorButtons.Length; i++)
            {
                ColorButton colorButton = colorButtons[i];
                if (colorButton != null &&
                    colorButton.ColorIndex >= 0 &&
                    colorButton.ColorIndex < activeColors.Length)
                {
                    colorButton.SetVisualColor(activeColors[colorButton.ColorIndex]);
                }
            }
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (boardManager == null)
            {
                Debug.LogError("Flood Fill GameManager is missing its BoardManager reference.", this);
                valid = false;
            }

            if (movesText == null || capturedText == null || scoreText == null ||
                resultPanel == null || resultText == null)
            {
                Debug.LogError("Flood Fill GameManager is missing one or more UI references.", this);
                valid = false;
            }

            if (colorButtons == null || colorButtons.Length < 2)
            {
                Debug.LogError("Flood Fill GameManager requires at least two color buttons.", this);
                valid = false;
            }

            if (colors == null || colorButtons == null || colors.Length < colorButtons.Length)
            {
                Debug.LogError(
                    "Flood Fill GameManager needs at least one source color for every color button.",
                    this);
                valid = false;
            }

            return valid;
        }

        private void OnDestroy()
        {
            if (boardSizeDropdown != null)
            {
                boardSizeDropdown.onValueChanged.RemoveListener(HandleBoardSizeDropdownChanged);
            }

            if (boardManager != null)
            {
                boardManager.CellClicked -= HandleCellClicked;
            }
        }

        private void OnValidate()
        {
            maxMoves = Mathf.Max(1, maxMoves);
            minimumRandomScore = Mathf.Max(1, minimumRandomScore);
            maximumRandomScore = Mathf.Max(minimumRandomScore, maximumRandomScore);
            minimumCellsForScoreMultiplier = Mathf.Max(1, minimumCellsForScoreMultiplier);
            minimumScoreMultiplier = Mathf.Max(2, minimumScoreMultiplier);
            maximumScoreMultiplier = Mathf.Max(minimumScoreMultiplier, maximumScoreMultiplier);
        }
    }
}
