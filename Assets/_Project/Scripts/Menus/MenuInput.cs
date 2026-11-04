using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InElementum.Menus
{
    /// <summary>
    /// Reads the "Menu" action map: Toggle opens and closes the menu, Navigate moves the cursor with auto-repeat,
    /// Confirm, Cancel, Previous/Next (switch member), PreviousTab/NextTab (switch a character's page), Secondary
    /// (party role, switch mode), and Switch (pick up and swap). Runs on
    /// unscaled time, since the open menu pauses the game.
    /// </summary>
    public sealed class MenuInput : IDisposable
    {
        private const float RepeatDelay = 0.35f;
        private const float RepeatInterval = 0.09f;
        private const float StickThreshold = 0.5f;

        private readonly InputActionMap _map;
        private readonly bool _ownsMap;
        private readonly InputAction _toggle;
        private readonly InputAction _navigate;
        private readonly InputAction _confirm;
        private readonly InputAction _cancel;
        private readonly InputAction _previous;
        private readonly InputAction _next;
        private readonly InputAction _secondary;
        private readonly InputAction _previousTab;
        private readonly InputAction _nextTab;
        private readonly InputAction _switch;

        private Vector2Int _heldDirection;
        private float _nextRepeat;

        public MenuInput(InputActionAsset asset)
        {
            _map = asset != null ? asset.FindActionMap("Menu") : null;
            if (_map == null)
            {
                Debug.LogWarning("[MenuInput] No 'Menu' map found in the input actions. Using built-in bindings.");
                _map = CreateFallbackMap();
                _ownsMap = true;
            }

            _toggle = _map.FindAction("Toggle", true);
            _navigate = _map.FindAction("Navigate", true);
            _confirm = _map.FindAction("Confirm", true);
            _cancel = _map.FindAction("Cancel", true);
            _previous = _map.FindAction("Previous", true);
            _next = _map.FindAction("Next", true);
            _secondary = _map.FindAction("Secondary", true);
            _previousTab = _map.FindAction("PreviousTab", true);
            _nextTab = _map.FindAction("NextTab", true);
            _switch = _map.FindAction("Switch", true);
            _map.Enable();
        }

        public bool TogglePressed => _toggle.WasPressedThisFrame();
        public bool ConfirmPressed => _confirm.WasPressedThisFrame();
        public bool CancelPressed => _cancel.WasPressedThisFrame();
        public bool PreviousPressed => _previous.WasPressedThisFrame();
        public bool NextPressed => _next.WasPressedThisFrame();
        public bool SecondaryPressed => _secondary.WasPressedThisFrame();
        public bool PreviousTabPressed => _previousTab.WasPressedThisFrame();
        public bool NextTabPressed => _nextTab.WasPressedThisFrame();
        public bool SwitchPressed => _switch.WasPressedThisFrame();

        /// <summary>Whether glyphs should name controller buttons: a pad is plugged in.</summary>
        public static bool UsingGamepad => Gamepad.current != null;

        /// <summary>
        /// The direction to move the cursor this frame, or zero: once on press, then repeating while held. Up is
        /// negative y, matching screen rows.
        /// </summary>
        public Vector2Int ReadMove()
        {
            var raw = _navigate.ReadValue<Vector2>();
            var direction = Vector2Int.zero;
            if (Mathf.Abs(raw.x) >= StickThreshold || Mathf.Abs(raw.y) >= StickThreshold)
            {
                direction = Mathf.Abs(raw.x) > Mathf.Abs(raw.y)
                    ? new Vector2Int(raw.x > 0f ? 1 : -1, 0)
                    : new Vector2Int(0, raw.y > 0f ? -1 : 1);
            }

            var now = Time.unscaledTime;
            if (direction == Vector2Int.zero)
            {
                _heldDirection = Vector2Int.zero;
                return Vector2Int.zero;
            }

            if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _nextRepeat = now + RepeatDelay;
                return direction;
            }

            if (now < _nextRepeat) return Vector2Int.zero;
            _nextRepeat = now + RepeatInterval;
            return direction;
        }

        /// <summary>The binding's name on the device in use, such as "Q" or "LB".</summary>
        public string Glyph(MenuButton button)
        {
            var action = button switch
            {
                MenuButton.Toggle => _toggle,
                MenuButton.Confirm => _confirm,
                MenuButton.Cancel => _cancel,
                MenuButton.Previous => _previous,
                MenuButton.Next => _next,
                MenuButton.Secondary => _secondary,
                MenuButton.PreviousTab => _previousTab,
                MenuButton.NextTab => _nextTab,
                MenuButton.Switch => _switch,
                _ => _confirm
            };

            var group = UsingGamepad ? "Gamepad" : "Keyboard&Mouse";
            var text = action.GetBindingDisplayString(InputBinding.MaskByGroup(group));
            if (string.IsNullOrEmpty(text)) text = action.GetBindingDisplayString();
            var separator = text.IndexOf(" | ", StringComparison.Ordinal);
            return separator > 0 ? text.Substring(0, separator) : text;
        }

        private static InputActionMap CreateFallbackMap()
        {
            var map = new InputActionMap("Menu");
            var toggle = map.AddAction("Toggle", InputActionType.Button, "<Keyboard>/escape");
            toggle.AddBinding("<Keyboard>/tab");
            toggle.AddBinding("<Gamepad>/start");
            var navigate = map.AddAction("Navigate", InputActionType.Value);
            navigate.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            navigate.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            navigate.AddBinding("<Gamepad>/leftStick");
            navigate.AddBinding("<Gamepad>/dpad");
            map.AddAction("Confirm", InputActionType.Button, "<Keyboard>/enter").AddBinding("<Gamepad>/buttonSouth");
            map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/backspace").AddBinding("<Gamepad>/buttonEast");
            map.FindAction("Cancel").AddBinding("<Keyboard>/escape");
            map.AddAction("Previous", InputActionType.Button, "<Keyboard>/q").AddBinding("<Gamepad>/leftShoulder");
            map.AddAction("Next", InputActionType.Button, "<Keyboard>/e").AddBinding("<Gamepad>/rightShoulder");
            map.AddAction("Secondary", InputActionType.Button, "<Keyboard>/x").AddBinding("<Gamepad>/buttonWest");
            map.AddAction("PreviousTab", InputActionType.Button, "<Keyboard>/z").AddBinding("<Gamepad>/leftTrigger");
            map.AddAction("NextTab", InputActionType.Button, "<Keyboard>/c").AddBinding("<Gamepad>/rightTrigger");
            map.AddAction("Switch", InputActionType.Button, "<Keyboard>/r").AddBinding("<Gamepad>/buttonNorth");
            return map;
        }

        public void Dispose()
        {
            _map.Disable();
            if (_ownsMap) _map.Dispose();
        }
    }

    public enum MenuButton
    {
        Toggle,
        Confirm,
        Cancel,
        Previous,
        Next,
        Secondary,
        PreviousTab,
        NextTab,
        Switch
    }
}
