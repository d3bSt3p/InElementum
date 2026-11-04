using InElementum.Combat.Characters;
using InElementum.Combat.Interfaces;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Settings;
using InElementum.Data.Weapons;
using UnityEngine;

namespace InElementum.Combat.Model
{
    /// <summary>
    /// One fighter in a battle: authored data plus runtime stats, side, and slot. Plain C#; a view in the scene sets
    /// <see cref="ViewRoot"/> and <see cref="TextRoot"/> so effects can play FX on it.
    /// </summary>
    public sealed class Combatant : ICombatant, ITimelineUnit
    {
        public Combatant(CharacterDataBase data, CharacterStats stats, BattleSide side, int slot)
        {
            Data = data;
            Stats = stats;
            Side = side;
            Slot = slot;

            if (side == BattleSide.Party)
                SurgeGauge = new SurgeGauge(stats.Tuning.SurgeGaugeMax);
        }

        public CharacterDataBase Data { get; }
        public CharacterStats Stats { get; }
        public BattleSide Side { get; }
        public int Slot { get; internal set; }

        /// <summary>Null for enemies.</summary>
        public SurgeGauge SurgeGauge { get; }

        public PartyMemberData Member => Data as PartyMemberData;
        public EnemyCharacterData Enemy => Data as EnemyCharacterData;
        public string Name => Data != null ? Data.CharacterName : "?";
        public bool IsAlive => !Stats.IsIncapacitated;
        public int Speed => Stats.FinalStats.Speed;

        /// <summary>The weapon a party member holds, which sets their basic attack's power and element. Null uses the attack's own.</summary>
        public WeaponData Weapon { get; private set; }
        public int WeaponLevel { get; private set; } = 1;

        /// <summary>The element of this member's basic attack hits: their weapon's, else the attack's own.</summary>
        public Element AttackElement =>
            Weapon != null ? Weapon.Element : Member?.BasicAttack != null ? Member.BasicAttack.Element : Element.None;

        /// <summary>
        /// Each basic attack hit's power: the weapon's power for this member's final stats, else the attack's own
        /// power per hit.
        /// </summary>
        public float AttackPower =>
            Weapon != null ? WeaponMath.GetPower(Weapon, WeaponLevel, Stats.FinalStats, Stats.Tuning)
            : Member?.BasicAttack != null ? Member.BasicAttack.PowerPerHit : 0f;

        /// <summary>Puts <paramref name="weapon"/> in this member's hands at <paramref name="level"/>. Null leaves them empty-handed.</summary>
        public void SetWeapon(WeaponData weapon, int level = 1)
        {
            Weapon = weapon;
            WeaponLevel = level < 1 ? 1 : level;
        }

        /// <summary>How many abilities this enemy has used, which drives pattern-following enemies.</summary>
        public int AbilityUseCount { get; internal set; }

        /// <summary>Full break meter. 0 for units that cannot be broken.</summary>
        public int BreakMax => Enemy != null ? Enemy.BreakMeter : 0;
        public int BreakMeter { get; internal set; }
        public bool CanBreak => BreakMax > 0;
        /// <summary>Frozen on the bar with resistances down until the stun ends and the meter refills.</summary>
        public bool IsBroken { get; internal set; }

        public Transform ViewRoot { get; set; }
        public Transform TextRoot { get; set; }

        public bool IsOpponentOf(Combatant other) => other != null && other.Side != Side;

        /// <param name="loadout">The member's pictos and Luminas. Null wears their default pictos.</param>
        /// <param name="weapon">The weapon they hold. Null holds their first starting weapon, if any.</param>
        public static Combatant ForPartyMember(PartyMemberData member, CombatTuning tuning, int slot, Loadout loadout = null,
            WeaponData weapon = null, int weaponLevel = 1)
        {
            var stats = new CharacterStats(member.BaseStats, tuning, member.MaxAp);
            stats.SetLoadout(loadout ?? Loadout.FromDefaults(member, tuning));
            var unit = new Combatant(member, stats, BattleSide.Party, slot);
            unit.SetWeapon(weapon ?? FirstStartingWeapon(member), weaponLevel);
            return unit;
        }

        private static WeaponData FirstStartingWeapon(PartyMemberData member)
        {
            if (member.StartingWeapons == null) return null;
            foreach (var weapon in member.StartingWeapons)
                if (weapon != null && weapon.CanBeWieldedBy(member)) return weapon;
            return null;
        }

        public static Combatant ForEnemy(EnemyCharacterData enemy, CombatTuning tuning, int slot)
        {
            var stats = new CharacterStats(enemy.BaseStats, tuning);
            stats.SetElementAffinities(enemy.Affinities);
            return new Combatant(enemy, stats, BattleSide.Enemy, slot) { BreakMeter = enemy.BreakMeter };
        }

        public override string ToString() => $"{Name} ({Side} {Slot})";
    }
}
