using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Characters;

namespace InElementum.Data.Weapons
{
    /// <summary>Why a weapon could not be equipped. <see cref="None"/> means it was.</summary>
    public enum WeaponError
    {
        None,
        Missing,
        NotOwned,
        /// <summary>The weapon belongs to another character.</summary>
        WrongWielder,
        /// <summary>Another member holds the weapon, and it was not taken from them.</summary>
        HeldByAnother
    }

    /// <summary>
    /// The party's weapons: which it owns, each one's upgrade level, and which one each member holds. A member holds
    /// at most one weapon and a weapon has at most one holder. Plain C#; persistence comes later.
    /// </summary>
    public class WeaponInventory
    {
        private readonly List<WeaponData> _owned = new List<WeaponData>();
        private readonly Dictionary<WeaponData, int> _levels = new Dictionary<WeaponData, int>();
        private readonly Dictionary<PartyMemberData, WeaponData> _equipped = new Dictionary<PartyMemberData, WeaponData>();

        public IReadOnlyList<WeaponData> Owned => _owned;

        /// <summary>Adds a weapon to the party at level 1. False if it is null or already owned.</summary>
        public bool Add(WeaponData weapon)
        {
            if (weapon == null || _owned.Contains(weapon)) return false;

            _owned.Add(weapon);
            return true;
        }

        public bool Owns(WeaponData weapon) => weapon != null && _owned.Contains(weapon);

        /// <summary>Owned weapons <paramref name="member"/> may wield, in the order the party got them.</summary>
        public IEnumerable<WeaponData> UsableBy(PartyMemberData member) => _owned.Where(weapon => weapon.CanBeWieldedBy(member));

        #region Levels
        /// <summary>The weapon's upgrade level, 1 until it is upgraded.</summary>
        public int GetLevel(WeaponData weapon) => weapon != null && _levels.TryGetValue(weapon, out var level) ? level : 1;

        /// <summary>Sets an owned weapon's upgrade level, clamped to 1 and its max. False for a weapon the party lacks.</summary>
        public bool SetLevel(WeaponData weapon, int level)
        {
            if (!Owns(weapon)) return false;
            _levels[weapon] = Math.Max(1, Math.Min(level, weapon.MaxLevel));
            return true;
        }
        #endregion

        #region Equipping
        /// <summary>The weapon <paramref name="member"/> holds, or null.</summary>
        public WeaponData GetWeapon(PartyMemberData member) =>
            member != null && _equipped.TryGetValue(member, out var weapon) ? weapon : null;

        /// <summary>The member holding <paramref name="weapon"/>, or null.</summary>
        public PartyMemberData GetHolder(WeaponData weapon)
        {
            if (weapon == null) return null;
            foreach (var pair in _equipped)
                if (pair.Value == weapon) return pair.Key;
            return null;
        }

        public WeaponError CanEquip(PartyMemberData member, WeaponData weapon, bool takeFromHolder = false)
        {
            if (member == null || weapon == null) return WeaponError.Missing;
            if (!Owns(weapon)) return WeaponError.NotOwned;
            if (!weapon.CanBeWieldedBy(member)) return WeaponError.WrongWielder;

            var holder = GetHolder(weapon);
            if (holder != null && holder != member && !takeFromHolder) return WeaponError.HeldByAnother;
            return WeaponError.None;
        }

        /// <summary>
        /// Puts <paramref name="weapon"/> in <paramref name="member"/>'s hands, replacing what they held. With
        /// <paramref name="takeFromHolder"/>, a weapon another member holds is taken from them, leaving them empty-handed.
        /// Nothing changes when the result is an error.
        /// </summary>
        public WeaponError Equip(PartyMemberData member, WeaponData weapon, bool takeFromHolder = false)
        {
            var error = CanEquip(member, weapon, takeFromHolder);
            if (error != WeaponError.None) return error;

            var holder = GetHolder(weapon);
            if (holder != null && holder != member) _equipped.Remove(holder);
            _equipped[member] = weapon;
            return WeaponError.None;
        }

        /// <summary>Empties <paramref name="member"/>'s hands. False if they held nothing.</summary>
        public bool Unequip(PartyMemberData member) => member != null && _equipped.Remove(member);
        #endregion
    }
}
