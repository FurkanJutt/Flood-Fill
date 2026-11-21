using FloodFill.ThreeD.Audio;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    /// <summary>Responsive mobile-first navigation and presentation for the generated 3D demo.</summary>
    public sealed partial class FloodFillMobileUI3D : MonoBehaviour
    {
        private static readonly Color Navy = new Color(0.025f, 0.045f, 0.09f, 1f);
        private static readonly Color Panel = new Color(0.07f, 0.105f, 0.18f, 0.96f);
        private static readonly Color Accent = new Color(0.24f, 0.62f, 0.98f, 1f);
        private static readonly Color Muted = new Color(0.60f, 0.68f, 0.81f, 1f);
        private static readonly Color[] Palette =
        {
            new Color(0.25f, 0.78f, 0.38f), new Color(0.62f, 0.32f, 0.86f),
            new Color(0.98f, 0.82f, 0.20f), new Color(1f, 0.52f, 0.16f),
            new Color(0.20f, 0.48f, 0.95f), new Color(0.30f, 0.82f, 0.80f)
        };
        private const string SetupModeKey = "FloodFill3D.SetupMode";
        private const string SetupSizeKey = "FloodFill3D.SetupSize";
        private const string SetupDifficultyKey = "FloodFill3D.SetupDifficulty";
        private const string SetupFruitKey = "FloodFill3D.SetupFruit";
        private const string ShowCurrentBlockKey = "FloodFill3D.ShowCurrentBlock";
        [SerializeField] private List<Button> modeButtons = new List<Button>();
        [SerializeField] private List<Button> sizeButtons = new List<Button>();
        [SerializeField] private List<Button> difficultyButtons = new List<Button>();
        [SerializeField] private List<Button> colorButtons = new List<Button>();
        private readonly List<RectTransform> menuButtonRects = new List<RectTransform>();
        private readonly List<float> menuButtonPreferredWidths = new List<float>();
        private readonly int[] sizes = { 6, 8, 10, 12, 15, 18, 20 };
        [SerializeField] private FloodFillGameManager3D manager;
        [SerializeField] private Transform canvasRoot;
        [SerializeField] private VoxelCell3D menuVoxelPrefab;
        [Header("Menu Decoration")]
        [SerializeField, Min(0f)] private float decorativeGap = 0.3f;
        [SerializeField] private Transform safeRoot;
        [SerializeField] private GameObject menu, setup, howTo, settings, hud, pause, results, loading;
        [SerializeField] private TMP_Text loadingTitle, loadingStatus;
        [SerializeField] private RectTransform loadingIndicator;
        [SerializeField] private GameObject loadingRetry;
        private bool loadingHudPrepared;
        [SerializeField] private GameObject continueButton;
        [SerializeField] private RectTransform menuButtonsRoot;
        [SerializeField] private GameObject sizeLabel, fruitLabel, fruitDropdownMenu;
        [SerializeField] private Button fruitDropdownButton;
        [SerializeField] private TMP_Text fruitDropdownText;
        [SerializeField] private TMP_Text fruitComingSoon;
        [SerializeField] private Toggle showCurrentBlockToggle;
        [SerializeField] private TMP_Text moves, captured, score, resultTitle, resultDetails, resultActionLabel, gameTitle;
        [SerializeField] private Image progress;
        private int modeIndex, sizeIndex, difficultyIndex = 1, fruitIndex;
        private static readonly string[] FruitNames = { "RANDOM FRUIT", "APPLE", "BANANA", "ORANGE", "PEAR", "STRAWBERRY", "GRAPES", "PINEAPPLE", "LEMON", "MANGO" };
        [SerializeField] private FloodLevelSelection3D levelSelection;
        private bool started;
        private GameObject cubeCluster;
        [SerializeField] private TMP_Text fpsLabel;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float fpsElapsed;
        private int fpsFrames;
#endif

        public void Initialize(FloodFillGameManager3D gameManager, Transform canvas, VoxelCell3D voxelPrefab)
        {
            manager = gameManager;
            canvasRoot = canvas;
            menuVoxelPrefab = voxelPrefab;
            if (Application.isPlaying) BuildUI();
        }

        private void Awake()
        {
            if (canvasRoot == null) canvasRoot = transform;
            BuildUI();
        }

        private bool built;
        public bool IsBuilt => built;

        private void BuildUI()
        {
            if (built || !Application.isPlaying) return;
            if (manager == null) manager = FindAnyObjectByType<FloodFillGameManager3D>();
            if (manager == null || safeRoot == null || menu == null)
            {
                Debug.LogError("Flood Fill UI needs the authored FloodFillCanvas prefab. Install it with Tools > Flood Fill > UI > Install Editable UI in Demo Scene.", this);
                return;
            }
            built = true;
            BindAuthoredControls();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (fpsLabel != null) fpsLabel.transform.parent.gameObject.SetActive(true);
#else
            if (fpsLabel != null) fpsLabel.transform.parent.gameObject.SetActive(false);
#endif
            modeIndex = manager.SelectedBoardMode == VoxelBoardManager3D.VoxelVolumeMode.HollowCube ? 1 : 0;
            sizeIndex = System.Array.IndexOf(sizes, manager.SelectedBoardSize);
            if (sizeIndex < 0) sizeIndex = 0;
            manager.UIChanged += Refresh;
            manager.ManagerReady += RefreshContinue;
            LoadSetupSelections();
            Show(menu);
            Refresh();
        }

        private void Start() { if (!built) BuildUI(); BindAuthoredControls(); RefreshContinue(); }

#if UNITY_EDITOR
        private void BuildScreens()
        {
            menu = Screen("MainMenu");
            Image menuBackground = menu.GetComponent<Image>();
            menuBackground.color = new Color(0f, 0f, 0f, 0.001f);
            menuBackground.raycastTarget = true;
            TMP_Text menuTitle = AddText(menu.transform, "FLOOD FILL <color=#26D9FF>3D</color>", 72, FontStyles.Bold | FontStyles.Italic, Vector2.zero, new Vector2(900, 110));
            RectTransform menuTitleRect = menuTitle.rectTransform;
            menuTitleRect.anchorMin = new Vector2(0f, 1f);
            menuTitleRect.anchorMax = new Vector2(1f, 1f);
            menuTitleRect.pivot = new Vector2(.5f, 1f);
            menuTitleRect.sizeDelta = new Vector2(-48f, 110f);
            menuTitleRect.anchoredPosition = new Vector2(0f, -105f);
            TMP_Text menuDescription = AddText(menu.transform, "A COLORFUL SPATIAL PUZZLE", 28, FontStyles.Normal, Vector2.zero, new Vector2(850, 58), Muted);
            RectTransform menuDescriptionRect = menuDescription.rectTransform;
            menuDescriptionRect.anchorMin = new Vector2(0f, 1f);
            menuDescriptionRect.anchorMax = new Vector2(1f, 1f);
            menuDescriptionRect.pivot = new Vector2(.5f, 1f);
            menuDescriptionRect.sizeDelta = new Vector2(-48f, 58f);
            menuDescriptionRect.anchoredPosition = new Vector2(0f, -190f);

            GameObject menuButtons = new GameObject("MenuButtons", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            menuButtons.transform.SetParent(menu.transform, false);
            menuButtonsRoot = menuButtons.GetComponent<RectTransform>();
            menuButtonsRoot.anchorMin = new Vector2(0f, 0f);
            menuButtonsRoot.anchorMax = new Vector2(1f, 0f);
            menuButtonsRoot.pivot = new Vector2(.5f, 0f);
            menuButtonsRoot.offsetMin = new Vector2(24f, 70f);
            menuButtonsRoot.offsetMax = new Vector2(-24f, 70f);
            VerticalLayoutGroup menuButtonLayout = menuButtons.GetComponent<VerticalLayoutGroup>();
            menuButtonLayout.childAlignment = TextAnchor.UpperCenter;
            menuButtonLayout.spacing = 12f;
            menuButtonLayout.childControlWidth = false;
            menuButtonLayout.childControlHeight = false;
            menuButtonLayout.childForceExpandWidth = false;
            menuButtonLayout.childForceExpandHeight = false;
            ContentSizeFitter menuButtonFitter = menuButtons.GetComponent<ContentSizeFitter>();
            menuButtonFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            menuButtonFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RegisterMenuButton(AddButton(menuButtons.transform, "PLAY", Vector2.zero, new Vector2(700, 94), Accent, PlayConfiguredGame, 36));
            continueButton = AddButton(menuButtons.transform, "CONTINUE", Vector2.zero, new Vector2(700, 82), Panel, Continue, 30).gameObject;
            RegisterMenuButton(continueButton.GetComponent<Button>());
            continueButton.SetActive(false);
            RegisterMenuButton(AddButton(menuButtons.transform, "CUSTOM", Vector2.zero, new Vector2(700, 82), Panel, () => Show(setup), 30));
            RegisterMenuButton(AddButton(menuButtons.transform, "HOW TO PLAY", Vector2.zero, new Vector2(700, 82), Panel, () => Show(howTo), 30));
            RegisterMenuButton(AddButton(menuButtons.transform, "SETTINGS", Vector2.zero, new Vector2(700, 82), Panel, OpenSettings, 30));
            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer)
                RegisterMenuButton(AddButton(menuButtons.transform, "QUIT", Vector2.zero, new Vector2(500, 68), Panel, Application.Quit, 25));


            setup = Screen("GameSetup");
            AddText(setup.transform, "GAME SETUP", 58, FontStyles.Bold, new Vector2(0, 690), new Vector2(850, 90));
            AddText(setup.transform, "GAME MODE", 28, FontStyles.Bold, new Vector2(0, 555), new Vector2(800, 50), Muted);
            AddChoiceRow(setup.transform, modeButtons, new[] { "RANDOM SHAPE", "FULL GRID", "FRUITS" }, new Vector2(0, 475), new Vector2(275, 86), SetMode);
            sizeLabel = AddText(setup.transform, "BOARD SIZE", 28, FontStyles.Bold, new Vector2(0, 335), new Vector2(800, 50), Muted).gameObject;
            AddChoiceRow(setup.transform, sizeButtons, new[] { "6", "8", "10", "12", "15", "18", "20" }, new Vector2(0, 245), new Vector2(130, 78), SetSize);
            fruitLabel = AddText(setup.transform, "FRUIT", 28, FontStyles.Bold, new Vector2(0, 335), new Vector2(800, 50), Muted).gameObject;
            fruitDropdownButton = AddButton(setup.transform, "RANDOM FRUIT  ▼", new Vector2(0, 245), new Vector2(540, 78), Panel, ToggleFruitDropdown, 28);
            fruitDropdownText = fruitDropdownButton.GetComponentInChildren<TMP_Text>();
            BuildFruitDropdown(setup.transform);
            fruitComingSoon = AddText(setup.transform, "COMING SOON", 25, FontStyles.Bold, new Vector2(0, 155), new Vector2(800, 44), Muted).gameObject.GetComponent<TMP_Text>();
            AddText(setup.transform, "DIFFICULTY", 28, FontStyles.Bold, new Vector2(0, 95), new Vector2(800, 50), Muted);
            AddChoiceRow(setup.transform, difficultyButtons, new[] { "EASY", "NORMAL", "HARD", "PERFECT" }, new Vector2(0, 5), new Vector2(190, 82), SetDifficulty);
            AddButton(setup.transform, "START GAME", new Vector2(0, -170), new Vector2(700, 110), Accent, StartGame, 40);
            AddButton(setup.transform, "BACK", new Vector2(0, -310), new Vector2(500, 82), Panel, () => Show(menu), 30);

            hud = Screen("GameplayHUD");
            hud.GetComponent<Image>().color = Color.clear;
            hud.GetComponent<Image>().raycastTarget = false;
            AddButton(hud.transform, "HOME", new Vector2(-390, 820), new Vector2(155, 72), Panel, OpenPause, 26);
            gameTitle = AddText(hud.transform, "FLOOD FILL 3D", 28, FontStyles.Bold, new Vector2(0, 820), new Vector2(450, 62));
            AddButton(hud.transform, "PAUSE", new Vector2(390, 820), new Vector2(155, 72), Panel, OpenPause, 26);
            BuildStat(hud.transform, "MOVES", new Vector2(-285, 670), out moves);
            BuildStat(hud.transform, "CAPTURED", new Vector2(0, 670), out captured);
            BuildStat(hud.transform, "SCORE", new Vector2(285, 670), out score);
            GameObject barBack = MakeImage("ProgressTrack", hud.transform, new Vector2(0, 570), new Vector2(790, 18), new Color(.16f, .22f, .34f, 1));
            GameObject fill = MakeImage("ProgressFill", barBack.transform, Vector2.zero, new Vector2(790, 18), Accent);
            RectTransform fr = fill.GetComponent<RectTransform>(); fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, .5f); fr.anchoredPosition = Vector2.zero;
            progress = fill.GetComponent<Image>();
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Horizontal;
            AddText(hud.transform, "Choose a color to expand your connected region.", 26, FontStyles.Normal, new Vector2(0, -680), new Vector2(900, 90), Muted, true);
            BuildPalette(hud.transform);
            SkinDecoration("TouchHint", hud.transform, "Touch", new Vector2(0, -570), new Vector2(64, 64));

            howTo = ModalScreen("HowToPlay");
            AddText(howTo.transform, "HOW TO PLAY", 52, FontStyles.Bold, new Vector2(0, 510), new Vector2(820, 80));
            AddText(howTo.transform, "1. Select one of the six available colors.\n\n2. Click a cube to expand your connected region.\n\n3. Adjacent cubes of that color join your region.\n\n4. Capture the whole object before moves run out.\n\n5. Finish in as few moves as you can.", 29, FontStyles.Normal, new Vector2(0, 80), new Vector2(760, 760), Color.white, true);
            AddButton(howTo.transform, "BACK", new Vector2(0, -500), new Vector2(540, 88), Accent, () => Show(menu), 32);

            settings = ModalScreen("Settings");
            AddText(settings.transform, "SETTINGS", 54, FontStyles.Bold, new Vector2(0, 480), new Vector2(800, 90));
            AddText(settings.transform, "Camera orbit, pan and zoom are enabled during play.", 30, FontStyles.Normal, new Vector2(0, 375), new Vector2(740, 90), Muted, true);
            BuildCurrentBlockToggle(settings.transform);
            BuildAudioSettings(settings.transform);
            AddButton(settings.transform, "BACK", new Vector2(0, -470), new Vector2(540, 88), Accent, () => Show(menu), 32);

            pause = ModalScreen("PauseMenu");
            pause.GetComponent<Image>().color = new Color(.015f, .025f, .055f, .72f);
            AddText(pause.transform, "PAUSED", 58, FontStyles.Bold, new Vector2(0, 315), new Vector2(760, 90));
            AddButton(pause.transform, "RESUME", new Vector2(0, 150), new Vector2(580, 92), Accent, Resume, 34);
            AddButton(pause.transform, "RESTART", new Vector2(0, 30), new Vector2(580, 92), Panel, Restart, 34);
            AddButton(pause.transform, "MAIN MENU", new Vector2(0, -90), new Vector2(580, 92), Panel, BackToMenu, 34);

            results = ModalScreen("GameResults");
            results.GetComponent<Image>().color = new Color(.015f, .025f, .055f, .74f);
            resultTitle = AddText(results.transform, "YOU WIN!", 58, FontStyles.Bold, new Vector2(0, 330), new Vector2(800, 90));
            resultDetails = AddText(results.transform, "", 30, FontStyles.Normal, new Vector2(0, 115), new Vector2(760, 240), Color.white, true);
            Button resultAction = AddButton(results.transform, "PLAY AGAIN", new Vector2(0, -90), new Vector2(580, 92), Accent, OnResultAction, 34).GetComponent<Button>();
            resultActionLabel = resultAction.GetComponentInChildren<TMP_Text>();
            AddButton(results.transform, "MAIN MENU", new Vector2(0, -210), new Vector2(580, 92), Panel, BackToMenu, 34);
            loading = Screen("LoadingScreen");
            loadingTitle = AddText(loading.transform, "LOADING", 54, FontStyles.Bold, new Vector2(0, 170), new Vector2(850, 90));
            loadingStatus = AddText(loading.transform, "Preparing your level…", 30, FontStyles.Normal, new Vector2(0, 65), new Vector2(800, 100), Muted, true);
            MakeImage("LoadingTrack", loading.transform, new Vector2(0, -45), new Vector2(480, 10), Panel).GetComponent<Image>().raycastTarget = false;
            loadingIndicator = MakeImage("LoadingIndicator", loading.transform, new Vector2(-180, -45), new Vector2(120, 10), Accent).GetComponent<RectTransform>();
            loadingIndicator.GetComponent<Image>().raycastTarget = false;
            loadingRetry = AddButton(loading.transform, "TRY AGAIN", new Vector2(0, -170), new Vector2(580, 86), Accent, Restart, 30).gameObject;
            loadingRetry.SetActive(false);
            AddButton(loading.transform, "BACK", new Vector2(0, -290), new Vector2(500, 80), Panel, BackToMenu, 28);
            victoryCrown = MakeRect("VictoryDecoration", results.transform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, Vector2.zero);
            SkinDecoration("Crown", victoryCrown.transform, "Crown", new Vector2(0, 460), new Vector2(130, 95));
            SkinDecoration("Ribbon", results.transform, "Ribbon", new Vector2(0, 330), new Vector2(760, 140));
            resultTitle.transform.SetAsLastSibling();
            HideAll();
        }
