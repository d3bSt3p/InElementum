using System.Linq;
using InElementum.Combat.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Stats;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class StatTests
    {
        private readonly TestAssets _assets = new TestAssets();

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private static StatModifier Flat(StatType stat, float value) => new StatModifier(stat, ModifierKind.Flat, value);
        private static StatModifier Percent(StatType stat, float value) => new StatModifier(stat, ModifierKind.Percent, value);

        [Test]
        public void AggregateWithNoModifiersReturnsBase()
        {
            var result = StatCalculator.Aggregate(new StatBlock(1, 2, 3, 4, 5), null);

            Assert.That(new[] { result.Vitality, result.Defense, result.Strength, result.Speed, result.Luck },
                Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void FlatModifiersAddToTheirStatOnly()
        {
            var result = StatCalculator.Aggregate(new StatBlock(10, 10, 10, 10, 10),
                new[] { Flat(StatType.Strength, 3), Flat(StatType.Strength, 2), Flat(StatType.Luck, -4) });

            Assert.That(result.Strength, Is.EqualTo(15));
            Assert.That(result.Luck, Is.EqualTo(6));
            Assert.That(result.Vitality, Is.EqualTo(10));
        }

        [Test]
        public void PercentAppliesAfterFlatAndPercentsAdd()
        {
            var result = StatCalculator.Aggregate(new StatBlock(0, 0, 10, 0, 0),
                new[] { Percent(StatType.Strength, 0.25f), Flat(StatType.Strength, 10), Percent(StatType.Strength, 0.25f) });

            // (10 + 10) * (1 + 0.5)
            Assert.That(result.Strength, Is.EqualTo(30));
        }

        [Test]
        public void HalvesRoundAwayFromZero()
        {
            var result = StatCalculator.Aggregate(new StatBlock(0, 0, 10, 0, 0), new[] { Percent(StatType.Strength, 0.25f) });

            Assert.That(result.Strength, Is.EqualTo(13));
        }

        [Test]
        public void StatsNeverGoBelowZero()
        {
            var result = StatCalculator.Aggregate(new StatBlock(5, 0, 0, 0, 0), new[] { Flat(StatType.Vitality, -20) });

            Assert.That(result.Vitality, Is.EqualTo(0));
        }

        [Test]
        public void MaxHealthIsDerivedFromVitality()
        {
            var tuning = _assets.Tuning();
            tuning.BaseHealth = 5;

            var stats = new CharacterStats(new StatBlock(12, 0, 0, 0, 0), tuning);

            Assert.That(stats.MaxHealth, Is.EqualTo(125));
            Assert.That(stats.CurrentHealth, Is.EqualTo(125));
        }

        [Test]
        public void MaxHealthIsAtLeastOne()
        {
            var stats = new CharacterStats(new StatBlock(0, 0, 0, 0, 0), _assets.Tuning());

            Assert.That(stats.MaxHealth, Is.EqualTo(1));
        }

        [Test]
        public void VitalityBuffRaisesMaxHealthAndClearingItClampsCurrentHealth()
        {
            var stats = _assets.Combatant().Stats;

            stats.ApplyStatus(StatusEffectType.StatBuff, 5, StatType.Vitality);
            Assert.That(stats.MaxHealth, Is.EqualTo(150));
            Assert.That(stats.CurrentHealth, Is.EqualTo(100), "A buff raises the cap, not current health.");

            stats.Heal(50);
            stats.ClearStatus(new StatusKey(StatusEffectType.StatBuff, StatType.Vitality));

            Assert.That(stats.MaxHealth, Is.EqualTo(100));
            Assert.That(stats.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void StatBuffsAndDebuffsAreKeyedByStatAndFeedFinalStats()
        {
            var stats = _assets.Combatant().Stats;

            stats.ApplyStatus(StatusEffectType.StatBuff, 4, StatType.Speed);
            stats.ApplyStatus(StatusEffectType.StatDebuff, 1, StatType.Speed);
            stats.ApplyStatus(StatusEffectType.StatDebuff, 3, StatType.Luck);

            Assert.That(stats.FinalStats.Speed, Is.EqualTo(13));
            Assert.That(stats.FinalStats.Luck, Is.EqualTo(7));
            Assert.That(stats.FinalStats.Strength, Is.EqualTo(10));
        }

        [Test]
        public void DamageIncapacitatesAtZeroAndRaisesEventOnce()
        {
            var stats = _assets.Combatant().Stats;
            var raised = 0;
            stats.OnIncapacitated += () => raised++;

            stats.Damage(250);
            stats.Damage(10);

            Assert.That(stats.CurrentHealth, Is.EqualTo(0));
            Assert.That(stats.IsIncapacitated, Is.True);
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void SoftAddsToDamageAndThornsHitsTheAttacker()
        {
            var defender = _assets.Combatant().Stats;
            var attacker = _assets.Combatant().Stats;
            defender.ApplyStatus(StatusEffectType.Soft, 3);
            defender.ApplyStatus(StatusEffectType.Thorns, 7);

            var dealt = defender.Damage(10, attacker);

            Assert.That(dealt, Is.EqualTo(13));
            Assert.That(defender.CurrentHealth, Is.EqualTo(87));
            Assert.That(attacker.CurrentHealth, Is.EqualTo(93));
        }

        [Test]
        public void ReviveOnlyWorksOnIncapacitatedAndClearsNegativeStatuses()
        {
            var stats = _assets.Combatant().Stats;
            Assert.That(stats.Revive(10), Is.False);

            stats.ApplyStatus(StatusEffectType.Poison, 5);
            stats.ApplyStatus(StatusEffectType.Thorns, 5);
            stats.Damage(999);
            stats.Heal(50);
            Assert.That(stats.CurrentHealth, Is.EqualTo(0), "Healing does not revive.");

            Assert.That(stats.Revive(25), Is.True);
            Assert.That(stats.IsIncapacitated, Is.False);
            Assert.That(stats.CurrentHealth, Is.EqualTo(25));
            Assert.That(stats.GetStatusValue(StatusEffectType.Poison), Is.EqualTo(0));
            Assert.That(stats.GetStatusValue(StatusEffectType.Thorns), Is.EqualTo(5));
        }

        [Test]
        public void ApIsClampedAndSpendingRequiresEnough()
        {
            var stats = _assets.Combatant().Stats;

            stats.GainAp(4);
            Assert.That(stats.TrySpendAp(5), Is.False);
            Assert.That(stats.CurrentAp, Is.EqualTo(4));
            Assert.That(stats.TrySpendAp(3), Is.True);
            Assert.That(stats.CurrentAp, Is.EqualTo(1));

            stats.GainAp(100);
            Assert.That(stats.CurrentAp, Is.EqualTo(stats.MaxAp));
        }

        [Test]
        public void PoisonDamagesThenDecays()
        {
            var stats = _assets.Combatant().Stats;
            stats.ApplyStatus(StatusEffectType.Poison, 2);

            stats.TriggerAllStatus();
            Assert.That(stats.CurrentHealth, Is.EqualTo(98));
            Assert.That(stats.GetStatusValue(StatusEffectType.Poison), Is.EqualTo(1));

            stats.TriggerAllStatus();
            Assert.That(stats.CurrentHealth, Is.EqualTo(97));
            Assert.That(stats.GetStatusValue(StatusEffectType.Poison), Is.EqualTo(0));
        }

        [Test]
        public void BurnDamagesThenGrows()
        {
            var stats = _assets.Combatant().Stats;
            stats.ApplyStatus(StatusEffectType.Burn, 2);

            stats.TriggerAllStatus();

            Assert.That(stats.CurrentHealth, Is.EqualTo(98));
            Assert.That(stats.GetStatusValue(StatusEffectType.Burn), Is.EqualTo(3));
        }

        [Test]
        public void StunLastsOneTurnPerStack()
        {
            var stats = _assets.Combatant().Stats;
            stats.ApplyStatus(StatusEffectType.Stun, 1);

            stats.TriggerAllStatus();
            Assert.That(stats.IsStunned, Is.True);

            stats.TriggerAllStatus();
            Assert.That(stats.IsStunned, Is.False);
        }

        [Test]
        public void StatusEnumHasNoDeckbuilderValues()
        {
            var names = System.Enum.GetNames(typeof(StatusEffectType));

            Assert.That(names.Intersect(new[] { "ExtraDraw", "ReduceDraw", "Block", "Dexterity", "Strength" }), Is.Empty);
        }
    }
}
