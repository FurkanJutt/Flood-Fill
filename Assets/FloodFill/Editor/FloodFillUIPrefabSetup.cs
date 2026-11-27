using System;
using System.IO;
using System.Linq;
using FloodFill.ThreeD;
using FloodFill.ThreeD.Achievements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FloodFill.EditorTools
{
    public static class FloodFillUIPrefabSetup
    {
        public const string Folder = "Assets/FloodFill/Prefabs/UI";
        public const string CanvasPath = Folder + "/FloodFillCanvas.prefab";
        private const string DemoPath = "Assets/FloodFill/Scenes/FloodFill3DDemo.unity";

        [MenuItem("Tools/Flood Fill/UI/Install Editable UI in Demo Scene")]
        public static void InstallInDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before installing UI prefabs.");
            // Only this scene's Canvas/controller are migrated; board, camera, materials and audio stay authored.
            var scene = SceneManager.GetSceneByPath(DemoPath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(DemoPath, OpenSceneMode.Additive);
            var manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FloodFillGameManager3D>(true)).Single();
            var canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).First(value => value.isRootCanvas);
            InstallInScene(scene, manager, canvas, manager.BoardManager.VoxelPrefab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static FloodFillMobileUI3D InstallInScene(Scene scene, FloodFillGameManager3D manager, Canvas oldCanvas, VoxelCell3D voxel)
        {
            var current = oldCanvas.GetComponent<FloodFillMobileUI3D>();
            if (current != null && PrefabUtility.IsPartOfPrefabInstance(current)) return current;
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
            if (prefab == null)
            {
                var temporary = new GameObject("FloodFillCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(temporary, scene);
                EditorUtility.CopySerialized(oldCanvas, temporary.GetComponent<Canvas>());
                EditorUtility.CopySerialized(oldCanvas.GetComponent<CanvasScaler>(), temporary.GetComponent<CanvasScaler>());
                temporary.layer = 5;
                var ui = temporary.AddComponent<FloodFillMobileUI3D>();
                var previous = manager.GetComponent<FloodFillMobileUI3D>();
                if (previous != null) EditorUtility.CopySerialized(previous, ui);
                ui.Initialize(manager, temporary.transform, voxel);
                ui.BuildEditableLayoutForEditor();
                foreach (GameObject screen in ui.EditorScreens)
                    PrefabUtility.SaveAsPrefabAssetAndConnect(screen, Folder + "/" + screen.name + ".prefab", InteractionMode.AutomatedAction);
                prefab = PrefabUtility.SaveAsPrefabAsset(temporary, CanvasPath);
                UnityEngine.Object.DestroyImmediate(temporary);
                if (prefab == null) throw new InvalidOperationException("UI prefab save failed.");
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var controller = instance.GetComponent<FloodFillMobileUI3D>();
            controller.Initialize(manager, instance.transform, voxel);
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            var legacy = manager.GetComponent<FloodFillMobileUI3D>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
            UnityEngine.Object.DestroyImmediate(oldCanvas.gameObject);
            Selection.activeGameObject = instance;
            return controller;
        }
    }

    [CustomEditor(typeof(FloodFillMobileUI3D))]
    public sealed class FloodFillMobileUIAuthoringInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var ui = (FloodFillMobileUI3D)target;
            EditorGUILayout.HelpBox("Edit the screen prefabs or RectTransforms directly. Preview switches below work outside Play Mode; runtime binds the saved controls and never rebuilds their layout.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                foreach (GameObject screen in ui.EditorScreens)
                    if (screen != null && GUILayout.Button("Preview " + screen.name))
                    {
                        foreach (GameObject item in ui.EditorScreens)
                        {
                            Undo.RecordObject(item, "Preview UI Screen");
                            item.SetActive(item == screen);
                            EditorUtility.SetDirty(item);
                        }
                        SceneView.RepaintAll();
                    }
                var notification = ui.GetComponentInChildren<FloodAchievementNotifications>(true);
                using (new EditorGUI.DisabledScope(notification == null || notification.panel == null))
                {
                    if (GUILayout.Button("Preview Achievement Notification"))
                    {
                        var panel = notification.panel;
                        Undo.RecordObjects(new UnityEngine.Object[] { panel, panel.gameObject }, "Preview Achievement Notification");
                        bool visible = !panel.gameObject.activeSelf;
                        panel.alpha = 1f;
                        panel.blocksRaycasts = false;
                        panel.interactable = false;
                        panel.gameObject.SetActive(visible);
                        EditorUtility.SetDirty(panel);
                        EditorUtility.SetDirty(panel.gameObject);
                        SceneView.RepaintAll();
                    }
                }
            }
            DrawDefaultInspector();
        }
    }
}
