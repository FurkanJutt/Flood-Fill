using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FloodFill.Editor
{
    public static class FloodFillDemoCreator
    {
        private const string RootFolder = "Assets/FloodFill";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string GeneratedFolder = RootFolder + "/Generated";
        private const string ScenePath = SceneFolder + "/FloodFillDemo.unity";
        private const string CellPrefabPath = PrefabFolder + "/Cell.prefab";
        private const string SquareSpritePath = GeneratedFolder + "/Square.png";
        private const int DefaultActiveColorCount = 6;

        private static readonly Color[] DefaultColors =
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

        [MenuItem("Tools/Flood Fill/Create Demo")]
        public static void CreateDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(GeneratedFolder);

            Sprite squareSprite = CreateOrLoadSquareSprite();
            if (squareSprite == null)
            {
                Debug.LogError("Flood Fill demo creation failed because the generated square sprite could not be loaded.");
                return;
            }

            Cell cellPrefab = CreateCellPrefab(squareSprite);
            if (cellPrefab == null)
            {
                Debug.LogError("Flood Fill demo creation failed because the Cell prefab could not be created.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera mainCamera = CreateCamera();
            BoardManager boardManager = CreateBoard(mainCamera, cellPrefab);
            GameManager gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            Canvas canvas = CreateCanvas();
            CreateEventSystem();

            TMP_Text movesText;
            TMP_Text capturedText;
            CreateHeader(canvas.transform, gameManager, out movesText, out capturedText);

            ColorButton[] colorButtons = CreateColorControls(canvas.transform, gameManager);

            GameObject resultPanel;
            TMP_Text resultText;
            CreateResultPanel(canvas.transform, gameManager, out resultPanel, out resultText);

            gameManager.Configure(
                boardManager,
                25,
                movesText,
                capturedText,
                resultPanel,
                resultText,
                colorButtons,
                (Color[])DefaultColors.Clone());

            EditorUtility.SetDirty(boardManager);
            EditorUtility.SetDirty(gameManager);
            for (int i = 0; i < colorButtons.Length; i++)
            {
                EditorUtility.SetDirty(colorButtons[i]);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"Flood Fill demo scene could not be saved at {ScenePath}.");
                return;
            }

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = gameManager.gameObject;
            Debug.Log($"Flood Fill demo created and saved at {ScenePath}. Press Play to begin.");
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.10f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<Physics2DRaycaster>();
            return camera;
        }

        private static BoardManager CreateBoard(Camera mainCamera, Cell cellPrefab)
        {
            var boardManagerObject = new GameObject("BoardManager");
            BoardManager boardManager = boardManagerObject.AddComponent<BoardManager>();

            var boardRootObject = new GameObject("Board");
            boardRootObject.transform.SetParent(boardManagerObject.transform, false);
            boardManager.Configure(
                boardRootObject.transform,
                cellPrefab,
                mainCamera,
                10,
                10,
                0.8f,
                0.06f);
            return boardManager;
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            Type inputSystemModuleType = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (inputSystemModuleType != null)
            {
                eventSystemObject.AddComponent(inputSystemModuleType);
            }
            else
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
        }

        private static void CreateHeader(
            Transform canvas,
            GameManager gameManager,
            out TMP_Text movesText,
            out TMP_Text capturedText)
        {
            TMP_Text title = CreateText("Title", canvas, "FLOOD FILL", 68f, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -74f), new Vector2(650f, 92f));

            movesText = CreateText("MovesText", canvas, "Moves: 0 / 25", 42f, FontStyles.Normal);
            SetRect(movesText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -166f), new Vector2(620f, 64f));

            capturedText = CreateText("CapturedText", canvas, "Captured: 0%", 42f, FontStyles.Normal);
            SetRect(capturedText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -224f), new Vector2(620f, 64f));

            TMP_Text instructions = CreateText(
                "InstructionsText",
                canvas,
                "Select a color, then tap a connected group",
                30f,
                FontStyles.Normal);
            instructions.color = new Color(0.70f, 0.73f, 0.82f);
            SetRect(instructions.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -282f), new Vector2(720f, 50f));

            Button restartButton = CreateTextButton(
                "RestartButton",
                canvas,
                "Restart",
                new Color(0.18f, 0.20f, 0.28f),
                new Vector2(190f, 76f),
                34f);
            SetRect(restartButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one,
                new Vector2(-120f, -62f), new Vector2(190f, 76f));
            UnityEventTools.AddPersistentListener(restartButton.onClick, gameManager.RestartGame);
        }

        private static ColorButton[] CreateColorControls(Transform canvas, GameManager gameManager)
        {
            GameObject controlsObject = CreateUIObject("ColorControls", canvas);
            RectTransform controlsRect = controlsObject.GetComponent<RectTransform>();
            SetRect(controlsRect, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 120f), new Vector2(-80f, 190f));

            HorizontalLayoutGroup layout = controlsObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 24f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var buttons = new ColorButton[DefaultActiveColorCount];
            for (int i = 0; i < DefaultActiveColorCount; i++)
            {
                GameObject buttonObject = CreateUIObject($"ColorButton_{i + 1}", controlsObject.transform);
                RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(130f, 130f);

                Image image = buttonObject.AddComponent<Image>();
                image.color = DefaultColors[i];

                Button button = buttonObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.colors = CreateButtonColors();

                ColorButton colorButton = buttonObject.AddComponent<ColorButton>();
                colorButton.Configure(i, DefaultColors[i], gameManager);
                buttons[i] = colorButton;
            }

            return buttons;
        }

        private static void CreateResultPanel(
            Transform canvas,
            GameManager gameManager,
            out GameObject resultPanel,
            out TMP_Text resultText)
        {
            resultPanel = CreateUIObject("ResultPanel", canvas);
            RectTransform panelRect = resultPanel.GetComponent<RectTransform>();
            StretchToParent(panelRect);
            Image overlay = resultPanel.AddComponent<Image>();
            overlay.color = new Color(0.025f, 0.03f, 0.05f, 0.92f);

            GameObject card = CreateUIObject("ResultCard", resultPanel.transform);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            SetRect(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(820f, 500f));
            Image cardImage = card.AddComponent<Image>();
            cardImage.color = new Color(0.12f, 0.14f, 0.21f, 1f);

            resultText = CreateText("ResultText", card.transform, "YOU WIN!", 66f, FontStyles.Bold);
            SetRect(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 72f), new Vector2(720f, 230f));

            Button playAgainButton = CreateTextButton(
                "PlayAgainButton",
                card.transform,
                "Play Again",
                new Color(0.22f, 0.55f, 0.94f),
                new Vector2(360f, 100f),
                42f);
            SetRect(playAgainButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -148f),
                new Vector2(360f, 100f));
            UnityEventTools.AddPersistentListener(playAgainButton.onClick, gameManager.RestartGame);

            resultPanel.SetActive(false);
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string content,
            float fontSize,
            FontStyles style)
        {
            GameObject textObject = CreateUIObject(name, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = new Color(0.95f, 0.96f, 1f);
            text.alignment = TextAlignmentOptions.Center;
#if UNITY_6000_0_OR_NEWER
            text.textWrappingMode = TextWrappingModes.NoWrap;
#else
            text.enableWordWrapping = false;
#endif
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateTextButton(
            string name,
            Transform parent,
            string label,
            Color backgroundColor,
            Vector2 size,
            float fontSize)
        {
            GameObject buttonObject = CreateUIObject(name, parent);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = size;

            Image image = buttonObject.AddComponent<Image>();
            image.color = backgroundColor;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = CreateButtonColors();

            TMP_Text text = CreateText("Label", buttonObject.transform, label, fontSize, FontStyles.Bold);
            StretchToParent(text.rectTransform);
            return button;
        }

        private static ColorBlock CreateButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite CreateOrLoadSquareSprite()
        {
            if (!File.Exists(SquareSpritePath))
            {
                const int textureSize = 32;
                var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[textureSize * textureSize];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(SquareSpritePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SquareSpritePath, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(SquareSpritePath) as TextureImporter;
            if (importer != null &&
                (importer.textureType != TextureImporterType.Sprite ||
                 !Mathf.Approximately(importer.spritePixelsPerUnit, 32f)))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        }

        private static Cell CreateCellPrefab(Sprite squareSprite)
        {
            var cellObject = new GameObject(
                "Cell",
                typeof(SpriteRenderer),
                typeof(BoxCollider2D),
                typeof(Cell));
            SpriteRenderer renderer = cellObject.GetComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 0;

            BoxCollider2D collider = cellObject.GetComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;

            Cell cell = cellObject.GetComponent<Cell>();
            cell.Initialize(0, 0, 0, Color.white);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cellObject, CellPrefabPath);
            UnityEngine.Object.DestroyImmediate(cellObject);
            return prefab != null ? prefab.GetComponent<Cell>() : null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException($"Invalid Flood Fill folder path: {folderPath}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
