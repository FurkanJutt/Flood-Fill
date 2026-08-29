using UnityEditor;
using UnityEngine;

namespace FloodFill.Editor
{
    [CustomEditor(typeof(GameManager)), CanEditMultipleObjects]
    public sealed class GameManagerEditor : UnityEditor.Editor
    {
        private static readonly string[] GridSizeLabels =
        {
            "10x10",
            "12x12",
            "15x15",
            "16x16",
            "18x18",
            "20x20",
            "24x24"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            bool setGridRequested = false;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "gridSize")
                {
                    setGridRequested = DrawGridSizeControls(property);
                    continue;
                }

                using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
            if (setGridRequested)
            {
                foreach (Object inspectedTarget in targets)
                {
                    var gameManager = (GameManager)inspectedTarget;
                    gameManager.SetGrid();
                    EditorUtility.SetDirty(gameManager);
                }
            }
        }

        private bool DrawGridSizeControls(SerializedProperty gridSizeProperty)
        {
            EditorGUI.showMixedValue = gridSizeProperty.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup(
                new GUIContent("Grid Size"),
                Mathf.Max(0, gridSizeProperty.enumValueIndex),
                GridSizeLabels);
            if (EditorGUI.EndChangeCheck())
            {
                gridSizeProperty.enumValueIndex = selectedIndex;
            }

            EditorGUI.showMixedValue = false;

            using (new EditorGUI.DisabledScope(
                       !Application.isPlaying || gridSizeProperty.hasMultipleDifferentValues))
            {
                return GUILayout.Button("Set Grid");
            }
        }
    }
}
