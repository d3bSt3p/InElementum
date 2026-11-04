using System.Collections.Generic;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class ActionDataTests
    {
        private readonly TestAssets _assets = new TestAssets();

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private AbilityData AbilityWithDescription(StatusEffectType scaleWith, StatType stat = default)
        {
            var ability = _assets.Create<AbilityData>();
            ability.Effects = new List<ActionEffectData> { new ActionEffectData(EffectType.Damage, ActionTargetType.Enemy, 10) };
            ability.Description = new List<ActionDescriptionData>
            {
                new ActionDescriptionData("Deals "),
                ActionDescriptionData.ForEffectValue(0, scaleWith, stat),
                new ActionDescriptionData(" damage.")
            };
            return ability;
        }

        [Test]
        public void DescriptionShowsAuthoredValueWithoutAViewer()
        {
            Assert.That(AbilityWithDescription(StatusEffectType.StatBuff).BuildDescription(), Is.EqualTo("Deals 10 damage."));
        }

        [Test]
        public void DescriptionScalesWithViewerBuff()
        {
            var viewer = _assets.Combatant().Stats;
            viewer.ApplyStatus(StatusEffectType.StatBuff, 3, StatType.Strength);

            var text = AbilityWithDescription(StatusEffectType.StatBuff, StatType.Strength).BuildDescription(viewer);

            Assert.That(text, Is.EqualTo("Deals 13 damage."));
        }

        [Test]
        public void DescriptionScalesDownWithViewerWeakness()
        {
            var viewer = _assets.Combatant().Stats;
            viewer.ApplyStatus(StatusEffectType.Weakness, 4);

            Assert.That(AbilityWithDescription(StatusEffectType.Weakness).BuildDescription(viewer), Is.EqualTo("Deals 6 damage."));
        }

        [Test]
        public void SurgeKnowsItsOwnersAndWhetherItIsAFusion()
        {
            var illumine = _assets.Create<PartyMemberData>();
            var phoenix = _assets.Create<PartyMemberData>();
            var terra = _assets.Create<PartyMemberData>();
            var surge = _assets.Create<SurgeData>();
            surge.Owner = illumine;

            Assert.That(surge.IsFusion, Is.False);
            Assert.That(surge.GaugeCost, Is.EqualTo(1f), "A surge costs the full gauge by default.");

            surge.FusionPartner = phoenix;

            Assert.That(surge.IsFusion, Is.True);
            Assert.That(surge.IsOwnedBy(illumine), Is.True);
            Assert.That(surge.IsOwnedBy(phoenix), Is.True);
            Assert.That(surge.IsOwnedBy(terra), Is.False);
        }

        [Test]
        public void AttackDefaultsToOneHit()
        {
            Assert.That(_assets.Create<AttackData>().BaseHitCount, Is.EqualTo(1));
        }

        [Test]
        public void InventoryRespectsStackLimitAndConsumes()
        {
            var potion = _assets.Create<ItemData>();
            potion.StackMax = 3;
            var inventory = new Inventory();

            Assert.That(inventory.Add(potion, 5), Is.EqualTo(3));
            Assert.That(inventory.GetCount(potion), Is.EqualTo(3));
            Assert.That(inventory.TryConsume(potion), Is.True);
            Assert.That(inventory.GetCount(potion), Is.EqualTo(2));

            inventory.TryConsume(potion);
            inventory.TryConsume(potion);

            Assert.That(inventory.TryConsume(potion), Is.False);
            Assert.That(inventory.Items, Is.Empty);
        }

        [Test]
        public void SurgeGaugeSpendsAFractionAndResets()
        {
            var gauge = new SurgeGauge(100);
            gauge.Add(60);

            Assert.That(gauge.TrySpend(1f), Is.False);
            Assert.That(gauge.TrySpend(0.5f), Is.True);
            Assert.That(gauge.Current, Is.EqualTo(10));

            gauge.Reset();
            Assert.That(gauge.Current, Is.EqualTo(0));
        }

        [Test]
        public void EnemyAffinityDefaultsToOneAndUsesListedValues()
        {
            var enemy = _assets.Create<EnemyCharacterData>();
            enemy.Affinities = new List<ElementAffinity>
            {
                new ElementAffinity(Element.Water, 2f),
                new ElementAffinity(Element.Earth, 0.5f),
                new ElementAffinity(Element.Fire, 0f)
            };

            Assert.That(enemy.GetDamageMultiplier(Element.Water), Is.EqualTo(2f));
            Assert.That(enemy.GetDamageMultiplier(Element.Earth), Is.EqualTo(0.5f));
            Assert.That(enemy.GetDamageMultiplier(Element.Fire), Is.EqualTo(0f));
            Assert.That(enemy.GetDamageMultiplier(Element.Wind), Is.EqualTo(1f));
        }

        [Test]
        public void EnemyAbilityHitCountIsAtLeastOne()
        {
            Assert.That(new EnemyAbilityData { HitCount = 0 }.HitCount, Is.EqualTo(1));
        }

        [Test]
        public void EnemyPatternCyclesThroughAbilities()
        {
            var enemy = _assets.Create<EnemyCharacterData>();
            var first = new EnemyAbilityData { Name = "First" };
            var second = new EnemyAbilityData { Name = "Second" };
            enemy.FollowAbilityPattern = true;
            enemy.EnemyAbilityList = new List<EnemyAbilityData> { first, second };

            Assert.That(enemy.GetAbility(0), Is.SameAs(first));
            Assert.That(enemy.GetAbility(1), Is.SameAs(second));
            Assert.That(enemy.GetAbility(2), Is.SameAs(first));
        }
    }
}