#endif

#if UNITY_EDITOR
        private void BuildPalette(Transform parent)
        {
            GameObject dock = MakeRect("ColorDock", parent, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 150), new Vector2(850, 150));
            Image dockImage = dock.AddComponent<Image>();
            SkinImage(dockImage, "Dock"); dockImage.raycastTarget = false;
            HorizontalLayoutGroup row = dock.AddComponent<HorizontalLayoutGroup>(); row.childAlignment = TextAnchor.MiddleCenter; row.spacing = 20; row.childControlWidth = false; row.childControlHeight = false; row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            for (int i = 0; i < Palette.Length; i++)
            {
                int index = i; GameObject item = new GameObject("Color_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); item.transform.SetParent(dock.transform, false); item.GetComponent<RectTransform>().sizeDelta = new Vector2(112, 112); item.GetComponent<Image>().color = Palette[i];
                SkinImage(item.GetComponent<Image>(), "Color" + i, false);
                Button b = item.GetComponent<Button>(); b.onClick.AddListener(() => manager.SelectColor(index)); colorButtons.Add(b);
            }
        }
#endif

#if UNITY_EDITOR
        private void BuildFruitDropdown(Transform parent)
        {
            fruitDropdownMenu = MakeImage("FruitDropdownMenu", parent, new Vector2(0, -140), new Vector2(620, 590), Navy);
            for (int i = 0; i < FruitNames.Length; i++)
            {
                int index = i;
                AddButton(fruitDropdownMenu.transform, FruitNames[i], new Vector2(0, 260 - i * 56), new Vector2(570, 50), Panel, () => SetFruit(index), 23);
            }
            fruitDropdownMenu.SetActive(false);
            RefreshFruitSelection();
        }
