using InElementum.Data.Actions;
using InElementum.Data.Enums;
using InElementum.Data.Stats;
using UnityEditor;
using UnityEngine;

namespace InElementum.Editor.Drawers
{
    /// <summary>Shows the status and stat fields only for the effects that read them.</summary>
    [CustomPropertyDrawer(typeof(ActionEffectData))]
    public class ActionEffectDataDrawer : PropertyDrawer
    {
        private static readonly float LineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lines = 4;
            if (ShowsStatus(property)) lines++;
            if (ShowsStat(property)) lines++;
            return lines * LineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Draw(ref line, property, "effectType");
            Draw(ref line, property, "targetType");
            Draw(ref line, property, "value");
            Draw(ref line, property, "delay");
            if (ShowsStatus(property)) Draw(ref line, property, "status");
            if (ShowsStat(property)) Draw(ref line, property, "stat");

            EditorGUI.EndProperty();
        }

        private static void Draw(ref Rect line, SerializedProperty property, string name)
        {
            EditorGUI.PropertyField(line, property.FindPropertyRelative(name));
            line.y += LineHeight;
        }

        private static EffectType GetEffect(SerializedProperty property) =>
            (EffectType)property.FindPropertyRelative("effectType").enumValueIndex;

        private static bool ShowsStatus(SerializedProperty property) => GetEffect(property) == EffectType.ApplyStatus;

        private static bool ShowsStat(SerializedProperty property)
        {
            var effect = GetEffect(property);
            if (effect == EffectType.ModifyStat) return true;

            return effect == EffectType.ApplyStatus &&
                   StatusKey.IsKeyedByStat((StatusEffectType)property.FindPropertyRelative("status").enumValueIndex);
        }
    }
}
