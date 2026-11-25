using FloodFill.ThreeD.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FloodFill.ThreeD.Solver;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace FloodFill.ThreeD
{
    public sealed partial class FloodFillGameManager3D : MonoBehaviour
    {
        public enum GameState
        {
            Calculating,
            Playing,
            Won,
            Lost
        }

        private static readonly VoxelBoardManager3D.VoxelVolumeMode[] BoardModeOptions =
        {
            VoxelBoardManager3D.VoxelVolumeMode.Procedural,
            VoxelBoardManager3D.VoxelVolumeMode.HollowCube,
            VoxelBoardManager3D.VoxelVolumeMode.Items
        };

        private static readonly int[] BoardSizeOptions =
        {
            6, 8, 10, 12, 15, 18, 20
        };
        private const int CampaignLevelsPerSize = 5;
        private const string CampaignLevelKey = "FloodFill3D.CampaignLevel";
        private const string CampaignRandomSizeLevelKey = "FloodFill3D.CampaignRandomSizeLevel";
        private const string CampaignRandomSizeIndexKey = "FloodFill3D.CampaignRandomSizeIndex";

        [Header("Game")]
        [SerializeField] private FloodFillDifficulty difficulty = FloodFillDifficulty.Normal;
        [SerializeField, Min(0f)] private float resultRevealDelay = 0.45f;
        [SerializeField] private VoxelBoardManager3D boardManager;
        [SerializeField] private OrbitCameraController3D orbitCamera;

        [Header("Dynamic Move Solver")]
        [SerializeField] private MoveSolverSettings solverSettings = new MoveSolverSettings();

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
        [SerializeField] private TMP_Dropdown difficultyDropdown;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private ColorButton3D[] colorButtons;

        private Coroutine resultCoroutine;
        private Coroutine solverCoroutine;
        private Coroutine loadingCoroutine;
        private bool loadingSavedSession;
        private int solverWorkerThreadId;
        private static readonly SemaphoreSlim SolverWorkerGate = new SemaphoreSlim(1, 1);
        public bool IsLoading { get; private set; }
        public string LoadingStatus { get; private set; } = "Preparing your level…";
        public string LoadingError { get; private set; }
        private sealed class SolverPreparation
        {
            public FloodFillSolverResult Result;
            public Goals.SecondarySolutionTrace GoalTrace;
            public System.Exception Error;
            public int WorkerThreadId = Thread.CurrentThread.ManagedThreadId;
        }
        private CancellationTokenSource solverCancellation;
        private int solverGenerationId;
        private int currentMoveBudget = 1;
        private bool audioPaused;
        private bool gameSessionActive;
        private bool elapsedTimerActive, elapsedTimerSuspended, skipElapsedTick;
        public double ElapsedSeconds { get; private set; }
        public string ElapsedTimeText => FormatElapsedTime(ElapsedSeconds);
        public static string FormatElapsedTime(double seconds)
        {
            long total = (long)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Floor(seconds)));
            return total >= 3600 ? $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}" : $"{total / 60:00}:{total % 60:00}";
        }
        private void Update()
        {
            if (skipElapsedTick) { skipElapsedTick = false; return; }
            if (elapsedTimerActive && !elapsedTimerSuspended && (CanInteract || PowerUps != null && PowerUps.IsExecuting && State == GameState.Playing && !IsLoading && !audioPaused) && Time.timeScale > 0)
                ElapsedSeconds += Time.unscaledDeltaTime;
        }

        private bool isCampaignGame;
        private int activeCampaignLevel = 1;

        public bool CanInteract => (PowerUps == null || !PowerUps.IsExecuting) && !IsLoading && string.IsNullOrEmpty(LoadingError) && State == GameState.Playing && !audioPaused && boardManager != null && boardManager.HasActiveBoard && !boardManager.IsStartAnimationPlaying;
        public int MoveCount { get; private set; }
        public int Score { get; private set; }
        public GameState State { get; private set; }
        public int SelectedColorIndex { get; private set; } = -1;
        public VoxelBoardManager3D.VoxelVolumeMode SelectedBoardMode { get; private set; } =
            VoxelBoardManager3D.VoxelVolumeMode.Procedural;
        public int SelectedBoardSize { get; private set; } = 6;
        public FloodFillDifficulty SelectedDifficulty => difficulty;
        public int CurrentMoveBudget => currentMoveBudget;
        public FloodFillSolverResult LastSolverResult { get; private set; }
        public MoveBudgetProfile CurrentMoveBudgetProfile { get; private set; }
        public int CurrentPlayerColor => boardManager != null ? boardManager.CurrentPlayerColor : -1;
        public float CapturedPercentage => boardManager != null ? boardManager.CoveredPercentage : 0f;
        public VoxelBoardManager3D BoardManager => boardManager;
        public bool IsCampaignGame => isCampaignGame;
        public int ActiveCampaignLevel => activeCampaignLevel;
        public int CampaignLevel { get; private set; } = 1;
        public event System.Action UIChanged;
        public event System.Action ManagerReady;
        public bool HasSavedSession { get; private set; }
        private FloodFillSessionSaveData3D availableSession;
        private bool failedToRestoreSession;

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            FloodAudioSignals.Register(this);
            boardManager.VoxelClicked += HandleVoxelClicked;
            boardManager.StartAnimationChanged += RefreshUI;
            if (boardModeDropdown != null) InitializeBoardModeDropdown();
            if (boardSizeDropdown != null) InitializeBoardSizeDropdown();
            if (difficultyDropdown != null) InitializeDifficultyDropdown();
            CampaignLevel = Mathf.Max(1, PlayerPrefs.GetInt(CampaignLevelKey, 1));
            HasSavedSession = TryRestoreSavedSession();
            gameSessionActive = HasSavedSession;
            if (HasSavedSession)
            {
                boardManager.SetInputEnabled(false);
                boardManager.SetBoardVisible(false);
            }
            else
            {
                State = GameState.Calculating;
                boardManager.SetInputEnabled(false);
            }
            UIChanged?.Invoke();
            ManagerReady?.Invoke();
        }

        public int NumberedLevel { get; private set; }
        public int NumberedMode { get; private set; }
        public bool StartNumberedLevel(int mode, int difficultyIndex, int level)
        {
            if (mode < 0 || mode > 2 || difficultyIndex < 0 || difficultyIndex > 3 || !FloodLevelProgression3D.Unlocked(mode, difficultyIndex, level)) return false;
            NumberedLevel = level; NumberedMode = mode;
            StartGame(mode == 0 ? 1 : mode == 1 ? 0 : 2, Mathf.Min(6, (level - 1) / 15), difficultyIndex, false);
            return true;
        }

        public void StartConfiguredGame(int modeIndex, int sizeIndex, int difficultyIndex)
        {
            NumberedLevel = 0;
            StartGame(modeIndex, sizeIndex, difficultyIndex, false);
        }

        public void StartCampaignGame(int difficultyIndex)
        {
            NumberedLevel = 0;
            CampaignLevel = Mathf.Max(1, PlayerPrefs.GetInt(CampaignLevelKey, CampaignLevel));
            int maxStructuredLevel = BoardSizeOptions.Length * CampaignLevelsPerSize;
            int sizeIndex = CampaignLevel <= maxStructuredLevel
                ? (CampaignLevel - 1) / CampaignLevelsPerSize
                : GetRandomCampaignSizeIndex();
            int modeIndex = CampaignLevel % CampaignLevelsPerSize == 0 ? 0 : 1;
            StartGame(modeIndex, sizeIndex, difficultyIndex, true);
        }

        public void RestartCurrentGame()
        {
            FloodAudioSignals.Restart();
            RestartGame();
        }

        private void StartGame(int modeIndex, int sizeIndex, int difficultyIndex, bool campaign)
        {
            if (modeIndex < 0 || modeIndex >= BoardModeOptions.Length ||
                sizeIndex < 0 || sizeIndex >= BoardSizeOptions.Length ||
                difficultyIndex < 0 || difficultyIndex > (int)FloodFillDifficulty.Perfect)
                return;
            gameSessionActive = true;
            isCampaignGame = campaign;
            if (campaign) activeCampaignLevel = CampaignLevel;
            SelectedBoardMode = BoardModeOptions[modeIndex];
            SelectedBoardSize = BoardSizeOptions[sizeIndex];
            difficulty = (FloodFillDifficulty)difficultyIndex;
            if (boardModeDropdown != null) boardModeDropdown.SetValueWithoutNotify(modeIndex);
            if (boardSizeDropdown != null) boardSizeDropdown.SetValueWithoutNotify(sizeIndex);
            if (difficultyDropdown != null) difficultyDropdown.SetValueWithoutNotify(difficultyIndex);
            RestartGame();
        }

        private int GetRandomCampaignSizeIndex()
        {
            int savedLevel = PlayerPrefs.GetInt(CampaignRandomSizeLevelKey, -1);
            int savedIndex = PlayerPrefs.GetInt(CampaignRandomSizeIndexKey, -1);
            if (savedLevel != CampaignLevel || savedIndex < 0 || savedIndex >= BoardSizeOptions.Length)
            {
                savedIndex = Random.Range(0, BoardSizeOptions.Length);
                PlayerPrefs.SetInt(CampaignRandomSizeLevelKey, CampaignLevel);
                PlayerPrefs.SetInt(CampaignRandomSizeIndexKey, savedIndex);
                PlayerPrefs.Save();
            }
            return savedIndex;
        }

        private void AdvanceCampaignAfterWin()
        {
            if (!isCampaignGame || CampaignLevel > activeCampaignLevel) return;
            CampaignLevel = activeCampaignLevel + 1;
            PlayerPrefs.SetInt(CampaignLevelKey, CampaignLevel);
            PlayerPrefs.Save();
        }

        public void PauseGame()
        {
            if (State != GameState.Playing) return;
            if (!audioPaused) { audioPaused = true; FloodAudioSignals.Pause(true); }
            RefreshUI();
        }

        public void ResumeGame()
        {
            if (State == GameState.Playing)
            {
                if (audioPaused) { audioPaused = false; FloodAudioSignals.Pause(false); }
                RefreshUI();
            }
        }

        public bool RefreshContinueAvailability()
        {
            if (failedToRestoreSession) { HasSavedSession = false; availableSession = null; return false; }
            HasSavedSession = FloodFillSessionPersistence3D.TryLoad(out var saved) && IsResumableSession(saved);
            availableSession = HasSavedSession ? saved : null;
            return HasSavedSession;
        }

        public bool HasSavedNumberedLevel(int mode, int selectedDifficulty, int level)
        {
            return RefreshContinueAvailability() && availableSession.numberedLevel == level &&
                availableSession.numberedMode == mode && availableSession.difficulty == selectedDifficulty;
        }

        public bool ContinueSavedGame()
        {
            if (IsLoading && loadingSavedSession) return boardManager != null && boardManager.HasActiveBoard;
            if (!RefreshContinueAvailability() || !TryRestoreSavedSession())
            {
                HasSavedSession = false; availableSession = null;
                return false;
            }
            boardManager.RestoreMenuPreview();
            gameSessionActive = true;
            IsLoading = true; loadingSavedSession = true;
            LoadingError = null; LoadingStatus = "Getting everything ready…";
            boardManager.SetBoardVisible(false);
            RefreshUI();
            if (loadingCoroutine != null) StopCoroutine(loadingCoroutine);
            loadingCoroutine = StartCoroutine(PresentPreparedLevel(solverGenerationId));
            return true;
        }

        public void ReturnToMenu()
        {
            elapsedTimerActive = false;
            if (IsLoading || !string.IsNullOrEmpty(LoadingError))
            {
                CancelPendingSolver(); solverGenerationId++;
                IsLoading = false; LoadingError = null;
                bool wasSavedSession = loadingSavedSession;
                RefreshContinueAvailability();
                gameSessionActive = wasSavedSession && HasSavedSession;
                loadingSavedSession = false;
                if (!wasSavedSession) boardManager.ClearBoard();
                boardManager.SetBoardVisible(false);
                UIChanged?.Invoke();
                return;
            }
            boardManager.SetInputEnabled(false);
            boardManager.SetBoardVisible(false);
            bool completed = State == GameState.Won || State == GameState.Lost;
            if (completed)
            {
                FloodFillSessionPersistence3D.Delete();
                HasSavedSession = false;
                gameSessionActive = false;
                isCampaignGame = false;
            }
            else
            {
                if (!SaveSession()) RefreshContinueAvailability();
            }
            UIChanged?.Invoke();
        }

        public void SelectColor(int colorIndex)
        {
            if (PowerUps != null && PowerUps.HandlePalette(colorIndex)) return;
            if (Goals.FloodSecondaryGoalsUI3D.ConsumedPointerThisFrame || !CanInteract ||
                colorIndex < 0 || colors == null || colorIndex >= colors.Length ||
                colorIndex == SelectedColorIndex)
            {
                return;
            }

            SelectedColorIndex = colorIndex;
            SecondaryGoals?.SelectColor(colorIndex);
            FloodAudioSignals.SelectColor();
            RefreshUI();
            SaveSession();
            Debug.Log($"Selected 3D paint color: {colorIndex}. Click a cube to recolor it.", this);
        }

        private void HandleVoxelClicked(VoxelCell3D voxel)
        {
            if (!CanInteract) return;
            if (PowerUps != null && PowerUps.HandleCube(voxel)) return;
            if (SelectedColorIndex < 0) return;

            if (!boardManager.RecolorConnectedRegion(voxel, SelectedColorIndex))
            {
                return;
            }

            MoveCount++;
            SecondaryGoals?.Move(MoveCount, boardManager.LastNewlyCapturedCount, boardManager.CapturedVoxelCount);
            AwardScore(PowerUpScoreCount(boardManager.LastNewlyCapturedCount));
            RefreshUI();
            if (boardManager.IsFullyCaptured)
            {
                ScheduleResult(GameState.Won);
            }
            else if (MoveCount >= currentMoveBudget)
            {
                ScheduleResult(GameState.Lost);
            }
            else
            {
                SaveSession();
            }
        }

        public void RestartGame()
        {
            ResetPowerUpAttempt();
            elapsedTimerActive = false; ElapsedSeconds = 0;
            ActiveLevelDefinition = null; SecondaryGoals = null; preparedGoalTrace = null;
            HasSavedSession = false; availableSession = null; failedToRestoreSession = false;
            CancelPendingSolver();
            solverGenerationId++;
            IsLoading = true;
            loadingSavedSession = false;
            LoadingError = null;
            LoadingStatus = "Building your cube…";
            State = GameState.Calculating;
            audioPaused = false;
            FloodAudioSignals.ResetLevel();
            boardManager.RestoreMenuPreview();
            boardManager.SetBoardVisible(false);
            if (resultCoroutine != null) { StopCoroutine(resultCoroutine); resultCoroutine = null; }
            MoveCount = 0;
            Score = 0;
            SelectedColorIndex = -1;
            LastSolverResult = null;
            CurrentMoveBudgetProfile = default;
            currentMoveBudget = 1;
            if (NumberedLevel == 0) { SyncBoardModeFromDropdown(); SyncBoardSizeFromDropdown(); }
            boardManager.NumberedLevelSeed = NumberedLevel > 0 ? (int?)FloodLevelProgression3D.Seed(NumberedMode, (int)difficulty, NumberedLevel) : null;
            boardManager.SetVolumeMode(SelectedBoardMode);
            boardManager.SetDimensions(SelectedBoardSize);
            if (resultPanel != null) resultPanel.SetActive(false);
            RefreshUI();
            loadingCoroutine = StartCoroutine(LoadLevel(solverGenerationId));
        }

        private IEnumerator LoadLevel(int generationId)
        {
            // Let the loading screen render before any board setup or disk work.
            yield return null;
            FloodFillSessionPersistence3D.Delete();
            if (NumberedLevel > 0)
            {
                if (FloodFillSessionPersistence3D.TryLoadLevel(NumberedMode,(int)difficulty,NumberedLevel,out var stored,out var error))
                {
                    if (!RestoreNumberedDefinition(stored)) { FailLoading("The saved level could not be reconstructed."); yield break; }
                    yield return PresentPreparedLevel(generationId); yield break;
                }
                if (error != null) { FailLoading(error); yield break; }
            }
            var watch = Stopwatch.StartNew();
            bool generated = false;
            yield return boardManager.GenerateBoardAsync(colors, value => generated = value);
            if (generationId != solverGenerationId) yield break;
            if (!generated) { FailLoading("Your level could not be prepared. Please try again."); yield break; }
            double framingMs = 0;
            if (boardManager.TryGetWorldBounds(out Bounds bounds))
            {
                var framing = Stopwatch.StartNew();
                orbitCamera.FrameBounds(bounds);
                framingMs = framing.Elapsed.TotalMilliseconds;
            }
            boardManager.LogLastGenerationPerformance(framingMs, watch.Elapsed.TotalMilliseconds);
            LoadingStatus = "Preparing your challenge…";
            RefreshUI();
            yield return null;
            BeginMoveBudgetCalculation(generationId);
            yield return PresentPreparedLevel(generationId);
        }

        private IEnumerator PresentPreparedLevel(int generationId)
        {
            while (generationId == solverGenerationId && State == GameState.Calculating && string.IsNullOrEmpty(LoadingError)) yield return null;
            if (generationId != solverGenerationId || !string.IsNullOrEmpty(LoadingError)) yield break;
            if (!boardManager.HasActiveBoard || !boardManager.TryGetWorldBounds(out _))
            {
                HasSavedSession = false; availableSession = null;
                if (loadingSavedSession) failedToRestoreSession = true;
                FailLoading("The saved board could not be loaded. Return to the menu and select a level.");
                yield break;
            }
            LoadingStatus = "Getting everything ready…";
            RefreshUI();
            boardManager.SetBoardVisible(true);
            Physics.SyncTransforms();
            // Render/warm the completed board behind the opaque loading screen,
            // then retain the requested buffer after preparation is complete.
            yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            if (generationId != solverGenerationId) yield break;
            IsLoading = false;
            loadingCoroutine = null;
            boardManager.PlayStartAnimation();
            elapsedTimerActive = true;
            RefreshUI();
            if (boardManager.IsFullyCaptured) ScheduleResult(GameState.Won);
        }

        private void FailLoading(string message)
        {
            loadingCoroutine = null;
            IsLoading = false;
            LoadingError = message;
            boardManager.SetBoardVisible(false);
            RefreshUI();
        }

        public void Configure(
            VoxelBoardManager3D board,
            OrbitCameraController3D cameraController,
            TMP_Text movesLabel,
            TMP_Text capturedLabel,
            TMP_Text scoreLabel,
            TMP_Dropdown modeDropdown,
            TMP_Dropdown sizeDropdown,
            GameObject endPanel,
            TMP_Text endLabel,
            ColorButton3D[] buttons,
            Color[] palette,
            TMP_Dropdown difficultySelector = null)
        {
            boardManager = board;
            orbitCamera = cameraController;
            movesText = movesLabel;
            capturedText = capturedLabel;
            scoreText = scoreLabel;
            boardModeDropdown = modeDropdown;
            boardSizeDropdown = sizeDropdown;
            difficultyDropdown = difficultySelector;
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

        private void InitializeDifficultyDropdown()
        {
            if (difficultyDropdown == null)
            {
                return;
            }

            difficultyDropdown.ClearOptions();
            difficultyDropdown.AddOptions(new List<string>
            {
                "Easy",
                "Normal",
                "Hard",
                "Perfect"
            });
            difficultyDropdown.SetValueWithoutNotify((int)difficulty);
            difficultyDropdown.RefreshShownValue();
            difficultyDropdown.onValueChanged.RemoveListener(HandleDifficultyDropdownChanged);
            difficultyDropdown.onValueChanged.AddListener(HandleDifficultyDropdownChanged);
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

        private void EnsureDifficultyDropdown()
        {
            difficultyDropdown = EnsureRuntimeDropdown(
                difficultyDropdown,
                "DifficultyDropdown",
                new Vector2(120f, -234f),
                220f);
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
                SaveSession();
            }
        }

        private void HandleBoardSizeDropdownChanged(int optionIndex)
        {
            if (optionIndex >= 0 && optionIndex < BoardSizeOptions.Length)
            {
                SelectedBoardSize = BoardSizeOptions[optionIndex];
                SaveSession();
            }
        }

        private void HandleDifficultyDropdownChanged(int optionIndex)
        {
            if (optionIndex < 0 || optionIndex > (int)FloodFillDifficulty.Perfect)
            {
                return;
            }

            difficulty = (FloodFillDifficulty)optionIndex;
            if (MoveCount == 0 && CurrentMoveBudgetProfile.bestSolutionMoves > 0)
            {
                currentMoveBudget = CurrentMoveBudgetProfile.GetMoves(difficulty);
                RefreshUI();
            }

            SaveSession();
        }

        private void SyncBoardModeFromDropdown()
        {
            if (boardModeDropdown != null && boardModeDropdown.value >= 0 &&
                boardModeDropdown.value < BoardModeOptions.Length)
            {
                SelectedBoardMode = BoardModeOptions[boardModeDropdown.value];
            }
        }

        private void SyncBoardSizeFromDropdown()
        {
            if (boardSizeDropdown != null && boardSizeDropdown.value >= 0 &&
                boardSizeDropdown.value < BoardSizeOptions.Length)
            {
                SelectedBoardSize = BoardSizeOptions[boardSizeDropdown.value];
            }
        }

        private void BeginMoveBudgetCalculation(int generationId)
        {
            SetColorInputEnabled(false);
            if (!boardManager.TryCreateSolverSnapshot(out FloodFillBoardSnapshot3D snapshot))
            {
                ApplyEmergencyMoveBudget(boardManager.TotalVoxelCount);
                return;
            }
            solverCancellation = new CancellationTokenSource();
            CancellationToken token = solverCancellation.Token;
            MoveSolverSettings settingsCopy = CopySolverSettings(solverSettings);
            int goalDifficulty = NumberedLevel > 0 ? (int)difficulty : -1;
            int traceLimit = GoalSettings.validationMoveLimit;
            Task<SolverPreparation> task = Task.Run(() => PrepareSolver(snapshot, settingsCopy, token, goalDifficulty, traceLimit), token);
            solverCoroutine = StartCoroutine(WaitForSolver(task, snapshot.VoxelCount, generationId, token));
        }

        private static SolverPreparation PrepareSolver(FloodFillBoardSnapshot3D snapshot, MoveSolverSettings settings, CancellationToken token, int goalDifficulty, int traceLimit)
        {
            // Rapid restarts must not leave multiple searches competing for CPU/GC.
            SolverWorkerGate.Wait(token);
            try
            {
                if (!FloodFillRegionGraph3D.TryBuild(snapshot, out var graph, token))
                    return new SolverPreparation { Error = new System.InvalidOperationException("Invalid move-solver snapshot.") };
                try
                {
                    // Normal play uses bounded runtime budgets, including development
                    // builds. Editor analysis tools retain their explicit larger budgets.
                    return PrepareGoalTrace(graph, FloodFillMoveSolver3D.Solve(graph, settings, false, token), goalDifficulty, traceLimit, token);
                }
                catch (System.OperationCanceledException) { throw; }
                catch (System.Exception)
                {
                    token.ThrowIfCancellationRequested();
                    return PrepareGoalTrace(graph, FloodFillMoveSolver3D.SolveGuaranteedFallback(graph, settings, token), goalDifficulty, traceLimit, token);
                }
            }
            catch (System.OperationCanceledException) { throw; }
            catch (System.Exception error) { return new SolverPreparation { Error = error }; }
            finally { SolverWorkerGate.Release(); }
        }

        private IEnumerator WaitForSolver(Task<SolverPreparation> task, int voxelCount, int generationId, CancellationToken token)
        {
            while (!task.IsCompleted) yield return null;
            solverCoroutine = null;
            if (token.IsCancellationRequested || generationId != solverGenerationId || task.IsCanceled) yield break;
            if (task.IsFaulted)
            {
                Debug.LogException(task.Exception.GetBaseException(), this);
                ApplyEmergencyMoveBudget(voxelCount);
                yield break;
            }
            if (task.Result.Error != null || task.Result.Result == null || !task.Result.Result.SolutionValidated)
            {
                if (task.Result.Error != null) Debug.LogException(task.Result.Error, this);
                ApplyEmergencyMoveBudget(voxelCount);
                yield break;
            }
            preparedGoalTrace = task.Result.GoalTrace;
            solverWorkerThreadId = task.Result.WorkerThreadId;
            ApplySolverResult(task.Result.Result, generationId, true);
        }

        private void ApplySolverResult(
            FloodFillSolverResult result,
            int generationId,
            bool solvedAsynchronously)
        {
            if (result == null || generationId != solverGenerationId ||
                !result.SolutionValidated)
            {
                return;
            }

            LastSolverResult = result;
            CurrentMoveBudgetProfile = result.BudgetProfile;
            currentMoveBudget = CurrentMoveBudgetProfile.GetMoves(difficulty);
            if (NumberedLevel > 0 && !PublishNumberedDefinition(result)) return;
            State = GameState.Playing;
            RefreshUI();
            SaveSession();
            if (solverSettings.logSolverPerformance)
            {
                LogSolverPerformance(result, solvedAsynchronously);
            }
        }

        private void ApplyEmergencyMoveBudget(int logicalCount)
        {
            if (NumberedLevel > 0) { FailLoading("Level search was inconclusive. No verified level was saved. Please retry."); return; }
            int guaranteedMoves = Mathf.Max(1, logicalCount - 1);
            CurrentMoveBudgetProfile = MoveBudgetCalculator3D.Calculate(
                guaranteedMoves,
                logicalCount,
                false,
                solverSettings);
            currentMoveBudget = CurrentMoveBudgetProfile.GetMoves(difficulty);
            State = GameState.Playing;
            RefreshUI();
            SaveSession();
        }

        private void LogSolverPerformance(FloodFillSolverResult result, bool asynchronous)
        {
            MoveBudgetProfile profile = result.BudgetProfile;
            string exactLabel = result.IsProvenOptimal
                ? result.ExactMoveCount.ToString()
                : "Not proven within budget";
            Debug.Log(
                "[Move Solver]\n" +
                $"Grid: {boardManager.Width}x{boardManager.Height}x{boardManager.Depth}\n" +
                $"Playable voxels: {result.PlayableVoxelCount}\n" +
                $"Compressed regions: {result.RegionCount}\n" +
                $"Distinct colors: {result.DistinctColorCount}\n" +
                $"Algorithm: {result.SearchAlgorithm}\n" +
                $"Graph build: {result.GraphBuildMilliseconds:0.###} ms\n" +
                $"Greedy: {result.GreedyMoveCount} moves / {result.GreedyMilliseconds:0.###} ms\n" +
                $"Beam: {result.BeamMoveCount} moves / {result.BeamMilliseconds:0.###} ms\n" +
                $"Exact: {exactLabel} / {result.ExactMilliseconds:0.###} ms\n" +
                $"Best solution: {result.BestMoveCount}\n" +
                $"Proven optimal: {result.IsProvenOptimal}\n" +
                $"Expanded states: {result.ExploredStates}\n" +
                $"Unique states: {result.UniqueStates}\n" +
                $"Duplicate states skipped: {result.DuplicateStatesSkipped}\n" +
                $"Estimated peak search memory: {result.EstimatedPeakSearchMemoryBytes / (1024d * 1024d):0.##} MiB\n" +
                $"Search termination: {result.SearchTerminationReason}\n" +
                $"Result status: {(result.IsProvenOptimal ? "OPTIMAL" : result.SolutionValidated ? "VALIDATED" : "FAILED")}\n" +
                $"Difficulty: Perfect {profile.perfectMoves}, Hard {profile.hardMoves}, " +
                $"Normal {profile.normalMoves}, Easy {profile.easyMoves}\n" +
                $"Complexity multiplier: {profile.complexityMultiplier:0.##}\n" +
                $"Mode: {(asynchronous ? "Asynchronous" : "Synchronous")}\n" +
                $"Total solver: {result.ElapsedMilliseconds:0.###} ms",
                this);
        }

        private static MoveSolverSettings CopySolverSettings(MoveSolverSettings source)
        {
            return new MoveSolverSettings
            {
                exactSolverRegionThreshold = source.exactSolverRegionThreshold,
                beamWidth = source.beamWidth,
                greedyRuns = source.greedyRuns,
                runtimeExactSearchTimeBudgetMs = source.runtimeExactSearchTimeBudgetMs,
                developmentExactSearchTimeBudgetMs = source.developmentExactSearchTimeBudgetMs,
                exactMaxExpandedStates = source.exactMaxExpandedStates,
                beamSearchTimeBudgetMs = source.beamSearchTimeBudgetMs,
                greedySearchTimeBudgetMs = source.greedySearchTimeBudgetMs,
                developmentGreedySearchTimeBudgetMs = source.developmentGreedySearchTimeBudgetMs,
                developmentBeamSearchTimeBudgetMs = source.developmentBeamSearchTimeBudgetMs,
                maximumSearchMemoryMegabytes = source.maximumSearchMemoryMegabytes,
                lookaheadDepth = source.lookaheadDepth,
                lookaheadBranchLimit = source.lookaheadBranchLimit,
                lookaheadNodeLimit = source.lookaheadNodeLimit,
                logSolverPerformance = source.logSolverPerformance,
                complexityThreshold1 = source.complexityThreshold1,
                complexityThreshold2 = source.complexityThreshold2,
                complexityThreshold3 = source.complexityThreshold3,
                complexityThreshold4 = source.complexityThreshold4,
                complexityMultiplier1 = source.complexityMultiplier1,
                complexityMultiplier2 = source.complexityMultiplier2,
                complexityMultiplier3 = source.complexityMultiplier3,
                complexityMultiplier4 = source.complexityMultiplier4,
                maximumMoveBudget = source.maximumMoveBudget
            };
        }

        private void CancelPendingSolver()
        {
            if (loadingCoroutine != null) { StopCoroutine(loadingCoroutine); loadingCoroutine = null; }
            solverCancellation?.Cancel();
            solverCancellation?.Dispose();
            solverCancellation = null;
            if (solverCoroutine != null)
            {
                StopCoroutine(solverCoroutine);
                solverCoroutine = null;
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
            FloodAudioSignals.Result(state == GameState.Won);
            SetColorInputEnabled(false);
            if (state == GameState.Won)
            {
                if (SecondaryGoals != null && NumberedLevel > 0)
                {
                    try { FloodLevelProgression3D.RecordSecondaryGoals(NumberedMode,(int)difficulty,NumberedLevel,SecondaryGoals.Goals,SecondaryGoals.Complete(true,MoveCount,CurrentPlayerColor,ElapsedSeconds),ElapsedSeconds); }
                    catch (System.Exception error) { Debug.LogWarning("Secondary goal rewards could not be saved: " + error.Message); }
                }
                AdvanceCampaignAfterWin();
                if (NumberedLevel > 0) FloodLevelProgression3D.Record(NumberedMode, (int)difficulty, NumberedLevel, MoveCount, FloodLevelProgression3D.Stars(MoveCount, currentMoveBudget));
            }
            if (state == GameState.Lost) SecondaryGoals?.Complete(false,MoveCount,CurrentPlayerColor,ElapsedSeconds);
            SaveSession();
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
            ShowResultPanel(state);
            SaveSession();

            Debug.Log(
                state == GameState.Won
                    ? $"3D game won in {MoveCount} moves."
                    : $"3D game lost after {MoveCount} moves.",
                this);
        }

        private void RefreshUI()
        {
            if (movesText != null) movesText.text = State == GameState.Calculating
                ? "Moves: Calculating..."
                : $"Moves: {MoveCount} / {currentMoveBudget}";
            int displayedPercentage = MoveCount == 0
                ? 0
                : Mathf.RoundToInt(boardManager.CoveredPercentage);
            if (capturedText != null) capturedText.text = $"Captured: {displayedPercentage}%";
            if (scoreText != null) scoreText.text = $"Score: {Score:N0}";
            SetColorInputEnabled(CanInteract);
            orbitCamera?.SetInputEnabled(!IsLoading && string.IsNullOrEmpty(LoadingError) && !boardManager.IsStartAnimationPlaying && !audioPaused);
            if (difficultyDropdown != null)
            {
                difficultyDropdown.interactable = State == GameState.Playing &&
                    MoveCount == 0 && CurrentMoveBudgetProfile.bestSolutionMoves > 0;
            }
            UIChanged?.Invoke();
        }

        private bool IsResumableSession(FloodFillSessionSaveData3D saved)
        {
            if (saved == null || boardManager == null || orbitCamera == null || colors == null ||
                saved.version != FloodFillSessionSaveData3D.CurrentVersion ||
                saved.gameState != (int)GameState.Playing && saved.gameState != (int)GameState.Calculating ||
                double.IsNaN(saved.elapsedSeconds) || double.IsInfinity(saved.elapsedSeconds) || saved.elapsedSeconds < 0 ||
                saved.moveCount < 0 || saved.score < 0 || saved.currentMoveBudget <= saved.moveCount ||
                saved.selectedColorIndex < -1 || saved.selectedColorIndex >= colors.Length ||
                saved.gameState == (int)GameState.Calculating && saved.moveCount != 0 ||
                !System.Enum.IsDefined(typeof(FloodFillDifficulty),saved.difficulty) ||
                !System.Enum.IsDefined(typeof(VoxelBoardManager3D.VoxelVolumeMode),saved.selectedBoardMode) ||
                System.Array.IndexOf(BoardSizeOptions,saved.selectedBoardSize) < 0 || saved.board == null ||
                saved.board.width != saved.selectedBoardSize || saved.board.height != saved.selectedBoardSize ||
                saved.board.depth != saved.selectedBoardSize || saved.board.volumeMode != saved.selectedBoardMode ||
                saved.numberedLevel < 0 || saved.numberedLevel > FloodLevelProgression3D.LevelsPerCategory)
                return false;
            if (saved.numberedLevel > 0)
            {
                if (saved.isCampaignGame || saved.numberedMode < 0 || saved.numberedMode > 2) return false;
                var expected = saved.numberedMode == 0 ? VoxelBoardManager3D.VoxelVolumeMode.HollowCube :
                    saved.numberedMode == 1 ? VoxelBoardManager3D.VoxelVolumeMode.Procedural : VoxelBoardManager3D.VoxelVolumeMode.Items;
                if (saved.selectedBoardMode != (int)expected) return false;
            }
            return boardManager.CanRestoreUnfinishedState(saved.board,colors);
        }

        private bool TryRestoreSavedSession()
        {
            try { return TryRestoreSavedSessionCore(); }
            catch (System.Exception exception)
            {
                HasSavedSession = false; availableSession = null; gameSessionActive = false; failedToRestoreSession = true;
                Debug.LogWarning("Saved level could not be restored: " + exception.Message,this);
                return false;
            }
        }

        private bool TryRestoreSavedSessionCore()
        {
            if (!FloodFillSessionPersistence3D.TryLoad(out FloodFillSessionSaveData3D saved) ||
                !IsResumableSession(saved) || !TryApplySavedSelections(saved))
                return false;
            boardManager.RestoreMenuPreview();
            if (!boardManager.RestorePersistentState(saved.board, colors))
            {
                failedToRestoreSession = true;
                return false;
            }

            CancelPendingSolver();
            if (resultCoroutine != null) { StopCoroutine(resultCoroutine); resultCoroutine = null; }
            solverGenerationId++;
            if (audioPaused) FloodAudioSignals.Pause(false);
            audioPaused = false; LoadingError = null;
            gameSessionActive = true; HasSavedSession = true; availableSession = saved;
            elapsedTimerActive = false; ElapsedSeconds = saved.elapsedSeconds;
            RestorePowerUpAttempt(saved.powerUpAttempt);
            MoveCount = Mathf.Max(0, saved.moveCount);
            Score = Mathf.Max(0, saved.score);
            currentMoveBudget = Mathf.Max(1, saved.currentMoveBudget);
            CurrentMoveBudgetProfile = saved.moveBudgetProfile;
            LastSolverResult = null;
            SelectedColorIndex = saved.selectedColorIndex >= 0 &&
                saved.selectedColorIndex < colors.Length
                    ? saved.selectedColorIndex
                    : -1;
            State = System.Enum.IsDefined(typeof(GameState), saved.gameState)
                ? (GameState)saved.gameState
                : GameState.Calculating;
            NumberedLevel = saved.numberedLevel >= 1 && saved.numberedLevel <= 100 && saved.numberedMode >= 0 && saved.numberedMode <= 2 ? saved.numberedLevel : 0;
            NumberedMode = saved.numberedMode;
            RestoreSecondaryAttempt(saved);
            isCampaignGame = saved.isCampaignGame;
            activeCampaignLevel = saved.campaignLevel > 0
                ? saved.campaignLevel
                : CampaignLevel;

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            if (!boardManager.HasActiveBoard || boardManager.IsFullyCaptured || !boardManager.TryGetWorldBounds(out Bounds bounds))
            {
                failedToRestoreSession = true; HasSavedSession = false; gameSessionActive = false;
                return false;
            }
            orbitCamera.FrameBounds(bounds);
            orbitCamera.RestorePersistentState(saved.camera);
            RefreshUI();
            if (State == GameState.Calculating) BeginMoveBudgetCalculation(solverGenerationId);

            Debug.Log($"Resumed saved 3D Flood Fill session at move {MoveCount}.", this);
            return true;
        }

        private bool TryApplySavedSelections(FloodFillSessionSaveData3D saved)
        {
            if (saved == null ||
                !System.Enum.IsDefined(typeof(FloodFillDifficulty), saved.difficulty) ||
                !System.Enum.IsDefined(
                    typeof(VoxelBoardManager3D.VoxelVolumeMode),
                    saved.selectedBoardMode) ||
                System.Array.IndexOf(BoardSizeOptions, saved.selectedBoardSize) < 0)
            {
                return false;
            }

            difficulty = (FloodFillDifficulty)saved.difficulty;
            SelectedBoardMode = (VoxelBoardManager3D.VoxelVolumeMode)saved.selectedBoardMode;
            SelectedBoardSize = saved.selectedBoardSize;
            if (boardModeDropdown != null)
            {
                int modeIndex = System.Array.IndexOf(BoardModeOptions, SelectedBoardMode);
                boardModeDropdown.SetValueWithoutNotify(Mathf.Max(0, modeIndex));
                boardModeDropdown.RefreshShownValue();
            }

            if (boardSizeDropdown != null)
            {
                int sizeIndex = System.Array.IndexOf(BoardSizeOptions, SelectedBoardSize);
                boardSizeDropdown.SetValueWithoutNotify(Mathf.Max(0, sizeIndex));
                boardSizeDropdown.RefreshShownValue();
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.SetValueWithoutNotify((int)difficulty);
                difficultyDropdown.RefreshShownValue();
            }

            return true;
        }

        private bool SaveSession()
        {
            if (!gameSessionActive || !string.IsNullOrEmpty(LoadingError) || (IsLoading && State == GameState.Calculating) || boardManager == null || orbitCamera == null || colors == null ||
                !boardManager.TryCapturePersistentState(out VoxelBoardSaveData3D boardState))
            {
                return false;
            }

            var saved = CreatePowerUpCheckpoint(boardState);
            bool written = FloodFillSessionPersistence3D.Save(saved);
            if (written) failedToRestoreSession = false;
            HasSavedSession = written && State == GameState.Playing && !boardManager.IsFullyCaptured && MoveCount < currentMoveBudget;
            availableSession = HasSavedSession ? saved : null;
            return written;
        }

        public FloodFillSessionSaveData3D CapturePowerUpCheckpoint()
        {
            if(boardManager==null||orbitCamera==null||!boardManager.TryCapturePersistentState(out var boardState))return null;
            return CreatePowerUpCheckpoint(boardState);
        }
        private FloodFillSessionSaveData3D CreatePowerUpCheckpoint(VoxelBoardSaveData3D boardState)
        {
            var saved = new FloodFillSessionSaveData3D
            {
                selectedBoardMode = (int)SelectedBoardMode,
                selectedBoardSize = SelectedBoardSize,
                difficulty = (int)difficulty,
                isCampaignGame = isCampaignGame,
                numberedLevel = NumberedLevel,
                numberedMode = NumberedMode,
                campaignLevel = activeCampaignLevel,
                moveCount = MoveCount,
                powerUpRevision = FloodFill.ThreeD.PowerUps.FloodPowerUpInventory3D.Revision,
                powerUpAttempt = CapturePowerUpAttempt(),
                elapsedSeconds = ElapsedSeconds,
                secondaryGoalAttempt = SecondaryGoals?.Attempt,
                score = Score,
                currentMoveBudget = currentMoveBudget,
                selectedColorIndex = SelectedColorIndex,
                gameState = (int)State,
                moveBudgetProfile = CurrentMoveBudgetProfile,
                board = boardState,
                camera = orbitCamera.CapturePersistentState()
            };
            return saved;
        }

        private void ShowResultPanel(GameState state)
        {
            if (resultText != null)
            {
                resultText.text = state == GameState.Won
                    ? $"YOU WIN!\n\nMoves used: {MoveCount}\nScore: {Score:N0}"
                    : $"OUT OF MOVES\n\nMoves used: {MoveCount} / {currentMoveBudget}\nScore: {Score:N0}";
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }
            UIChanged?.Invoke();
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
                colors != null && colors.Length >= 2;
            if (!valid)
            {
                Debug.LogError("Flood Fill 3D GameManager has missing references.", this);
            }

            return valid;
        }

        private void OnValidate()
        {
            resultRevealDelay = Mathf.Max(0f, resultRevealDelay);
            if (solverSettings == null)
            {
                solverSettings = new MoveSolverSettings();
            }
        }

        private void OnDestroy()
        {
            if (boardManager != null)
            {
                boardManager.VoxelClicked -= HandleVoxelClicked;
                boardManager.StartAnimationChanged -= RefreshUI;
            }
            FloodAudioSignals.Unregister(this);
            SaveSession();
            if (boardModeDropdown != null)
            {
                boardModeDropdown.onValueChanged.RemoveListener(HandleBoardModeDropdownChanged);
            }

            if (boardSizeDropdown != null)
            {
                boardSizeDropdown.onValueChanged.RemoveListener(HandleBoardSizeDropdownChanged);
            }

            if (difficultyDropdown != null)
            {
                difficultyDropdown.onValueChanged.RemoveListener(HandleDifficultyDropdownChanged);
            }

            CancelPendingSolver();

            if (boardManager != null)
            {
                boardManager.VoxelClicked -= HandleVoxelClicked;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            elapsedTimerSuspended = paused;
            if (!paused) skipElapsedTick = true;
            if (paused)
            {
                SaveSession();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                SaveSession();
            }
        }

        private void OnApplicationQuit()
        {
            SaveSession();
        }
    }
}