#endif

        private void ToggleFruitDropdown()
        {
            fruitDropdownMenu.SetActive(!fruitDropdownMenu.activeSelf);
            FloodAudioSignals.Popup(fruitDropdownMenu.activeSelf);
            if (fruitDropdownMenu.activeSelf) fruitDropdownMenu.transform.SetAsLastSibling();
        }

        private void SetFruit(int value)
        {
            fruitIndex = Mathf.Clamp(value, 0, FruitNames.Length - 1);
            PlayerPrefs.SetInt(SetupFruitKey, fruitIndex);
            PlayerPrefs.Save();
            RefreshFruitSelection();
            if (fruitDropdownMenu.activeSelf) FloodAudioSignals.Popup(false);
            fruitDropdownMenu.SetActive(false);
        }

        private void RefreshFruitSelection()
        {
            if (fruitDropdownText != null) fruitDropdownText.text = FruitNames[Mathf.Clamp(fruitIndex, 0, FruitNames.Length - 1)] + "  ▼";
        }

        private void UpdateModeSections()
        {
            bool fruitsMode = modeIndex == 2;
            if (sizeLabel != null) sizeLabel.SetActive(!fruitsMode);
            for (int i = 0; i < sizeButtons.Count; i++) sizeButtons[i].gameObject.SetActive(!fruitsMode);
            if (fruitLabel != null) fruitLabel.SetActive(fruitsMode);
            if (fruitDropdownButton != null) fruitDropdownButton.gameObject.SetActive(fruitsMode);
            if (fruitComingSoon != null) fruitComingSoon.gameObject.SetActive(fruitsMode);
            if (!fruitsMode && fruitDropdownMenu != null) fruitDropdownMenu.SetActive(false);
        }

