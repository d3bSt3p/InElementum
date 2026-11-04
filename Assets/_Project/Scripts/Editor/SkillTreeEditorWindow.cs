using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Characters;
using InElementum.Data.SkillTrees;
using UnityEditor;
using UnityEngine;

namespace InElementum.Editor
{
    /// <summary>
    /// Grid editor for skill trees. Double-click an empty cell to add a node (linked to the selected one), drag a node
    /// to move it, Shift-click another node to link or unlink it as a prerequisite of the selection, and Delete to
    /// remove the selection. The node's fields are edited on the right.
    /// </summary>
    public class SkillTreeEditorWindow : EditorWindow
    {
        private const float Cell = 64f;
        private const float NodeSize = 40f;
        private const float SideWidth = 340f;
        private const int GridColumns = 14;
        private const int GridRows = 10;
        private const string TreeFolder = "Assets/_Project/Data/Characters/Skill Trees";

        [SerializeField] private SkillTreeData tree;
        [SerializeField] private string selectedId;

        private List<SkillTreeData> _trees = new List<SkillTreeData>();
        private SerializedObject _serialized;
        private Vector2 _canvasScroll;
        private Vector2 _sideScroll;
        private string _dragId;
        private List<string> _problems = new List<string>();

        [MenuItem("Tools/In Elementum/Skill Tree Editor")]
        public static void Open() => GetWindow<SkillTreeEditorWindow>("Skill Trees");

        private void OnEnable()
        {
            RefreshTrees();
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.projectChanged += RefreshTrees;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.projectChanged -= RefreshTrees;
        }

        private void OnUndo()
        {
            _serialized?.Update();
            Validate();
            Repaint();
        }

        private void RefreshTrees()
        {
            _trees = DataAssetOps.FindAll<SkillTreeData>();
            if (tree == null && _trees.Count > 0) tree = _trees[0];
            Bind(tree);
        }

        private void Bind(SkillTreeData value)
        {
            tree = value;
            _serialized = tree != null ? new SerializedObject(tree) : null;
            Validate();
            Repaint();
        }

        private void Validate() => _problems = tree != null ? tree.Validate() : new List<string>();

        private SkillNodeData Selected => tree != null ? tree.Find(selectedId) : null;

        private void OnGUI()
        {
            DrawToolbar();
            if (tree == null)
            {
                EditorGUILayout.HelpBox("No skill tree selected. Create one, or run Tools/In Elementum/Create Sample Data.", MessageType.Info);
                return;
            }

            _serialized.Update();
            EditorGUILayout.BeginHorizontal();
            DrawCanvas();
            DrawSide();
            EditorGUILayout.EndHorizontal();
        }

        #region Toolbar
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var names = _trees.Select(Label).ToArray();
            var index = _trees.IndexOf(tree);
            var chosen = EditorGUILayout.Popup(index, names, EditorStyles.toolbarPopup, GUILayout.Width(260f));
            if (chosen != index && chosen >= 0) Bind(_trees[chosen]);

            if (GUILayout.Button("New Tree", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                var created = DataAssetOps.Create<SkillTreeData>(TreeFolder, "Skill Tree");
                AssetDatabase.SaveAssets();
                RefreshTrees();
                Bind(created);
            }

            if (tree != null && GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(44f))) EditorGUIUtility.PingObject(tree);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Double-click: add   Drag: move   Shift-click: link   Del: remove", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static string Label(SkillTreeData value)
        {
            var owner = Owner(value);
            return owner != null ? $"{value.name}  ({owner.CharacterName})" : value.name;
        }

        private static PartyMemberData Owner(SkillTreeData value) =>
            DataAssetOps.FindAll<PartyMemberData>().FirstOrDefault(member => member.SkillTree == value);
        #endregion

        #region Canvas
        private void DrawCanvas()
        {
            var columns = Mathf.Max(GridColumns, tree.Nodes.Where(node => node != null).Select(node => node.Position.x + 3).DefaultIfEmpty(0).Max());
            var rows = Mathf.Max(GridRows, tree.Nodes.Where(node => node != null).Select(node => node.Position.y + 3).DefaultIfEmpty(0).Max());

            _canvasScroll = EditorGUILayout.BeginScrollView(_canvasScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var canvas = GUILayoutUtility.GetRect(columns * Cell, rows * Cell);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(canvas, new Color(0.16f, 0.18f, 0.2f));
                for (var x = 0; x < columns; x++)
                for (var y = 0; y < rows; y++)
                    EditorGUI.DrawRect(new Rect(canvas.x + (x + 0.5f) * Cell - 1f, canvas.y + (y + 0.5f) * Cell - 1f, 2f, 2f), new Color(1f, 1f, 1f, 0.15f));

                DrawLinks(canvas);
                foreach (var node in tree.Nodes.Where(node => node != null)) DrawNode(canvas, node);
            }

            HandleCanvasInput(canvas);
            EditorGUILayout.EndScrollView();
        }

