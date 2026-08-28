using System.Collections;
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

        [Header("Game")]
        [SerializeField, Min(1)] private int maxMoves = 25;
        [SerializeField, Min(0f)] private float resultRevealDelay = 0.25f;
        [SerializeField] private BoardManager boardManager;

        [Header("UI")]
        [SerializeField] private TMP_Text movesText;
        [SerializeField] private TMP_Text capturedText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private ColorButton[] colorButtons;

        public int MoveCount { get; private set; }
        public int MaxMoves => maxMoves;
        public GameState State { get; private set; }
        public int SelectedColorIndex { get; private set; } = -1;

        private bool awaitingResult;
        private Coroutine resultRevealCoroutine;

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            boardManager.CellClicked += HandleCellClicked;
            RestartGame();
        }

        public void SelectColor(int colorIndex)
        {
            if (State != GameState.Playing || awaitingResult || boardManager == null)
            {
                return;
            }

            if (colorIndex < 0 || colorIndex >= boardManager.Colors.Count || colorIndex == SelectedColorIndex)
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

            if (resultRevealCoroutine != null)
            {
                StopCoroutine(resultRevealCoroutine);
                resultRevealCoroutine = null;
            }

            awaitingResult = false;
            MoveCount = 0;
            State = GameState.Playing;

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            if (!boardManager.GenerateBoard())
            {
                SetColorInputEnabled(false);
                return;
            }

            SelectedColorIndex = -1;
            if (boardManager.IsFullyCaptured)
            {
                FinishGame(GameState.Won);
                return;
            }

            RefreshUI();
        }

        private void ScheduleResult(GameState finalState)
        {
            awaitingResult = true;
            RefreshUI();
            SetColorInputEnabled(false);
            float delay = boardManager.LastRecolorAnimationDuration + resultRevealDelay;
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
            GameObject endPanel,
            TMP_Text endLabel,
            ColorButton[] buttons)
        {
            boardManager = board;
            maxMoves = Mathf.Max(1, moveLimit);
            movesText = movesLabel;
            capturedText = capturedLabel;
            resultPanel = endPanel;
            resultText = endLabel;
            colorButtons = buttons;
        }

        private void FinishGame(GameState finalState)
        {
            State = finalState;
            RefreshUI();
            SetColorInputEnabled(false);

            if (resultText != null)
            {
                resultText.text = finalState == GameState.Won
                    ? $"YOU WIN!\n\nMoves used: {MoveCount}"
                    : $"OUT OF MOVES\n\nMoves used: {MoveCount} / {maxMoves}";
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
                    if (colorButton.ColorIndex >= 0 && colorButton.ColorIndex < boardManager.Colors.Count)
                    {
                        colorButton.SetVisualColor(boardManager.Colors[colorButton.ColorIndex]);
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

        private bool ValidateReferences()
        {
            bool valid = true;

            if (boardManager == null)
            {
                Debug.LogError("Flood Fill GameManager is missing its BoardManager reference.", this);
                valid = false;
            }

            if (movesText == null || capturedText == null || resultPanel == null || resultText == null)
            {
                Debug.LogError("Flood Fill GameManager is missing one or more UI references.", this);
                valid = false;
            }

            if (colorButtons == null || colorButtons.Length < 2)
            {
                Debug.LogError("Flood Fill GameManager requires at least two color buttons.", this);
                valid = false;
            }

            return valid;
        }

        private void OnDestroy()
        {
            if (boardManager != null)
            {
                boardManager.CellClicked -= HandleCellClicked;
            }
        }

        private void OnValidate()
        {
            maxMoves = Mathf.Max(1, maxMoves);
        }
    }
}
