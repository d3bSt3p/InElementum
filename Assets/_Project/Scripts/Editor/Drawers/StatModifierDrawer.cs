using InElementum.Data.Stats;
using UnityEditor;
using UnityEngine;

namespace InElementum.Editor.Drawers
{
    /// <summary>Draws a modifier on one line: stat, kind, value.</summary>
    [CustomPropertyDrawer(typeof(StatModifier))]
    public class StatModifierDrawer : PropertyDrawer
    {
        private const float Gap = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var content = EditorGUI.PrefixLabel(position, label);
            var width = (content.width - 2 * Gap) / 3f;
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var cell = new Rect(content.x, content.y, width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(cell, property.FindPropertyRelative("stat"), GUIContent.none);
            cell.x += width + Gap;
            EditorGUI.PropertyField(cell, property.FindPropertyRelative("kind"), GUIContent.none);
            cell.x += width + Gap;
            EditorGUI.PropertyField(cell, property.FindPropertyRelative("value"), GUIContent.none);

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
