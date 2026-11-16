using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FloodFill.ThreeD
{
    /// <summary>Responsive mobile-first navigation and presentation for the generated 3D demo.</summary>
    public sealed class FloodFillMobileUI3D : MonoBehaviour
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
        private readonly List<Button> modeButtons = new List<Button>();
        private readonly List<Button> sizeButtons = new List<Button>();
        private readonly List<Button> difficultyButtons = new List<Button>();
        private readonly List<Button> colorButtons = new List<Button>();
        private readonly List<RectTransform> menuButtonRects = new List<RectTransform>();
        private readonly List<float> menuButtonPreferredWidths = new List<float>();
        private readonly int[] sizes = { 6, 8, 10, 12, 15, 18, 20 };
        [SerializeField] private FloodFillGameManager3D manager;
        [SerializeField] private Transform canvasRoot;
        [SerializeField] private VoxelCell3D menuVoxelPrefab;
        [Header("Menu Decoration")]
        [SerializeField, Min(0f)] private float decorativeGap = 0.3f;
        private Transform safeRoot;
        private GameObject menu, setup, howTo, settings, hud, pause, results;
        private GameObject continueButton;
        private RectTransform menuButtonsRoot;
        private GameObject sizeLabel, fruitLabel, fruitDropdownMenu;
        private Button fruitDropdownButton;
        private TMP_Text fruitDropdownText;
        private TMP_Text fruitComingSoon;
        private Toggle showCurrentBlockToggle;
        private TMP_Text moves, captured, score, resultTitle, resultDetails, gameTitle;
        private Image progress;
        private int modeIndex, sizeIndex, difficultyIndex = 1, fruitIndex;
        private static readonly string[] FruitNames = { "RANDOM FRUIT", "APPLE", "BANANA", "ORANGE", "PEAR", "STRAWBERRY", "GRAPES", "PINEAPPLE", "LEMON", "MANGO" };
        private bool started;
        private GameObject cubeCluster;

        public void Initialize(FloodFillGameManager3D gameManager, Transform canvas, VoxelCell3D voxelPrefab)
        {
            manager = gameManager;
            canvasRoot = canvas;
            menuVoxelPrefab = voxelPrefab;
            if (Application.isPlaying) BuildUI();
        }

        private void Awake()
        {
            if (manager != null && canvasRoot != null) BuildUI();
        }

        private bool built;
        private Vector2 lastMenuLayoutSize;
        public bool IsBuilt => built;

        private void BuildUI()
        {
            if (built) return;
            built = true;
            HideLegacyUI(canvasRoot);
            safeRoot = MakeRect("SafeArea", canvasRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero).transform;
            safeRoot.gameObject.AddComponent<FloodFillSafeArea3D>();
            BuildScreens();
            modeIndex = manager.SelectedBoardMode == VoxelBoardManager3D.VoxelVolumeMode.HollowCube ? 1 : 0;
            sizeIndex = System.Array.IndexOf(sizes, manager.SelectedBoardSize);
            if (sizeIndex < 0) sizeIndex = 0;
            manager.UIChanged += Refresh;
            manager.ManagerReady += RefreshContinue;
            LoadSetupSelections();
            Show(menu);
            Refresh();
        }

        private void Start() => RefreshContinue();

        private void BuildScreens()
        {
            menu = Screen("MainMenu");
            Image menuBackground = menu.GetComponent<Image>();
            menuBackground.color = new Color(0f, 0f, 0f, 0.001f);
            menuBackground.raycastTarget = true;
            TMP_Text menuTitle = AddText(menu.transform, "FLOOD FILL 3D", 72, FontStyles.Bold, Vector2.zero, new Vector2(900, 110));
            RectTransform menuTitleRect = menuTitle.rectTransform;
            menuTitleRect.anchorMin = new Vector2(0f, 1f);
            menuTitleRect.anchorMax = new Vector2(1f, 1f);
            menuTitleRect.pivot = new Vector2(.5f, 1f);
            menuTitleRect.sizeDelta = new Vector2(-48f, 110f);
            menuTitleRect.anchoredPosition = new Vector2(0f, -105f);
            TMP_Text menuDescription = AddText(menu.transform, "A colorful spatial puzzle", 32, FontStyles.Normal, Vector2.zero, new Vector2(850, 58), Muted);
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
            RefreshMenuLayout(true);

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

            howTo = ModalScreen("HowToPlay");
            AddText(howTo.transform, "HOW TO PLAY", 52, FontStyles.Bold, new Vector2(0, 510), new Vector2(820, 80));
            AddText(howTo.transform, "1. Select one of the six available colors.\n\n2. Click a cube to expand your connected region.\n\n3. Adjacent cubes of that color join your region.\n\n4. Capture the whole object before moves run out.\n\n5. Finish in as few moves as you can.", 29, FontStyles.Normal, new Vector2(0, 80), new Vector2(760, 760), Color.white, true);
            AddButton(howTo.transform, "BACK", new Vector2(0, -500), new Vector2(540, 88), Accent, () => Show(menu), 32);

            settings = ModalScreen("Settings");
            AddText(settings.transform, "SETTINGS", 54, FontStyles.Bold, new Vector2(0, 320), new Vector2(800, 90));
            AddText(settings.transform, "Camera orbit, pan and zoom are enabled during play.", 30, FontStyles.Normal, new Vector2(0, 145), new Vector2(740, 150), Muted, true);
            BuildCurrentBlockToggle(settings.transform);
            AddButton(settings.transform, "BACK", new Vector2(0, -260), new Vector2(540, 88), Accent, () => Show(menu), 32);

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
            AddButton(results.transform, "PLAY AGAIN", new Vector2(0, -90), new Vector2(580, 92), Accent, Restart, 34);
            AddButton(results.transform, "MAIN MENU", new Vector2(0, -210), new Vector2(580, 92), Panel, BackToMenu, 34);
            HideAll();
        }

        private void BuildPalette(Transform parent)
        {
            GameObject dock = MakeRect("ColorDock", parent, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 150), new Vector2(850, 150));
            HorizontalLayoutGroup row = dock.AddComponent<HorizontalLayoutGroup>(); row.childAlignment = TextAnchor.MiddleCenter; row.spacing = 20; row.childControlWidth = false; row.childControlHeight = false; row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            for (int i = 0; i < Palette.Length; i++)
            {
                int index = i; GameObject item = new GameObject("Color_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); item.transform.SetParent(dock.transform, false); item.GetComponent<RectTransform>().sizeDelta = new Vector2(112, 112); item.GetComponent<Image>().color = Palette[i];
                Button b = item.GetComponent<Button>(); b.onClick.AddListener(() => manager.SelectColor(index)); colorButtons.Add(b);
            }
        }

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

        private void ToggleFruitDropdown()
        {
            fruitDropdownMenu.SetActive(!fruitDropdownMenu.activeSelf);
            if (fruitDropdownMenu.activeSelf) fruitDropdownMenu.transform.SetAsLastSibling();
        }

        private void SetFruit(int value)
        {
            fruitIndex = Mathf.Clamp(value, 0, FruitNames.Length - 1);
            PlayerPrefs.SetInt(SetupFruitKey, fruitIndex);
            PlayerPrefs.Save();
            RefreshFruitSelection();
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

        private void BuildCurrentBlockToggle(Transform parent)
        {
            GameObject row = MakeRect("ShowCurrentBlockOption", parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 20), new Vector2(700, 90));
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            GameObject toggleObject = new GameObject("ShowCurrentBlockToggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
            toggleObject.transform.SetParent(row.transform, false);
            toggleObject.GetComponent<RectTransform>().sizeDelta = new Vector2(52, 52);
            Image background = toggleObject.GetComponent<Image>();
            background.color = Panel;
            GameObject checkmark = MakeImage("Checkmark", toggleObject.transform, Vector2.zero, new Vector2(32, 32), Accent);
            checkmark.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            checkmark.GetComponent<RectTransform>().anchorMax = Vector2.one;
            checkmark.GetComponent<RectTransform>().offsetMin = new Vector2(8, 8);
            checkmark.GetComponent<RectTransform>().offsetMax = new Vector2(-8, -8);
            showCurrentBlockToggle = toggleObject.GetComponent<Toggle>();
            showCurrentBlockToggle.targetGraphic = background;
            showCurrentBlockToggle.graphic = checkmark.GetComponent<Image>();
            showCurrentBlockToggle.onValueChanged.AddListener(SetShowCurrentBlock);

            TMP_Text label = AddText(row.transform, "SHOW CURRENT BLOCK", 27, FontStyles.Bold, Vector2.zero, new Vector2(520, 64), Color.white, false);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            RefreshShowCurrentBlockToggle();
        }

        private void RefreshShowCurrentBlockToggle()
        {
            if (showCurrentBlockToggle == null) return;
            showCurrentBlockToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(ShowCurrentBlockKey, 0) != 0);
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

        private void BuildStat(Transform parent, string label, Vector2 position, out TMP_Text value)
        {
            GameObject card = MakeImage(label + "Card", parent, position, new Vector2(265, 132), Panel);
            AddText(card.transform, label, 22, FontStyles.Bold, new Vector2(0, 30), new Vector2(245, 36), Muted);
            value = AddText(card.transform, "0", 34, FontStyles.Bold, new Vector2(0, -18), new Vector2(245, 54));
        }

        private void RegisterMenuButton(Button button)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            menuButtonRects.Add(rect);
            menuButtonPreferredWidths.Add(rect.sizeDelta.x);
        }

        private void LateUpdate()
        {
            RefreshMenuLayout(false);
        }

        private void RefreshMenuLayout(bool force)
        {
            if (safeRoot == null || menuButtonsRoot == null) return;
            Vector2 safeSize = safeRoot.GetComponent<RectTransform>().rect.size;
            if (!force && safeSize == lastMenuLayoutSize) return;
            lastMenuLayoutSize = safeSize;

            float buttonWidth = Mathf.Max(0f, safeSize.x - 48f);
            for (int i = 0; i < menuButtonRects.Count; i++)
            {
                RectTransform button = menuButtonRects[i];
                Vector2 size = button.sizeDelta;
                size.x = Mathf.Min(menuButtonPreferredWidths[i], buttonWidth);
                button.sizeDelta = size;
                RectTransform label = button.childCount > 0 ? button.GetChild(0) as RectTransform : null;
                if (label != null)
                {
                    Vector2 labelSize = label.sizeDelta;
                    labelSize.x = Mathf.Max(0f, size.x - 20f);
                    label.sizeDelta = labelSize;
                }
            }

            VerticalLayoutGroup layout = menuButtonsRoot.GetComponent<VerticalLayoutGroup>();
            float desiredHeight = layout != null ? layout.preferredHeight : 0f;
            float availableHeight = Mathf.Max(0f, safeSize.y - 250f);
            float scale = desiredHeight > 0f ? Mathf.Min(1f, availableHeight / desiredHeight) : 1f;
            menuButtonsRoot.localScale = Vector3.one * scale;
        }

        private GameObject Screen(string name)
        {
            GameObject screen = MakeImage(name, safeRoot, Vector2.zero, Vector2.zero, Navy);
            Stretch(screen.GetComponent<RectTransform>()); screen.GetComponent<Image>().raycastTarget = true;
            return screen;
        }

        private GameObject ModalScreen(string name)
        {
            GameObject screen = Screen(name); screen.GetComponent<Image>().color = new Color(.015f, .025f, .055f, .96f);
            GameObject card = MakeImage("Card", screen.transform, Vector2.zero, new Vector2(900, 1200), Panel);
            return screen;
        }

        private void AddChoiceRow(Transform parent, List<Button> collection, string[] labels, Vector2 center, Vector2 size, System.Action<int> onChoose)
        {
            float gap = 14f, total = labels.Length * size.x + (labels.Length - 1) * gap;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i; float x = center.x - total * .5f + size.x * .5f + i * (size.x + gap);
                collection.Add(AddButton(parent, labels[i], new Vector2(x, center.y), size, Panel, () => onChoose(index), 25));
            }
        }

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
        private static void MarkChoices(List<Button> buttons, int selected)
        {
            for (int i = 0; i < buttons.Count; i++) buttons[i].image.color = i == selected ? Accent : Panel;
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
            SaveSetupSelections();
            started = true;
            manager.StartCampaignGame(difficultyIndex);
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
            RefreshMenuLayout(true);
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
            Show(settings);
        }
        private void Resume() { manager.ResumeGame(); Show(hud); }
        private void Restart()
        {
            started = true;
            manager.RestartCurrentGame();
            Show(hud);
        }
        private void BackToMenu() { manager.ReturnToMenu(); started = false; Show(menu); }

        private void Refresh()
        {
            if (!started || moves == null) return;
            if (gameTitle != null)
                gameTitle.text = manager.IsCampaignGame
                    ? "LEVEL " + manager.ActiveCampaignLevel
                    : "FLOOD FILL 3D";
            moves.text = manager.State == FloodFillGameManager3D.GameState.Calculating ? "…" : manager.MoveCount + " / " + manager.CurrentMoveBudget;
            captured.text = Mathf.RoundToInt(manager.CapturedPercentage) + "%";
            score.text = manager.Score.ToString("N0");
            progress.DOKill(); progress.DOFillAmount(manager.CapturedPercentage / 100f, .25f);
            for (int i = 0; i < colorButtons.Count; i++)
            {
                colorButtons[i].interactable = manager.State == FloodFillGameManager3D.GameState.Playing && i != manager.SelectedColorIndex;
                colorButtons[i].transform.DOScale(i == manager.SelectedColorIndex ? 1.12f : 1f, .16f).SetEase(Ease.OutSine);
            }
            if (manager.State == FloodFillGameManager3D.GameState.Won || manager.State == FloodFillGameManager3D.GameState.Lost)
            {
                resultTitle.text = manager.State == FloodFillGameManager3D.GameState.Won ? "YOU WIN!" : "OUT OF MOVES";
                string levelDetails = manager.IsCampaignGame ? "Level: " + manager.ActiveCampaignLevel + "\n" : string.Empty;
                resultDetails.text = levelDetails + "Final score: " + manager.Score.ToString("N0") + "\nMoves used: " + manager.MoveCount + "\nCaptured: " + Mathf.RoundToInt(manager.CapturedPercentage) + "%";
                Show(results);
            }
        }

        private void Show(GameObject target)
        {
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
        private void HideAll() { menu.SetActive(false); setup.SetActive(false); howTo.SetActive(false); settings.SetActive(false); hud.SetActive(false); pause.SetActive(false); results.SetActive(false); }

        private void HideLegacyUI(Transform canvas)
        {
            string[] legacy = { "Title", "MovesText", "CapturedText", "ScoreText", "InstructionsText", "RestartButton", "BoardModeDropdown", "BoardSizeDropdown", "DifficultyDropdown", "ColorControls", "ResultPanel" };
            foreach (string name in legacy) { Transform child = canvas.Find(name); if (child != null) child.gameObject.SetActive(false); }
        }

        private TMP_Text AddText(Transform parent, string value, float size, FontStyles style, Vector2 position, Vector2 dimensions, Color? color = null, bool wrap = false)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            TMP_Text text = go.GetComponent<TMP_Text>(); text.text = value; text.fontSize = size; text.fontStyle = style; text.color = color ?? Color.white; text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap; text.raycastTarget = false; return text;
        }

        private Button AddButton(Transform parent, string label, Vector2 position, Vector2 dimensions, Color color, UnityEngine.Events.UnityAction action, float fontSize)
        {
            GameObject go = MakeImage(label + "Button", parent, position, dimensions, color); Button button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); button.onClick.AddListener(action); button.navigation = new Navigation { mode = Navigation.Mode.None };
            AddText(go.transform, label, fontSize, FontStyles.Bold, Vector2.zero, dimensions - new Vector2(20, 12)); button.transform.localScale = Vector3.one; button.onClick.AddListener(() => button.transform.DOPunchScale(Vector3.one * .045f, .18f, 6)); return button;
        }

        private GameObject MakeImage(string name, Transform parent, Vector2 position, Vector2 dimensions, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions; go.GetComponent<Image>().color = color; return go;
        }

        private GameObject MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
        {
            GameObject go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.anchoredPosition = position; rect.sizeDelta = dimensions; return go;
        }

        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }

        private void OnDestroy() { if (manager != null) { manager.UIChanged -= Refresh; manager.ManagerReady -= RefreshContinue; } }
    }

}
