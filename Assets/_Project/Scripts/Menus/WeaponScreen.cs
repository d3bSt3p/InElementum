using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Model;
using InElementum.Core.UI;
using InElementum.Data.Enums;
using InElementum.Data.Stats;
using InElementum.Data.Weapons;
using InElementum.Core.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Menus
{
    /// <summary>
    /// The member's weapons, laid out like Clair Obscur's Weapon page: the weapons they can wield on the left, the
    /// highlighted weapon's card with the equipped one's card under it, and the member's sheet on the right with the
    /// attribute pentagon marked with the highlighted weapon's scaling grades. Confirm equips; Secondary changes the
    /// sort. Every power shown is for this member's current stats, as their basic attack would hit.
    /// </summary>
    public sealed class WeaponScreen : MenuScreen
    {
        private enum Sort
        {
            Power,
            Name
        }

        /// <summary>One weapon card: name and level, then Power, Element, and a column per scaling grade.</summary>
        private sealed class WeaponCard
        {
            public VisualElement Root;
            public Label Name;
            public Label Level;
            public Label Power;
            public Label Delta;
            public Diamond ElementMark;
            public Label ElementName;
            public VisualElement Grades;
            public Label Text;
            public Label Footer;
        }

        private readonly List<WeaponData> _entries = new List<WeaponData>();
        private readonly List<VisualElement> _rows = new List<VisualElement>();

        private Label _listHeading;
        private ScrollView _list;
        private Label _empty;
        private WeaponCard _highlight;
        private Label _equippedHeading;
        private WeaponCard _equipped;

        private Label _level;
        private VisualElement _xpBar;
        private Label _xpText;
        private VisualElement _hpBar;
        private Label _hpText;
        private Label _hpMax;
        private (Label value, Label bonus) _attackPower;
        private Label _attackElement;
        private Label _hits;
        private Label _crit;
        private StatPentagon _pentagon;

        private Combatant _unit;
        private Sort _sort = Sort.Power;
        private int _cursor;

        public override string Title => "Weapon";

        private WeaponInventory Weapons => Party?.State.Weapons;
        private WeaponData Selected => _cursor >= 0 && _cursor < _entries.Count ? _entries[_cursor] : null;
        private WeaponData Held => Member != null ? Weapons?.GetWeapon(Member) : null;

        protected override void Build()
        {
            var left = MenuUi.Box(Root, "weapon-left");
            _listHeading = MenuUi.Heading(left, "Weapons");
            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList("weapon-list");
            _list.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            left.Add(_list);
            _empty = MenuUi.Text(left, "", "inventory-empty");

            var middle = MenuUi.Box(Root, "weapon-middle");
            _highlight = Card(middle);
            _equippedHeading = MenuUi.Heading(middle, "Equipped");
            _equipped = Card(middle);

            var right = MenuUi.Box(Root, "weapon-right");
            // The tab bar names the member; the sheet starts at their level, as on the Pictos page.
            var levelRow = MenuUi.Box(right, "mgauge", "pictos-level");
            MenuUi.Text(levelRow, "LV", "mgauge__tag", "mgauge__tag--xp");
            _level = MenuUi.Text(levelRow, "", "mgauge__value");
            _xpText = MenuUi.Text(levelRow, "", "mgauge__max");
            _xpBar = MenuUi.Bar(right, "bar--xp", "pictos-bar");
            var hpRow = MenuUi.Gauge(right, "HP", "mgauge__tag--hp");
            _hpText = hpRow.value;
            _hpMax = hpRow.max;
            _hpBar = MenuUi.Bar(right, "bar--hp", "pictos-bar");

            MenuUi.Heading(right, "Combat Stats");
            var grid = MenuUi.Box(right, "weapon-stats");
            _attackPower = Row(grid, "Attack Power");
            _attackElement = Row(grid, "Element", true).value;
            _hits = Row(grid, "Hits").value;
            _crit = Row(grid, "Critical Rate", true).value;

            MenuUi.Heading(right, "Attributes");
            _pentagon = new StatPentagon();
            _pentagon.AddToClassList("weapon-pentagon");
            right.Add(_pentagon);
        }

        private static (Label value, Label bonus) Row(VisualElement parent, string caption, bool text = false)
        {
            var row = MenuUi.Box(parent, "stat-row");
            MenuUi.Text(row, caption, "stat-row__name");
            var value = text ? MenuUi.Text(row, "", "stat-row__value", "stat-row__value--text") : MenuUi.Text(row, "", "stat-row__value");
            var bonus = text ? null : MenuUi.Text(row, "", "stat-row__bonus");
            return (value, bonus);
        }

        private static WeaponCard Card(VisualElement parent)
        {
            var card = new WeaponCard { Root = MenuUi.Plate(MenuUi.Box(parent, "weapon-card"), MenuUi.SmallCorner, small: true) };

            var top = MenuUi.Box(card.Root, "weapon-card__top");
            card.Name = MenuUi.Text(top, "", "weapon-card__name");
            var level = MenuUi.Box(top, "weapon-card__level");
            MenuUi.Text(level, "LEVEL", "weapon-stat__caption");
            card.Level = MenuUi.Text(level, "", "weapon-card__level-value");

            var stats = MenuUi.Box(card.Root, "weapon-card__stats");
            var power = MenuUi.Box(stats, "weapon-stat");
            MenuUi.Text(power, "POWER", "weapon-stat__caption");
            var powerValue = MenuUi.Box(power, "weapon-stat__line");
            card.Power = MenuUi.Text(powerValue, "", "weapon-stat__value");
            card.Delta = MenuUi.Text(powerValue, "", "weapon-stat__delta");

            var element = MenuUi.Box(stats, "weapon-stat");
            MenuUi.Text(element, "ELEMENT", "weapon-stat__caption");
            var elementValue = MenuUi.Box(element, "weapon-stat__line", "weapon-stat__line--element");
            card.ElementMark = MenuUi.Mark(elementValue, Color.white, 16f);
            card.ElementName = MenuUi.Text(elementValue, "", "weapon-stat__element");

            card.Grades = MenuUi.Box(stats, "weapon-card__grades");
            MenuUi.Box(card.Root, "mrule").style.backgroundImage = new StyleBackground(UiTextures.FadeRight);
            card.Text = MenuUi.Text(card.Root, "", "weapon-card__text");
            card.Footer = MenuUi.Text(card.Root, "", "weapon-card__footer");
            return card;
        }

        public override void Refresh()
        {
            var member = Member;
            var weapons = Weapons;
            if (member == null || weapons == null) return;

            _unit = Party.CreateCombatant(member, 0);

            var previous = Selected;
            _entries.Clear();
            var usable = weapons.UsableBy(member);
            _entries.AddRange(_sort == Sort.Power
                ? usable.OrderByDescending(Power).ThenBy(weapon => weapon.DisplayName)
                : usable.OrderBy(weapon => weapon.DisplayName));
            var kept = previous != null ? _entries.IndexOf(previous) : -1;
            _cursor = _entries.Count == 0 ? -1 : kept >= 0 ? kept : Mathf.Clamp(_cursor, 0, _entries.Count - 1);
            _empty.text = _entries.Count == 0 ? $"{member.CharacterName} has no weapons yet." : "";

            var held = Held;
            _list.Clear();
            _rows.Clear();
            for (var i = 0; i < _entries.Count; i++)
            {
                var weapon = _entries[i];
                var row = MenuUi.Row(_list, "weapon-row");
                row.pickingMode = PickingMode.Position;
                MenuUi.Box(row, "weapon-row__equipped");
                MenuUi.Mark(row, ElementColor(weapon.Element), 14f);
                MenuUi.Text(row, weapon.DisplayName, "weapon-row__name");
                MenuUi.Text(row, $"LV {weapons.GetLevel(weapon)}", "weapon-row__level");
                MenuUi.Text(row, Power(weapon).ToString(), "weapon-row__power");

                // Weapons anyone can wield show who holds them. Every row keeps the badge's room so the columns line up.
                var holder = weapons.GetHolder(weapon);
                var badge = new Emblem(18f);
                badge.AddToClassList("weapon-row__holder");
                row.Add(badge);
                if (weapon.Wielder == null && holder != null)
                {
                    badge.SetCharacter(holder.CharacterName, holder.Portrait, MenuUi.ElementColor(holder.Element));
                    badge.SetRim(holder == member ? HudColors.Accent : HudColors.Rail, 1.5f);
                }
                else
                {
                    badge.style.visibility = Visibility.Hidden;
                }

                MenuUi.SetClass(row, "weapon-row--equipped", weapon == held);

                var index = i;
                row.RegisterCallback<PointerDownEvent>(_ =>
                {
                    if (!Menu.InScreen) Menu.EnterScreen();
                    if (_cursor == index) Confirm();
                    else _cursor = index;
                    UpdateCursor();
                });
                _rows.Add(row);
            }

            RefreshSheet();
            UpdateCursor();
        }

        private void RefreshSheet()
        {
            var member = Member;
            var progress = Party.State.GetProgress(member);
            var stats = _unit.Stats;

            _level.text = (progress?.Level ?? 1).ToString();
            var toNext = progress != null && !progress.IsMaxLevel ? progress.XpToNextLevel : 0;
            _xpText.text = toNext > 0 ? $"{progress.Xp} / {toNext} EXP" : "MAX";
            MenuUi.SetBar(_xpBar, toNext > 0 ? (float)progress.Xp / toNext : 1f);
            _hpText.text = stats.CurrentHealth.ToString("N0");
            _hpMax.text = $"/ {stats.MaxHealth:N0}";
            MenuUi.SetBar(_hpBar, stats.MaxHealth > 0 ? (float)stats.CurrentHealth / stats.MaxHealth : 0f);

            var attack = member.BasicAttack;
            _hits.text = attack != null ? StatCalculator.GetAttackHitCount(attack, stats.Passives).ToString() : "0";
            _crit.text = $"{Mathf.RoundToInt(DamageCalculator.GetCritChance(stats.FinalStats, Party.Tuning) * 100f)}%";
        }

        private void UpdateCursor()
        {
            var inScreen = Menu.InScreen;
            for (var i = 0; i < _rows.Count; i++)
                MenuUi.SetClass(_rows[i], "mrow--selected", inScreen && i == _cursor);
            if (inScreen && _cursor >= 0 && _cursor < _rows.Count) _list.ScrollTo(_rows[_cursor]);

            var held = Held;
            // Outside the page, the top card shows the held weapon; inside, whatever the cursor is on.
            var shown = inScreen ? Selected : held ?? Selected;
            var heldPower = held != null ? Power(held) : _unit != null ? Mathf.RoundToInt(_unit.AttackPower) : 0;

            ShowCard(_highlight, shown, shown != null && shown != held ? Power(shown) - heldPower : 0);
            var compare = shown != null && shown != held;
            _equippedHeading.parent.style.display = compare ? DisplayStyle.Flex : DisplayStyle.None;
            _equipped.Root.style.display = compare ? DisplayStyle.Flex : DisplayStyle.None;
            if (compare) ShowCard(_equipped, held, 0);

            // The sheet previews the highlighted weapon: its power next to the current one, its element, its grades.
            if (_unit == null) return;
            var preview = shown ?? held;
            var previewPower = preview != null ? Power(preview) : heldPower;
            _attackPower.value.text = previewPower.ToString();
            var delta = previewPower - heldPower;
            _attackPower.bonus.text = delta > 0 ? $"+{delta}" : delta < 0 ? delta.ToString() : "";
            MenuUi.SetClass(_attackPower.bonus, "stat-row__bonus--down", delta < 0);
            var element = preview != null ? preview.Element : _unit.AttackElement;
            _attackElement.text = ElementName(element);
            _attackElement.style.color = element == Element.None ? HudColors.Text : MenuUi.ElementColor(element);

            _pentagon.SetStats(_unit.Stats.FinalStats, Member.BaseStats,
                stat => preview?.GetGrade(stat) is ScalingGrade grade ? grade.ToString() : null);
        }

        private void ShowCard(WeaponCard card, WeaponData weapon, int delta)
        {
            card.Root.style.visibility = weapon != null ? Visibility.Visible : Visibility.Hidden;
            if (weapon == null) return;

            var weapons = Weapons;
            card.Name.text = weapon.DisplayName.ToUpperInvariant();
            card.Level.text = weapons.GetLevel(weapon).ToString();
            card.Power.text = Power(weapon).ToString();
            card.Delta.text = delta > 0 ? $"+{delta}" : delta < 0 ? delta.ToString() : "";
            MenuUi.SetClass(card.Delta, "weapon-stat__delta--down", delta < 0);
            card.ElementMark.Fill = ElementColor(weapon.Element);
            card.ElementName.text = ElementName(weapon.Element);
            card.ElementName.style.color = weapon.Element == Element.None ? HudColors.Text : MenuUi.ElementColor(weapon.Element);

            card.Grades.Clear();
            foreach (var entry in weapon.Scaling)
            {
                var column = MenuUi.Box(card.Grades, "weapon-stat");
                MenuUi.Text(column, StatPentagon.Short(entry.Stat), "weapon-stat__caption");
                MenuUi.Text(column, entry.Grade.ToString(), "weapon-stat__value", "weapon-stat__value--grade");
            }

            card.Text.text = weapon.Description ?? "";
            var holder = weapons.GetHolder(weapon);
            card.Footer.text = holder == Member ? "Equipped."
                : holder != null ? $"Held by {holder.CharacterName}."
                : weapon.Wielder == null ? "Any character can wield it." : "";
        }

        private int Power(WeaponData weapon) =>
            _unit == null ? 0 : WeaponMath.GetPower(weapon, Weapons.GetLevel(weapon), _unit.Stats.FinalStats, Party.Tuning);

        private static Color ElementColor(Element element) =>
            element == Element.None ? new Color(0.8f, 0.82f, 0.86f) : MenuUi.ElementColor(element);

        private static string ElementName(Element element) => element == Element.None ? "Physical" : element.ToString();

        public override void Enter()
        {
            var held = Held;
            var index = held != null ? _entries.IndexOf(held) : -1;
            _cursor = index >= 0 ? index : _entries.Count > 0 ? Mathf.Clamp(_cursor, 0, _entries.Count - 1) : -1;
            UpdateCursor();
        }

        public override void Exit() => UpdateCursor();

        public override void Move(Vector2Int direction)
        {
            if (_entries.Count == 0 || direction.y == 0) return;
            _cursor = MenuNavigation.Wrap(_cursor, direction.y, _entries.Count);
            UpdateCursor();
        }

        public override void Confirm()
        {
            var weapon = Selected;
            var weapons = Weapons;
            if (weapon == null || weapons == null) return;

            if (weapon == Held) return;

            var error = weapons.Equip(Member, weapon, takeFromHolder: true);
            if (error != WeaponError.None) return;

            AudioManager.PlayUi(AudioActionType.EquipWeapon);
            Menu.RefreshAll();
        }

        public override void Secondary()
        {
            _sort = _sort == Sort.Power ? Sort.Name : Sort.Power;
            Menu.RefreshAll();
        }

        public override IEnumerable<(MenuButton button, string label)> Prompts()
        {
            yield return (MenuButton.Secondary, _sort == Sort.Power ? "Sort by Name" : "Sort by Power");
            yield return (MenuButton.Confirm, "Equip Weapon");
            yield return (MenuButton.Cancel, "Back");
        }
    }
}