        private static Vector2 Center(Rect canvas, Vector2Int cell) =>
            new Vector2(canvas.x + (cell.x + 0.5f) * Cell, canvas.y + (cell.y + 0.5f) * Cell);

        private static Vector2Int CellAt(Rect canvas, Vector2 point) =>
            new Vector2Int(Mathf.FloorToInt((point.x - canvas.x) / Cell), Mathf.FloorToInt((point.y - canvas.y) / Cell));

        private void DrawLinks(Rect canvas)
        {
            Handles.BeginGUI();
            foreach (var node in tree.Nodes.Where(node => node != null))
            {
                foreach (var other in node.Prerequisites.Select(tree.Find).Where(other => other != null))
                {
                    var from = Center(canvas, other.Position);
                    var to = Center(canvas, node.Position);
                    var selected = node.Id == selectedId || other.Id == selectedId;
                    Handles.color = selected ? new Color(1f, 0.8f, 0.3f) : new Color(0.7f, 0.76f, 0.82f, 0.8f);
                    Handles.DrawAAPolyLine(4f, from, to);

                    // A small arrowhead shows which way the requirement runs.
                    var direction = (to - from).normalized;
                    var tip = to - direction * (NodeSize * 0.5f + 2f);
                    var side = new Vector2(-direction.y, direction.x) * 5f;
                    Handles.DrawAAConvexPolygon(tip, tip - direction * 9f + (Vector2)side, tip - direction * 9f - (Vector2)side);
                }
            }
            Handles.EndGUI();
        }

        private void DrawNode(Rect canvas, SkillNodeData node)
        {
            var center = Center(canvas, node.Position);
            var rect = new Rect(center.x - NodeSize * 0.5f, center.y - NodeSize * 0.5f, NodeSize, NodeSize);
            var color = KindColor(node.Kind);
            var isSelected = node.Id == selectedId;

            if (isSelected) EditorGUI.DrawRect(new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f), new Color(1f, 0.85f, 0.3f));
            if (node.IsMajor)
            {
                EditorGUI.DrawRect(rect, color);
            }
            else
            {
                Handles.BeginGUI();
                Handles.color = color;
                Handles.DrawSolidDisc(center, Vector3.forward, NodeSize * 0.5f);
                Handles.EndGUI();
            }

            if (node.Icon != null)
            {
                GUI.DrawTexture(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f), AssetPreview.GetAssetPreview(node.Icon) ?? node.Icon.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                GUI.Label(rect, Menus.SkillsScreen.Glyph(node), style);
            }

