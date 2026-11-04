using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Actions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace InElementum.Editor
{
    public class ActionEditorWindow : DataEditorWindow<CombatActionData>
    {
        private const string RootFolder = "Assets/_Project/Data/Actions";

        private static readonly (string Label, Type Type, string Folder)[] Tabs =
        {
            ("Attacks", typeof(AttackData), "Attacks"),
            ("Abilities", typeof(AbilityData), "Abilities"),
            ("Items", typeof(ItemData), "Items"),
            ("Surges", typeof(SurgeData), "Surges")
        };

        [SerializeField] private int tab;

        [MenuItem("Tools/In Elementum/Action Editor")]
        public static void Open() => GetWindow<ActionEditorWindow>("Action Editor");

        public static void Open(CombatActionData action)
        {
            var window = GetWindow<ActionEditorWindow>("Action Editor");
            window.tab = Array.FindIndex(Tabs, x => x.Type == action.GetType());
            window.Refresh();
            window.Select(action);
        }

        [OnOpenAsset]
        public static bool OpenOnDoubleClick(EntityId entityId, int line)
        {
            if (!(EditorUtility.EntityIdToObject(entityId) is CombatActionData action)) return false;

            Open(action);
            return true;
        }

        protected override IEnumerable<CombatActionData> LoadItems() =>
            DataAssetOps.FindAll(Tabs[tab].Type).Cast<CombatActionData>();

        protected override CombatActionData CreateItem()
        {
            var (label, type, folder) = Tabs[tab];
            return (CombatActionData)DataAssetOps.Create(type, $"{RootFolder}/{folder}", $"New {label.TrimEnd('s')}");
        }

        protected override string GetLabel(CombatActionData item) =>
            string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName;

        protected override void InitializeItem(CombatActionData item)
        {
            item.Id = item.name;
            item.DisplayName = item.name;
        }

        protected override void DrawHeader()
        {
            var newTab = GUILayout.Toolbar(tab, Tabs.Select(x => x.Label).ToArray());
            if (newTab == tab) return;

            tab = newTab;
            Refresh();
        }

        protected override void DrawExtraDetails(SerializedObject serializedObject)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Description Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Selected.BuildDescription(), MessageType.None);
        }
    }
}
