using System.Collections.Generic;
using System.Linq;
using InElementum.Core.UI;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Stats;
using InElementum.Encounters;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Menus
{
    /// <summary>
    /// The party and the root of each member's character pages. One list of every member: the active party first, in
    /// slot order, on blue cards, then the bench; the cursor steps its card out to the right, as in battle. Down the
    /// side, the highlighted member's sheet: element, role, HP and level, attributes, growth, and their basic attack
    /// and Surge. Confirm opens the member's Weapon, Pictos, and Skills pages. Secondary sends an active member to the bench or brings a benched one in; with the party full it picks
    /// the benched member up to swap with an active one. Switch picks up any member; Switch or Confirm on another
    /// then trades their places. The last active member cannot leave.
    /// </summary>
    public sealed class PartyScreen : MenuScreen
    {
        private static readonly StatType[] Stats = { StatType.Vitality, StatType.Strength, StatType.Speed, StatType.Luck, StatType.Defense };

        private sealed class MemberRow
        {
            public VisualElement Root;
            public Label Index;
            public CharacterFigure Figure;
            public Label Name;
            public Label Level;
            public Label Hp;
            public Label HpMax;
            public VisualElement Bar;
        }

        private sealed class ActionCard
        {
            public Label Name;
            public Label State;
            public Label Text;
        }

        private readonly List<PartyMemberData> _entries = new List<PartyMemberData>();
        private readonly List<MemberRow> _rows = new List<MemberRow>();
        private readonly Dictionary<StatType, (Label value, Label bonus)> _stats = new Dictionary<StatType, (Label, Label)>();

        private Label _activeCount;
        private ScrollView _list;

        private VisualElement _detail;
        private Emblem _detailEmblem;
        private Label _detailName;
        private Diamond _detailMark;
        private Label _detailElement;
        private Label _detailRole;
        private Label _detailHp;
        private Label _detailHpMax;
        private VisualElement _detailHpBar;
        private Label _detailLevel;
        private Label _detailToNext;
        private VisualElement _detailXpBar;
        private Label _maxAp;
        private Label _hits;
        private Label _hitsBonus;
        private Label _fieldSkill;
        private Label _lumina;
        private Label _skillPoints;
        private ActionCard _attack;
        private ActionCard _surge;

        private int _cursor;
        /// <summary>The benched member picked up to swap into the full active party, or null.</summary>
        private PartyMemberData _held;

        public override string Title => "Party";
        public override bool PerMember => false;

        private PartyMemberData Selected => _cursor >= 0 && _cursor < _entries.Count ? _entries[_cursor] : null;
        private bool PartyFull => Party.Active.Count >= Party.MaxActive;

        #region Building
        protected override void Build()
        {
            var main = MenuUi.Box(Root, "party-main");
            var head = MenuUi.Heading(main, "Party").parent;
            _activeCount = MenuUi.Text(head, "", "mhead__other");
            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList("party-list");
            _list.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            // The cursor's stepped-out card would otherwise count as horizontal overflow.
            _list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            main.Add(_list);

            var side = MenuUi.Box(Root, "party-side");
            MenuUi.Heading(side, "Details");
            _detail = MenuUi.Plate(MenuUi.Box(side, "party-detail"), MenuUi.SmallCorner, small: true);

            var who = MenuUi.Box(_detail, "party-detail__who");
            _detailEmblem = MenuUi.Badge(who, 52f, "", null, Element.Light);
            _detailEmblem.AddToClassList("party-detail__emblem");
            var titles = MenuUi.Box(who, "party-detail__titles");
            _detailName = MenuUi.Text(titles, "", "party-detail__name");
            var tags = MenuUi.Box(titles, "status-tags");
            _detailMark = MenuUi.Mark(tags, Color.white, 11f);
            _detailElement = MenuUi.Text(tags, "", "status-tag", "status-tag--element");
            _detailRole = MenuUi.Text(tags, "", "status-tag");

            var gauges = MenuUi.Box(_detail, "party-detail__gauges");
            var health = MenuUi.Box(gauges, "party-detail__gauge", "party-detail__gauge--left");
            (_detailHp, _detailHpMax) = MenuUi.Gauge(health, "HP", "mgauge__tag--hp");
            _detailHpBar = MenuUi.Bar(health, "bar--hp");
            var level = MenuUi.Box(gauges, "party-detail__gauge");
            var levelRow = MenuUi.Box(level, "mgauge");
            MenuUi.Text(levelRow, "LV", "mgauge__tag", "mgauge__tag--xp");
            _detailLevel = MenuUi.Text(levelRow, "", "mgauge__value");
            _detailToNext = MenuUi.Text(levelRow, "", "mgauge__max");
            _detailXpBar = MenuUi.Bar(level, "bar--xp");

            var columns = MenuUi.Box(_detail, "party-detail__columns");
            var attributes = MenuUi.Box(columns, "party-detail__column", "party-detail__column--left");
            MenuUi.Heading(attributes, "Attributes");
            foreach (var stat in Stats) _stats[stat] = Row(attributes, stat.ToString());

            var abilities = MenuUi.Box(columns, "party-detail__column");
            MenuUi.Heading(abilities, "Growth");
            _maxAp = Row(abilities, "Max AP").value;
            (_hits, _hitsBonus) = Row(abilities, "Hits per Attack");
            _fieldSkill = Row(abilities, "Field Skill", true).value;
            _lumina = Row(abilities, "Lumina Points", true).value;
            _skillPoints = Row(abilities, "Skill Points", true).value;

            MenuUi.Heading(_detail, "Unique Actions");
            var actions = MenuUi.Box(_detail, "party-detail__actions");
            _attack = Card(actions, "Basic Attack", "action-card__kind--attack");
            _surge = Card(actions, "Surge", "action-card__kind--surge");
            _surge.Name.parent.AddToClassList("action-card--last");
        }

        private static (Label value, Label bonus) Row(VisualElement parent, string caption, bool text = false)
        {
            var row = MenuUi.Box(parent, "stat-row");
            MenuUi.Text(row, caption, "stat-row__name");
            var value = text ? MenuUi.Text(row, "", "stat-row__value", "stat-row__value--text") : MenuUi.Text(row, "", "stat-row__value");
            var bonus = text ? null : MenuUi.Text(row, "", "stat-row__bonus");
            return (value, bonus);
        }

        private static ActionCard Card(VisualElement parent, string kind, string kindClass)
        {
            var card = MenuUi.Plate(MenuUi.Box(parent, "action-card"), MenuUi.SmallCorner, small: true);
            var top = MenuUi.Box(card, "action-card__top");
            MenuUi.Text(top, kind.ToUpperInvariant(), "action-card__kind", kindClass);
            var state = MenuUi.Text(top, "", "action-card__state");
            var name = MenuUi.Text(card, "", "action-card__name");
            var text = MenuUi.Text(card, "", "action-card__text");
            return new ActionCard { Name = name, State = state, Text = text };
        }

        private MemberRow CreateRow()
        {
            var root = MenuUi.Row(_list, "party-row");
            root.pickingMode = PickingMode.Position;
            MenuUi.Text(root, "▶", "table-row__pointer");
            var index = MenuUi.Text(root, "", "party-row__index");
            var figure = new CharacterFigure(34f, 1f);
            figure.AddToClassList("party-row__figure");
            root.Add(figure);

            var info = MenuUi.Box(root, "party-row__info");
            var top = MenuUi.Box(info, "party-row__top");
            var name = MenuUi.Text(top, "", "party-row__name");

            var level = MenuUi.Text(root, "", "party-row__level");
            var gauge = MenuUi.Box(root, "party-row__gauge");
            var (hp, hpMax) = MenuUi.Gauge(gauge, "HP", "mgauge__tag--hp");
            var bar = MenuUi.Bar(gauge, "bar--hp", "party-row__bar");

            var row = new MemberRow
            {
                Root = root, Index = index, Figure = figure, Name = name,
                Level = level, Hp = hp, HpMax = hpMax, Bar = bar
            };
            root.RegisterCallback<PointerDownEvent>(_ => OnRowClicked(row));
            return row;
        }
        #endregion

        #region Drawing
        public override void Refresh()
        {
            if (Party == null) return;

            CollectEntries();
            if (_held != null && !_entries.Contains(_held)) _held = null;
            _cursor = Mathf.Clamp(_cursor, 0, Mathf.Max(0, _entries.Count - 1));
            _activeCount.text = $"{Party.Active.Count(member => member != null)} / {Party.MaxActive} ACTIVE";

            while (_rows.Count < _entries.Count) _rows.Add(CreateRow());
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var shown = i < _entries.Count;
                row.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (shown) Fill(row, _entries[i]);
            }

            UpdateCursor();
        }

        private void CollectEntries()
        {
            _entries.Clear();
            _entries.AddRange(Party.Active.Where(member => member != null));
            _entries.AddRange(Party.Benched.Where(member => member != null));
        }

        private void Fill(MemberRow row, PartyMemberData member)
        {
            var slot = IndexOfActive(member);
            var active = slot >= 0;
            MenuUi.SetClass(row.Root, "party-row--active", active);
            row.Index.text = active ? (slot + 1).ToString() : "";

            row.Figure.SetCharacter(member);
            row.Name.text = member.CharacterName;
            row.Level.text = $"LV {Party.State.GetProgress(member)?.Level ?? 1}";
            var stats = Party.CreateCombatant(member, 0).Stats;
            row.Hp.text = stats.CurrentHealth.ToString("N0");
            row.HpMax.text = $"/ {stats.MaxHealth:N0}";
            MenuUi.SetBar(row.Bar, stats.MaxHealth > 0 ? (float)stats.CurrentHealth / stats.MaxHealth : 0f);
        }

        private int IndexOfActive(PartyMemberData member)
        {
            var active = Party.Active;
            for (var i = 0; i < active.Count; i++)
                if (active[i] == member) return i;
            return -1;
        }

        private void UpdateCursor()
        {
            for (var i = 0; i < _entries.Count && i < _rows.Count; i++)
            {
                var row = _rows[i];
                var selected = Menu.InScreen && i == _cursor;
                var held = _entries[i] == _held;
                var active = IndexOfActive(_entries[i]) >= 0;
                MenuUi.SetClass(row.Root, "mrow--selected", selected);
                MenuUi.SetClass(row.Root, "table-row--held", held);
                row.Figure.SetRim(selected || held || active ? HudColors.Accent : HudColors.Rail, selected ? 2.5f : 2f);
                row.Figure.Running = selected;
            }

            if (_cursor >= 0 && _cursor < _rows.Count && Menu.InScreen) _list.ScrollTo(_rows[_cursor].Root);
            RefreshDetail(Selected);
        }

        private void RefreshDetail(PartyMemberData member)
        {
            _detail.style.visibility = member != null ? Visibility.Visible : Visibility.Hidden;
            if (member == null) return;

            var progress = Party.State.GetProgress(member);
            var loadout = Party.State.GetLoadout(member);
            var tree = Party.State.GetSkillTree(member);
            var unit = Party.CreateCombatant(member, 0);
            var stats = unit.Stats;
            var color = MenuUi.ElementColor(member.Element);

            _detailEmblem.SetCharacter(member.CharacterName, member.Portrait, color);
            _detailEmblem.SetRim(HudColors.Accent, 2.5f);
            _detailName.text = member.CharacterName;
            _detailMark.Fill = color;
            _detailElement.text = member.Element.ToString().ToUpperInvariant();
            _detailElement.style.color = color;
            _detailRole.text = MenuUi.Pretty(Party.GetRole(member)).ToUpperInvariant();
            MenuUi.SetClass(_detailRole, "status-tag--active", Party.GetRole(member) == PartyRole.Active);

            _detailHp.text = stats.CurrentHealth.ToString("N0");
            _detailHpMax.text = $"/ {stats.MaxHealth:N0}";
            MenuUi.SetBar(_detailHpBar, stats.MaxHealth > 0 ? (float)stats.CurrentHealth / stats.MaxHealth : 0f);
            _detailLevel.text = (progress?.Level ?? 1).ToString();
            var maxed = progress == null || progress.IsMaxLevel;
            _detailToNext.text = maxed ? "MAX" : $"{progress.XpToNextLevel - progress.Xp} to next";
            MenuUi.SetBar(_detailXpBar, maxed ? 1f : progress.XpToNextLevel > 0 ? (float)progress.Xp / progress.XpToNextLevel : 0f);

            foreach (var stat in Stats)
            {
                var final = stats.FinalStats[stat];
                SetValue(_stats[stat], final.ToString(), Bonus(final - member.BaseStats[stat]));
            }

            _maxAp.text = stats.MaxAp.ToString();
            var attack = member.BasicAttack;
            var hits = attack != null ? StatCalculator.GetAttackHitCount(attack, stats.Passives) : 0;
            SetValue((_hits, _hitsBonus), hits.ToString(), Bonus(hits - (attack != null ? attack.BaseHitCount : 0)));
            _fieldSkill.text = MenuUi.Pretty(member.ExplorationAbility);
            _lumina.text = loadout != null ? $"{loadout.LuminaPointsLeft} / {loadout.LuminaPoints}" : "-";
            _skillPoints.text = tree != null ? $"{tree.SkillPointsLeft} / {tree.SkillPoints}" : "No tree";

            var weapon = unit.Weapon;
            var element = unit.AttackElement != Element.None ? $"{unit.AttackElement} " : "";
            _attack.Name.text = weapon != null ? weapon.DisplayName : attack != null ? attack.DisplayName : "None";
            _attack.State.text = attack != null ? $"{hits} HIT{(hits == 1 ? "" : "S")}" : "";
            _attack.Text.text = attack == null
                ? "No basic attack assigned."
                : weapon != null
                    ? $"{hits} hit{(hits == 1 ? "" : "s")} of {unit.AttackPower:0.#} {element}power from {weapon.DisplayName}. Each landed hit grants AP."
                    : Describe(attack, $"{hits} hit{(hits == 1 ? "" : "s")} of {unit.AttackPower:0.#} {element}power. Each landed hit grants AP.");

            var surge = member.Surge;
            _surge.Name.text = surge != null ? surge.DisplayName : "None";
            var unlocked = surge != null && (surge.StartsUnlocked || (tree != null && tree.UnlockedActions.Contains(surge)));
            _surge.State.text = surge == null ? "" : unlocked ? "READY" : "LOCKED";
            MenuUi.SetClass(_surge.State, "action-card__state--locked", surge != null && !unlocked);
            _surge.Text.text = surge == null
                ? "No Surge assigned."
                : unlocked ? Describe(surge, "") : Describe(surge, "") + " Learn it in the skill tree.";
        }

        private static void SetValue((Label value, Label bonus) labels, string value, string bonus)
        {
            labels.value.text = value;
            if (labels.bonus == null) return;
            labels.bonus.text = bonus ?? "";
            MenuUi.SetClass(labels.bonus, "stat-row__bonus--down", bonus != null && bonus.StartsWith("-"));
        }

        private static string Bonus(int difference) => difference > 0 ? $"+{difference}" : difference < 0 ? difference.ToString() : null;

        private static string Describe(CombatActionData action, string fallback)
        {
            var text = action.BuildDescription();
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
        #endregion

        #region Input
        /// <summary>Puts the cursor on <paramref name="member"/>, such as after their character pages close.</summary>
        public void Focus(PartyMemberData member)
        {
            if (Party == null || member == null) return;
            CollectEntries();
            var index = _entries.IndexOf(member);
            if (index >= 0) _cursor = index;
        }

        public override void Enter()
        {
            _cursor = Mathf.Clamp(_cursor, 0, Mathf.Max(0, _entries.Count - 1));
            UpdateCursor();
        }

        public override void Exit()
        {
            _held = null;
            UpdateCursor();
        }

        public override void Move(Vector2Int direction)
        {
            if (_entries.Count == 0 || direction.y == 0) return;
            _cursor = MenuNavigation.Wrap(_cursor, direction.y, _entries.Count);
            UpdateCursor();
        }

        public override void Confirm()
        {
            var member = Selected;
            if (member == null) return;

            if (_held != null)
            {
                DropOn(member);
                return;
            }

            Menu.OpenCharacter(member);
        }

        public override void Secondary()
        {
            var member = Selected;
            if (member == null) return;

            if (_held != null)
            {
                DropOn(member);
                return;
            }

            if (Party.GetRole(member) == PartyRole.Active)
            {
                if (!Party.TrySetRole(member, PartyRole.Benched)) return;
            }
            else if (PartyFull)
            {
                // No room: pick them up, then choose who they replace.
                _held = member;
                UpdateCursor();
                return;
            }
            else
            {
                Party.TrySetRole(member, PartyRole.Active);
            }

            Changed(member);
        }

        /// <summary>Picks up the highlighted member to switch places with another, or drops the held one here.</summary>
        public override void Switch()
        {
            var member = Selected;
            if (member == null) return;

            if (_held != null)
            {
                DropOn(member);
                return;
            }

            if (_entries.Count < 2) return;
            _held = member;
            UpdateCursor();
        }

        /// <summary>
        /// Switches the held member with <paramref name="target"/>: two active members trade slots, and an active and
        /// a benched member trade roles. Two benched members cannot switch.
        /// </summary>
        private void DropOn(PartyMemberData target)
        {
            var held = _held;
            if (target == held)
            {
                _held = null;
                UpdateCursor();
                return;
            }

            var heldActive = Party.GetRole(held) == PartyRole.Active;
            var targetActive = Party.GetRole(target) == PartyRole.Active;
            if (!heldActive && !targetActive) return;

            _held = null;
            if (!Party.TrySwap(held, target))
            {
                UpdateCursor();
                return;
            }

            Changed(held);
        }

        public override bool Cancel()
        {
            if (_held == null) return false;
            _held = null;
            UpdateCursor();
            return true;
        }

        /// <summary>Redraws after the formation changes, with the cursor following <paramref name="member"/>.</summary>
        private void Changed(PartyMemberData member)
        {
            Focus(member);
            Menu.PartyChanged();
        }

        private void OnRowClicked(MemberRow row)
        {
            var index = _rows.IndexOf(row);
            if (index < 0 || index >= _entries.Count) return;

            if (!Menu.InScreen)
            {
                _cursor = index;
                Menu.EnterScreen();
                return;
            }

            if (_cursor == index || _held != null)
            {
                _cursor = index;
                Confirm();
            }
            else
            {
                _cursor = index;
                UpdateCursor();
            }

            Menu.RefreshChrome();
        }

        public override IEnumerable<(MenuButton button, string label)> Prompts()
        {
            if (_held != null)
            {
                if (Selected != _held) yield return (MenuButton.Switch, "Switch");
                yield return (MenuButton.Cancel, "Put down");
                yield break;
            }

            var member = Selected;
            if (member != null)
            {
                yield return (MenuButton.Confirm, "Weapon / Pictos / Skills");
                var active = Party.GetRole(member) == PartyRole.Active;
                yield return (MenuButton.Secondary, active ? "To bench" : PartyFull ? "Swap in" : "Join party");
                if (_entries.Count > 1) yield return (MenuButton.Switch, "Switch");
            }

            yield return (MenuButton.Cancel, "Return");
        }
        #endregion
    }
}
