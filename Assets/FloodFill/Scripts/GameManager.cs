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

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            RestartGame();
        }

        public void SelectColor(int colorIndex)
        {
            if (State != GameState.Playing || boardManager == null)
            {
                return;
            }

            if (!boardManager.ChangePlayerColor(colorIndex))
            {
                return;
            }

            MoveCount++;

            if (boardManager.IsFullyCaptured)
            {
                FinishGame(GameState.Won);
            }
            else if (MoveCount >= maxMoves)
            {
                FinishGame(GameState.Lost);
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

            if (boardManager.IsFullyCaptured)
            {
                FinishGame(GameState.Won);
                return;
            }

            RefreshUI();
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
                capturedText.text = $"Captured: {Mathf.RoundToInt(boardManager.CapturedPercentage)}%";
            }

            bool canPlay = State == GameState.Playing;
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

                    colorButton.SetInteractable(canPlay && colorButton.ColorIndex != boardManager.CurrentPlayerColor);
                }
            }
        }

        private void SetColorInputEnabled(bool enabledInput)
        {
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

        private void OnValidate()
        {
            maxMoves = Mathf.Max(1, maxMoves);
        }
    }
}