#if UNITY_EDITOR
        private void BuildCurrentBlockToggle(Transform parent)
        {
            if (showCurrentBlockToggle != null)
            {
                RectTransform existingRow = showCurrentBlockToggle.transform.parent as RectTransform;
                if (existingRow != null && existingRow.GetComponent<HorizontalLayoutGroup>() == null)
                {
                    existingRow.anchoredPosition = new Vector2(0, 270);
                    existingRow.gameObject.SetActive(true);
                    existingRow.SetAsLastSibling();
                    return;
                }
                // Repair an older generated layout after a script reload without retaining
                // duplicate controls or changing the saved current-block preference.
                showCurrentBlockToggle.onValueChanged.RemoveListener(SetShowCurrentBlock);
                if (existingRow != null) { existingRow.gameObject.SetActive(false); Destroy(existingRow.gameObject); }
            }
            showCurrentBlockToggle = AudioToggle(parent, "SHOW CURRENT BLOCK", 270);
            showCurrentBlockToggle.name = "ShowCurrentBlockToggle";
            showCurrentBlockToggle.transform.parent.name = "ShowCurrentBlockOption";
            showCurrentBlockToggle.onValueChanged.AddListener(SetShowCurrentBlock);
            RefreshShowCurrentBlockToggle();
        }
