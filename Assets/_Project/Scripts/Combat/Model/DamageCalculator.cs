using System;
using InElementum.Data.Settings;
using InElementum.Data.Stats;

namespace InElementum.Combat.Model
{
    public enum ElementReaction
    {
        Normal,
        Weak,
        Resist,
        Immune
    }

    /// <summary>Inputs to one damage calculation. Stats are final stats, statuses included.</summary>
    public struct DamageRequest
    {
        public float Power;
        public StatBlock Attacker;
        public StatBlock Defender;
        /// <summary>Product of situational multipliers, such as a QTE bonus or a Perilous attack. 1 by default.</summary>
        public float Multiplier;
        /// <summary>Flat reduction to raw damage, such as the attacker's Weakness status.</summary>
        public int FlatReduction;
        /// <summary>The defender's multiplier for the attack's element: 2 weak, 0.5 resistant, 0 immune.</summary>
        public float ElementMultiplier;
        public bool IsCrit;

        public static DamageRequest Create(float power, StatBlock attacker, StatBlock defender)
        {
            return new DamageRequest
            {
                Power = power,
                Attacker = attacker,
                Defender = defender,
                Multiplier = 1f,
                ElementMultiplier = 1f
            };
        }
    }

    public readonly struct DamageResult
    {
        public DamageResult(int amount, ElementReaction reaction, bool isCrit)
        {
            Amount = amount;
            Reaction = reaction;
            IsCrit = isCrit;
        }

        public int Amount { get; }
        public ElementReaction Reaction { get; }
        public bool IsCrit { get; }
    }

    /// <summary>
    /// The one damage formula. raw = (power + Strength * k_str) * multiplier - flat reduction;
    /// final = max(min, raw - Defense * k_def) * element multiplier, then * crit multiplier on a crit.
    /// </summary>
    public static class DamageCalculator
    {
        public static DamageResult Calculate(DamageRequest request, CombatTuning tuning)
        {
            var raw = (request.Power + request.Attacker.Strength * tuning.StrengthCoefficient) * request.Multiplier
                      - request.FlatReduction;
            var afterDefense = Math.Max(tuning.MinimumDamage, raw - request.Defender.Defense * tuning.DefenseCoefficient);
            var final = afterDefense * Math.Max(0f, request.ElementMultiplier);
            if (request.IsCrit) final *= tuning.CritMultiplier;

            var amount = (int)Math.Round(final, MidpointRounding.AwayFromZero);
            return new DamageResult(amount, GetReaction(request.ElementMultiplier), request.IsCrit);
        }

        public static float GetCritChance(StatBlock attacker, CombatTuning tuning) =>
            Math.Min(1f, Math.Max(0f, tuning.BaseCritChance + attacker.Luck * tuning.CritChancePerLuck));

        public static bool RollCrit(StatBlock attacker, CombatTuning tuning, IRandomSource random) =>
            random.Value() < GetCritChance(attacker, tuning);

        public static ElementReaction GetReaction(float elementMultiplier)
        {
            if (elementMultiplier <= 0f) return ElementReaction.Immune;
            if (elementMultiplier > 1f) return ElementReaction.Weak;
            if (elementMultiplier < 1f) return ElementReaction.Resist;
            return ElementReaction.Normal;
        }
    }
}
