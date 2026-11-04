using System;
using System.Collections.Generic;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using UnityEditor;
using UnityEngine;

namespace InElementum.Editor
{
    public class EnemyEditorWindow : DataEditorWindow<EnemyCharacterData>
    {
        private const string Folder = "Assets/_Project/Data/Characters/Enemies";
        private const string AffinitiesProperty = "affinities";
        private const int GridColumns = 4;
        private const float CellWidth = 90f;

        [MenuItem("Tools/In Elementum/Enemy Editor")]
        public static void Open() => GetWindow<EnemyEditorWindow>("Enemy Editor");

        protected override IEnumerable<EnemyCharacterData> LoadItems() => DataAssetOps.FindAll<EnemyCharacterData>();

        protected override EnemyCharacterData CreateItem() => DataAssetOps.Create<EnemyCharacterData>(Folder, "New Enemy");

        protected override string GetLabel(EnemyCharacterData item) =>
            string.IsNullOrEmpty(item.CharacterName) ? item.name : item.CharacterName;

        protected override void InitializeItem(EnemyCharacterData item)
        {
            item.CharacterID = item.name;
            item.CharacterName = item.name;
        }

        protected override bool ShouldDrawProperty(string propertyName) => propertyName != AffinitiesProperty;

        protected override void DrawExtraDetails(SerializedObject serializedObject)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Element Affinities (damage multiplier)", EditorStyles.boldLabel);

            var affinities = serializedObject.FindProperty(AffinitiesProperty);
            var elements = (Element[])Enum.GetValues(typeof(Element));
            var column = 0;

            EditorGUILayout.BeginHorizontal();
            foreach (var element in elements)
            {
                if (element == Element.None) continue;

                if (column == GridColumns)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    column = 0;
                }

                DrawAffinityCell(affinities, element);
                column++;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawAffinityCell(SerializedProperty affinities, Element element)
        {
            var index = FindAffinity(affinities, element);
            var current = index >= 0
                ? affinities.GetArrayElementAtIndex(index).FindPropertyRelative("damageMultiplier").floatValue
                : 1f;

            EditorGUILayout.BeginVertical(GUILayout.Width(CellWidth));
            EditorGUILayout.LabelField(element.ToString(), GUILayout.Width(CellWidth));
            var updated = Mathf.Max(0f, EditorGUILayout.FloatField(current, GUILayout.Width(CellWidth)));
            EditorGUILayout.LabelField(Describe(updated), EditorStyles.miniLabel, GUILayout.Width(CellWidth));
            EditorGUILayout.EndVertical();

            if (Mathf.Approximately(updated, current)) return;

            if (index < 0)
            {
                index = affinities.arraySize;
                affinities.InsertArrayElementAtIndex(index);
                affinities.GetArrayElementAtIndex(index).FindPropertyRelative("element").enumValueIndex = (int)element;
            }

            affinities.GetArrayElementAtIndex(index).FindPropertyRelative("damageMultiplier").floatValue = updated;
        }

        private static int FindAffinity(SerializedProperty affinities, Element element)
        {
            for (var i = 0; i < affinities.arraySize; i++)
            {
                if (affinities.GetArrayElementAtIndex(i).FindPropertyRelative("element").enumValueIndex == (int)element)
                    return i;
            }

            return -1;
        }

        private static string Describe(float multiplier)
        {
            if (multiplier <= 0f) return "Immune";
            if (multiplier < 1f) return "Resist";
            return multiplier > 1f ? "Weak" : "Normal";
        }
    }
}