#endif

        private void RefreshShowCurrentBlockToggle()
        {
            if (showCurrentBlockToggle == null) return;
            showCurrentBlockToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(ShowCurrentBlockKey, 0) != 0);
            SkinSwitch(showCurrentBlockToggle);
        }

        private void SetShowCurrentBlock(bool enabled)
        {
            PlayerPrefs.SetInt(ShowCurrentBlockKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            if (menu != null && menu.activeSelf) UpdateMenuCenterpiece(false);
        }

        private void BuildCubeCluster(Transform parent)
        {
            cubeCluster = new GameObject("DecorativeCubeCluster");
            const float decorativeVoxelSize = 0.68f;
            const float decorativeClusterScale = 0.4f;
            float decorativeVoxelSpacing = decorativeVoxelSize +
                Mathf.Max(0f, decorativeGap) / decorativeClusterScale;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (!ShouldCreateDecorativeVoxel(x, y, z) || menuVoxelPrefab == null) continue;

                        int colorIndex = Mathf.Abs(x * 3 + y * 5 + z * 7) % Palette.Length;
                        VoxelCell3D cube = Instantiate(menuVoxelPrefab, cubeCluster.transform);
                        cube.name = "MenuCube";
                        cube.transform.localPosition = new Vector3(x, y, z) * decorativeVoxelSpacing;
                        cube.transform.localScale = Vector3.one * decorativeVoxelSize;
                        cube.SetColor(colorIndex, Palette[colorIndex]);
                        cube.SetShadowCasting(false);
                        Collider cubeCollider = cube.GetComponent<Collider>();
                        if (cubeCollider != null) cubeCollider.enabled = false;
                    }
                }
            }
            Camera menuCamera = Camera.main;
            if (menuCamera != null)
            {
                cubeCluster.transform.SetParent(menuCamera.transform, false);
                cubeCluster.transform.localPosition = new Vector3(0f, 0.65f, 8f);
                cubeCluster.transform.localRotation = Quaternion.identity;
                cubeCluster.transform.localScale = Vector3.one * decorativeClusterScale;
            }
            else
            {
                cubeCluster.transform.position = Vector3.zero;
            }
            cubeCluster.AddComponent<DecorativeRotation3D>();
        }

        private static bool ShouldCreateDecorativeVoxel(int x, int y, int z)
        {
            // Keep a compact, cube-like silhouette with a few open cells for depth.
            if (x == 0 && y == 0 && z == 0) return false;
            return Mathf.Abs(x + y + z) != 3 &&
                   !(x == -1 && y == 1 && z == 0) &&
                   !(x == 1 && y == -1 && z == 0) &&
                   !(x == 0 && y == 1 && z == 1) &&
                   !(x == 0 && y == -1 && z == -1);
        }

#if UNITY_EDITOR
        private void BuildStat(Transform parent, string label, Vector2 position, out TMP_Text value)
        {
            GameObject card = MakeImage(label + "Card", parent, position, new Vector2(265, 132), Panel);
            SkinImage(card.GetComponent<Image>(), "Stat");
            card.GetComponent<Image>().raycastTarget = false;
            AddText(card.transform, label, 22, FontStyles.Bold, new Vector2(0, 30), new Vector2(245, 36), Muted);
            value = AddText(card.transform, "0", 34, FontStyles.Bold, new Vector2(0, -18), new Vector2(245, 54));
        }
#endif

#if UNITY_EDITOR
        private void RegisterMenuButton(Button button)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            menuButtonRects.Add(rect);
            menuButtonPreferredWidths.Add(rect.sizeDelta.x);
        }
#endif

        private void LateUpdate()
        {

            if (loading != null && loading.activeSelf && manager.IsLoading)
                loadingIndicator.anchoredPosition = new Vector2(Mathf.PingPong(Time.unscaledTime * 240f, 360f) - 180f, -45f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (fpsLabel == null) return;
            fpsElapsed += Time.unscaledDeltaTime;
            fpsFrames++;
            if (fpsElapsed >= .25f)
            {
                fpsLabel.SetText("FPS: {0}", Mathf.Round(fpsFrames / fpsElapsed));
                fpsElapsed = 0f;
                fpsFrames = 0;
            }
#endif
        }

#if UNITY_EDITOR
        private void BuildFpsCounter()
        {
            GameObject counter = MakeImage("Development FPS", safeRoot, new Vector2(20, -16), new Vector2(200, 52), new Color(0, 0, 0, .75f));
            RectTransform rect = counter.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            counter.GetComponent<Image>().raycastTarget = false;
            // Keep the counter above every screen and isolate its text rebuilds.
            Canvas overlay = counter.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 1000;
            fpsLabel = AddText(counter.transform, "FPS: --", 28, FontStyles.Bold, Vector2.zero, new Vector2(176, 52));
            fpsLabel.alignment = TextAlignmentOptions.MidlineLeft;
        }
#endif

#if UNITY_EDITOR
        private GameObject Screen(string name)
        {
            GameObject screen = MakeImage(name, safeRoot, Vector2.zero, Vector2.zero, Navy);
            Stretch(screen.GetComponent<RectTransform>()); screen.GetComponent<Image>().raycastTarget = true;
            return screen;
        }
#endif

#if UNITY_EDITOR
        private GameObject ModalScreen(string name)
        {
            GameObject screen = Screen(name); screen.GetComponent<Image>().color = new Color(.015f, .025f, .055f, .96f);
            GameObject card = MakeImage("Card", screen.transform, Vector2.zero, new Vector2(900, 1200), Panel);
            SkinImage(card.GetComponent<Image>(), name == "PauseMenu" ? "PausePanel" : name == "GameResults" ? "ResultPanel" : "SettingsPanel");
            card.GetComponent<Image>().raycastTarget = false;
            return screen;
        }
#endif

#if UNITY_EDITOR
        private void AddChoiceRow(Transform parent, List<Button> collection, string[] labels, Vector2 center, Vector2 size, System.Action<int> onChoose)
        {
            float gap = 14f, total = labels.Length * size.x + (labels.Length - 1) * gap;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i; float x = center.x - total * .5f + size.x * .5f + i * (size.x + gap);
                collection.Add(AddButton(parent, labels[i], new Vector2(x, center.y), size, Panel, () => onChoose(index), 25));
            }
        }
