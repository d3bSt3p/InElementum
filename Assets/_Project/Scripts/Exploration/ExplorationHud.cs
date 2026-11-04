using System.Collections.Generic;
using System.Text.RegularExpressions;
using InElementum.Combat.Characters;
using InElementum.Core.UI;
using InElementum.Data.Characters;
using InElementum.Encounters;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace InElementum.Exploration
{
    /// <summary>
    /// The exploration HUD, in UI Toolkit (layout in <c>Resources/UI/ExplorationHud.uxml</c>, look in
    /// <c>Exploration.uss</c>): the explored character's portrait, name, and HP in the top left, with the previous
    /// and next characters beside their trigger buttons on a controller; the controls strip in the bottom left; the
    /// character wheel; and a burst of the new character's element color over them on a switch. Reads the
    /// <see cref="CharacterSwitcher"/> and the room's party, and hides while the room is out of play (fading or in
    /// combat).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class ExplorationHud : MonoBehaviour
    {
        private const string LayoutName = "ExplorationHud";
        private const float PortraitSize = 80f;
        private const float WheelRadius = 165f;
        private const float WheelCenter = 220f;
        private const float SlotEmblemSize = 60f;
        private const float PopSeconds = 0.22f;
        private const float FlashSeconds = 0.35f;

        [Tooltip("Read for who is explored. Found in the scene when empty.")]
        [SerializeField] private CharacterSwitcher switcher;
        [Tooltip("Show the pause menu's key in the controls strip.")]
        [FormerlySerializedAs("showPartyPanelHint")]
        [SerializeField] private bool showMenuHint = true;

        private UIDocument _document;
        private VisualElement _hud;
        private VisualElement _character;
        private Emblem _portrait;
        private KeyPrompt _slotKey;
        private Label _name;
        private Label _hp;
        private Label _hpMax;
        private VisualElement _hpFill;
        private VisualElement _flash;
        private VisualElement _switchStrip;
        private KeyPrompt _previousKey;
        private KeyPrompt _nextKey;
        private Emblem _previous;
        private Emblem _next;
        private HintBar _hints;

        private VisualElement _wheel;
        private VisualElement _wheelSlots;
        private Diamond _pointer;
        private Label _wheelName;
        private Label _wheelElement;
        private Label _wheelCurrent;
        private readonly List<WheelSlot> _slots = new List<WheelSlot>();

        private PartyMemberData _shown;
        private float _poppedAt = float.NegativeInfinity;
        private int _maxHealth;
        private float _nextHealthRead;

        private sealed class WheelSlot
        {
            public VisualElement Root;
            public Emblem Emblem;
            public KeyPrompt Key;
            public PartyMemberData Member;
        }

        private void Awake()
        {
            _document = HudPanel.CreateDocument(transform, "Exploration", HudPanel.Layer.Exploration, "Exploration");
            var root = _document.rootVisualElement;
            var layout = HudPanel.Layout(LayoutName);
            if (layout == null)
            {
                Debug.LogError($"[ExplorationHud] Missing Resources/{HudPanel.ResourceFolder}{LayoutName}.uxml", this);
                enabled = false;
                return;
            }

            layout.CloneTree(root);
            root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
            Build(root);
        }

        private void Build(VisualElement root)
        {
            _hud = root.Q("exploration-hud");
            _flash = root.Q("switch-flash");
            _flash.style.backgroundImage = new StyleBackground(UiTextures.Radial);
            _character = root.Q("character");

            var band = root.Q("band");
            band.style.backgroundImage = new StyleBackground(UiTextures.FadeRight);
            band.style.unityBackgroundImageTintColor = HudColors.Panel.Alpha(0.85f);

            _portrait = new Emblem(PortraitSize);
            root.Q("portrait").Add(_portrait);
            _slotKey = new KeyPrompt(22f);
            root.Q("slot-key").Add(_slotKey);

            _name = root.Q<Label>("name");
            _hp = root.Q<Label>("hp");
            _hpMax = root.Q<Label>("hp-max");
            _hpFill = root.Q("hp-fill");

            _switchStrip = root.Q("switch-strip");
            _previousKey = new KeyPrompt(24f);
            _nextKey = new KeyPrompt(24f);
            root.Q("switch-previous-key").Add(_previousKey);
            root.Q("switch-next-key").Add(_nextKey);
            _previous = new Emblem(22f);
            _next = new Emblem(22f);
            root.Q("switch-previous").Add(_previous);
            root.Q("switch-next").Add(_next);

            _wheel = root.Q("wheel");
            _wheelSlots = root.Q("wheel-slots");
            var glow = root.Q("wheel-glow");
            glow.style.backgroundImage = new StyleBackground(UiTextures.Radial);
            glow.style.unityBackgroundImageTintColor = new Color(0.1f, 0.2f, 0.45f, 0.55f);
            _pointer = new Diamond(16f).Absolute();
            _pointer.Fill = HudColors.Accent;
            root.Q("wheel-pointer").Add(_pointer);
            _wheelName = root.Q<Label>("wheel-name");
            _wheelElement = root.Q<Label>("wheel-element");
            _wheelCurrent = root.Q<Label>("wheel-current");

            _hints = new HintBar();
            root.Add(_hints);
        }

        private void LateUpdate()
        {
            if (_hud == null) return;
            if (switcher == null) switcher = FindAnyObjectByType<CharacterSwitcher>();

            var director = EncounterDirector.Instance;
            var party = director != null ? director.Party : null;
            var member = switcher != null ? switcher.Current : null;
            // The pause menu covers the room and brings its own key prompts.
            var visible = member != null && party != null && !director.Busy && !Menus.GameMenu.IsOpen;
            _hud.Show(visible);
            _hints.Show(visible);
            if (!visible) return;

            var input = switcher.Input;
            var gamepad = input != null && input.UsingGamepad;
            UpdateCharacter(member, party, gamepad, input);
            UpdateWheel(gamepad);
            UpdateHints(input, gamepad);
        }

        #region Character
        private void UpdateCharacter(PartyMemberData member, PartyService party, bool gamepad, ExplorationInput input)
        {
            var roster = switcher.Roster;
            if (member != _shown)
            {
                if (_shown != null) _poppedAt = Time.unscaledTime;
                _shown = member;
                _portrait.SetCharacter(member.CharacterName, member.Portrait, ElementColor(member));
                _portrait.SetRim(HudColors.Accent, 2.5f);
                _name.text = member.CharacterName;
                _flash.style.unityBackgroundImageTintColor = Color.Lerp(ElementColor(member), Color.white, 0.35f);
                _nextHealthRead = 0f;
            }

            // Max HP depends on level and pictos, which change only between fights; read it now and then.
            if (Time.unscaledTime >= _nextHealthRead)
            {
                _maxHealth = party.GetMaxHealth(member);
                _nextHealthRead = Time.unscaledTime + 0.5f;
            }

            var health = Mathf.Min(party.GetHealth(member) ?? _maxHealth, _maxHealth);
            _hp.text = health.ToString("N0");
            _hpMax.text = $"/ {_maxHealth:N0}";
            _hpFill.style.width = Length.Percent(_maxHealth > 0 ? 100f * health / _maxHealth : 0f);

            // A short pop when the character changes.
            var pop = Mathf.Clamp01((Time.unscaledTime - _poppedAt) / PopSeconds);
            var scale = 1f + 0.25f * (1f - Hud.EaseOutCubic(pop));
            _portrait.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _character.style.opacity = Mathf.Lerp(0.4f, 1f, Hud.EaseOutCubic(pop * 1.5f));
            UpdateFlash();

            // On a keyboard the portrait wears its number key; on a controller the triggers flank the neighbours.
            var slot = roster.CurrentIndex;
            _slotKey.Show(!gamepad && slot < 9);
            if (!gamepad && slot < 9) _slotKey.Set((slot + 1).ToString(), false);

            var showStrip = gamepad && roster.Count > 1 && input != null;
            _switchStrip.Show(showStrip);
            if (!showStrip) return;

            var previous = roster.Members[(slot - 1 + roster.Count) % roster.Count];
            var next = roster.Members[(slot + 1) % roster.Count];
            _previous.SetCharacter(previous.CharacterName, previous.Portrait, ElementColor(previous));
            _previous.SetRim(HudColors.Rail);
            _next.SetCharacter(next.CharacterName, next.Portrait, ElementColor(next));
            _next.SetRim(HudColors.Rail);
            _previousKey.Set(input.PreviousCharacterGlyph, true);
            _nextKey.Set(input.NextCharacterGlyph, true);
        }
        #endregion

        /// <summary>The switch burst: a soft disc of the new element's color that swells out of the character and fades.</summary>
        private void UpdateFlash()
        {
            var t = (Time.unscaledTime - _poppedAt) / FlashSeconds;
            var on = t >= 0f && t < 1f;
            _flash.Show(on);
            if (!on) return;

            var camera = Camera.main;
            var panel = _flash.panel;
            if (camera != null && panel != null)
            {
                var screen = camera.WorldToScreenPoint(switcher.transform.position);
                _flash.Move(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y)));
            }

            var eased = Hud.EaseOutCubic(t);
            var scale = Mathf.Lerp(0.4f, 1.3f, eased);
            _flash.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _flash.style.opacity = 0.75f * (1f - eased);
        }

        #region Wheel
        private void UpdateWheel(bool gamepad)
        {
            var open = switcher.WheelOpen;
            _wheel.Show(open);
            if (!open) return;

            var roster = switcher.Roster;
            if (_slots.Count != roster.Count || !SameMembers(roster)) BuildSlots(roster);

            var highlight = switcher.WheelHighlight;
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var selected = i == highlight;
                var current = i == roster.CurrentIndex;
                slot.Emblem.SetRim(selected ? HudColors.Accent : current ? HudColors.PartyHealth : HudColors.Rail, selected ? 3f : 2f);
                var scale = selected ? 1.22f : 1f;
                slot.Emblem.style.scale = new Scale(new Vector3(scale, scale, 1f));
                slot.Root.style.opacity = selected || current ? 1f : 0.8f;
                slot.Key.Show(!gamepad && i < 9);
                if (!gamepad && i < 9) slot.Key.Set((i + 1).ToString(), false, 20f);
            }

            var aimed = highlight >= 0 && highlight < roster.Count ? roster.Members[highlight] : null;
            _pointer.Show(aimed != null);
            if (aimed == null) return;

            var direction = CharacterRoster.SlotDirection(highlight, roster.Count);
            _pointer.CenterOn(new Vector2(WheelCenter + direction.x * 92f, WheelCenter - direction.y * 92f));
            _wheelName.text = aimed.CharacterName;
            _wheelElement.text = aimed.Element.ToString().ToUpperInvariant();
            _wheelElement.style.color = ElementColor(aimed);
            _wheelCurrent.Show(highlight == roster.CurrentIndex);
        }

        private bool SameMembers(CharacterRoster roster)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Member != roster.Members[i]) return false;
            }

            return true;
        }

        private void BuildSlots(CharacterRoster roster)
        {
            _wheelSlots.Clear();
            _slots.Clear();
            for (var i = 0; i < roster.Count; i++)
            {
                var member = roster.Members[i];
                var root = Hud.Box("ex-wheel__slot");
                root.pickingMode = PickingMode.Position;

                var direction = CharacterRoster.SlotDirection(i, roster.Count);
                var center = new Vector2(WheelCenter + direction.x * WheelRadius, WheelCenter - direction.y * WheelRadius);
                root.style.left = center.x - 42f;
                root.style.top = center.y - (SlotEmblemSize + 8f) * 0.5f;

                var emblem = new Emblem(SlotEmblemSize);
                emblem.SetCharacter(member.CharacterName, member.Portrait, ElementColor(member));
                root.Add(emblem);
                var key = new KeyPrompt(20f).Classes("ex-wheel__slot-key");
                root.Add(key);

                var index = i;
                root.RegisterCallback<PointerMoveEvent>(_ => switcher.Highlight(index));
                root.RegisterCallback<ClickEvent>(_ =>
                {
                    switcher.Highlight(index);
                    switcher.CloseWheel(true);
                });

                _wheelSlots.Add(root);
                _slots.Add(new WheelSlot { Root = root, Emblem = emblem, Key = key, Member = member });
            }
        }
        #endregion

        private void UpdateHints(ExplorationInput input, bool gamepad)
        {
            if (input == null) return;

            if (switcher.WheelOpen)
            {
                var hints = new List<HintBar.Hint>
                {
                    new HintBar.Hint(gamepad ? "R-Stick" : "Mouse", "Aim"),
                    new HintBar.Hint(input.JumpGlyph, "Select", () => switcher.CloseWheel(true)),
                    new HintBar.Hint(input.CancelGlyph, "Cancel", () => switcher.CloseWheel())
                };
                if (!gamepad) hints.Insert(1, new HintBar.Hint($"1-{Mathf.Min(9, switcher.Roster.Count)}", "Pick"));
                if (switcher.PicksOnRelease) hints.Insert(0, new HintBar.Hint(input.WheelGlyph, "Release to select"));
                _hints.Set(gamepad, hints.ToArray());
                return;
            }

            var list = new List<HintBar.Hint>
            {
                new HintBar.Hint(input.MoveGlyph, "Move"),
                new HintBar.Hint(input.JumpGlyph, "Jump"),
                new HintBar.Hint(input.FieldAttackGlyph, "Attack")
            };

            if (switcher.Roster.Count > 1)
            {
                list.Add(gamepad
                    ? new HintBar.Hint($"{input.PreviousCharacterGlyph} / {input.NextCharacterGlyph}", "Switch")
                    : new HintBar.Hint($"1-{Mathf.Min(9, switcher.Roster.Count)}", "Switch"));
                list.Add(new HintBar.Hint(input.WheelGlyph, gamepad ? "Characters" : "Hold: Characters", () => switcher.OpenWheel()));
            }

            var menu = Menus.GameMenu.Instance;
            if (showMenuHint && menu != null) list.Add(new HintBar.Hint(menu.Input.Glyph(Menus.MenuButton.Toggle), "Menu", () => menu.Show()));
            _hints.Set(gamepad, list.ToArray());
        }

        private static Color ElementColor(CharacterDataBase member) =>
            PlaceholderCharacter.ElementColor(member != null ? member.Element : Data.Enums.Element.None);

        /// <summary>The member's exploration ability as words: "Light Orb", "Horizontal Dash".</summary>
        public static string AbilityName(PartyMemberData member) =>
            member == null ? string.Empty : Regex.Replace(member.ExplorationAbility.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
    }
}
