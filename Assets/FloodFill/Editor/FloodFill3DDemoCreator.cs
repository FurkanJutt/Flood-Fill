using System;
using System.IO;
using FloodFill.ThreeD;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FloodFill.Editor
{
    public static class FloodFill3DDemoCreator
    {
        private const string RootFolder = "Assets/FloodFill";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ScenePath = SceneFolder + "/FloodFill3DDemo.unity";
        private const string PrefabPath = PrefabFolder + "/VoxelCell3D.prefab";
        private const string MaterialPath = MaterialFolder + "/VoxelFlatKit.mat";
        private const string PreviousMaterialPath = MaterialFolder + "/Voxel3D.mat";
        private const string SourceArtFolder = "Assets/SourceArt";
        private const string FlatKitFolder = "Assets/FlatKit";
        private const string FlatKitShaderPath =
            "Assets/FlatKit/Shaders/StylizedSurface/StylizedSurface.shader";
        private const string FlatKitShaderName = "FlatKit/Stylized Surface";

        private static readonly Color[] Palette =
        {
            new Color(0.25f, 0.78f, 0.38f),
            new Color(0.62f, 0.32f, 0.86f),
            new Color(0.98f, 0.82f, 0.20f),
            new Color(1.00f, 0.52f, 0.16f),
            new Color(0.20f, 0.48f, 0.95f),
            new Color(0.30f, 0.82f, 0.80f)
        };

        [MenuItem("Tools/Flood Fill/Create 3D Demo")]
        public static void Create3DDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder(RootFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            Material voxelMaterial = CreateOrUpdateVoxelMaterial();
            if (voxelMaterial == null)
            {
                Debug.LogError("Flood Fill 3D demo creation failed because no usable voxel shader was found.");
                return;
            }
            GameObject cubeModel = FindImportedCubeModel(out string cubeAssetPath);
            VoxelCell3D voxelPrefab = CreateOrUpdateVoxelPrefab(
                cubeModel,
                cubeAssetPath,
                voxelMaterial);
            if (voxelPrefab == null)
            {
                Debug.LogError("Flood Fill 3D demo creation failed while building its voxel prefab.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            VoxelBoardManager3D boardManager = CreateBoard(voxelPrefab);
            OrbitCameraController3D orbitCamera = CreateCameraRig();
            CreateLighting();
            var gameManager = new GameObject("GameManager3D").AddComponent<FloodFillGameManager3D>();
            Canvas canvas = CreateCanvas();
            CreateEventSystem();

            CreateHeader(
                canvas.transform,
                gameManager,
                out TMP_Text movesText,
                out TMP_Text capturedText,
                out TMP_Text scoreText);
            ColorButton3D[] colorButtons = CreateColorControls(canvas.transform, gameManager);
            CreateResultPanel(
                canvas.transform,
                gameManager,
                out GameObject resultPanel,
                out TMP_Text resultText);

            gameManager.Configure(
                boardManager,
                orbitCamera,
                25,
                movesText,
                capturedText,
                scoreText,
                resultPanel,
                resultText,
                colorButtons,
                (Color[])Palette.Clone());

            EditorUtility.SetDirty(boardManager);
            EditorUtility.SetDirty(orbitCamera);
            EditorUtility.SetDirty(gameManager);
            for (int i = 0; i < colorButtons.Length; i++)
            {
                EditorUtility.SetDirty(colorButtons[i]);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"Flood Fill 3D demo could not be saved at {ScenePath}.");
                return;
            }

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = gameManager.gameObject;
            Debug.Log(
                $"Flood Fill 3D demo created successfully. Scene: {ScenePath}. " +
                $"Voxel source: {(string.IsNullOrEmpty(cubeAssetPath) ? "Unity cube fallback" : cubeAssetPath)}.");
        }

        [MenuItem("Tools/Flood Fill/Add Camera Fill Light to 3D Demo")]
        public static void AddCameraFillLightToExistingDemo()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogWarning(
                    $"No 3D demo scene was found at {ScenePath}. Use Tools > Flood Fill > Create 3D Demo first.");
                return;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            OrbitCameraController3D controller = FindSceneComponent<OrbitCameraController3D>(scene);
            if (controller == null || controller.EnsureCameraFillLight() == null)
            {
                Debug.LogError("The 3D demo camera rig could not create its camera fill light.");
                return;
            }

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"The camera fill light could not be saved at {ScenePath}.");
                return;
            }

            Debug.Log("Camera Fill Light added to the existing Flood Fill 3D camera.");
        }

        [MenuItem("Tools/Flood Fill/Upgrade Existing 3D Demo Rendering")]
        public static void UpgradeExisting3DDemoRendering()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            Material voxelMaterial = CreateOrUpdateVoxelMaterial();
            if (voxelMaterial == null)
            {
                Debug.LogError("The existing 3D demo could not be upgraded because no usable voxel shader was found.");
                return;
            }

            if (!AssignSharedMaterialToExistingVoxelPrefab(voxelMaterial))
            {
                Debug.LogWarning(
                    $"No voxel prefab was found at {PrefabPath}. Use Tools > Flood Fill > Create 3D Demo first.");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Existing Flood Fill 3D voxel prefab now uses the shared {MaterialPath} material.");
        }

        private static Material CreateOrUpdateVoxelMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader shader = FindFlatKitToonShader();
            bool usingFlatKit = shader != null;
            if (!usingFlatKit)
            {
                Debug.LogWarning(
                    "Flat Kit toon shader could not be located. Falling back to the previous voxel shader.");
                shader = FindPreviousVoxelShader();
            }

            if (shader == null)
            {
                Debug.LogError("No compatible Flat Kit, URP Lit, Standard, or Unlit shader was found.");
                return null;
            }

            if (material == null)
            {
                material = new Material(shader) { name = "VoxelFlatKit" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            if (usingFlatKit)
            {
                ConfigureFlatKitVoxelMaterial(material);
            }
            else
            {
                ConfigureFallbackVoxelMaterial(material);
            }

            // The verified Flat Kit shader contains multi_compile_instancing in its lit passes.
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader FindFlatKitToonShader()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(FlatKitShaderPath);
            if (shader != null && shader.name == FlatKitShaderName)
            {
                return shader;
            }

            if (!AssetDatabase.IsValidFolder(FlatKitFolder))
            {
                return null;
            }

            string[] shaderGuids = AssetDatabase.FindAssets("t:Shader", new[] { FlatKitFolder });
            for (int i = 0; i < shaderGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(shaderGuids[i]);
                Shader candidate = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (candidate != null && candidate.name == FlatKitShaderName)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Shader FindPreviousVoxelShader()
        {
            Material previousMaterial = AssetDatabase.LoadAssetAtPath<Material>(PreviousMaterialPath);
            if (previousMaterial != null && previousMaterial.shader != null && previousMaterial.shader.isSupported)
            {
                return previousMaterial.shader;
            }

            return Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Unlit/Color");
        }

        private static void ConfigureFlatKitVoxelMaterial(Material material)
        {
            SetColorIfPresent(material, "_BaseColor", Color.white);
            SetColorIfPresent(material, "_ColorDim", new Color(0.55f, 0.58f, 0.68f, 1f));
            SetColorIfPresent(material, "_FlatSpecularColor", new Color(1f, 1f, 1f, 1f));
            SetColorIfPresent(material, "_EmissionColor", Color.black);
            SetColorIfPresent(material, "_UnityShadowColor", new Color(0.16f, 0.19f, 0.28f, 0.35f));
            SetColorIfPresent(material, "_OutlineColor", new Color(0.025f, 0.035f, 0.075f, 1f));

            SetFloatIfPresent(material, "_CelPrimaryMode", 1f);
            SetFloatIfPresent(material, "_SelfShadingSize", 0.52f);
            SetFloatIfPresent(material, "_ShadowEdgeSize", 0.06f);
            SetFloatIfPresent(material, "_Flatness", 0.92f);
            SetFloatIfPresent(material, "_CelExtraEnabled", 0f);
            SetFloatIfPresent(material, "_SpecularEnabled", 1f);
            SetFloatIfPresent(material, "_FlatSpecularSize", 0.08f);
            SetFloatIfPresent(material, "_FlatSpecularEdgeSmoothness", 0.35f);
            SetFloatIfPresent(material, "_RimEnabled", 0f);
            SetFloatIfPresent(material, "_GradientEnabled", 0f);
            SetFloatIfPresent(material, "_OutlineEnabled", 0f);
            SetFloatIfPresent(material, "_LightContribution", 0.35f);
            SetFloatIfPresent(material, "_LightFalloffSize", 0.0001f);
            SetFloatIfPresent(material, "_UnityShadowMode", 1f);
            SetFloatIfPresent(material, "_UnityShadowPower", 0.18f);
            SetFloatIfPresent(material, "_UnityShadowSharpness", 2f);
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_SrcBlend", 1f);
            SetFloatIfPresent(material, "_DstBlend", 0f);
            SetFloatIfPresent(material, "_ZWrite", 1f);
            SetFloatIfPresent(material, "_Cull", 2f);

            SetKeyword(material, "_CELPRIMARYMODE_SINGLE", true);
            SetKeyword(material, "_CELPRIMARYMODE_NONE", false);
            SetKeyword(material, "_CELPRIMARYMODE_STEPS", false);
            SetKeyword(material, "_CELPRIMARYMODE_CURVE", false);
            SetKeyword(material, "DR_CEL_EXTRA_ON", false);
            SetKeyword(material, "DR_SPECULAR_ON", true);
            SetKeyword(material, "DR_RIM_ON", false);
            SetKeyword(material, "DR_GRADIENT_ON", false);
            SetKeyword(material, "DR_OUTLINE_ON", false);
            SetKeyword(material, "_UNITYSHADOWMODE_MULTIPLY", true);
            SetKeyword(material, "_UNITYSHADOWMODE_COLOR", false);
            SetKeyword(material, "_ALPHATEST_ON", false);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", false);
            SetKeyword(material, "_EMISSION", false);

            material.SetShaderPassEnabled("Outline", false);
            material.SetShaderPassEnabled("SRPDEFAULTUNLIT", false);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
        }

        private static void ConfigureFallbackVoxelMaterial(Material material)
        {
            SetColorIfPresent(material, "_BaseColor", Color.white);
            SetColorIfPresent(material, "_Color", Color.white);
            SetFloatIfPresent(material, "_Smoothness", 0.34f);
            SetFloatIfPresent(material, "_Metallic", 0.04f);
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static bool AssignSharedMaterialToExistingVoxelPrefab(Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                return false;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                MeshRenderer[] renderers = prefabContents.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length == 0)
                {
                    return false;
                }

                for (int i = 0; i < renderers.Length; i++)
                {
                    int materialSlotCount = Mathf.Max(1, renderers[i].sharedMaterials.Length);
                    var sharedMaterials = new Material[materialSlotCount];
                    for (int slot = 0; slot < materialSlotCount; slot++)
                    {
                        sharedMaterials[slot] = material;
                    }
                    renderers[i].sharedMaterials = sharedMaterials;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabContents, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static GameObject FindImportedCubeModel(out string assetPath)
        {
            assetPath = null;
            if (!AssetDatabase.IsValidFolder(SourceArtFolder))
            {
                Debug.LogWarning(
                    $"{SourceArtFolder} was not found. The 3D demo will use a Unity cube fallback.");
                return null;
            }

            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { SourceArtFolder });
            GameObject firstUsableModel = null;
            string firstUsablePath = null;
            for (int i = 0; i < modelGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate == null || candidate.GetComponentInChildren<MeshRenderer>(true) == null)
                {
                    continue;
                }

                if (firstUsableModel == null)
                {
                    firstUsableModel = candidate;
                    firstUsablePath = path;
                }

                if (Path.GetFileNameWithoutExtension(path)
                    .IndexOf("cube", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    assetPath = path;
                    Debug.Log($"Using imported Blender voxel model: {assetPath}");
                    return candidate;
                }
            }

            if (firstUsableModel != null)
            {
                assetPath = firstUsablePath;
                Debug.LogWarning(
                    $"No model named Cube was found. Using the first renderable model: {assetPath}");
                return firstUsableModel;
            }

            Debug.LogWarning(
                $"No renderable model was found under {SourceArtFolder}. " +
                "The 3D demo will use a Unity cube fallback.");
            return null;
        }

        private static VoxelCell3D CreateOrUpdateVoxelPrefab(
            GameObject importedModel,
            string importedModelPath,
            Material material)
        {
            var root = new GameObject("VoxelCell3D", typeof(VoxelCell3D));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            GameObject modelInstance;
            if (importedModel != null)
            {
                modelInstance = PrefabUtility.InstantiatePrefab(importedModel) as GameObject;
                if (modelInstance == null)
                {
                    modelInstance = UnityEngine.Object.Instantiate(importedModel);
                }
                modelInstance.name = Path.GetFileNameWithoutExtension(importedModelPath);
            }
            else
            {
                modelInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                modelInstance.name = "UnityCubeFallback";
            }

            modelInstance.transform.SetParent(visual, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;
            RemoveUnneededModelComponents(modelInstance);

            MeshRenderer[] renderers = modelInstance.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning(
                    "The imported Cube contained no MeshRenderer. Replacing it with a Unity cube fallback.");
                UnityEngine.Object.DestroyImmediate(modelInstance);
                modelInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                modelInstance.name = "UnityCubeFallback";
                modelInstance.transform.SetParent(visual, false);
                UnityEngine.Object.DestroyImmediate(modelInstance.GetComponent<Collider>());
                renderers = modelInstance.GetComponentsInChildren<MeshRenderer>(true);
            }

            Bounds modelBounds = renderers[0].bounds;
            for (int i = 0; i < renderers.Length; i++)
            {
                int materialSlotCount = Mathf.Max(1, renderers[i].sharedMaterials.Length);
                var sharedMaterials = new Material[materialSlotCount];
                for (int slot = 0; slot < sharedMaterials.Length; slot++)
                {
                    sharedMaterials[slot] = material;
                }
                renderers[i].sharedMaterials = sharedMaterials;
                renderers[i].shadowCastingMode = ShadowCastingMode.On;
                renderers[i].receiveShadows = true;
                if (i > 0)
                {
                    modelBounds.Encapsulate(renderers[i].bounds);
                }
            }

            modelInstance.transform.position -= modelBounds.center;
            float largestDimension = Mathf.Max(
                modelBounds.size.x,
                Mathf.Max(modelBounds.size.y, modelBounds.size.z));
            visual.localScale = Vector3.one / Mathf.Max(0.0001f, largestDimension);

            VoxelCell3D voxel = root.GetComponent<VoxelCell3D>();
            voxel.Configure(visual, renderers);
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefabAsset == null)
            {
                return null;
            }

            return prefabAsset.GetComponent<VoxelCell3D>();
        }

        private static void RemoveUnneededModelComponents(GameObject modelInstance)
        {
            foreach (Collider collider in modelInstance.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            foreach (Camera camera in modelInstance.GetComponentsInChildren<Camera>(true))
            {
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
            foreach (Light light in modelInstance.GetComponentsInChildren<Light>(true))
            {
                UnityEngine.Object.DestroyImmediate(light.gameObject);
            }
        }

        private static VoxelBoardManager3D CreateBoard(VoxelCell3D voxelPrefab)
        {
            var managerObject = new GameObject("VoxelBoardManager3D");
            VoxelBoardManager3D manager = managerObject.AddComponent<VoxelBoardManager3D>();
            var boardRoot = new GameObject("VoxelBoard");
            boardRoot.transform.SetParent(managerObject.transform, false);
            manager.Configure(
                boardRoot.transform,
                voxelPrefab,
                6,
                6,
                6,
                1f,
                0.06f,
                VoxelBoardManager3D.VoxelVolumeMode.HollowCube);
            return manager;
        }

        private static OrbitCameraController3D CreateCameraRig()
        {
            var rig = new GameObject("CameraRig");
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(rig.transform, false);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(pivot, false);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.065f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.AddComponent<AudioListener>();

            OrbitCameraController3D controller = rig.AddComponent<OrbitCameraController3D>();
            controller.Configure(pivot, camera);
            return controller;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.90f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.65f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.34f, 0.44f);
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
            FloodFillGameManager3D gameManager,
            out TMP_Text movesText,
            out TMP_Text capturedText,
            out TMP_Text scoreText)
        {
            TMP_Text title = CreateText("Title", canvas, "FLOOD FILL 3D", 66f, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -72f), new Vector2(720f, 90f));

            movesText = CreateText("MovesText", canvas, "Moves: 0 / 25", 40f, FontStyles.Normal);
            SetRect(movesText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -156f), new Vector2(620f, 58f));

            capturedText = CreateText("CapturedText", canvas, "Captured: 0%", 40f, FontStyles.Normal);
            SetRect(capturedText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -210f), new Vector2(620f, 58f));

            scoreText = CreateText("ScoreText", canvas, "Score: 0", 40f, FontStyles.Normal);
            SetRect(scoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -264f), new Vector2(620f, 58f));

            TMP_Text instructions = CreateText(
                "InstructionsText",
                canvas,
                "Drag to orbit  •  Right drag to pan  •  Scroll to zoom",
                27f,
                FontStyles.Normal);
            instructions.color = new Color(0.70f, 0.73f, 0.82f);
            SetRect(instructions.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -322f), new Vector2(900f, 48f));

            Button restart = CreateTextButton(
                "RestartButton",
                canvas,
                "Restart",
                new Color(0.18f, 0.20f, 0.28f),
                new Vector2(190f, 76f),
                34f);
            SetRect(restart.GetComponent<RectTransform>(), Vector2.one, Vector2.one,
                new Vector2(-120f, -62f), new Vector2(190f, 76f));
            UnityEventTools.AddPersistentListener(restart.onClick, gameManager.RestartGame);
        }

        private static ColorButton3D[] CreateColorControls(
            Transform canvas,
            FloodFillGameManager3D gameManager)
        {
            GameObject controlsObject = CreateUIObject("ColorControls", canvas);
            SetRect(
                controlsObject.GetComponent<RectTransform>(),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 120f),
                new Vector2(-80f, 180f));
            HorizontalLayoutGroup layout = controlsObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 24f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var buttons = new ColorButton3D[Palette.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                GameObject buttonObject = CreateUIObject($"ColorButton3D_{i + 1}", controlsObject.transform);
                buttonObject.GetComponent<RectTransform>().sizeDelta = new Vector2(126f, 126f);
                Image image = buttonObject.AddComponent<Image>();
                image.color = Palette[i];
                Button button = buttonObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.colors = CreateButtonColors();
                ColorButton3D colorButton = buttonObject.AddComponent<ColorButton3D>();
                colorButton.Configure(i, Palette[i], gameManager);
                buttons[i] = colorButton;
            }

            return buttons;
        }

        private static void CreateResultPanel(
            Transform canvas,
            FloodFillGameManager3D gameManager,
            out GameObject panel,
            out TMP_Text resultText)
        {
            panel = CreateUIObject("ResultPanel", canvas);
            StretchToParent(panel.GetComponent<RectTransform>());
            panel.AddComponent<Image>().color = new Color(0.025f, 0.03f, 0.05f, 0.92f);

            GameObject card = CreateUIObject("ResultCard", panel.transform);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 500f));
            card.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.21f);
            resultText = CreateText("ResultText", card.transform, "YOU WIN!", 62f, FontStyles.Bold);
            SetRect(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 72f), new Vector2(720f, 230f));

            Button playAgain = CreateTextButton(
                "PlayAgainButton",
                card.transform,
                "Play Again",
                new Color(0.22f, 0.55f, 0.94f),
                new Vector2(360f, 100f),
                42f);
            SetRect(playAgain.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -148f), new Vector2(360f, 100f));
            UnityEventTools.AddPersistentListener(playAgain.onClick, gameManager.RestartGame);
            panel.SetActive(false);
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
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateTextButton(
            string name,
            Transform parent,
            string label,
            Color color,
            Vector2 size,
            float fontSize)
        {
            GameObject buttonObject = CreateUIObject(name, parent);
            buttonObject.GetComponent<RectTransform>().sizeDelta = size;
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
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
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f);
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
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
