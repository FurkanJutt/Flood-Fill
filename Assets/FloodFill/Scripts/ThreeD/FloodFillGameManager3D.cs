using System.Collections;
using TMPro;
using UnityEngine;

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
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private ColorButton3D[] colorButtons;

        private Coroutine resultCoroutine;

        public int MoveCount { get; private set; }
        public int Score { get; private set; }
        public GameState State { get; private set; }
        public int CurrentPlayerColor => boardManager != null ? boardManager.CurrentPlayerColor : -1;

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
            if (State != GameState.Playing || boardManager == null ||
                colorIndex == boardManager.CurrentPlayerColor)
            {
                return;
            }

            if (!boardManager.ChangePlayerColor(colorIndex))
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
            if (resultCoroutine != null)
            {
                StopCoroutine(resultCoroutine);
                resultCoroutine = null;
            }

            MoveCount = 0;
            Score = 0;
            State = GameState.Playing;
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            if (!boardManager.GenerateBoard(colors))
            {
                SetColorInputEnabled(false);
                return;
            }

            if (boardManager.TryGetWorldBounds(out Bounds bounds))
            {
                orbitCamera.FrameBounds(bounds);
            }

            RefreshUI();
            if (boardManager.IsFullyCaptured)
            {
                ScheduleResult(GameState.Won);
            }
        }

        public void Configure(
            VoxelBoardManager3D board,
            OrbitCameraController3D cameraController,
            int moveLimit,
            TMP_Text movesLabel,
            TMP_Text capturedLabel,
            TMP_Text scoreLabel,
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
            resultPanel = endPanel;
            resultText = endLabel;
            colorButtons = buttons;
            colors = palette;
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

            resultCoroutine = StartCoroutine(RevealResult(state));
        }

        private IEnumerator RevealResult(GameState state)
        {
            if (resultRevealDelay > 0f)
            {
                yield return new WaitForSeconds(resultRevealDelay);
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
            capturedText.text = $"Captured: {Mathf.RoundToInt(boardManager.CapturedPercentage)}%";
            scoreText.text = $"Score: {Score:N0}";
            SetColorInputEnabled(State == GameState.Playing);
        }

        private void SetColorInputEnabled(bool enabledInput)
        {
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

                bool selected = colorButton.ColorIndex == CurrentPlayerColor;
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
    }
}
