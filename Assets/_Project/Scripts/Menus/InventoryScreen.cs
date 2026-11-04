using System.Collections.Generic;
using System.Linq;
using InElementum.Core.UI;
using InElementum.Data.Actions;
using InElementum.Data.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Menus
{
    /// <summary>
    /// The party's possessions: category tabs between the Previous and Next keys, a list of bevelled bars (name, on
    /// hand), the selected entry's description in a framed box like the combat HUD's, and the party's HP down
    /// the side. Items marked usable outside combat that restore HP can be used on a member from here.
    /// </summary>
    public sealed class InventoryScreen : MenuScreen
    {
        private enum Category
        {
            All,
            Items,
            Materials
        }

        private sealed class Entry
        {
            public string Name;
            public Sprite Icon;
            public int Count;
            public string Kind;
            public string Description;
            public ItemData Item;
        }

        private sealed class MemberRow
        {
            public VisualElement Root;
            public Emblem Emblem;
            public Label Name;
            public Label Level;
            public Label Hp;
            public Label HpMax;
            public VisualElement Bar;
        }

        private static readonly Category[] Categories = { Category.All, Category.Items, Category.Materials };

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<VisualElement> _tabs = new List<VisualElement>();
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<MemberRow> _memberRows = new List<MemberRow>();

        private KeyPrompt _previousKey;
        private KeyPrompt _nextKey;
        private Label _count;
        private ScrollView _list;
        private Label _empty;
        private VisualElement _descriptionBox;
        private Label _description;
        private Label _kind;
        private VisualElement _sideParty;

        private int _category;
        private int _cursor;
        private bool _picking;
        private int _target;

        public override string Title => "Inventory";
        public override bool PerMember => false;

        private Entry Selected => _cursor >= 0 && _cursor < _entries.Count ? _entries[_cursor] : null;

        protected override void Build()
        {
            var main = MenuUi.Box(Root, "inventory-main");

            var tabs = MenuUi.Box(main, "inventory-tabs");
            _previousKey = new KeyPrompt(20f);
            _previousKey.AddToClassList("inventory-tabs__key");
            tabs.Add(_previousKey);
            for (var i = 0; i < Categories.Length; i++)
            {
                var tab = MenuUi.Box(tabs, "inventory-tab");
                tab.pickingMode = PickingMode.Position;
                var sweep = MenuUi.Box(tab, "inventory-tab__sweep");
                sweep.style.backgroundImage = new StyleBackground(UiTextures.FadeBoth);
                MenuUi.Text(tab, MenuUi.Pretty(Categories[i]).ToUpperInvariant(), "inventory-tab__label");
                MenuUi.Box(tab, "inventory-tab__underline");
                var index = i;
                tab.RegisterCallback<PointerDownEvent>(_ => SetCategory(index));
                _tabs.Add(tab);
            }

            _nextKey = new KeyPrompt(20f);
            _nextKey.AddToClassList("inventory-tabs__key");
            tabs.Add(_nextKey);
            MenuUi.Box(tabs, "inventory-tabs__spacer");
            _count = MenuUi.Text(tabs, "", "inventory-count");

            var header = MenuUi.Box(main, "table-header");
            MenuUi.Text(header, "NAME", "col-name");
            MenuUi.Text(header, "ON HAND", "col-count");

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList("inventory-list");
            _list.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            main.Add(_list);
            _empty = MenuUi.Text(main, "Nothing here yet.", "inventory-empty");

            _descriptionBox = MenuUi.Plate(MenuUi.Box(main, "description-box"), MenuUi.SmallCorner, small: true);
            _description = MenuUi.Text(_descriptionBox, "", "description-box__text");
            var meta = MenuUi.Box(_descriptionBox, "description-box__meta");
            _kind = MenuUi.Text(meta, "", "description-box__caption");

            var side = MenuUi.Box(Root, "inventory-side");
            MenuUi.Heading(side, "Party");
            _sideParty = MenuUi.Box(side, "side-party");
        }

        public override void Refresh()
        {
            if (Party == null) return;

            for (var i = 0; i < _tabs.Count; i++) MenuUi.SetClass(_tabs[i], "inventory-tab--selected", i == _category);
            var gamepad = MenuInput.UsingGamepad;
            _previousKey.Set(Menu.Input.Glyph(MenuButton.Previous), gamepad);
            _nextKey.Set(Menu.Input.Glyph(MenuButton.Next), gamepad);

            CollectEntries(Categories[_category]);
            _cursor = _entries.Count == 0 ? -1 : Mathf.Clamp(_cursor, 0, _entries.Count - 1);
            _count.text = _entries.Count == 1 ? "1 ENTRY" : $"{_entries.Count} ENTRIES";

            _list.Clear();
            _rows.Clear();
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var row = MenuUi.Row(_list, "table-row");
                row.pickingMode = PickingMode.Position;
                MenuUi.Text(row, "▶", "table-row__pointer");
                if (entry.Icon != null)
                {
                    var icon = MenuUi.Box(row, "table-row__icon");
                    MenuUi.SetSprite(icon, entry.Icon);
                }
                else
                {
                    MenuUi.Mark(row, HudColors.Gilt, 12f).AddToClassList("table-row__mark");
                }

                MenuUi.Text(row, entry.Name, "col-name");
                MenuUi.Text(row, entry.Count.ToString(), "col-count");
                var index = i;
                row.RegisterCallback<PointerDownEvent>(_ =>
                {
                    _cursor = index;
                    if (!Menu.InScreen) Menu.EnterScreen();
                    UpdateCursor();
                });
                _rows.Add(row);
            }

            _empty.style.display = _entries.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshParty();
            UpdateCursor();
        }

        private void CollectEntries(Category category)
        {
            _entries.Clear();
            if (category == Category.All || category == Category.Items)
            {
                foreach (var pair in Party.Items.Items.Where(pair => pair.Key != null).OrderBy(pair => pair.Key.DisplayName))
                {
                    _entries.Add(new Entry
                    {
                        Name = pair.Key.DisplayName,
                        Icon = pair.Key.Icon,
                        Count = pair.Value,
                        Kind = "Item",
                        Description = pair.Key.BuildDescription(),
                        Item = pair.Key
                    });
                }
            }

            if (category == Category.All || category == Category.Materials)
            {
                foreach (var pair in Party.Materials.Where(pair => pair.Key != null && pair.Value > 0).OrderBy(pair => pair.Key.DisplayName))
                    _entries.Add(Material(pair.Key, pair.Value));
            }
        }

        private static Entry Material(MaterialData material, int count) => new Entry
        {
            Name = material.DisplayName,
            Icon = material.Icon,
            Count = count,
            Kind = "Material",
            Description = "A crafting material. Upgrades will use it."
        };

        private void RefreshParty()
        {
            var members = Menu.Members;
            while (_memberRows.Count < members.Count) _memberRows.Add(CreateMemberRow(_memberRows.Count));
            for (var i = 0; i < _memberRows.Count; i++)
            {
                var row = _memberRows[i];
                var shown = i < members.Count;
                row.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown) continue;

                var member = members[i];
                var unit = Party.CreateCombatant(member, 0);
                row.Emblem.SetCharacter(member.CharacterName, member.Portrait, MenuUi.ElementColor(member.Element));
                row.Name.text = member.CharacterName;
                row.Level.text = $"LV {Party.State.GetProgress(member)?.Level ?? 1}";
                row.Hp.text = unit.Stats.CurrentHealth.ToString("N0");
                row.HpMax.text = $"/ {unit.Stats.MaxHealth:N0}";
                MenuUi.SetBar(row.Bar, unit.Stats.MaxHealth > 0 ? (float)unit.Stats.CurrentHealth / unit.Stats.MaxHealth : 0f);
            }

            UpdateTargets();
        }

        private MemberRow CreateMemberRow(int index)
        {
            var root = MenuUi.Row(_sideParty, "side-member");
            root.pickingMode = PickingMode.Position;
            var emblem = new Emblem(26f);
            emblem.AddToClassList("side-member__emblem");
            root.Add(emblem);
            var info = MenuUi.Box(root, "side-member__info");
            var top = MenuUi.Box(info, "side-member__top");
            var name = MenuUi.Text(top, "", "side-member__name");
            var level = MenuUi.Text(top, "", "side-member__level");
            var (hp, hpMax) = MenuUi.Gauge(info, "HP", "mgauge__tag--hp");
            var bar = MenuUi.Bar(info, "bar--hp", "side-member__bar");

            root.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (!_picking) return;
                _target = index;
                UseOnTarget();
            });
            return new MemberRow { Root = root, Emblem = emblem, Name = name, Level = level, Hp = hp, HpMax = hpMax, Bar = bar };
        }

        private void UpdateCursor()
        {
            for (var i = 0; i < _rows.Count; i++)
                MenuUi.SetClass(_rows[i], "mrow--selected", Menu.InScreen && !_picking && i == _cursor);
            for (var i = 0; i < _rows.Count; i++)
                MenuUi.SetClass(_rows[i], "table-row--held", _picking && i == _cursor);

            if (_cursor >= 0 && _cursor < _rows.Count) _list.ScrollTo(_rows[_cursor]);
            var entry = Selected;
            _description.text = entry?.Description ?? "";
            _kind.text = entry != null ? entry.Kind.ToUpperInvariant() : "";
            _descriptionBox.style.visibility = entry != null ? Visibility.Visible : Visibility.Hidden;
        }

        private void UpdateTargets()
        {
            for (var i = 0; i < _memberRows.Count; i++)
            {
                var row = _memberRows[i];
                var target = _picking && i == _target;
                MenuUi.SetClass(row.Root, "mrow--selected", target);
                row.Emblem.SetRim(target ? HudColors.Accent : HudColors.Rail, target ? 2.5f : 2f);
                var member = i < Menu.Members.Count ? Menu.Members[i] : null;
                MenuUi.SetClass(row.Root, "side-member--disabled",
                    _picking && Selected?.Item != null && !Party.CanUseItem(Selected.Item, member));
            }
        }

        private void SetCategory(int index)
        {
            _category = MenuNavigation.Wrap(index, 0, Categories.Length);
            _cursor = 0;
            _picking = false;
            Refresh();
        }

        public override void Enter()
        {
            if (_cursor < 0 && _entries.Count > 0) _cursor = 0;
            UpdateCursor();
        }

        public override void Exit()
        {
            _picking = false;
            UpdateCursor();
            UpdateTargets();
        }

        public override void Move(Vector2Int direction)
        {
            if (_picking)
            {
                if (direction.y != 0) _target = MenuNavigation.Wrap(_target, direction.y, Menu.Members.Count);
                UpdateTargets();
                return;
            }

            if (direction.x != 0)
            {
                SetCategory(_category + direction.x);
                return;
            }

            if (_entries.Count == 0) return;
            _cursor = MenuNavigation.Wrap(_cursor, direction.y, _entries.Count);
            UpdateCursor();
        }

        public override bool Cycle(int step)
        {
            SetCategory(_category + step);
            return true;
        }

        public override void Confirm()
        {
            if (_picking)
            {
                UseOnTarget();
                return;
            }

            var item = Selected?.Item;
            if (item == null) return;
            if (!item.UsableOutsideCombat) return;

            _picking = true;
            _target = Mathf.Clamp(_target, 0, Mathf.Max(0, Menu.Members.Count - 1));
            UpdateCursor();
            UpdateTargets();
        }

        private void UseOnTarget()
        {
            var item = Selected?.Item;
            var member = _target >= 0 && _target < Menu.Members.Count ? Menu.Members[_target] : null;
            if (item == null || member == null) return;

            if (!Party.UseItem(item, member)) return;

            if (Party.Items.GetCount(item) <= 0) _picking = false;
            Refresh();
        }

        public override bool Cancel()
        {
            if (!_picking) return false;
            _picking = false;
            UpdateCursor();
            UpdateTargets();
            return true;
        }

        public override IEnumerable<(MenuButton button, string label)> Prompts()
        {
            yield return (MenuButton.Previous, "Category");
            yield return (MenuButton.Confirm, _picking ? "Use" : "Select");
            yield return (MenuButton.Cancel, "Return");
        }
    }
}