#endif

        private void SetMode(int value) { modeIndex = value; SaveSetupSelections(); RefreshChoices(); }
        private void SetSize(int value) { sizeIndex = value; SaveSetupSelections(); RefreshChoices(); }
        private void SetDifficulty(int value) { difficultyIndex = value; SaveSetupSelections(); RefreshChoices(); }

        private void LoadSetupSelections()
        {
            sizeIndex = Mathf.Clamp(PlayerPrefs.GetInt(SetupSizeKey, sizeIndex), 0, sizes.Length - 1);
            difficultyIndex = Mathf.Clamp(PlayerPrefs.GetInt(SetupDifficultyKey, difficultyIndex), 0, 3);
            modeIndex = Mathf.Clamp(PlayerPrefs.GetInt(SetupModeKey, modeIndex), 0, 2);
            fruitIndex = Mathf.Clamp(PlayerPrefs.GetInt(SetupFruitKey, fruitIndex), 0, FruitNames.Length - 1);
            RefreshFruitSelection();
            RefreshChoices();
        }

        private void SaveSetupSelections()
        {
            PlayerPrefs.SetInt(SetupModeKey, modeIndex);
            PlayerPrefs.SetInt(SetupSizeKey, sizeIndex);
            PlayerPrefs.SetInt(SetupDifficultyKey, difficultyIndex);
            PlayerPrefs.SetInt(SetupFruitKey, fruitIndex);
            PlayerPrefs.Save();
        }

        private void RefreshChoices()
        {
            MarkChoices(modeButtons, modeIndex); MarkChoices(sizeButtons, sizeIndex); MarkChoices(difficultyButtons, difficultyIndex);
            UpdateModeSections();
        }
        private void MarkChoices(List<Button> buttons, int selected)
        {
            for (int i = 0; i < buttons.Count; i++) SkinChoice(buttons[i], i == selected);
        }

        private void StartGame()
        {
            SaveSetupSelections();
            if (modeIndex == 2)
            {
                fruitComingSoon.text = "COMING SOON";
                fruitComingSoon.transform.DOPunchScale(Vector3.one * .08f, .3f, 5);
                return;
            }
            started = true; manager.StartConfiguredGame(modeIndex, sizeIndex, difficultyIndex); Show(hud);
        }
        private void PlayConfiguredGame()
        {
            OpenLevelSelection();
        }
        public void OpenLevelSelection()
        {
            if (levelSelection == null) { Debug.LogError("Level selection prefab is not installed."); return; }
            manager.ReturnToMenu(); started = false;
            Show(levelSelection.gameObject);
            if (manager.NumberedLevel > 0) levelSelection.Open(manager.NumberedMode, (int)manager.SelectedDifficulty);
        }
        public void LevelSelectionHome() { started = false; Show(menu); }
        public bool WouldRestartSavedLevel(int mode, int difficulty, int level)
        {
            return manager != null && manager.HasSavedSession && manager.NumberedLevel == level &&
                manager.NumberedMode == mode && (int)manager.SelectedDifficulty == difficulty;
        }
        public void StartSelectedLevel(int mode, int difficulty, int level)
        {
            started = true;
            if (!manager.StartNumberedLevel(mode,difficulty,level)) { started = false; return; }
            Show(hud);
        }

        private void Continue()
        {
            started = true;
            manager.ContinueSavedGame();
            Show(manager.State == FloodFillGameManager3D.GameState.Won || manager.State == FloodFillGameManager3D.GameState.Lost ? results : hud);
            Refresh();
        }
        private void RefreshContinue()
        {
            if (manager == null) return;
            if (continueButton != null) continueButton.SetActive(manager.HasSavedSession);

            if (!PlayerPrefs.HasKey(SetupModeKey))
            {
                modeIndex = manager.SelectedBoardMode == VoxelBoardManager3D.VoxelVolumeMode.HollowCube ? 1 : 0;
                sizeIndex = System.Array.IndexOf(sizes, manager.SelectedBoardSize);
                if (sizeIndex < 0) sizeIndex = 0;
                difficultyIndex = (int)manager.SelectedDifficulty;
                RefreshChoices();
            }
            if (menu != null && menu.activeSelf) UpdateMenuCenterpiece(false);
        }
        private void OpenPause() { if (manager.State == FloodFillGameManager3D.GameState.Playing) { manager.PauseGame(); Show(pause); } }
        private void OpenSettings()
        {
            RefreshShowCurrentBlockToggle();
            RefreshAudioControls();
            Show(settings);
        }
        private void Resume() { manager.ResumeGame(); Show(hud); }
        private void Restart()
        {
            started = true;
            manager.RestartCurrentGame();
            Show(hud);
        }
        private void OnResultAction()
        {
            if (manager.State == FloodFillGameManager3D.GameState.Won)
            {
                started = true;
                if (manager.NumberedLevel > 0)
                {
                    if (manager.NumberedLevel == 100) { OpenLevelSelection(); return; }
                    manager.StartNumberedLevel(manager.NumberedMode, (int)manager.SelectedDifficulty, manager.NumberedLevel + 1);
                }
                else manager.StartCampaignGame((int)manager.SelectedDifficulty);
                Show(hud);
                return;
            }
            Restart();
        }
        private void BackToMenu() { manager.ReturnToMenu(); started = false; Show(menu); }

        private void Refresh()
        {
            if (!started || moves == null) return;
            if (manager.IsLoading || !string.IsNullOrEmpty(manager.LoadingError))
            {
                if (!loading.activeSelf) Show(loading);
                loadingTitle.text = manager.IsLoading ? "LOADING" : "PLEASE TRY AGAIN";
                loadingStatus.text = manager.IsLoading ? manager.LoadingStatus : manager.LoadingError;
                loadingRetry.SetActive(!manager.IsLoading);
                loadingIndicator.gameObject.SetActive(manager.IsLoading);
            }
            else if (loading.activeSelf && manager.State == FloodFillGameManager3D.GameState.Playing) Show(hud);
            if (gameTitle != null)
                gameTitle.text = manager.NumberedLevel > 0 ? "LEVEL " + manager.NumberedLevel : manager.IsCampaignGame
                    ? "LEVEL " + manager.ActiveCampaignLevel
                    : "FLOOD FILL 3D";
            moves.text = manager.State == FloodFillGameManager3D.GameState.Calculating ? "…" : manager.MoveCount + " / " + manager.CurrentMoveBudget;
            captured.text = manager.BoardManager.DisplayedCapturedPercentage + "%";
            score.text = manager.Score.ToString("N0");
            progress.DOKill(); progress.DOFillAmount(manager.CapturedPercentage / 100f, .25f);
            for (int i = 0; i < colorButtons.Count; i++)
            {
                colorButtons[i].interactable = manager.CanInteract && i != manager.SelectedColorIndex;
                colorButtons[i].transform.DOScale(i == manager.SelectedColorIndex ? 1.12f : 1f, .16f).SetEase(Ease.OutSine);
            }
            if (manager.IsLoading || !string.IsNullOrEmpty(manager.LoadingError))
            {
                if (!loadingHudPrepared && manager.State == FloodFillGameManager3D.GameState.Playing)
                {
                    foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true, true);
                    loadingHudPrepared = true;
                }
                return;
            }
            if (manager.State == FloodFillGameManager3D.GameState.Won || manager.State == FloodFillGameManager3D.GameState.Lost)
            {
                bool won = manager.State == FloodFillGameManager3D.GameState.Won;
                bool categoryComplete = won && manager.NumberedLevel == FloodLevelProgression3D.LevelsPerCategory;
                // Inactive controls are removed from the ResultActions vertical layout.
                foreach (var action in results.GetComponentsInChildren<FloodFillUIAction3D>(true))
                {
                    if (action.action != FloodFillUIAction.Restart && action.action != FloodFillUIAction.Levels) continue;
                    bool visible = action.action == FloodFillUIAction.Restart ? won : !categoryComplete;
                    var slot = action.GetComponentInParent<FloodFillResultButtonSlot3D>(true);
                    if (slot != null) slot.SetVisible(visible);
                    else action.gameObject.SetActive(visible);
                }
                if (victoryCrown != null) victoryCrown.SetActive(won);
                resultTitle.text = won ? "YOU WIN!" : "OUT OF MOVES";
                if (resultActionLabel != null)
                {
                    resultActionLabel.text = won ? (categoryComplete ? "LEVELS" : "NEXT LEVEL") : "PLAY AGAIN";
                    Transform icon = resultActionLabel.transform.parent.Find("Icon");
                    if (icon != null) icon.GetComponent<FloodFillUIState3D>()?.Apply(won);
                }
                string levelDetails = manager.NumberedLevel > 0 ? "Level: " + manager.NumberedLevel + "\n" : manager.IsCampaignGame ? "Level: " + manager.ActiveCampaignLevel + "\n" : string.Empty;
                resultDetails.text = levelDetails + "Final score: " + manager.Score.ToString("N0") + "\nMoves used: " + manager.MoveCount + " / " + manager.CurrentMoveBudget + "\nCaptured: " + manager.BoardManager.CapturedVoxelCount + " / " + manager.BoardManager.TotalVoxelCount + " (" +
                    (manager.BoardManager.IsFullyCaptured ? "100" : Mathf.Min(99.99f, manager.CapturedPercentage).ToString("0.##")) + "%)";
                Show(results);
            }
            else if (started && results != null && results.activeSelf && (manager.State == FloodFillGameManager3D.GameState.Calculating || manager.State == FloodFillGameManager3D.GameState.Playing))
            {
                Show(hud);
            }
        }

        private GameObject audioScreen;
        private void Show(GameObject target)
        {
            if (target == hud && (manager.IsLoading || !string.IsNullOrEmpty(manager.LoadingError))) target = loading;
            if (target == loading && loading.activeSelf) return;
            if (target == loading) loadingHudPrepared = false;
            if (settings != null && settings.activeSelf && target != settings) FloodAudioManager.Instance?.SaveSettings();
            if (audioScreen != target)
            {
                bool wasPopup = audioScreen == setup || audioScreen == howTo || audioScreen == settings || audioScreen == pause || audioScreen == results;
                bool isPopup = target == setup || target == howTo || target == settings || target == pause || target == results;
                if (isPopup || wasPopup) FloodAudioSignals.Popup(isPopup);
                audioScreen = target;
            }
            FloodAudioSignals.Menu(target != hud && target != pause && target != results);
            if (target != setup && fruitDropdownMenu != null) fruitDropdownMenu.SetActive(false);
            HideAll(); target.SetActive(true); target.transform.SetAsLastSibling();
            target.transform.localScale = Vector3.one * .96f; target.transform.DOScale(1f, .22f).SetEase(Ease.OutBack);
            if (target == menu)
            {
                RefreshContinue();
            }
            else
            {
                VoxelBoardManager3D board = manager != null ? manager.BoardManager : null;
                if (board != null && board.RestoreMenuPreview()) board.SetBoardVisible(false);
                if (cubeCluster != null) cubeCluster.SetActive(false);
            }
            if (target == hud) { manager.ResumeGame(); }
        }

        private void UpdateMenuCenterpiece(bool rebuildDecorative)
        {
            VoxelBoardManager3D board = manager != null ? manager.BoardManager : null;
            bool showCurrentBlock = PlayerPrefs.GetInt(ShowCurrentBlockKey, 0) != 0;
            bool showingCurrent = showCurrentBlock && board != null && board.ShowMenuPreview(Camera.main);
            if (showingCurrent)
            {
                if (cubeCluster != null) cubeCluster.SetActive(false);
                return;
            }

            if (board != null)
            {
                if (board.RestoreMenuPreview()) board.SetBoardVisible(false);
                board.SetBoardVisible(false);
            }
            if (rebuildDecorative || cubeCluster == null) RebuildDecorativeCubeCluster();
            else cubeCluster.SetActive(true);
        }

        private void RebuildDecorativeCubeCluster()
        {
            if (cubeCluster != null) Destroy(cubeCluster);
            Camera menuCamera = Camera.main;
            if (menuCamera != null)
            {
                for (int i = menuCamera.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = menuCamera.transform.GetChild(i);
                    if (child.name == "DecorativeCubeCluster") Destroy(child.gameObject);
                }
            }
            BuildCubeCluster(menu.transform);
        }
        private void HideAll() { if (levelSelection != null) levelSelection.gameObject.SetActive(false); menu.SetActive(false); setup.SetActive(false); howTo.SetActive(false); settings.SetActive(false); hud.SetActive(false); pause.SetActive(false); results.SetActive(false); if (loading != null) loading.SetActive(false); }

