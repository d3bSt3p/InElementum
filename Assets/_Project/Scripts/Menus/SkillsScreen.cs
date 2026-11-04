using System.Collections.Generic;
using System.Linq;
using InElementum.Core.UI;
using InElementum.Data.Enums;
using InElementum.Data.SkillTrees;
using InElementum.Core.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Menus
{
    /// <summary>
    /// The member's skill tree: the node grid with its links on the left and the skill points left under it, and on
    /// the right the member's attribute pentagon above the selected node's name, description, and cost. Confirm buys
    /// the node.
    /// </summary>
    public sealed class SkillsScreen : MenuScreen
    {
        private const float MaxCell = 60f;

        private readonly List<SkillNodeData> _nodes = new List<SkillNodeData>();
        private readonly List<VisualElement> _nodeViews = new List<VisualElement>();

        private VisualElement _area;
        private VisualElement _canvas;
        private VisualElement _links;
        private Label _emptyTree;

        private Label _points;
        private StatPentagon _pentagon;
        private VisualElement _detailIcon;
        private Label _detailGlyph;
        private Label _detailName;
        private Label _detailText;
        private Label _detailCost;
        private Label _detailState;

        private SkillTreeState _state;
        private int _cursor;
        private float _cell = 80f;
        private Vector2Int _origin;

        public override string Title => "Skills";

        private SkillNodeData Selected => _cursor >= 0 && _cursor < _nodes.Count ? _nodes[_cursor] : null;

        protected override void Build()
        {
            var left = MenuUi.Box(Root, "skills-left");
            MenuUi.Heading(left, "Skill Tree");
            _area = MenuUi.Box(left, "skills-area");
            _canvas = MenuUi.Box(_area, "skills-canvas");
            _links = MenuUi.Box(_canvas, "skills-links");
            _links.generateVisualContent += DrawLinks;
            _emptyTree = MenuUi.Text(_area, "No skill tree assigned.", "inventory-empty");
            _area.RegisterCallback<GeometryChangedEvent>(_ => Layout());
            var points = MenuUi.Box(left, "skills-points");
            MenuUi.Text(points, "SKILL POINTS", "skills-points__caption");
            _points = MenuUi.Text(points, "", "skills-points__value");

            var side = MenuUi.Box(Root, "skills-side");
            var attributes = MenuUi.Plate(MenuUi.Box(side, "skills-card", "skills-card--stats"), MenuUi.SmallCorner, small: true);
            _pentagon = new StatPentagon();
            attributes.Add(_pentagon);

            var detail = MenuUi.Plate(MenuUi.Box(side, "skills-card", "skills-card--detail"), MenuUi.SmallCorner, small: true);
            var heading = MenuUi.Box(detail, "skills-detail__heading");
            _detailIcon = MenuUi.Box(heading, "skills-detail__icon");
            _detailGlyph = MenuUi.Text(_detailIcon, "", "skill-node__glyph");
            _detailName = MenuUi.Text(heading, "", "skills-detail__name");
            MenuUi.Box(detail, "mrule").style.backgroundImage = new StyleBackground(Core.UI.UiTextures.FadeRight);
            _detailText = MenuUi.Text(detail, "", "skills-detail__text");
            MenuUi.Box(detail, "skills-detail__spacer");
            var footer = MenuUi.Box(detail, "skills-detail__footer");
            MenuUi.Text(footer, "COST", "skills-detail__cost-caption");
            _detailCost = MenuUi.Text(footer, "", "skills-detail__cost");
            _detailState = MenuUi.Text(footer, "", "skills-detail__state");
        }

        public override void Refresh()
        {
            var member = Member;
            if (member == null || Party == null) return;

            var previousTree = _state?.Tree;
            _state = Party.State.GetSkillTree(member);

            _points.text = _state != null ? _state.SkillPointsLeft.ToString() : "0";
            _pentagon.SetStats(Party.CreateCombatant(member, 0).Stats.FinalStats, member.BaseStats);

            if (_state?.Tree != previousTree || _nodeViews.Count != (_state?.Tree.Nodes.Count(node => node != null) ?? 0))
                BuildNodes();

            _emptyTree.style.display = _nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var state = _state.GetState(_nodes[i]);
                var view = _nodeViews[i];
                MenuUi.SetClass(view, "skill-node--unlocked", state == SkillNodeState.Unlocked);
                MenuUi.SetClass(view, "skill-node--available", state == SkillNodeState.Available);
                MenuUi.SetClass(view, "skill-node--locked", state == SkillNodeState.Locked);
            }

            _links.MarkDirtyRepaint();
            UpdateCursor();
        }

        private void BuildNodes()
        {
            foreach (var view in _nodeViews) view.RemoveFromHierarchy();
            _nodeViews.Clear();
            _nodes.Clear();
            if (_state == null) return;

            _nodes.AddRange(_state.Tree.Nodes.Where(node => node != null).OrderBy(node => node.Position.y).ThenBy(node => node.Position.x));
            _cursor = Mathf.Clamp(StartIndex(), 0, Mathf.Max(0, _nodes.Count - 1));

            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                var view = new VisualElement();
                view.AddToClassList("skill-node");
                view.AddToClassList(node.IsMajor ? "skill-node--major" : "skill-node--minor");
                view.AddToClassList("skill-node--" + node.Kind.ToString().ToLowerInvariant());
                var icon = MenuUi.Box(view, "skill-node__icon");
                MenuUi.SetSprite(icon, node.Icon);
                if (node.Icon == null) MenuUi.Text(view, Glyph(node), "skill-node__glyph");
                if (node.IsMajor) MenuUi.Box(view, "skill-node__star");

                var index = i;
                view.RegisterCallback<PointerDownEvent>(_ =>
                {
                    if (!Menu.InScreen) Menu.EnterScreen();
                    if (_cursor == index)
                    {
                        Confirm();
                        return;
                    }

                    _cursor = index;
                    UpdateCursor();
                });
                _canvas.Add(view);
                _nodeViews.Add(view);
            }

            Layout();
        }

        /// <summary>The first node worth showing: an affordable one, else an available one, else the first.</summary>
        private int StartIndex()
        {
            var affordable = _nodes.FindIndex(node => _state.CanUnlock(node) == SkillUnlockError.None);
            if (affordable >= 0) return affordable;
            var available = _nodes.FindIndex(node => _state.GetState(node) == SkillNodeState.Available);
            return available >= 0 ? available : 0;
        }

        /// <summary>Short text drawn in a node without an icon.</summary>
        public static string Glyph(SkillNodeData node)
        {
            switch (node.Kind)
            {
                case SkillNodeKind.Ability: return "AB";
                case SkillNodeKind.Surge: return "SG";
                case SkillNodeKind.Passive: return "PS";
                default:
                    var modifier = node.Modifiers.Count > 0 ? node.Modifiers[0] : default;
                    if (node.Modifiers.Count == 0) return "+";
                    switch (modifier.Stat)
                    {
                        case StatType.Vitality: return "VIT";
                        case StatType.Defense: return "DEF";
                        case StatType.Strength: return "STR";
                        case StatType.Speed: return "SPD";
                        default: return "LCK";
                    }
            }
        }

        private void Layout()
        {
            if (_nodes.Count == 0) return;

            var min = new Vector2Int(_nodes.Min(node => node.Position.x), _nodes.Min(node => node.Position.y));
            var max = new Vector2Int(_nodes.Max(node => node.Position.x), _nodes.Max(node => node.Position.y));
            _origin = min;
            var columns = max.x - min.x + 1;
            var rows = max.y - min.y + 1;

            var area = _area.contentRect;
            if (float.IsNaN(area.width) || area.width <= 0f) return;
            _cell = Mathf.Min(MaxCell, area.width / columns, area.height / rows);

            var width = _cell * columns;
            var height = _cell * rows;
            _canvas.style.width = width;
            _canvas.style.height = height;
            _canvas.style.left = (area.width - width) * 0.5f;
            _canvas.style.top = (area.height - height) * 0.5f;

            var size = _cell * 0.62f;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var center = CellCenter(_nodes[i].Position);
                var view = _nodeViews[i];
                view.style.width = size;
                view.style.height = size;
                view.style.left = center.x - size * 0.5f;
                view.style.top = center.y - size * 0.5f;
                if (!_nodes[i].IsMajor)
                {
                    var radius = size * 0.5f;
                    view.style.borderTopLeftRadius = radius;
                    view.style.borderTopRightRadius = radius;
                    view.style.borderBottomLeftRadius = radius;
                    view.style.borderBottomRightRadius = radius;
                }
            }

            _links.MarkDirtyRepaint();
        }

        private Vector2 CellCenter(Vector2Int cell) =>
            new Vector2((cell.x - _origin.x + 0.5f) * _cell, (cell.y - _origin.y + 0.5f) * _cell);

        private void DrawLinks(MeshGenerationContext context)
        {
            if (_state == null || _nodes.Count == 0) return;

            var painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            foreach (var node in _nodes)
            {
                foreach (var id in node.Prerequisites)
                {
                    var other = _state.Tree.Find(id);
                    if (other == null) continue;

                    var lit = _state.IsUnlocked(node) && _state.IsUnlocked(other);
                    painter.lineWidth = Mathf.Max(1.5f, _cell * 0.04f);
                    painter.strokeColor = lit ? HudColors.Accent : new Color(0.8f, 0.82f, 0.86f, 0.28f);
                    painter.BeginPath();
                    painter.MoveTo(CellCenter(other.Position));
                    painter.LineTo(CellCenter(node.Position));
                    painter.Stroke();
                }
            }
        }

        private void UpdateCursor()
        {
            for (var i = 0; i < _nodeViews.Count; i++)
                MenuUi.SetClass(_nodeViews[i], "skill-node--selected", i == _cursor && Menu.InScreen);

            var node = Selected;
            var shown = node != null && _state != null;
            _detailName.parent.parent.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
            if (!shown) return;

            _detailName.text = node.DisplayName;
            MenuUi.SetSprite(_detailIcon, node.Icon);
            _detailGlyph.text = node.Icon == null ? Glyph(node) : "";
            _detailText.text = node.Description;
            _detailCost.text = node.Cost.ToString();
            var state = _state.GetState(node);
            _detailState.text = state == SkillNodeState.Unlocked ? "UNLOCKED" : state == SkillNodeState.Available ? "AVAILABLE" : "LOCKED";
            MenuUi.SetClass(_detailState, "skills-detail__state--unlocked", state == SkillNodeState.Unlocked);
            MenuUi.SetClass(_detailState, "skills-detail__state--available", state == SkillNodeState.Available);
            MenuUi.SetClass(_detailCost, "skills-detail__cost--short", state != SkillNodeState.Unlocked && node.Cost > _state.SkillPointsLeft);
        }

        public override void Enter()
        {
            if (_nodes.Count > 0) _cursor = Mathf.Clamp(_cursor, 0, _nodes.Count - 1);
            UpdateCursor();
        }

        public override void Exit() => UpdateCursor();

        public override void Move(Vector2Int direction)
        {
            if (_nodes.Count == 0) return;
            var positions = _nodes.Select(node => (Vector2)node.Position).ToList();
            _cursor = MenuNavigation.Nearest(positions, _cursor, direction);
            UpdateCursor();
        }

        public override void Confirm()
        {
            var node = Selected;
            if (node == null || _state == null) return;

            var error = _state.Unlock(node);
            if (error != SkillUnlockError.None) return;

            AudioManager.PlayUi(AudioActionType.LearnSkill);
            Menu.RefreshAll();
        }

        public override IEnumerable<(MenuButton button, string label)> Prompts()
        {
            yield return (MenuButton.Confirm, "Unlock");
            yield return (MenuButton.Cancel, "Back");
        }
    }
}
