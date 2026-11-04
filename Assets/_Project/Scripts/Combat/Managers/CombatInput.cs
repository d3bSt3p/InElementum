using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InElementum.Combat.Managers
{
    /// <summary>The combat actions the HUD shows button prompts for.</summary>
    public enum CombatButton
    {
        Confirm,
        Cancel,
        Dodge,
        Parry,
        Qte,
        Attack,
        Abilities,
        Items,
        Surge,
        Skip
    }

    /// <summary>
    /// Reads the "Combat" action map once per frame, so menus and prompts see each press once.
    /// Navigation fires on a fresh press and then repeats while held.
    /// Also tracks whether the player is on a controller or on mouse and keyboard, so prompts can show the right
    /// buttons: a controller takes over when it is connected or used, mouse and keyboard when they are used.
    /// </summary>
    public sealed class CombatInput : IDisposable
    {
        private enum PadStyle
        {
            Xbox,
            PlayStation,
            Nintendo
        }

        private const float RepeatDelay = 0.35f;
        private const float RepeatInterval = 0.1f;

        private readonly InputActionMap _map;
        private readonly bool _ownsMap;
        private readonly InputAction _navigate;
        private readonly InputAction _confirm;
        private readonly InputAction _cancel;
        private readonly InputAction _dodge;
        private readonly InputAction _parry;
        private readonly InputAction _qte;
        // Command menu shortcuts. Optional, so an action map without them still works.
        private readonly InputAction _attack;
        private readonly InputAction _abilities;
        private readonly InputAction _items;
        private readonly InputAction _surge;
        private readonly InputAction _skip;

        private readonly Dictionary<(CombatButton, bool, bool, PadStyle), string> _glyphs = new();

        private Vector2Int _heldDirection;
        private float _nextRepeat;

        public CombatInput(InputActionAsset asset)
        {
            UsingGamepad = Gamepad.current != null;
            InputSystem.onDeviceChange += OnDeviceChange;

            _map = asset != null ? asset.FindActionMap("Combat") : null;
            if (_map == null)
            {
                Debug.LogWarning("[CombatInput] No 'Combat' action map found. Using built-in keyboard and gamepad bindings.");
                _map = CreateFallbackMap();
                _ownsMap = true;
            }

            _navigate = _map.FindAction("Navigate", true);
            _confirm = _map.FindAction("Confirm", true);
            _cancel = _map.FindAction("Cancel", true);
            _dodge = _map.FindAction("Dodge", true);
            _parry = _map.FindAction("Parry", true);
            _qte = _map.FindAction("QTE", true);
            _attack = _map.FindAction("Attack");
            _abilities = _map.FindAction("Abilities");
            _items = _map.FindAction("Items");
            _surge = _map.FindAction("Surge");
            _skip = _map.FindAction("Skip");
            _map.Enable();
        }

        /// <summary>This frame's navigation step: a unit direction on press or repeat, otherwise zero.</summary>
        public Vector2Int Navigate { get; private set; }
        public bool Confirm { get; private set; }
        public bool Cancel { get; private set; }
        public bool Dodge { get; private set; }
        public bool Parry { get; private set; }
        public bool Qte { get; private set; }
        /// <summary>
        /// Command menu shortcuts: attack, jump straight to Abilities, Items, or Surge, or skip the turn. Attack
        /// shares its button with Confirm, so the root menu checks shortcuts first.
        /// </summary>
        public bool Attack { get; private set; }
        public bool OpenAbilities { get; private set; }
        public bool OpenItems { get; private set; }
        public bool OpenSurge { get; private set; }
        public bool SkipTurn { get; private set; }

        /// <summary>Mouse position in screen pixels, origin bottom left.</summary>
        public Vector2 PointerPosition { get; private set; }
        /// <summary>True on frames the mouse moved, so a resting pointer never overrides keyboard navigation.</summary>
        public bool PointerMoved { get; private set; }
        public bool Click { get; private set; }
        public bool RightClick { get; private set; }
        /// <summary>Scroll wheel this frame: positive is up.</summary>
        public float Scroll { get; private set; }

        /// <summary>True while prompts should show controller buttons rather than mouse and keyboard ones.</summary>
        public bool UsingGamepad { get; private set; }

        private bool _hasPointer;

        /// <summary>Call once per frame before anything reads input.</summary>
        public void Update()
        {
            UpdatePointer();

            Confirm = Pressed(_confirm);
            Cancel = Pressed(_cancel);
            Dodge = Pressed(_dodge);
            Parry = Pressed(_parry);
            Qte = Pressed(_qte);
            Attack = Pressed(_attack);
            OpenAbilities = Pressed(_abilities);
            OpenItems = Pressed(_items);
            OpenSurge = Pressed(_surge);
            SkipTurn = Pressed(_skip);

            var stick = _navigate.ReadValue<Vector2>();
            if (stick.sqrMagnitude >= 0.25f) NoteDevice(_navigate.activeControl?.device);
            if (Click || RightClick || PointerMoved || Scroll != 0f) UsingGamepad = false;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) UsingGamepad = false;

            var direction = ToDirection(stick);
            Navigate = Vector2Int.zero;
            if (direction != _heldDirection)
            {
                _heldDirection = direction;
                Navigate = direction;
                _nextRepeat = Time.unscaledTime + RepeatDelay;
            }
            else if (direction != Vector2Int.zero && Time.unscaledTime >= _nextRepeat)
            {
                Navigate = direction;
                _nextRepeat = Time.unscaledTime + RepeatInterval;
            }
        }

        private bool Pressed(InputAction action)
        {
            if (action == null || !action.WasPressedThisFrame()) return false;
            NoteDevice(action.activeControl?.device);
            return true;
        }

        private void NoteDevice(InputDevice device)
        {
            if (device is Gamepad) UsingGamepad = true;
            else if (device is Keyboard || device is Mouse) UsingGamepad = false;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad)) return;

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    UsingGamepad = true;
                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    UsingGamepad = AnyOtherGamepad(device);
                    break;
                default:
                    return;
            }

            // A different controller may have a different button style.
            _glyphs.Clear();
        }

        private static bool AnyOtherGamepad(InputDevice except)
        {
            foreach (var pad in Gamepad.all)
            {
                if (pad != except && pad.added) return true;
            }

            return false;
        }

        #region Glyphs
        /// <summary>
        /// Keycap text for <paramref name="button"/> on the current scheme, read from its bindings so it follows any
        /// change to the action map: "A" or "✕" on a controller, "LMB" or "E" on mouse and keyboard, or "LMB / E" for an action bound to both.
        /// <paramref name="primaryOnly"/> keeps just the first, the mouse button when the action has one.
        /// Null when the action has nothing bound on the current scheme, such as a controller-only shortcut while on
        /// mouse and keyboard.
        /// </summary>
        public string Glyph(CombatButton button, bool primaryOnly = false)
        {
            var style = CurrentPadStyle();
            var key = (button, UsingGamepad, primaryOnly, style);
            if (_glyphs.TryGetValue(key, out var glyph)) return glyph;

            var action = ActionFor(button);
            glyph = action != null ? BuildGlyph(action, UsingGamepad, primaryOnly, style) : null;
            _glyphs[key] = glyph;
            return glyph;
        }

        private InputAction ActionFor(CombatButton button) => button switch
        {
            CombatButton.Confirm => _confirm,
            CombatButton.Cancel => _cancel,
            CombatButton.Dodge => _dodge,
            CombatButton.Parry => _parry,
            CombatButton.Qte => _qte,
            CombatButton.Attack => _attack,
            CombatButton.Abilities => _abilities,
            CombatButton.Items => _items,
            CombatButton.Surge => _surge,
            _ => _skip
        };

        private static string BuildGlyph(InputAction action, bool gamepad, bool primaryOnly, PadStyle style)
        {
            string pad = null, mouse = null, key = null;
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;

                var path = binding.effectivePath;
                var layout = InputControlPath.TryGetDeviceLayout(path);
                if (string.IsNullOrEmpty(layout)) continue;

                if (IsLayout(layout, "Gamepad")) pad ??= PadGlyph(path, style);
                else if (IsLayout(layout, "Mouse")) mouse ??= MouseGlyph(path);
                else if (IsLayout(layout, "Keyboard")) key ??= KeyGlyph(action, i);
            }

            if (gamepad && pad != null) return pad;
            if (primaryOnly || mouse == null || key == null) return mouse ?? key;
            return $"{mouse} / {key}";
        }

        private static bool IsLayout(string layout, string baseLayout) =>
            string.Equals(layout, baseLayout, StringComparison.OrdinalIgnoreCase) ||
            InputSystem.IsFirstLayoutBasedOnSecond(layout, baseLayout);

        private static PadStyle CurrentPadStyle()
        {
            var layout = Gamepad.current?.layout;
            if (string.IsNullOrEmpty(layout)) return PadStyle.Xbox;
            if (IsLayout(layout, "DualShockGamepad")) return PadStyle.PlayStation;
            if (IsLayout(layout, "SwitchProControllerHID")) return PadStyle.Nintendo;
            return PadStyle.Xbox;
        }

        private static string ControlName(string path)
        {
            var slash = path.IndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        private static string PadGlyph(string path, PadStyle style)
        {
            var control = ControlName(path);
            var playStation = style == PadStyle.PlayStation;
            var nintendo = style == PadStyle.Nintendo;
            switch (control)
            {
                case "buttonSouth": return playStation ? "✕" : nintendo ? "B" : "A";
                case "buttonEast": return playStation ? "○" : nintendo ? "A" : "B";
                case "buttonWest": return playStation ? "□" : nintendo ? "Y" : "X";
                case "buttonNorth": return playStation ? "△" : nintendo ? "X" : "Y";
                case "leftShoulder": return playStation ? "L1" : nintendo ? "L" : "LB";
                case "rightShoulder": return playStation ? "R1" : nintendo ? "R" : "RB";
                case "leftTrigger": return playStation ? "L2" : nintendo ? "ZL" : "LT";
                case "rightTrigger": return playStation ? "R2" : nintendo ? "ZR" : "RT";
                case "leftStickPress": return playStation ? "L3" : "LS";
                case "rightStickPress": return playStation ? "R3" : "RS";
                case "start": return playStation ? "Options" : nintendo ? "+" : "Menu";
                case "select": return playStation ? "Share" : nintendo ? "−" : "View";
                case "dpad": return "D-Pad";
                case "dpad/up": return "D-Pad ↑";
                case "dpad/down": return "D-Pad ↓";
                case "dpad/left": return "D-Pad ←";
                case "dpad/right": return "D-Pad →";
                case "leftStick": return "L-Stick";
                case "rightStick": return "R-Stick";
                default: return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }

        private static string MouseGlyph(string path)
        {
            switch (ControlName(path))
            {
                case "leftButton": return "LMB";
                case "rightButton": return "RMB";
                case "middleButton": return "MMB";
                default: return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }

        /// <summary>The key as printed on the player's keyboard layout, with long names shortened to fit a keycap.</summary>
        private static string KeyGlyph(InputAction action, int bindingIndex)
        {
            var text = action.GetBindingDisplayString(bindingIndex);
            switch (text)
            {
                case "Escape": return "Esc";
                case "Backspace": return "Bksp";
                default: return text;
            }
        }
        #endregion

        private void UpdatePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                PointerMoved = Click = RightClick = false;
                Scroll = 0f;
                return;
            }

            var position = mouse.position.ReadValue();
            PointerMoved = _hasPointer && (position - PointerPosition).sqrMagnitude > 0.25f;
            PointerPosition = position;
            _hasPointer = true;

            Click = mouse.leftButton.wasPressedThisFrame;
            RightClick = mouse.rightButton.wasPressedThisFrame;
            Scroll = mouse.scroll.ReadValue().y;
        }

        private static Vector2Int ToDirection(Vector2 value)
        {
            if (value.sqrMagnitude < 0.25f) return Vector2Int.zero;
            return Mathf.Abs(value.x) > Mathf.Abs(value.y)
                ? new Vector2Int(value.x > 0 ? 1 : -1, 0)
                : new Vector2Int(0, value.y > 0 ? 1 : -1);
        }

        private static InputActionMap CreateFallbackMap()
        {
            var map = new InputActionMap("Combat");
            map.AddAction("Navigate", InputActionType.Value)
                .AddCompositeBinding("2DVector")
                .With("up", "<Keyboard>/upArrow").With("down", "<Keyboard>/downArrow")
                .With("left", "<Keyboard>/leftArrow").With("right", "<Keyboard>/rightArrow");
            map.FindAction("Navigate").AddBinding("<Gamepad>/dpad");
            var confirm = map.AddAction("Confirm", binding: "<Keyboard>/e");
            confirm.AddBinding("<Keyboard>/enter");
            confirm.AddBinding("<Gamepad>/buttonSouth");
            var cancel = map.AddAction("Cancel", binding: "<Keyboard>/q");
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");
            map.AddAction("Dodge", binding: "<Mouse>/leftButton").AddBinding("<Gamepad>/buttonEast");
            map.AddAction("Parry", binding: "<Mouse>/rightButton").AddBinding("<Gamepad>/rightShoulder");
            map.AddAction("QTE", binding: "<Mouse>/leftButton").AddBinding("<Gamepad>/buttonSouth");
            map.AddAction("Attack", binding: "<Gamepad>/buttonSouth");
            map.AddAction("Abilities", binding: "<Gamepad>/buttonWest");
            map.AddAction("Items", binding: "<Gamepad>/buttonNorth");
            map.AddAction("Surge", binding: "<Gamepad>/rightTrigger");
            map.AddAction("Skip", binding: "<Gamepad>/rightStickPress");
            return map;
        }

        public void Dispose()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            _map.Disable();
            if (_ownsMap) _map.Dispose();
        }
    }
}
