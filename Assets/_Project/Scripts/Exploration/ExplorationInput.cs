using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace InElementum.Exploration
{
    /// <summary>
    /// Reads the "Player" action map: Move, Jump, Interact, FieldAttack (left mouse button or X), and character switching (Previous/Next
    /// Character on the triggers, Select Character on the number keys, the Character Wheel on Y or Q, Wheel Aim on
    /// the sticks, and Cancel). Enabled while the explored character can act, and disabled while combat or a
    /// transition has the controls. Also tracks whether the player is on a controller, for prompts.
    /// </summary>
    public sealed class ExplorationInput : IDisposable
    {
        private readonly InputActionMap _map;
        private readonly bool _ownsMap;
        private readonly InputAction _move;
        private readonly InputAction _jump;
        private readonly InputAction _interact;
        private readonly InputAction _fieldAttack;
        private readonly InputAction _previousCharacter;
        private readonly InputAction _nextCharacter;
        private readonly InputAction _selectCharacter;
        private readonly InputAction _wheel;
        private readonly InputAction _wheelAim;
        private readonly InputAction _cancel;

        public ExplorationInput(InputActionAsset asset)
        {
            _map = asset != null ? asset.FindActionMap("Player") : null;
            if (_map == null || _map.FindAction("FieldAttack") == null || _map.FindAction("CharacterWheel") == null)
            {
                Debug.LogWarning("[ExplorationInput] No 'Player' map with FieldAttack and CharacterWheel found. Using built-in bindings.");
                _map = CreateFallbackMap();
                _ownsMap = true;
            }

            _move = _map.FindAction("Move", true);
            _jump = _map.FindAction("Jump", true);
            _interact = _map.FindAction("Interact", true);
            _fieldAttack = _map.FindAction("FieldAttack", true);
            _previousCharacter = _map.FindAction("PreviousCharacter", true);
            _nextCharacter = _map.FindAction("NextCharacter", true);
            _selectCharacter = _map.FindAction("SelectCharacter", true);
            _wheel = _map.FindAction("CharacterWheel", true);
            _wheelAim = _map.FindAction("WheelAim", true);
            _cancel = _map.FindAction("Cancel", true);
            UsingGamepad = Gamepad.current != null;
        }

        public bool Enabled => _map.enabled;

        public Vector2 Move => _map.enabled ? _move.ReadValue<Vector2>() : Vector2.zero;
        public bool JumpPressed => _map.enabled && _jump.WasPressedThisFrame();
        public bool JumpHeld => _map.enabled && _jump.IsPressed();
        public bool InteractPressed => _map.enabled && _interact.WasPressedThisFrame();
        public bool FieldAttackPressed => _map.enabled && _fieldAttack.WasPressedThisFrame();

        public bool PreviousCharacterPressed => _map.enabled && _previousCharacter.WasPressedThisFrame();
        public bool NextCharacterPressed => _map.enabled && _nextCharacter.WasPressedThisFrame();
        public bool WheelPressed => _map.enabled && _wheel.WasPressedThisFrame();
        public bool WheelReleased => _map.enabled && _wheel.WasReleasedThisFrame();
        public bool WheelHeld => _map.enabled && _wheel.IsPressed();
        public bool CancelPressed => _map.enabled && _cancel.WasPressedThisFrame();
        /// <summary>Stick direction for aiming the wheel, y up.</summary>
        public Vector2 WheelAim => _map.enabled ? _wheelAim.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>The 0-based slot whose Select Character key went down this frame (1 is slot 0), or -1.</summary>
        public int SelectedSlot
        {
            get
            {
                if (!_map.enabled || !_selectCharacter.WasPressedThisFrame()) return -1;
                var control = _selectCharacter.activeControl as KeyControl;
                var name = control != null ? control.name : null;
                return name != null && name.Length == 1 && char.IsDigit(name[0]) && name[0] != '0' ? name[0] - '1' : -1;
            }
        }

        /// <summary>The Interact button's name for prompts, on the device in use.</summary>
        public string InteractGlyph => Glyph(_interact);
        public string FieldAttackGlyph => Glyph(_fieldAttack);
        public string JumpGlyph => Glyph(_jump);
        public string WheelGlyph => Glyph(_wheel);
        public string CancelGlyph => Glyph(_cancel);
        public string PreviousCharacterGlyph => Glyph(_previousCharacter);
        public string NextCharacterGlyph => Glyph(_nextCharacter);
        /// <summary>The move keys or stick, such as "A / D" or "L-Stick".</summary>
        public string MoveGlyph => UsingGamepad ? "L-Stick" : "A / D";

        /// <summary>Whether prompts should name controller buttons: the last input came from a pad.</summary>
        public bool UsingGamepad { get; private set; }

        /// <summary>Notes which device the player last touched. Call once a frame.</summary>
        public void UpdateDevice()
        {
            var pad = Gamepad.current;
            if (pad != null && pad.wasUpdatedThisFrame && PadActuated(pad))
            {
                UsingGamepad = true;
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame) ||
                (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 4f)))
                UsingGamepad = false;
        }

        private static bool PadActuated(Gamepad pad)
        {
            if (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.rightStick.ReadValue().sqrMagnitude > 0.25f) return true;
            foreach (var control in pad.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame) return true;
            }

            return false;
        }

        public void Enable() => _map.Enable();
        public void Disable() => _map.Disable();

        private string Glyph(InputAction action)
        {
            var group = UsingGamepad ? "Gamepad" : "Keyboard&Mouse";
            var text = action.GetBindingDisplayString(InputBinding.MaskByGroup(group));
            if (string.IsNullOrEmpty(text)) text = action.GetBindingDisplayString();
            var separator = text.IndexOf(" | ", StringComparison.Ordinal);
            return separator > 0 ? text.Substring(0, separator) : text;
        }

        private static InputActionMap CreateFallbackMap()
        {
            var map = new InputActionMap("Player");
            var move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddBinding("<Gamepad>/dpad");
            map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space").AddBinding("<Gamepad>/buttonSouth");
            map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e").AddBinding("<Gamepad>/buttonEast");
            map.AddAction("FieldAttack", InputActionType.Button, "<Mouse>/leftButton").AddBinding("<Gamepad>/buttonWest");
            map.AddAction("PreviousCharacter", InputActionType.Button, "<Gamepad>/leftTrigger");
            map.AddAction("NextCharacter", InputActionType.Button, "<Gamepad>/rightTrigger");
            var select = map.AddAction("SelectCharacter", InputActionType.Button);
            for (var i = 1; i <= 9; i++) select.AddBinding($"<Keyboard>/{i}");
            map.AddAction("CharacterWheel", InputActionType.Button, "<Keyboard>/q").AddBinding("<Gamepad>/buttonNorth");
            var aim = map.AddAction("WheelAim", InputActionType.Value);
            aim.AddBinding("<Gamepad>/rightStick");
            aim.AddBinding("<Gamepad>/leftStick");
            var cancel = map.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");
            cancel.AddBinding("<Mouse>/rightButton");
            return map;
        }

        public void Dispose()
        {
            _map.Disable();
            if (_ownsMap) _map.Dispose();
        }
    }
}