            var cost = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(1f, 1f, 1f, 0.7f) } };
            GUI.Label(new Rect(center.x - 30f, rect.yMax, 60f, 14f), node.Cost.ToString(), cost);
            if (node.IsRoot) GUI.Label(new Rect(center.x - 30f, rect.y - 15f, 60f, 14f), "root", cost);
        }

        private static Color KindColor(SkillNodeKind kind)
        {
            switch (kind)
            {
                case SkillNodeKind.Ability: return new Color(0.25f, 0.55f, 0.85f);
                case SkillNodeKind.Surge: return new Color(0.75f, 0.35f, 0.85f);
                case SkillNodeKind.Passive: return new Color(0.3f, 0.7f, 0.5f);
                default: return new Color(0.45f, 0.5f, 0.56f);
            }
        }

        private void HandleCanvasInput(Rect canvas)
        {
            var current = Event.current;
            if (!canvas.Contains(current.mousePosition) && current.type != EventType.MouseUp && current.type != EventType.KeyDown) return;

            var cell = CellAt(canvas, current.mousePosition);
            var hit = tree.FindAt(cell);

            switch (current.type)
            {
                case EventType.MouseDown when current.button == 0:
                    if (current.clickCount == 2 && hit == null)
                    {
                        AddNode(cell);
                    }
                    else if (hit != null && current.shift && Selected != null && hit != Selected)
                    {
                        ToggleLink(Selected, hit);
                    }
                    else
                    {
                        selectedId = hit?.Id;
                        _dragId = hit?.Id;
                        GUI.FocusControl(null);
                    }

                    current.Use();
                    Repaint();
                    break;

                case EventType.MouseUp when _dragId != null:
                    var dragged = tree.Find(_dragId);
                    _dragId = null;
                    if (dragged != null && dragged.Position != cell && cell.x >= 0 && cell.y >= 0 && hit == null)
                    {
                        Undo.RecordObject(tree, "Move Skill Node");
                        dragged.Position = cell;
                        EditorUtility.SetDirty(tree);
                        Validate();
                    }

                    current.Use();
                    Repaint();
                    break;

                case EventType.KeyDown when (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace) && Selected != null
                                            && GUIUtility.keyboardControl == 0:
                    RemoveNode(Selected);
                    current.Use();
                    break;
            }
        }

        private void AddNode(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0) return;

            Undo.RecordObject(tree, "Add Skill Node");
            var node = new SkillNodeData { Id = SkillNodeData.NewId(), Position = cell, Cost = 1 };
            if (Selected != null) node.Prerequisites.Add(Selected.Id);
            tree.Nodes.Add(node);
            selectedId = node.Id;
            EditorUtility.SetDirty(tree);
            _serialized.Update();
            Validate();
        }

        private void ToggleLink(SkillNodeData node, SkillNodeData prerequisite)
        {
            Undo.RecordObject(tree, "Link Skill Nodes");
            if (!node.Prerequisites.Remove(prerequisite.Id)) node.Prerequisites.Add(prerequisite.Id);
            EditorUtility.SetDirty(tree);
            _serialized.Update();
            Validate();
        }

        private void RemoveNode(SkillNodeData node)
        {
            Undo.RecordObject(tree, "Remove Skill Node");
            tree.Nodes.Remove(node);
            foreach (var other in tree.Nodes.Where(other => other != null)) other.Prerequisites.Remove(node.Id);
            selectedId = null;
            EditorUtility.SetDirty(tree);
            _serialized.Update();
            Validate();
            Repaint();
        }
        #endregion

        #region Side panel
        private void DrawSide()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SideWidth));
            _sideScroll = EditorGUILayout.BeginScrollView(_sideScroll);

            DrawOwner();

            foreach (var problem in _problems) EditorGUILayout.HelpBox(problem, MessageType.Warning);

            var node = Selected;
            if (node == null)
            {
                EditorGUILayout.HelpBox($"{tree.Nodes.Count} nodes, {tree.Nodes.Where(x => x != null).Sum(x => x.Cost)} skill points to unlock them all.\nSelect a node to edit it.", MessageType.None);
            }
            else
            {
                DrawNodeFields(node);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawOwner()
        {
            var owner = Owner(tree);
            EditorGUI.BeginChangeCheck();
            var chosen = (PartyMemberData)EditorGUILayout.ObjectField("Used by", owner, typeof(PartyMemberData), false);
            if (EditorGUI.EndChangeCheck() && chosen != owner)
            {
                if (owner != null)
                {
                    Undo.RecordObject(owner, "Unassign Skill Tree");
                    owner.SkillTree = null;
                    EditorUtility.SetDirty(owner);
                }

                if (chosen != null)
                {
                    Undo.RecordObject(chosen, "Assign Skill Tree");
                    chosen.SkillTree = tree;
                    EditorUtility.SetDirty(chosen);
                }
            }

            EditorGUILayout.Space();
        }

        private void DrawNodeFields(SkillNodeData node)
        {
            var index = tree.Nodes.IndexOf(node);
            var property = _serialized.FindProperty("nodes").GetArrayElementAtIndex(index);

            EditorGUILayout.LabelField(node.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(node.Description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            Field(property, "displayName");
            Field(property, "description");
            Field(property, "icon");
            Field(property, "position");
            Field(property, "isMajor");
            Field(property, "cost");
            Field(property, "kind");

            switch (node.Kind)
            {
                case SkillNodeKind.StatBoost: Field(property, "modifiers"); break;
                case SkillNodeKind.Ability: Field(property, "ability"); break;
                case SkillNodeKind.Surge: Field(property, "surge"); break;
                case SkillNodeKind.Passive: Field(property, "passive"); break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                _serialized.ApplyModifiedProperties();
                Validate();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Requires any of", EditorStyles.boldLabel);
            if (node.IsRoot) EditorGUILayout.LabelField("Nothing: a root, buyable from the start.", EditorStyles.miniLabel);
            foreach (var id in node.Prerequisites.ToList())
            {
                EditorGUILayout.BeginHorizontal();
                var other = tree.Find(id);
                EditorGUILayout.LabelField(other != null ? $"{other.DisplayName}  {other.Position}" : $"Missing ({id})");
                if (GUILayout.Button("Unlink", GUILayout.Width(60f)) && other != null) ToggleLink(node, other);
                else if (other == null && GUILayout.Button("Drop", GUILayout.Width(50f)))
                {
                    Undo.RecordObject(tree, "Drop Missing Link");
                    node.Prerequisites.Remove(id);
                    EditorUtility.SetDirty(tree);
                    Validate();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField("Shift-click a node on the grid to link it.", EditorStyles.miniLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Id: {node.Id}", EditorStyles.miniLabel);
            if (GUILayout.Button("Remove Node")) RemoveNode(node);
        }

        private static void Field(SerializedProperty node, string name)
        {
            var field = node.FindPropertyRelative(name);
            if (field != null) EditorGUILayout.PropertyField(field, true);
        }
        #endregion
    }
}
