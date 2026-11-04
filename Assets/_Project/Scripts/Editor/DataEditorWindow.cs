using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InElementum.Editor
{
    /// <summary>List of assets on the left, the selected asset's fields on the right. Subclasses add the specifics.</summary>
    public abstract class DataEditorWindow<T> : EditorWindow where T : ScriptableObject
    {
        private const float ListWidth = 220f;

        [SerializeField] private T selected;
        [SerializeField] private string search = string.Empty;

        private readonly List<T> _items = new List<T>();
        private SerializedObject _serializedObject;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Action _pendingAction;

        protected T Selected => selected;

        protected abstract IEnumerable<T> LoadItems();
        protected abstract T CreateItem();
        protected abstract string GetLabel(T item);

        /// <summary>Gives a new or duplicated asset its id and display name.</summary>
        protected abstract void InitializeItem(T item);

        protected virtual void DrawHeader() { }
        protected virtual bool ShouldDrawProperty(string propertyName) => true;
        protected virtual void DrawExtraDetails(SerializedObject serializedObject) { }

        protected virtual void OnEnable()
        {
            Refresh();
            Undo.undoRedoPerformed += Repaint;
            EditorApplication.projectChanged += Refresh;
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
            EditorApplication.projectChanged -= Refresh;
        }

        public void Select(T item)
        {
            selected = item;
            _serializedObject = item != null ? new SerializedObject(item) : null;
            GUI.FocusControl(null);
            Repaint();
        }

        protected void Refresh()
        {
            _items.Clear();
            _items.AddRange(LoadItems());

            if (selected != null && !_items.Contains(selected))
                Select(null);

            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetails();
            EditorGUILayout.EndHorizontal();

            // Selection and asset changes wait until the layout closes, so this event's layout stays consistent.
            if (_pendingAction == null) return;

            var action = _pendingAction;
            _pendingAction = null;
            action();
            GUIUtility.ExitGUI();
        }

        private void DrawList()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(ListWidth), GUILayout.ExpandHeight(true));

            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            foreach (var item in _items)
            {
                if (item == null) continue;

                var label = GetLabel(item);
                if (!string.IsNullOrEmpty(search) && label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var clicked = GUILayout.Toggle(item == selected, label, "Button");
                if (clicked && item != selected)
                {
                    var target = item;
                    _pendingAction = () => Select(target);
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("New"))
                _pendingAction = CreateAndSelect;

            EditorGUI.BeginDisabledGroup(selected == null);
            if (GUILayout.Button("Duplicate"))
                _pendingAction = DuplicateSelected;
            if (GUILayout.Button("Delete"))
                _pendingAction = DeleteSelected;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (selected == null)
            {
                EditorGUILayout.LabelField("Select an asset, or press New.");
                EditorGUILayout.EndVertical();
                return;
            }

            if (_serializedObject == null || _serializedObject.targetObject != selected)
                _serializedObject = new SerializedObject(selected);

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            _serializedObject.Update();

            var property = _serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script" || !ShouldDrawProperty(property.name)) continue;
                EditorGUILayout.PropertyField(property, true);
            }

            DrawExtraDetails(_serializedObject);
            _serializedObject.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show in Project", GUILayout.Width(120)))
                EditorGUIUtility.PingObject(selected);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save", GUILayout.Width(100)))
                AssetDatabase.SaveAssetIfDirty(selected);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void CreateAndSelect()
        {
            var item = CreateItem();
            InitializeItem(item);
            SaveNewItem(item);
        }

        private void DuplicateSelected()
        {
            var item = DataAssetOps.Duplicate(selected);
            if (item == null) return;

            InitializeItem(item);
            SaveNewItem(item);
        }

        private void SaveNewItem(T item)
        {
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(item);
            Refresh();
            Select(item);
        }

        private void DeleteSelected()
        {
            var message = $"Move '{selected.name}' to the trash? Anything that references it will lose the reference.";
            if (!EditorUtility.DisplayDialog("Delete asset", message, "Delete", "Cancel")) return;

            DataAssetOps.Delete(selected);
            Select(null);
            Refresh();
        }
    }
}
