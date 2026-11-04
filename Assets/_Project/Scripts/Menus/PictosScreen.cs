using System.Collections.Generic;
using System.Linq;
using InElementum.Core.UI;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Stats;
using InElementum.Core.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Menus
{
    /// <summary>
    /// Pictos and Luminas, laid out like Clair Obscur's Pictos page: the party's pictos on the left, the member's three
    /// picto slots as diamonds with the selected picto's card, and the member's sheet with their Lumina budget on the
    /// right. Secondary switches between wearing pictos and equipping learned Luminas.
    /// </summary>
    public sealed class PictosScreen : MenuScreen
    {
        private enum Focus
        {
            Slots,
            List
        }

        private readonly List<PictoData> _entries = new List<PictoData>();
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<VisualElement> _slots = new List<VisualElement>();

        private Label _modeLabel;
        private ScrollView _list;
        private Label _empty;
        private VisualElement _slotColumn;
        private VisualElement _card;
        private Label _cardName;
        private Label _cardStats;
        private Label _cardText;
        private Label _cardFooter;
        private Label _hpMax;

        private Label _level;
        private VisualElement _xpBar;
        private Label _xpText;
        private VisualElement _hpBar;
        private Label _hpText;
        private readonly Dictionary<StatType, Label> _stats = new Dictionary<StatType, Label>();
        private Label _luminaPoints;
        private VisualElement _luminaList;

        private bool _luminaMode;
        private Focus _focus = Focus.Slots;
        private int _slot;
        private int _cursor;

        public override string Title => "Pictos";

        private PictoData Selected => _cursor >= 0 && _cursor < _entries.Count ? _entries[_cursor] : null;
        private Loadout Loadout => Member != null ? Party?.State.GetLoadout(Member) : null;

        protected override void Build()
        {
            var left = MenuUi.Box(Root, "pictos-left");
            _modeLabel = MenuUi.Heading(left, "Pictos");
            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.AddToClassList("picto-list");
            _list.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            left.Add(_list);
            _empty = MenuUi.Text(left, "", "inventory-empty");

            var middle = MenuUi.Box(Root, "pictos-middle");
            _card = MenuUi.Plate(MenuUi.Box(middle, "picto-card"), MenuUi.SmallCorner, small: true);
            _cardName = MenuUi.Text(_card, "", "picto-card__name");
            _cardStats = MenuUi.Text(_card, "", "picto-card__stats");
            _cardText = MenuUi.Text(_card, "", "picto-card__text");
            _cardFooter = MenuUi.Text(_card, "", "picto-card__footer");
            _slotColumn = MenuUi.Box(middle, "slot-column");

            var right = MenuUi.Box(Root, "pictos-right");
            // The tab bar names the member; the sheet starts at their level.
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
            var grid = MenuUi.Box(right, "pictos-stats");
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                var cell = MenuUi.Box(grid, "stat-row", "pictos-stat");
                MenuUi.Text(cell, stat.ToString(), "stat-row__name");
                _stats[stat] = MenuUi.Text(cell, "", "stat-row__value");
            }

            var luminaHeading = MenuUi.Heading(right, "Luminas");
            _luminaPoints = MenuUi.Text(luminaHeading.parent, "", "lumina-points");
            _luminaList = MenuUi.Box(right, "lumina-list");
        }

        public override void Refresh()
        {
            var member = Member;
            var loadout = Loadout;
            if (member == null || loadout == null) return;

            _modeLabel.text = _luminaMode ? "LUMINAS" : "PICTOS";

            var pictos = Party.State.Pictos;
            _entries.Clear();
            _entries.AddRange(_luminaMode
                ? pictos.Owned.Where(picto => picto != null).OrderByDescending(pictos.IsLearned).ThenBy(picto => picto.DisplayName)
                : pictos.Owned.Where(picto => picto != null).OrderBy(picto => picto.DisplayName));
            _cursor = _entries.Count == 0 ? -1 : Mathf.Clamp(_cursor, 0, _entries.Count - 1);
            _empty.text = _entries.Count == 0 ? "The party owns no pictos yet." : "";

            _list.Clear();
            _rows.Clear();
            for (var i = 0; i < _entries.Count; i++)
            {
                var picto = _entries[i];
                var row = MenuUi.Row(_list, "picto-row");
                row.pickingMode = PickingMode.Position;
                MenuUi.Box(row, "picto-row__equipped");
                var icon = MenuUi.Box(row, "picto-row__icon");
                var iconArt = MenuUi.Box(icon, "picto-row__art");
                MenuUi.SetSprite(iconArt, picto.Icon);
                MenuUi.Text(row, picto.DisplayName, "picto-row__name");

                if (_luminaMode)
                {
                    var learned = pictos.IsLearned(picto);
                    MenuUi.Text(row, learned ? $"{picto.LuminaCost} pt" : $"{pictos.GetMastery(picto)}/{picto.BattlesToMaster}", "picto-row__level");
                    MenuUi.SetClass(row, "picto-row--locked", !learned);
                    MenuUi.SetClass(row, "picto-row--equipped", loadout.HasLumina(picto));
                }
                else
                {
                    var wearer = pictos.GetWearer(picto)?.Member;
                    MenuUi.Text(row, pictos.IsLearned(picto) ? "Mastered" : $"{pictos.GetMastery(picto)}/{picto.BattlesToMaster}", "picto-row__level");
                    var badge = new Emblem(18f);
                    badge.AddToClassList("picto-row__wearer");
                    row.Add(badge);
                    if (wearer != null)
                    {
                        badge.SetCharacter(wearer.CharacterName, wearer.Portrait, MenuUi.ElementColor(wearer.Element));
                        badge.SetRim(wearer == member ? HudColors.Accent : HudColors.Rail, 1.5f);
                    }
                    else
                    {
                        badge.style.visibility = Visibility.Hidden;
                    }

                    MenuUi.SetClass(row, "picto-row--equipped", wearer == member);
                }

                var index = i;
                row.RegisterCallback<PointerDownEvent>(_ =>
                {
                    if (!Menu.InScreen) Menu.EnterScreen();
                    _focus = Focus.List;
                    if (_cursor == index) Confirm();
                    else _cursor = index;
                    UpdateCursor();
                });
                _rows.Add(row);
            }

            _slotColumn.style.display = _luminaMode ? DisplayStyle.None : DisplayStyle.Flex;
            _slotColumn.Clear();
            _slots.Clear();
            for (var i = 0; i < loadout.PictoSlots; i++)
            {
                var picto = i < loadout.Pictos.Count ? loadout.Pictos[i] : null;
                var slot = MenuUi.Box(_slotColumn, "slot");
                slot.pickingMode = PickingMode.Position;
                var diamond = MenuUi.Box(slot, "slot__diamond");
                var art = MenuUi.Box(diamond, "slot__icon");
                MenuUi.SetSprite(art, picto != null ? picto.Icon : null);
                var words = MenuUi.Box(slot, "slot__words");
                MenuUi.Text(words, $"SLOT {i + 1}", "slot__index");
                MenuUi.Text(words, picto != null ? picto.DisplayName : "Empty", "slot__label");
                MenuUi.SetClass(slot, "slot--empty", picto == null);
                var index = i;
                slot.RegisterCallback<PointerDownEvent>(_ =>
                {
                    if (!Menu.InScreen) Menu.EnterScreen();
                    _slot = index;
                    _focus = Focus.List;
                    UpdateCursor();
                });
                _slots.Add(slot);
            }

            RefreshSheet(loadout);
            UpdateCursor();
        }

        private void RefreshSheet(Loadout loadout)
        {
            var member = Member;
            var progress = Party.State.GetProgress(member);
            var unit = Party.CreateCombatant(member, 0);

            _level.text = (progress?.Level ?? 1).ToString();
            var toNext = progress != null && !progress.IsMaxLevel ? progress.XpToNextLevel : 0;
            _xpText.text = toNext > 0 ? $"{progress.Xp} / {toNext} EXP" : "MAX";
            MenuUi.SetBar(_xpBar, toNext > 0 ? (float)progress.Xp / toNext : 1f);
            _hpText.text = unit.Stats.CurrentHealth.ToString("N0");
            _hpMax.text = $"/ {unit.Stats.MaxHealth:N0}";
            MenuUi.SetBar(_hpBar, unit.Stats.MaxHealth > 0 ? (float)unit.Stats.CurrentHealth / unit.Stats.MaxHealth : 0f);

            foreach (var pair in _stats)
                pair.Value.text = unit.Stats.FinalStats[pair.Key].ToString();

            _luminaPoints.text = $"{loadout.LuminaPointsSpent} / {loadout.LuminaPoints} PT";
            _luminaList.Clear();
            foreach (var lumina in loadout.Luminas)
            {
                var row = MenuUi.Row(_luminaList, "lumina-row");
                MenuUi.Text(row, lumina.DisplayName, "lumina-row__name");
                MenuUi.Text(row, lumina.LuminaCost.ToString(), "lumina-row__cost");
            }

            if (loadout.Luminas.Count == 0) MenuUi.Text(_luminaList, "None equipped", "lumina-row__none");
        }

        private void UpdateCursor()
        {
            var inScreen = Menu.InScreen;
            for (var i = 0; i < _rows.Count; i++)
                MenuUi.SetClass(_rows[i], "mrow--selected", inScreen && (_luminaMode || _focus == Focus.List) && i == _cursor);
            for (var i = 0; i < _slots.Count; i++)
            {
                MenuUi.SetClass(_slots[i], "slot--selected", inScreen && !_luminaMode && i == _slot);
                MenuUi.SetClass(_slots[i], "slot--target", inScreen && !_luminaMode && _focus == Focus.List && i == _slot);
            }

            if (_cursor >= 0 && _cursor < _rows.Count) _list.ScrollTo(_rows[_cursor]);

            var loadout = Loadout;
            var shown = !_luminaMode && _focus == Focus.Slots && loadout != null && _slot < loadout.Pictos.Count
                ? loadout.Pictos[_slot]
                : Selected;
            if (!inScreen) shown = loadout != null && loadout.Pictos.Count > 0 ? loadout.Pictos[0] : Selected;
            ShowCard(shown);
        }

        private void ShowCard(PictoData picto)
        {
            _card.style.visibility = picto != null ? Visibility.Visible : Visibility.Hidden;
            if (picto == null) return;

            var pictos = Party.State.Pictos;
            _cardName.text = picto.DisplayName;
            var stats = picto.Modifiers.Select(Format).ToList();
            _cardStats.text = stats.Count > 0 ? string.Join("   ", stats) : "No stats";
            var passive = picto.Passive != null && !string.IsNullOrWhiteSpace(picto.Passive.Description) ? picto.Passive.Description : "";
            _cardText.text = string.IsNullOrWhiteSpace(picto.Description) ? passive : picto.Description;
            var mastery = pictos.IsLearned(picto)
                ? $"Lumina learned. Costs {picto.LuminaCost} Lumina point{(picto.LuminaCost == 1 ? "" : "s")}."
                : $"Mastery {pictos.GetMastery(picto)}/{picto.BattlesToMaster} victories to learn its Lumina.";
            var wearer = pictos.GetWearer(picto)?.Member;
            _cardFooter.text = wearer != null ? $"{mastery}\nWorn by {wearer.CharacterName}." : mastery;
        }

        private static string Format(StatModifier modifier)
        {
            var sign = modifier.Value >= 0f ? "+" : "-";
            var amount = modifier.Kind == ModifierKind.Percent
                ? $"{Mathf.RoundToInt(Mathf.Abs(modifier.Value) * 100f)}%"
                : Mathf.RoundToInt(Mathf.Abs(modifier.Value)).ToString();
            return $"{modifier.Stat} {sign}{amount}";
        }

        public override void Enter()
        {
            _focus = _luminaMode ? Focus.List : Focus.Slots;
            if (_cursor < 0 && _entries.Count > 0) _cursor = 0;
            UpdateCursor();
        }

        public override void Exit()
        {
            _focus = Focus.Slots;
            UpdateCursor();
        }

        public override void Move(Vector2Int direction)
        {
            if (!_luminaMode && direction.x != 0)
            {
                _focus = direction.x < 0 ? Focus.List : Focus.Slots;
                UpdateCursor();
                return;
            }

            if (!_luminaMode && _focus == Focus.Slots)
            {
                if (_slots.Count > 0) _slot = MenuNavigation.Wrap(_slot, direction.y, _slots.Count);
            }
            else if (_entries.Count > 0)
            {
                _cursor = MenuNavigation.Wrap(_cursor, direction.y, _entries.Count);
            }

            UpdateCursor();
        }

        public override void Confirm()
        {
            var loadout = Loadout;
            if (loadout == null) return;

            if (_luminaMode)
            {
                ToggleLumina(loadout, Selected);
                return;
            }

            if (_focus == Focus.Slots)
            {
                _focus = Focus.List;
                var worn = _slot < loadout.Pictos.Count ? loadout.Pictos[_slot] : null;
                var index = worn != null ? _entries.IndexOf(worn) : -1;
                if (index >= 0) _cursor = index;
                UpdateCursor();
                return;
            }

            var picto = Selected;
            if (picto == null) return;

            if (_slot < loadout.Pictos.Count && loadout.Pictos[_slot] == picto)
                loadout.Unequip(picto);
            else
            {
                var error = loadout.EquipAt(_slot, picto, takeFromWearer: true);
                if (error != LoadoutError.None) return;

                AudioManager.PlayUi(AudioActionType.EquipPicto);
                _focus = Focus.Slots;
            }

            Menu.RefreshAll();
        }

        private void ToggleLumina(Loadout loadout, PictoData picto)
        {
            if (picto == null) return;

            if (loadout.HasLumina(picto))
            {
                loadout.UnequipLumina(picto);
            }
            else
            {
                var error = loadout.EquipLumina(picto);
                if (error != LoadoutError.None) return;

                AudioManager.PlayUi(AudioActionType.EquipPicto);
            }

            Menu.RefreshAll();
        }

        public override bool Cancel()
        {
            if (_luminaMode || _focus == Focus.Slots) return false;
            _focus = Focus.Slots;
            UpdateCursor();
            return true;
        }

        public override void Secondary()
        {
            _luminaMode = !_luminaMode;
            _focus = _luminaMode ? Focus.List : Focus.Slots;
            _cursor = 0;
            Menu.RefreshAll();
        }

        public override IEnumerable<(MenuButton button, string label)> Prompts()
        {
            yield return (MenuButton.Secondary, _luminaMode ? "Pictos" : "Luminas");
            yield return (MenuButton.Confirm, _luminaMode ? "Equip Lumina" : _focus == Focus.Slots ? "Choose Slot" : "Equip Picto");
            yield return (MenuButton.Cancel, "Back");
        }
    }
}
