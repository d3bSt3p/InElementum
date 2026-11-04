using System;
using InElementum.Data.Settings;
using InElementum.Data.Stats;

namespace InElementum.Data.Weapons
{
    /// <summary>
    /// Pure weapon power math. Power = base power + power per level * (level - 1) + the sum, over the weapon's scaling
    /// entries, of the wielder's attribute * that grade's coefficient from <see cref="CombatTuning"/>. Rounded, minimum 0.
    /// </summary>
    public static class WeaponMath
    {
        /// <summary>The weapon's power at <paramref name="level"/> before attribute scaling.</summary>
        public static float GetLevelPower(WeaponData weapon, int level)
        {
            if (weapon == null) return 0f;
            var clamped = Math.Max(1, Math.Min(level, weapon.MaxLevel));
            return weapon.BasePower + weapon.PowerPerLevel * (clamped - 1);
        }

        /// <summary>What <paramref name="stats"/> add to the weapon's power through its scaling.</summary>
        public static float GetScalingPower(WeaponData weapon, StatBlock stats, CombatTuning tuning)
        {
            if (weapon == null || tuning == null) return 0f;

            var total = 0f;
            foreach (var entry in weapon.Scaling)
                total += stats[entry.Stat] * tuning.GetScaling(entry.Grade);
            return total;
        }

        /// <summary>Each basic attack hit's power with this weapon, for a wielder with <paramref name="stats"/>.</summary>
        public static int GetPower(WeaponData weapon, int level, StatBlock stats, CombatTuning tuning)
        {
            if (weapon == null) return 0;
            var power = GetLevelPower(weapon, level) + GetScalingPower(weapon, stats, tuning);
            return Math.Max(0, (int)Math.Round(power, MidpointRounding.AwayFromZero));
        }
    }
}
