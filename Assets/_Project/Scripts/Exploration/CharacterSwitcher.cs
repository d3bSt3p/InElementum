using System;
using InElementum.Core.Utils;
using InElementum.Data.Characters;
using InElementum.Encounters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InElementum.Exploration
{
    /// <summary>
    /// Switches the explored character: the triggers step to the previous or next party member, the number keys jump
    /// to a slot, and the character wheel (Y, or hold Q) picks one by aiming. Holding the wheel button and letting go
    /// picks whoever is aimed at; a tap leaves the wheel open until a pick (Jump or click), a slot key, or Cancel.
    /// Time slows and the character stands still while the wheel is open. Reads the party from the room's
    /// <see cref="EncounterDirector"/>. A switch plays the <see cref="CharacterSwitchFeedback"/>, added here when missing.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public class CharacterSwitcher : MonoBehaviour
    {
        [Tooltip("Time scale while the wheel is open, so the world slows rather than stops.")]
        [SerializeField] [Range(0f, 1f)] private float wheelTimeScale = 0.2f;
        [Tooltip("Seconds the wheel button must be held for letting go to pick. A shorter tap leaves the wheel open.")]
        [SerializeField] [Min(0f)] private float holdToPick = 0.25f;
        [Tooltip("How far a stick must lean before it aims the wheel, 0 to 1.")]
        [SerializeField] [Range(0.1f, 0.95f)] private float stickDeadzone = 0.5f;
        [Tooltip("How far the mouse must sit from the screen's center before it aims the wheel, as a fraction of screen height.")]
        [SerializeField] [Range(0.01f, 0.4f)] private float mouseDeadzone = 0.06f;

        private PlayerController2D _player;
        private float _openedAt;
        private bool _openedByHold;
        private float _timeScaleBefore = 1f;
        private Vector2 _mouseAtOpen;
        private bool _lockHeld;

        /// <summary>Raised when the wheel opens or closes.</summary>
        public event Action<bool> WheelToggled;
        /// <summary>Raised with the new character after a switch.</summary>
        public event Action<PartyMemberData> Switched;

        public CharacterRoster Roster { get; } = new CharacterRoster();
        public PartyMemberData Current => Roster.Current;
        public bool WheelOpen { get; private set; }
        /// <summary>True while the wheel button is still held from opening it, so letting go picks.</summary>
        public bool PicksOnRelease => WheelOpen && _openedByHold;
        /// <summary>The slot the wheel is aimed at, or the current one before any aiming.</summary>
        public int WheelHighlight { get; private set; } = -1;
        public ExplorationInput Input => _player != null ? _player.Input : null;

        private void Awake()
        {
            _player = GetComponent<PlayerController2D>();
            Roster.Changed += OnRosterChanged;
            if (!TryGetComponent<CharacterSwitchFeedback>(out _)) gameObject.AddComponent<CharacterSwitchFeedback>();
        }

        private void OnDisable()
        {
            CloseWheel();
            ReleaseLock();
        }

        /// <summary>
        /// The character stays locked until the frame ends, so the press that picked a character on the wheel (Jump,
        /// say) is not also read as a jump by the controller later in the same frame.
        /// </summary>
        private void LateUpdate()
        {
            if (!WheelOpen) ReleaseLock();
        }

        private void ReleaseLock()
        {
            if (_lockHeld && _player != null) _player.InputLocked = false;
            _lockHeld = false;
        }

        private void OnDestroy() => Roster.Changed -= OnRosterChanged;

        private void OnRosterChanged(PartyMemberData member) => Switched?.Invoke(member);

        private void Update()
        {
            SyncMembers();
            if (_player.Input == null) return;

            if (!_player.ControlsEnabled)
            {
                CloseWheel();
                return;
            }

            if (WheelOpen)
            {
                UpdateWheel();
                return;
            }

            var input = _player.Input;
            var slot = input.SelectedSlot;
            if (slot >= 0) Roster.Select(slot);
            else if (input.NextCharacterPressed) Roster.Next();
            else if (input.PreviousCharacterPressed) Roster.Previous();
            else if (input.WheelPressed) OpenWheel();
        }

        /// <summary>Keeps the roster in step with the party, which can grow or shrink between scenes.</summary>
        private void SyncMembers()
        {
            var party = EncounterDirector.Instance != null ? EncounterDirector.Instance.Party : null;
            if (party == null) return;

            var count = 0;
            var same = true;
            foreach (var member in party.Members)
            {
                if (count >= Roster.Count || Roster.Members[count] != member) same = false;
                count++;
            }

            if (!same || count != Roster.Count) Roster.SetMembers(party.Members);
        }

        #region Wheel
        public void OpenWheel()
        {
            if (WheelOpen || Roster.Count == 0) return;

            WheelOpen = true;
            _openedAt = Time.unscaledTime;
            _openedByHold = true;
            _mouseAtOpen = MousePosition;
            WheelHighlight = Roster.CurrentIndex;
            _player.InputLocked = true;
            _lockHeld = true;
            // A switch's hit stop may still hold time at 0; end it so the wheel saves the real scale.
            HitStop.Release();
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = wheelTimeScale;
            WheelToggled?.Invoke(true);
        }

        /// <summary>Closes the wheel; when <paramref name="pick"/>, switches to the highlighted character first.</summary>
        public void CloseWheel(bool pick = false)
        {
            if (!WheelOpen) return;

            WheelOpen = false;
            Time.timeScale = _timeScaleBefore;
            if (pick && WheelHighlight >= 0) Roster.Select(WheelHighlight);
            WheelToggled?.Invoke(false);
        }

        /// <summary>Points the wheel at <paramref name="slot"/>, as hovering it with the mouse does.</summary>
        public void Highlight(int slot)
        {
            if (WheelOpen && slot >= 0 && slot < Roster.Count) WheelHighlight = slot;
        }

        private void UpdateWheel()
        {
            var input = _player.Input;
            Aim(input);

            var slot = input.SelectedSlot;
            if (slot >= 0 && slot < Roster.Count)
            {
                WheelHighlight = slot;
                CloseWheel(true);
                return;
            }

            if (input.CancelPressed)
            {
                CloseWheel();
                return;
            }

            if (_openedByHold && input.WheelReleased)
            {
                // A quick tap leaves the wheel open to pick at leisure; a hold picks on release.
                if (Time.unscaledTime - _openedAt >= holdToPick) CloseWheel(true);
                else _openedByHold = false;
                return;
            }

            if (input.JumpPressed || input.FieldAttackPressed || (!_openedByHold && input.WheelPressed))
                CloseWheel(true);
        }

        private void Aim(ExplorationInput input)
        {
            var stick = input.WheelAim;
            var aimed = CharacterRoster.SlotAt(stick, Roster.Count, stickDeadzone);
            if (aimed < 0 && !input.UsingGamepad)
            {
                var mouse = MousePosition;
                var fromCenter = mouse - new Vector2(Screen.width, Screen.height) * 0.5f;
                if ((mouse - _mouseAtOpen).sqrMagnitude > 1f || fromCenter.magnitude > Screen.height * mouseDeadzone * 2f)
                    aimed = CharacterRoster.SlotAt(fromCenter, Roster.Count, Screen.height * mouseDeadzone);
            }

            if (aimed >= 0) WheelHighlight = aimed;
        }

        private static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        #endregion
    }
}
