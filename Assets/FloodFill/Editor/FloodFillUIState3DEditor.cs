using FloodFill.ThreeD;
using UnityEditor;
using UnityEngine;

namespace FloodFill.EditorTools
{
    [CustomEditor(typeof(FloodFillUIState3D)), CanEditMultipleObjects]
    public sealed class FloodFillUIState3DEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            DrawState("normalSprite", "normalOverrideSize", "normalSize");
            EditorGUILayout.Space();
            DrawState("selectedSprite", "selectedOverrideSize", "selectedSize");
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("normalColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("selectedColor"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("disabledSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("disabledColor"));
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawState(string spriteName, string overrideName, string sizeName)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(spriteName));
            using (new EditorGUI.IndentLevelScope())
            {
                SerializedProperty enabled = serializedObject.FindProperty(overrideName);
                EditorGUILayout.PropertyField(enabled, new GUIContent("Override Size"));
                if (!enabled.boolValue && !enabled.hasMultipleDifferentValues) return;
                using (new EditorGUI.IndentLevelScope())
                {
                    SerializedProperty size = serializedObject.FindProperty(sizeName);
                    EditorGUILayout.PropertyField(size.FindPropertyRelative("x"), new GUIContent("Width"));
                    EditorGUILayout.PropertyField(size.FindPropertyRelative("y"), new GUIContent("Height"));
                }
            }
        }
    }
}
