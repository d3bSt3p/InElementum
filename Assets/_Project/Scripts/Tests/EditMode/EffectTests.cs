using System;
using InElementum.Combat.Actions;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class EffectTests
    {
        private readonly TestAssets _assets = new TestAssets();
        private TestCombatant _source;
        private TestCombatant _target;

        [SetUp]
        public void SetUp()
        {
            EffectProcessor.Initialize();
            _source = _assets.Combatant();
            _target = _assets.Combatant();
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private void Apply(EffectType type, float value, StatusEffectType status = StatusEffectType.None,
            StatType stat = default, SurgeGauge gauge = null)
        {
            EffectProcessor.GetEffect(type).Apply(new EffectContext(_source, _target, value)
            {
                Status = status,
                Stat = stat,
                SurgeGauge = gauge
            });
        }

        [Test]
        public void ProcessorFindsEveryEffectType()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                var effect = EffectProcessor.GetEffect(type);
                Assert.That(effect, Is.Not.Null, type.ToString());
                Assert.That(effect.EffectType, Is.EqualTo(type));
            }
        }

        [Test]
        public void DamageIsReducedBySourceWeakness()
        {
            _source.Stats.ApplyStatus(StatusEffectType.Weakness, 2);

            Apply(EffectType.Damage, 10);

            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(92));
        }

        [Test]
        public void DamageUsesTargetElementAffinity()
        {
            _target.Stats.SetElementAffinities(new[] { new ElementAffinity(Element.Fire, 2f) });

            EffectProcessor.GetEffect(EffectType.Damage).Apply(new EffectContext(_source, _target, 10) { Element = Element.Fire });

            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(80));
        }

        [Test]
        public void DamageAppliesTheContextMultiplier()
        {
            EffectProcessor.GetEffect(EffectType.Damage).Apply(new EffectContext(_source, _target, 10) { Multiplier = 1.5f });

            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(85));
        }

        [Test]
        public void DamageWorksWithoutASource()
        {
            EffectProcessor.GetEffect(EffectType.Damage).Apply(new EffectContext(null, _target, 10));

            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(90));
        }

        [Test]
        public void HealRestoresTargetUpToMax()
        {
            _target.Stats.Damage(30);

            Apply(EffectType.Heal, 10);
            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(80));

            Apply(EffectType.Heal, 500);
            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void HealFallsBackToSourceWithoutATarget()
        {
            _source.Stats.Damage(30);

            EffectProcessor.GetEffect(EffectType.Heal).Apply(new EffectContext(_source, null, 10));

            Assert.That(_source.Stats.CurrentHealth, Is.EqualTo(80));
        }

        [TestCase(StatusEffectType.Poison)]
        [TestCase(StatusEffectType.Burn)]
        [TestCase(StatusEffectType.Stun)]
        [TestCase(StatusEffectType.Weakness)]
        [TestCase(StatusEffectType.Soft)]
        [TestCase(StatusEffectType.Thorns)]
        public void ApplyStatusAppliesTheAuthoredStatus(StatusEffectType status)
        {
            Apply(EffectType.ApplyStatus, 3, status);

            Assert.That(_target.Stats.GetStatusValue(status), Is.EqualTo(3));
        }

        [Test]
        public void ApplyStatusReadsStatForStatBuffs()
        {
            Apply(EffectType.ApplyStatus, 3, StatusEffectType.StatBuff, StatType.Luck);

            Assert.That(_target.Stats.FinalStats.Luck, Is.EqualTo(13));
        }

        [Test]
        public void LifeStealHealsSourceByDamageDealt()
        {
            _source.Stats.Damage(50);

            Apply(EffectType.LifeSteal, 20);

            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(80));
            Assert.That(_source.Stats.CurrentHealth, Is.EqualTo(70));
        }

        [Test]
        public void StunStunsTargetOnItsNextTurn()
        {
            Apply(EffectType.Stun, 1);

            _target.Stats.TriggerAllStatus();

            Assert.That(_target.Stats.IsStunned, Is.True);
        }

        [Test]
        public void CleanseRemovesOnlyNegativeStatuses()
        {
            _target.Stats.ApplyStatus(StatusEffectType.Poison, 3);
            _target.Stats.ApplyStatus(StatusEffectType.StatDebuff, 3, StatType.Speed);
            _target.Stats.ApplyStatus(StatusEffectType.Thorns, 3);

            Apply(EffectType.Cleanse, 0);

            Assert.That(_target.Stats.GetStatusValue(StatusEffectType.Poison), Is.EqualTo(0));
            Assert.That(_target.Stats.FinalStats.Speed, Is.EqualTo(10));
            Assert.That(_target.Stats.GetStatusValue(StatusEffectType.Thorns), Is.EqualTo(3));
        }

        [Test]
        public void IncreaseMaxHealthRaisesTheCap()
        {
            Apply(EffectType.IncreaseMaxHealth, 20);

            Assert.That(_target.Stats.MaxHealth, Is.EqualTo(120));
            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void RestoreApAddsAp()
        {
            Apply(EffectType.RestoreAP, 4);

            Assert.That(_target.Stats.CurrentAp, Is.EqualTo(4));
        }

        [Test]
        public void ReviveReturnsIncapacitatedTargetWithValueAsHealth()
        {
            _target.Stats.Damage(999);

            Apply(EffectType.Revive, 30);

            Assert.That(_target.Stats.IsIncapacitated, Is.False);
            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(30));
        }

        [Test]
        public void ReviveDoesNothingToStandingTarget()
        {
            _target.Stats.Damage(10);

            Apply(EffectType.Revive, 30);

            Assert.That(_target.Stats.CurrentHealth, Is.EqualTo(90));
        }

        [Test]
        public void ModifyStatBuffsOnPositiveAndDebuffsOnNegative()
        {
            Apply(EffectType.ModifyStat, 4, stat: StatType.Defense);
            Assert.That(_target.Stats.FinalStats.Defense, Is.EqualTo(14));

            Apply(EffectType.ModifyStat, -6, stat: StatType.Speed);
            Assert.That(_target.Stats.FinalStats.Speed, Is.EqualTo(4));
            Assert.That(_target.Stats.GetStatusValue(StatusEffectType.StatDebuff, StatType.Speed), Is.EqualTo(6));
        }

        [Test]
        public void FillSurgeGaugeAddsToGaugeUpToMax()
        {
            var gauge = new SurgeGauge(100);

            Apply(EffectType.FillSurgeGauge, 30, gauge: gauge);
            Assert.That(gauge.Current, Is.EqualTo(30));

            Apply(EffectType.FillSurgeGauge, 500, gauge: gauge);
            Assert.That(gauge.IsFull, Is.True);
            Assert.That(gauge.Current, Is.EqualTo(100));
        }

        [Test]
        public void ContextFromDataCarriesActionAndEffectFields()
        {
            var action = _assets.Create<AbilityData>();
            action.Element = Element.Fire;
            var data = new ActionEffectData(EffectType.ApplyStatus, ActionTargetType.Enemy, 3, StatusEffectType.Burn);

            var context = EffectContext.From(data, _source, _target, action);

            Assert.That(context.Value, Is.EqualTo(3));
            Assert.That(context.Status, Is.EqualTo(StatusEffectType.Burn));
            Assert.That(context.Element, Is.EqualTo(Element.Fire));
            Assert.That(context.Action, Is.SameAs(action));
        }
    }
}
