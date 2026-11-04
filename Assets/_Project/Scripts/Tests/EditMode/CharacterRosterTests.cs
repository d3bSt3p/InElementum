using System.Collections.Generic;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Encounters;
using InElementum.Exploration;
using NUnit.Framework;
using UnityEngine;

namespace InElementum.Tests.EditMode
{
    public class CharacterRosterTests
    {
        private TestAssets _assets;
        private PartyMemberData _a;
        private PartyMemberData _b;
        private PartyMemberData _c;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssets();
            _a = _assets.Member("A", 10);
            _b = _assets.Member("B", 10);
            _c = _assets.Member("C", 10);
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private CharacterRoster Roster() => new CharacterRoster(new[] { _a, _b, _c });

        [Test]
        public void StartsOnTheFirstMember()
        {
            var roster = Roster();
            Assert.AreEqual(0, roster.CurrentIndex);
            Assert.AreSame(_a, roster.Current);
        }

        [Test]
        public void EmptyRosterHasNobody()
        {
            var roster = new CharacterRoster();
            Assert.AreEqual(-1, roster.CurrentIndex);
            Assert.IsNull(roster.Current);
            Assert.IsFalse(roster.Next());
            Assert.IsFalse(roster.Select(0));
        }

        [Test]
        public void NextAndPreviousWrapAround()
        {
            var roster = Roster();
            Assert.IsTrue(roster.Previous());
            Assert.AreSame(_c, roster.Current);
            Assert.IsTrue(roster.Next());
            Assert.AreSame(_a, roster.Current);
            roster.Next();
            Assert.AreSame(_b, roster.Current);
        }

        [Test]
        public void ASingleMemberCannotSwitch()
        {
            var roster = new CharacterRoster(new[] { _a });
            Assert.IsFalse(roster.Next());
            Assert.IsFalse(roster.Previous());
            Assert.AreSame(_a, roster.Current);
        }

        [Test]
        public void SelectJumpsToASlotAndRefusesBadOnes()
        {
            var roster = Roster();
            Assert.IsTrue(roster.Select(2));
            Assert.AreSame(_c, roster.Current);
            Assert.IsFalse(roster.Select(2), "already out");
            Assert.IsFalse(roster.Select(3));
            Assert.IsFalse(roster.Select(-1));
            Assert.AreSame(_c, roster.Current);
        }

        [Test]
        public void ChangedFiresOncePerSwitch()
        {
            var roster = Roster();
            var seen = new List<PartyMemberData>();
            roster.Changed += seen.Add;

            roster.Next();
            roster.Select(1);
            roster.Select(_c);
            CollectionAssert.AreEqual(new[] { _b, _c }, seen);
        }

        [Test]
        public void SetMembersKeepsTheCurrentCharacterWhenStillThere()
        {
            var roster = Roster();
            roster.Select(_b);
            var changes = 0;
            roster.Changed += _ => changes++;

            roster.SetMembers(new[] { _c, _b });
            Assert.AreSame(_b, roster.Current);
            Assert.AreEqual(1, roster.CurrentIndex);
            Assert.AreEqual(0, changes);

            roster.SetMembers(new[] { _a, _c });
            Assert.AreSame(_a, roster.Current, "falls back to the first member");
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SetMembersDropsNullsAndDuplicates()
        {
            var roster = new CharacterRoster(new[] { _a, null, _a, _b });
            Assert.AreEqual(2, roster.Count);
        }

        [Test]
        public void SlotZeroIsAtTheTopAndSlotsRunClockwise()
        {
            Assert.AreEqual(Vector2.up.x, CharacterRoster.SlotDirection(0, 4).x, 1e-4f);
            Assert.AreEqual(Vector2.up.y, CharacterRoster.SlotDirection(0, 4).y, 1e-4f);
            Assert.AreEqual(1f, CharacterRoster.SlotDirection(1, 4).x, 1e-4f, "slot 1 of 4 is to the right");
            Assert.AreEqual(-1f, CharacterRoster.SlotDirection(2, 4).y, 1e-4f, "slot 2 of 4 is at the bottom");
        }

        [Test]
        public void SlotAtPicksTheWedgeAimedAt()
        {
            Assert.AreEqual(0, CharacterRoster.SlotAt(Vector2.up, 4, 0.5f));
            Assert.AreEqual(1, CharacterRoster.SlotAt(Vector2.right, 4, 0.5f));
            Assert.AreEqual(2, CharacterRoster.SlotAt(Vector2.down, 4, 0.5f));
            Assert.AreEqual(3, CharacterRoster.SlotAt(Vector2.left, 4, 0.5f));
            // Just left of straight up still belongs to slot 0, not the last slot.
            Assert.AreEqual(0, CharacterRoster.SlotAt(new Vector2(-0.1f, 1f), 4, 0.5f));

            for (var slot = 0; slot < 7; slot++)
                Assert.AreEqual(slot, CharacterRoster.SlotAt(CharacterRoster.SlotDirection(slot, 7), 7, 0.5f));
        }

        [Test]
        public void SlotAtIgnoresAimInsideTheDeadzone()
        {
            Assert.AreEqual(-1, CharacterRoster.SlotAt(new Vector2(0.2f, 0.2f), 4, 0.5f));
            Assert.AreEqual(-1, CharacterRoster.SlotAt(Vector2.up, 0, 0.5f));
        }

        [Test]
        public void AbilityNamesReadAsWords()
        {
            var member = _assets.Member("D", 10);
            member.ExplorationAbility = ExplorationAbility.HorizontalDash;
            Assert.AreEqual("Horizontal Dash", ExplorationHud.AbilityName(member));
        }

        [Test]
        public void PartyServiceReportsMaxAndCurrentHealth()
        {
            var party = new PartyService(_assets.Tuning(), _assets.Progression());
            party.AddMember(_a, PartyRole.Active);
            var max = party.GetMaxHealth(_a);
            Assert.Greater(max, 0);
            Assert.AreEqual(max, party.GetCurrentHealth(_a), "unhurt members are at max");
            Assert.AreEqual(0, party.GetMaxHealth(_b), "not in the party");
        }
    }
}
