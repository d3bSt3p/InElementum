using System;
using System.Collections.Generic;
using InElementum.Data.Actions;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Settings;

namespace InElementum.Data.Stats
{
    /// <summary>Pure stat math. A stat is (base + flat modifiers) * (1 + percent modifiers), rounded, minimum 0.</summary>
    public static class StatCalculator
    {
        private static readonly StatType[] AllStats = (StatType[])Enum.GetValues(typeof(StatType));

        public static StatBlock Aggregate(StatBlock baseStats, IEnumerable<StatModifier> modifiers)
        {
            var flat = new float[AllStats.Length];
            var percent = new float[AllStats.Length];

            if (modifiers != null)
            {
                foreach (var modifier in modifiers)
                {
                    if (modifier.Kind == ModifierKind.Flat)
                        flat[(int)modifier.Stat] += modifier.Value;
                    else
                        percent[(int)modifier.Stat] += modifier.Value;
                }
            }

            var result = new StatBlock();
            foreach (var stat in AllStats)
            {
                var value = (baseStats[stat] + flat[(int)stat]) * (1f + percent[(int)stat]);
                result[stat] = Math.Max(0, (int)Math.Round(value, MidpointRounding.AwayFromZero));
            }

            return result;
        }

        /// <param name="loadout">Worn pictos add their stats. Luminas add none.</param>
        /// <param name="mealBuff">Placeholder until cooking exists.</param>
        /// <param name="permanent">Permanent modifiers, such as the skill tree's stat boosts.</param>
        public static StatBlock GetFinalStats(
            StatBlock baseStats,
            Loadout loadout = null,
            IEnumerable<StatModifier> statuses = null,
            IEnumerable<StatModifier> mealBuff = null,
            IEnumerable<StatModifier> permanent = null)
        {
            var modifiers = new List<StatModifier>();

            if (permanent != null) modifiers.AddRange(permanent);
            if (loadout != null) modifiers.AddRange(loadout.Modifiers);

            if (statuses != null) modifiers.AddRange(statuses);
            if (mealBuff != null) modifiers.AddRange(mealBuff);

            return Aggregate(baseStats, modifiers);
        }

        public static int GetMaxHealth(int vitality, CombatTuning tuning) =>
            Math.Max(1, tuning.BaseHealth + vitality * tuning.HealthPerVitality);

        /// <summary>The attack's base hits plus extra hits from passives of worn pictos and equipped Luminas.</summary>
        public static int GetAttackHitCount(AttackData attack, Loadout loadout) =>
            attack.BaseHitCount + (loadout?.ExtraAttackHits ?? 0);

        /// <summary>The attack's base hits plus extra hits from every passive in effect.</summary>
        public static int GetAttackHitCount(AttackData attack, IEnumerable<PassiveData> passives)
        {
            var extra = 0;
            if (passives != null)
            {
                foreach (var passive in passives)
                    extra += passive != null ? passive.ExtraAttackHits : 0;
            }

            return attack.BaseHitCount + extra;
        }
    }
}
