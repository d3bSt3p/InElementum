using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    /// <summary>The relationship stub (2.7) and discovered enemy weaknesses (2.8).</summary>
    public class RelationshipTests
    {
        private readonly TestAssets _assets = new TestAssets();
        private CombatTuning _tuning;

        [SetUp]
        public void SetUp() => _tuning = _assets.Tuning();

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private BattleModel Battle(IEnumerable<PartyMemberData> party, IEnumerable<EnemyCharacterData> enemies, ActionUnlocks unlocks,
            ElementKnowledge knowledge = null)
        {
            return new BattleModel(_tuning,
                party.Select((member, slot) => Combatant.ForPartyMember(member, _tuning, slot)).ToList(),
                enemies.Select((enemy, slot) => Combatant.ForEnemy(enemy, _tuning, slot)).ToList(),
                BattleSide.Party, new Inventory(), unlocks, new FixedRandom(), knowledge);
        }

        private SurgeData FusionSurge(PartyMemberData owner, PartyMemberData partner)
        {
            var surge = _assets.Action<SurgeData>(ActionTargetType.AllEnemies, 0f,
                new ActionEffectData(EffectType.Damage, ActionTargetType.AllEnemies, 50));
            surge.Owner = owner;
            surge.FusionPartner = partner;
            surge.UnlockSource = UnlockSource.RelationshipLevel5;
            return surge;
        }

        #region Relationship levels
        [Test]
        public void PairsStartAtZeroAndReadTheSameEitherWay()
        {
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var relationships = new RelationshipService();

            Assert.That(relationships.GetLevel(a, b), Is.EqualTo(0));

            relationships.SetLevel(a, b, 3);

            Assert.That(relationships.GetLevel(b, a), Is.EqualTo(3));
            Assert.That(relationships.HasLevel(a, b, 3), Is.True);
            Assert.That(relationships.HasLevel(a, b, 4), Is.False);
        }

        [Test]
        public void LevelsAreClampedAndAMemberHasNoRelationshipWithThemself()
        {
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var relationships = new RelationshipService();

            relationships.SetLevel(a, b, 9);
            relationships.SetLevel(a, a, 4);

            Assert.That(relationships.GetLevel(a, b), Is.EqualTo(RelationshipService.MaxLevel));
            Assert.That(relationships.GetLevel(a, a), Is.EqualTo(0));
            Assert.That(relationships.GetLevel(a, null), Is.EqualTo(0));
        }

        [Test]
        public void DebugLevelOverridesEveryPair()
        {
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var relationships = new RelationshipService();
            relationships.SetLevel(a, b, 1);

            relationships.DebugLevel = 4;
            Assert.That(relationships.GetLevel(a, b), Is.EqualTo(4));

            relationships.DebugLevel = null;
            Assert.That(relationships.GetLevel(a, b), Is.EqualTo(1));
        }

        [Test]
        public void FightingTogetherRaisesEachPairOnce()
        {
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var c = _assets.Member("C", 10);
            var relationships = new RelationshipService();
            var raised = new List<PartyPair>();
            relationships.FoughtTogether += raised.Add;

            relationships.RecordFoughtTogether(new[] { a, b, c, a, null });

            Assert.That(raised, Is.EquivalentTo(new[] { new PartyPair(a, b), new PartyPair(a, c), new PartyPair(b, c) }));
            Assert.That(new PartyPair(a, b), Is.EqualTo(new PartyPair(b, a)));
        }

        [Test]
        public void VictoryRecordsEveryParticipantPair()
        {
            var relationships = new RelationshipService();
            var raised = new List<PartyPair>();
            relationships.FoughtTogether += raised.Add;
            var hero = _assets.Member("Hero", 10, _assets.Attack(100));
            var ally = _assets.Member("Ally", 5);
            var battle = Battle(new[] { hero, ally }, new[] { _assets.Enemy("Slime", 1, TestAssets.EnemyHit(10)) },
                new ActionUnlocks { Relationships = relationships });
            battle.Start();

            battle.Submit(BattleCommand.Attack(battle.Enemies[0]));
            battle.Tick(0f);
            battle.CurrentExecution.RunToEnd();
            battle.FinishAction();

            Assert.That(battle.State, Is.EqualTo(BattleState.Victory));
            Assert.That(raised, Is.EqualTo(new[] { new PartyPair(hero, ally) }));
            Assert.That(battle.Result.FoughtTogether, Is.EqualTo(raised));
        }
        #endregion

        #region Relationship unlocks
        [Test]
        public void FusionSurgeUnlocksForBothPartnersAtLevelFive()
        {
            var illumine = _assets.Member("Illumine", 10);
            var phoenix = _assets.Member("Phoenix", 10);
            var surge = FusionSurge(illumine, phoenix);
            var relationships = new RelationshipService();
            var unlocks = new ActionUnlocks { Relationships = relationships };
            unlocks.AddFusionSurge(surge);

            // Only Phoenix fights: the partner does not need to be there.
            var battle = Battle(new[] { phoenix }, new[] { _assets.Enemy("Slime", 1, TestAssets.EnemyHit(10)) }, unlocks);
            var actor = battle.Party[0];

            relationships.SetLevel(illumine, phoenix, 4);
            Assert.That(battle.GetSurges(actor), Is.Empty);

            relationships.SetLevel(illumine, phoenix, 5);
            Assert.That(battle.GetSurges(actor), Is.EqualTo(new[] { surge }));
        }

        [Test]
        public void FusionSurgeNeedsBeingKnownOrUnlocked()
        {
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var surge = FusionSurge(a, b);
            var unlocks = new ActionUnlocks();
            var battle = Battle(new[] { a }, new[] { _assets.Enemy("Slime", 1, TestAssets.EnemyHit(10)) }, unlocks);

            unlocks.UnlockEverything = true;
            Assert.That(battle.GetSurges(battle.Party[0]), Is.Empty, "Unknown Fusion Surges cannot be found.");

            unlocks.AddFusionSurge(surge);
            Assert.That(battle.GetSurges(battle.Party[0]), Is.EqualTo(new[] { surge }));
        }

        [Test]
        public void RelationshipAbilityUnlocksAtItsLevelWithItsPartner()
        {
            var hero = _assets.Member("Hero", 10);
            var partner = _assets.Member("Partner", 10);
            var stranger = _assets.Member("Stranger", 10);
            var ability = _assets.Action<AbilityData>(ActionTargetType.Enemy, 0f,
                new ActionEffectData(EffectType.Damage, ActionTargetType.Enemy, 10));
            ability.UnlockSource = UnlockSource.RelationshipLevel3;
            ability.RelationshipPartner = partner;
            hero.Abilities = new List<AbilityData> { ability };
            var relationships = new RelationshipService();
            var battle = Battle(new[] { hero }, new[] { _assets.Enemy("Slime", 1, TestAssets.EnemyHit(10)) },
                new ActionUnlocks { Relationships = relationships });

            relationships.SetLevel(hero, stranger, 5);
            relationships.SetLevel(hero, partner, 2);
            Assert.That(battle.GetAbilities(battle.Party[0]), Is.Empty);

            relationships.SetLevel(hero, partner, 3);
            Assert.That(battle.GetAbilities(battle.Party[0]), Is.EqualTo(new[] { ability }));
        }

        [Test]
        public void RelationshipActionsStayLockedWithoutARelationshipService()
        {
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var unlocks = new ActionUnlocks();

            Assert.That(unlocks.IsUnlocked(FusionSurge(a, b), a), Is.False);
        }
        #endregion

        #region Element discovery
        [Test]
        public void PartyHitsDiscoverTheirElementOnTheEnemyType()
        {
            var fireAttack = _assets.Attack(10);
            fireAttack.Element = Element.Fire;
            var slimeData = _assets.Enemy("Slime", 1, TestAssets.EnemyHit(10));
            slimeData.Affinities = new List<ElementAffinity> { new ElementAffinity(Element.Fire, 2f) };
            var knowledge = new ElementKnowledge();
            var battle = Battle(new[] { _assets.Member("Hero", 10, fireAttack) }, new[] { slimeData, slimeData }, new ActionUnlocks(), knowledge);
            battle.Start();
            var slime = battle.Enemies[0];

            Assert.That(battle.IsElementKnown(slime, Element.Fire), Is.False);
            Assert.That(battle.IsElementKnown(slime, Element.None), Is.True, "None has no reaction to find.");

            battle.Submit(BattleCommand.Attack(slime));
            battle.Tick(0f);
            battle.CurrentExecution.RunToEnd();

            Assert.That(battle.IsElementKnown(slime, Element.Fire), Is.True);
            Assert.That(battle.IsElementKnown(battle.Enemies[1], Element.Fire), Is.True, "Known for every enemy of that type.");
            Assert.That(battle.IsElementKnown(slime, Element.Water), Is.False);
            Assert.That(knowledge.GetDiscovered(slimeData), Is.EqualTo(new[] { Element.Fire }));
        }

        [Test]
        public void DiscoveryCarriesIntoLaterBattles()
        {
            var slimeData = _assets.Enemy("Slime", 1, TestAssets.EnemyHit(10));
            var knowledge = new ElementKnowledge();
            knowledge.Discover(slimeData, Element.Water);

            var battle = Battle(new[] { _assets.Member("Hero", 10) }, new[] { slimeData }, new ActionUnlocks(), knowledge);

            Assert.That(battle.IsElementKnown(battle.Enemies[0], Element.Water), Is.True);
        }

        [Test]
        public void EnemyHitsDiscoverNothing()
        {
            var slimeData = _assets.Enemy("Slime", 50, TestAssets.EnemyHit(10, element: Element.Fire));
            var knowledge = new ElementKnowledge();
            var battle = new BattleModel(_tuning, new[] { Combatant.ForPartyMember(_assets.Member("Hero", 10), _tuning, 0) },
                new[] { Combatant.ForEnemy(slimeData, _tuning, 0) }, BattleSide.Enemy,
                new Inventory(), new ActionUnlocks(), new FixedRandom(), knowledge);
            battle.Start();

            battle.CurrentExecution.RunToEnd();

            Assert.That(knowledge.GetDiscovered(slimeData), Is.Empty);
        }

        [Test]
        public void AuthoredAffinityIgnoresTheBreakResistanceDrop()
        {
            var slimeData = _assets.Enemy("Slime", 1, TestAssets.EnemyHit(10));
            slimeData.Affinities = new List<ElementAffinity> { new ElementAffinity(Element.Earth, 0.5f) };
            var slime = Combatant.ForEnemy(slimeData, _tuning, 0);
            slime.Stats.IgnoreResistances = true;

            Assert.That(slime.Stats.GetElementMultiplier(Element.Earth), Is.EqualTo(1f));
            Assert.That(ElementKnowledge.GetAffinity(slimeData, Element.Earth), Is.EqualTo(0.5f));
            Assert.That(ElementKnowledge.GetAffinity(slimeData, Element.Fire), Is.EqualTo(1f));
        }
        #endregion
    }
}
