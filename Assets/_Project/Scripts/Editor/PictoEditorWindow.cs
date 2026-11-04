using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Stats;
using UnityEditor;
using UnityEngine;

namespace InElementum.Editor
{
    /// <summary>Pictos on the left; the selected picto and its passive, edited in place, on the right.</summary>
    public class PictoEditorWindow : DataEditorWindow<PictoData>
    {
        public const string Folder = "Assets/_Project/Data/Pictos";
        public const string PassiveFolder = Folder + "/Passives";
        private const string PassiveProperty = "passive";

        private SerializedObject _passiveObject;

        [MenuItem("Tools/In Elementum/Picto Editor")]
        public static void Open() => GetWindow<PictoEditorWindow>("Picto Editor");

        protected override IEnumerable<PictoData> LoadItems() => DataAssetOps.FindAll<PictoData>();

        protected override PictoData CreateItem() => DataAssetOps.Create<PictoData>(Folder, "New Picto");

        protected override string GetLabel(PictoData item) =>
            string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName;

        protected override void InitializeItem(PictoData item)
        {
            item.Id = item.name;
            item.DisplayName = item.name;
        }

        protected override void DrawExtraDetails(SerializedObject serializedObject)
        {
            var passiveProperty = serializedObject.FindProperty(PassiveProperty);
            var passive = passiveProperty.objectReferenceValue as PassiveData;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Passive", EditorStyles.boldLabel);

            if (passive == null)
            {
                EditorGUILayout.HelpBox("No passive: this picto gives stats only, and its Lumina does nothing.", MessageType.Info);
                if (GUILayout.Button("Create Passive", GUILayout.Width(140)))
                {
                    passiveProperty.objectReferenceValue = CreatePassive((PictoData)serializedObject.targetObject);
                    _passiveObject = null;
                }
            }
            else
            {
                DrawPassive(passive);
                var sharers = DataAssetOps.FindAll<PictoData>()
                    .Where(other => other != serializedObject.targetObject && other.Passive == passive)
                    .Select(other => other.DisplayName)
                    .ToList();
                if (sharers.Count > 0)
                {
                    EditorGUILayout.HelpBox($"Shared with {string.Join(", ", sharers)}. A character cannot have two " +
                        "sources of the same passive, and editing it here changes those pictos too.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(Summarize((PictoData)serializedObject.targetObject, passive), EditorStyles.wordWrappedLabel);
        }

        private void DrawPassive(PassiveData passive)
        {
            if (_passiveObject == null || _passiveObject.targetObject != passive)
                _passiveObject = new SerializedObject(passive);

            _passiveObject.Update();
            EditorGUI.indentLevel++;

            var property = _passiveObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script") continue;
                EditorGUILayout.PropertyField(property, true);
            }

            EditorGUI.indentLevel--;
            _passiveObject.ApplyModifiedProperties();
        }

        private static PassiveData CreatePassive(PictoData picto)
        {
            var passive = DataAssetOps.Create<PassiveData>(PassiveFolder, picto.DisplayName);
            passive.Id = passive.name;
            passive.DisplayName = picto.DisplayName;
            EditorUtility.SetDirty(passive);
            AssetDatabase.SaveAssetIfDirty(passive);
            return passive;
        }

        private static string Summarize(PictoData picto, PassiveData passive)
        {
            var stats = picto.Modifiers.Count == 0 ? "no stats" : string.Join(", ", picto.Modifiers.Select(Describe));
            var effect = passive == null ? "no passive"
                : !string.IsNullOrEmpty(passive.Description) ? passive.Description
                : passive.DisplayName;

            return $"Worn: {stats}; {effect}\n" +
                   $"Lumina: learned after {picto.BattlesToMaster} victories wearing it, costs {picto.LuminaCost} Lumina points, " +
                   "gives the passive without the stats.";
        }

        private static string Describe(StatModifier modifier)
        {
            var sign = modifier.Value >= 0f ? "+" : string.Empty;
            return modifier.Kind == ModifierKind.Percent
                ? $"{sign}{modifier.Value * 100f:0.#}% {modifier.Stat}"
                : $"{sign}{modifier.Value:0.#} {modifier.Stat}";
        }
    }
}
