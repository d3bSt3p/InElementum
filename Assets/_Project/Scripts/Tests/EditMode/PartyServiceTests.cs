using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Containers;
using InElementum.Data.Items;
using InElementum.Data.Settings;
using InElementum.Encounters;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class PartyServiceTests
    {
        private TestAssets _assets;
        private CombatTuning _tuning;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssets();
            _tuning = _assets.Tuning();
            _tuning.MaxActivePartySize = 2;
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private PartyService Service() => new PartyService(_tuning, _assets.Progression());

        private ItemData Item(string name)
        {
            var item = _assets.Create<ItemData>();
            item.DisplayName = name;
            item.StackMax = 9;
            return item;
        }

        private static BattleResult Victory(IReadOnlyList<Combatant> participants, int xp = 0, int currency = 0)
        {
            var rewards = new BattleRewards { Experience = xp, Currency = currency };
            return new BattleResult(BattleOutcome.Victory, rewards, participants);
        }

        #region Formation
        [Test]
        public void AddMember_FillsTheActivePartyThenTheBench()
        {
            var party = Service();
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var c = _assets.Member("C", 10);
            var d = _assets.Member("D", 10);

            party.AddMember(a, PartyRole.Active);
            party.AddMember(b, PartyRole.Active);
            party.AddMember(c, PartyRole.Active);
            party.AddMember(d);

            Assert.That(party.Active, Is.EqualTo(new[] { a, b }));
            Assert.That(party.Benched, Is.EqualTo(new[] { c, d }));
            Assert.That(party.GetRole(c), Is.EqualTo(PartyRole.Benched));
            Assert.That(party.Members.Count(), Is.EqualTo(4));
        }

        [Test]
        public void TrySetRole_MovesMembersBetweenActiveAndBench()
        {
            var party = Service();
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            party.AddMember(a, PartyRole.Active);
            party.AddMember(b);

            Assert.That(party.TrySetRole(b, PartyRole.Active), Is.True);
            Assert.That(party.Active, Is.EqualTo(new[] { a, b }));
            Assert.That(party.Benched, Is.Empty);

            Assert.That(party.TrySetRole(a, PartyRole.Benched), Is.True);
            Assert.That(party.Active, Is.EqualTo(new[] { b }));
            Assert.That(party.Benched, Is.EqualTo(new[] { a }));
        }

        [Test]
        public void TrySetRole_RefusesAFullPartyTheLastActiveMemberAndStrangers()
        {
            var party = Service();
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var c = _assets.Member("C", 10);
            party.AddMember(a, PartyRole.Active);
            party.AddMember(b);

            Assert.That(party.TrySetRole(a, PartyRole.Benched), Is.False, "last active member");
            Assert.That(party.TrySetRole(_assets.Member("Stranger", 10), PartyRole.Active), Is.False, "not in party");

            party.TrySetRole(b, PartyRole.Active);
            party.AddMember(c);
            Assert.That(party.TrySetRole(c, PartyRole.Active), Is.False, "active party full");
            Assert.That(party.Active, Is.EqualTo(new[] { a, b }));
        }

        [Test]
        public void TrySwap_TradesSlotsOrRoles()
        {
            var party = Service();
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var c = _assets.Member("C", 10);
            var d = _assets.Member("D", 10);
            party.AddMember(a, PartyRole.Active);
            party.AddMember(b, PartyRole.Active);
            party.AddMember(c);
            party.AddMember(d);

            Assert.That(party.TrySwap(a, b), Is.True);
            Assert.That(party.Active, Is.EqualTo(new[] { b, a }), "two active members trade slots");

            Assert.That(party.TrySwap(c, b), Is.True);
            Assert.That(party.Active, Is.EqualTo(new[] { c, a }), "the benched member takes the leaver's slot");
            Assert.That(party.GetRole(b), Is.EqualTo(PartyRole.Benched));

            Assert.That(party.TrySwap(b, d), Is.False, "two benched members");
            Assert.That(party.TrySwap(a, _assets.Member("Stranger", 10)), Is.False, "not in party");
        }

        [Test]
        public void TryMoveToSlot_ReordersAndFillsFreeSlots()
        {
            _tuning.MaxActivePartySize = 3;
            var party = Service();
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            var c = _assets.Member("C", 10);
            var d = _assets.Member("D", 10);
            party.AddMember(a, PartyRole.Active);
            party.AddMember(b, PartyRole.Active);
            party.AddMember(c);
            party.AddMember(d);

            Assert.That(party.TryMoveToSlot(a, 5), Is.True);
            Assert.That(party.Active, Is.EqualTo(new[] { b, a }), "a slot past the end means last");

            Assert.That(party.TryMoveToSlot(c, 0), Is.True);
            Assert.That(party.Active, Is.EqualTo(new[] { c, b, a }));

            Assert.That(party.TryMoveToSlot(d, 1), Is.False, "active party full");
        }

        [Test]
        public void FromDebugBattle_TakesItsPartyAndItems()
        {
            var potion = Item("Potion");
            var debug = _assets.Create<DebugBattleData>();
            debug.ActiveParty = new List<PartyMemberData> { _assets.Member("A", 10) };
            debug.BenchedParty = new List<PartyMemberData> { _assets.Member("B", 10) };
            debug.StartingItems = new List<ItemStack> { new ItemStack(potion, 3) };

            var party = PartyService.FromDebugBattle(debug, _tuning, _assets.Progression());

            Assert.That(party.Active, Is.EqualTo(debug.ActiveParty));
            Assert.That(party.Benched, Is.EqualTo(debug.BenchedParty));
            Assert.That(party.Items.GetCount(potion), Is.EqualTo(3));
        }
        #endregion

        #region Battle outcome
        [Test]
        public void Victory_CarriesHpIntoTheNextBattle()
        {
            var party = Service();
            var a = _assets.Member("A", 10);
            party.AddMember(a, PartyRole.Active);
            var unit = party.CreateCombatant(a, 0);
            unit.Stats.Damage(30);

            party.ApplyBattle(Victory(new[] { unit }), null);

            Assert.That(party.GetHealth(a), Is.EqualTo(70));
            Assert.That(party.CreateCombatant(a, 0).Stats.CurrentHealth, Is.EqualTo(70));
        }

        [Test]
        public void Victory_GetsDownedMembersUpAt1Hp_AndForgetsFullHealth()
        {
            var party = Service();
            var a = _assets.Member("A", 10);
            var b = _assets.Member("B", 10);
            party.AddMember(a, PartyRole.Active);
            party.AddMember(b, PartyRole.Active);
            var downed = party.CreateCombatant(a, 0);
            downed.Stats.Damage(1000);
            var unhurt = party.CreateCombatant(b, 1);

            party.ApplyBattle(Victory(new[] { downed, unhurt }), null);

            Assert.That(party.GetHealth(a), Is.EqualTo(1));
            Assert.That(party.GetHealth(b), Is.Null);
            Assert.That(party.CreateCombatant(a, 0).IsAlive, Is.True);
        }

        [Test]
        public void Victory_AddsXpCurrencyMaterialsAndKeepsTheItemsLeft()
        {
            var party = Service();
            var a = _assets.Member("A", 10);
            party.AddMember(a, PartyRole.Active);
            var potion = Item("Potion");
            party.Items.Add(potion, 3);

            var gel = _assets.Create<MaterialData>();
            var battleItems = party.Items.Clone();
            battleItems.TryConsume(potion);
            var result = Victory(new[] { party.CreateCombatant(a, 0) }, xp: 100, currency: 25);
            result.Rewards.Materials.Add((gel, 2));
            result.Rewards.Materials.Add((gel, 1));

            var report = party.ApplyBattle(result, battleItems);
            party.ApplyBattle(Victory(new[] { party.CreateCombatant(a, 0) }, currency: 5), null);

            Assert.That(report, Is.Not.Null);
            Assert.That(report.LevelUps.Single().Level, Is.EqualTo(2));
            Assert.That(party.State.GetProgress(a).Level, Is.EqualTo(2));
            Assert.That(party.Currency, Is.EqualTo(30));
            Assert.That(party.Materials[gel], Is.EqualTo(3));
            Assert.That(party.Items.GetCount(potion), Is.EqualTo(2));
        }

        [Test]
        public void Defeat_ChangesNothing()
        {
            var party = Service();
            var a = _assets.Member("A", 10);
            party.AddMember(a, PartyRole.Active);
            var potion = Item("Potion");
            party.Items.Add(potion, 3);
            var unit = party.CreateCombatant(a, 0);
            unit.Stats.Damage(1000);
            var battleItems = party.Items.Clone();
            battleItems.TryConsume(potion);

            var report = party.ApplyBattle(new BattleResult(BattleOutcome.Defeat, null, new[] { unit }), battleItems);

            Assert.That(report, Is.Null);
            Assert.That(party.GetHealth(a), Is.Null);
            Assert.That(party.Items.GetCount(potion), Is.EqualTo(3));
            Assert.That(party.State.GetProgress(a).Xp, Is.EqualTo(0));
        }

        [Test]
        public void RestoreAll_HealsEveryone()
        {
            var party = Service();
            var a = _assets.Member("A", 10);
            party.AddMember(a, PartyRole.Active);
            var unit = party.CreateCombatant(a, 0);
            unit.Stats.Damage(30);
            party.ApplyBattle(Victory(new[] { unit }), null);

            party.RestoreAll();

            Assert.That(party.GetHealth(a), Is.Null);
            Assert.That(party.CreateCombatant(a, 0).Stats.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void InventoryClone_IsIndependent()
        {
            var potion = Item("Potion");
            var items = new Inventory();
            items.Add(potion, 2);

            var copy = items.Clone();
            copy.TryConsume(potion);

            Assert.That(items.GetCount(potion), Is.EqualTo(2));
            Assert.That(copy.GetCount(potion), Is.EqualTo(1));
        }
        #endregion

        #region Encounters
        [Test]
        public void RoomSession_TracksBeatenEnemiesWithIds()
        {
            var session = new RoomSession();
            session.MarkDefeated("Room.A");
            session.MarkDefeated("");
            session.MarkDefeated(null);

            Assert.That(session.IsDefeated("Room.A"), Is.True);
            Assert.That(session.IsDefeated("Room.B"), Is.False);
            Assert.That(session.IsDefeated(null), Is.False);
            Assert.That(session.Defeated.Count, Is.EqualTo(1));
        }

        [Test]
        public void EncounterContext_FromEncounterData_CopiesItsEnemies()
        {
            var encounter = _assets.Create<EncounterData>();
            var golem = _assets.Enemy("Golem", 10);
            encounter.Enemies = new List<EnemyCharacterData> { golem };

            var context = new EncounterContext(encounter, BattleSide.Enemy);
            encounter.Enemies.Clear();

            Assert.That(context.Encounter, Is.SameAs(encounter));
            Assert.That(context.Enemies, Is.EqualTo(new[] { golem }));
            Assert.That(context.Initiative, Is.EqualTo(BattleSide.Enemy));
        }
        #endregion
    }
}
