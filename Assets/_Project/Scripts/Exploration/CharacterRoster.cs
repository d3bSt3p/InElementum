using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Characters;
using UnityEngine;

namespace InElementum.Exploration
{
    /// <summary>
    /// Who the player explores as, out of the party: one character at a time, whose exploration ability is the one
    /// that works. Switching steps through the members in party order (slot 1 first) or jumps to a slot. Independent
    /// of the combat formation. Plain C#.
    /// </summary>
    public sealed class CharacterRoster
    {
        private readonly List<PartyMemberData> _members = new List<PartyMemberData>();

        public CharacterRoster(IEnumerable<PartyMemberData> members = null) => SetMembers(members);

        /// <summary>Raised with the new character whenever the explored character changes.</summary>
        public event Action<PartyMemberData> Changed;

        public IReadOnlyList<PartyMemberData> Members => _members;
        public int Count => _members.Count;
        /// <summary>The explored character's slot, or -1 with nobody to explore as.</summary>
        public int CurrentIndex { get; private set; } = -1;
        public PartyMemberData Current => CurrentIndex >= 0 ? _members[CurrentIndex] : null;

        /// <summary>
        /// Replaces the members, keeping the current character if they are still among them, else the first.
        /// Raises <see cref="Changed"/> only if that changes who is explored.
        /// </summary>
        public void SetMembers(IEnumerable<PartyMemberData> members)
        {
            var previous = Current;
            _members.Clear();
            if (members != null) _members.AddRange(members.Where(member => member != null).Distinct());

            var index = previous != null ? _members.IndexOf(previous) : -1;
            CurrentIndex = index >= 0 ? index : _members.Count > 0 ? 0 : -1;
            if (Current != previous) Changed?.Invoke(Current);
        }

        /// <summary>Switches to the character in <paramref name="slot"/> (0-based). False if there is none or they are already out.</summary>
        public bool Select(int slot)
        {
            if (slot < 0 || slot >= _members.Count || slot == CurrentIndex) return false;
            CurrentIndex = slot;
            Changed?.Invoke(Current);
            return true;
        }

        public bool Select(PartyMemberData member) => Select(_members.IndexOf(member));

        /// <summary>The next character in slot order, wrapping round. False with fewer than two.</summary>
        public bool Next() => Step(1);

        public bool Previous() => Step(-1);

        private bool Step(int direction)
        {
            if (_members.Count < 2) return false;
            return Select(Wrap(CurrentIndex + direction, _members.Count));
        }

        private static int Wrap(int value, int count) => ((value % count) + count) % count;

        #region Wheel layout
        /// <summary>
        /// Unit direction from the wheel's center to <paramref name="slot"/> of <paramref name="count"/>, y up. Slot 0
        /// sits at the top and the rest follow clockwise.
        /// </summary>
        public static Vector2 SlotDirection(int slot, int count)
        {
            if (count <= 0) return Vector2.up;
            var angle = slot * Mathf.PI * 2f / count;
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        /// <summary>
        /// The slot <paramref name="aim"/> points at (y up), or -1 when it is shorter than <paramref name="deadzone"/>
        /// or there are no slots. Each slot owns the wedge centered on its direction.
        /// </summary>
        public static int SlotAt(Vector2 aim, int count, float deadzone)
        {
            if (count <= 0 || aim.magnitude < deadzone) return -1;

            // Clockwise from straight up, in 0..2π.
            var angle = Mathf.Atan2(aim.x, aim.y);
            if (angle < 0f) angle += Mathf.PI * 2f;
            var wedge = Mathf.PI * 2f / count;
            return Mathf.FloorToInt((angle + wedge * 0.5f) / wedge) % count;
        }
        #endregion
    }
}