#if UNITY_EDITOR
        private void HideLegacyUI(Transform canvas)
        {
            string[] legacy = { "Title", "MovesText", "CapturedText", "ScoreText", "InstructionsText", "RestartButton", "BoardModeDropdown", "BoardSizeDropdown", "DifficultyDropdown", "ColorControls", "ResultPanel" };
            foreach (string name in legacy) { Transform child = canvas.Find(name); if (child != null) child.gameObject.SetActive(false); }
        }
#endif

#if UNITY_EDITOR
        private TMP_Text AddText(Transform parent, string value, float size, FontStyles style, Vector2 position, Vector2 dimensions, Color? color = null, bool wrap = false)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); go.layer = 5; RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            TMP_Text text = go.GetComponent<TMP_Text>(); text.text = value; text.fontSize = size; text.fontStyle = style; text.color = color ?? Color.white; text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap; text.raycastTarget = false; return text;
        }
#endif

#if UNITY_EDITOR
        private Button AddButton(Transform parent, string label, Vector2 position, Vector2 dimensions, Color color, UnityEngine.Events.UnityAction action, float fontSize)
        {
            GameObject go = MakeImage(label + "Button", parent, position, dimensions, color); Button button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); button.onClick.AddListener(action); button.navigation = new Navigation { mode = Navigation.Mode.None };
            FloodAudioManager.Instance?.BindControl(button, label);
            AddText(go.transform, label, fontSize, FontStyles.Bold, Vector2.zero, dimensions - new Vector2(20, 12)); SkinButton(button, label, color); button.transform.localScale = Vector3.one; button.onClick.AddListener(() => button.transform.DOPunchScale(Vector3.one * .045f, .18f, 6)); return button;
        }
#endif

#if UNITY_EDITOR
        private GameObject MakeImage(string name, Transform parent, Vector2 position, Vector2 dimensions, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); go.layer = 5; RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions; Image image = go.GetComponent<Image>(); image.color = color;
            if (name == "ProgressTrack" || name == "ProgressFill") SkinImage(image, name);
            else if (name == "Track") SkinImage(image, "SettingsTrack");
            else if (name == "Fill") SkinImage(image, "SettingsFill");
            else if (name == "Handle") SkinImage(image, "Handle", false);
            return go;
        }
#endif

#if UNITY_EDITOR
        private GameObject MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
        {
            GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); go.layer = 5; RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.anchoredPosition = position; rect.sizeDelta = dimensions; return go;
        }
#endif

#if UNITY_EDITOR
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
#endif

        private void OnDestroy() { UnbindAudioControls(); if (manager != null) { manager.UIChanged -= Refresh; manager.ManagerReady -= RefreshContinue; } }
    }

}
