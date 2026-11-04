using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using InElementum.Data.SkillTrees;
using InElementum.Data.Stats;
using InElementum.Editor;
using InElementum.Encounters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace InElementum.Tests.EditMode
{
    public class SkillTreeTests
    {
        private TestAssets _assets;
        private CombatTuning _tuning;
        private ProgressionTuning _progression;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssets();
            _tuning = _assets.Tuning();
            _progression = _assets.Progression();
            _progression.StartingSkillPoints = 2;
            _progression.SkillPointsPerLevel = 1;
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private static SkillNodeData Node(string id, int x, int y, int cost, params string[] requires) => new SkillNodeData
        {
            Id = id,
            Position = new Vector2Int(x, y),
            Cost = cost,
            Kind = SkillNodeKind.StatBoost,
            Modifiers = new List<StatModifier> { new StatModifier(StatType.Strength, ModifierKind.Flat, 2) },
            Prerequisites = requires.ToList()
        };

        /// <summary>root -> a -> b (cost 1 each), and a bridge reachable from a or from a second root.</summary>
        private SkillTreeData Tree()
        {
            var tree = _assets.Create<SkillTreeData>();
            tree.Nodes = new List<SkillNodeData>
            {
                Node("root", 0, 0, 1),
                Node("a", 1, 0, 1, "root"),
                Node("b", 2, 0, 1, "a"),
                Node("root2", 0, 2, 1),
                Node("bridge", 1, 1, 1, "a", "root2")
            };
            return tree;
        }

        private SkillTreeState State(SkillTreeData tree, int level = 1) =>
            new SkillTreeState(tree, new CharacterProgress(_progression, level));

        #region State
        [Test]
        public void Roots_AreAvailable_AndTheRestLocked()
        {
            var tree = Tree();
            var state = State(tree);

            Assert.That(state.GetState(tree.Find("root")), Is.EqualTo(SkillNodeState.Available));
            Assert.That(state.GetState(tree.Find("root2")), Is.EqualTo(SkillNodeState.Available));
            Assert.That(state.GetState(tree.Find("a")), Is.EqualTo(SkillNodeState.Locked));
            Assert.That(state.CanUnlock(tree.Find("a")), Is.EqualTo(SkillUnlockError.NotReachable));
        }

        [Test]
        public void Unlock_SpendsPoints_AndOpensLinkedNodes()
        {
            var tree = Tree();
            var state = State(tree);

            Assert.That(state.Unlock(tree.Find("root")), Is.EqualTo(SkillUnlockError.None));

            Assert.That(state.SkillPointsLeft, Is.EqualTo(1));
            Assert.That(state.GetState(tree.Find("root")), Is.EqualTo(SkillNodeState.Unlocked));
            Assert.That(state.GetState(tree.Find("a")), Is.EqualTo(SkillNodeState.Available));
            Assert.That(state.Unlock(tree.Find("root")), Is.EqualTo(SkillUnlockError.AlreadyUnlocked));
        }

        [Test]
        public void Unlock_IsRefusedWithoutEnoughPoints()
        {
            var tree = Tree();
            var state = State(tree);
            state.Unlock(tree.Find("root"));
            state.Unlock(tree.Find("a"));

            Assert.That(state.SkillPointsLeft, Is.EqualTo(0));
            Assert.That(state.Unlock(tree.Find("b")), Is.EqualTo(SkillUnlockError.NotEnoughSkillPoints));
            Assert.That(state.IsUnlocked(tree.Find("b")), Is.False);
        }

        [Test]
        public void AnyOnePrerequisite_MakesANodeReachable()
        {
            var tree = Tree();
            var state = State(tree);

            state.Unlock(tree.Find("root2"));

            Assert.That(state.CanUnlock(tree.Find("bridge")), Is.EqualTo(SkillUnlockError.None));
        }

        [Test]
        public void SkillPoints_GrowWithLevel()
        {
            var progress = new CharacterProgress(_progression);
            var state = new SkillTreeState(Tree(), progress);
            Assert.That(state.SkillPoints, Is.EqualTo(2));

            progress.AddXp(_progression.GetXpToNextLevel(1));

            Assert.That(state.SkillPoints, Is.EqualTo(3));
        }

        [Test]
        public void Validate_FlagsMissingLinksAndSharedCells()
        {
            var tree = Tree();
            tree.Nodes.Add(Node("ghost", 0, 0, 1, "nowhere"));

            var problems = tree.Validate();

            Assert.That(problems.Any(problem => problem.Contains("nowhere")), Is.True);
            Assert.That(problems.Any(problem => problem.Contains("share cell")), Is.True);
        }
        #endregion

        #region Party and combat
        private (PartyService party, PartyMemberData member, SkillTreeData tree) PartyWithTree()
        {
            var member = _assets.Member("A", 10, _assets.Attack(10, hits: 1));
            var tree = Tree();
            member.SkillTree = tree;
            var party = new PartyService(_tuning, _progression);
            party.AddMember(member, PartyRole.Active);
            return (party, member, tree);
        }

        [Test]
        public void PartyState_GivesEachMemberTheirTree()
        {
            var (party, member, tree) = PartyWithTree();
            var other = _assets.Member("B", 10);
            party.AddMember(other);

            Assert.That(party.State.GetSkillTree(member).Tree, Is.SameAs(tree));
            Assert.That(party.State.GetSkillTree(other), Is.Null, "A member without a tree has no state.");
        }

        [Test]
        public void StatBoosts_RaiseTheCombatantsStats()
        {
            var (party, member, tree) = PartyWithTree();
            party.State.GetSkillTree(member).Unlock(tree.Find("root"));

            var unit = party.CreateCombatant(member, 0);

            Assert.That(unit.Stats.FinalStats.Strength, Is.EqualTo(12));
        }

        [Test]
        public void VitalityBoosts_LeaveAnUnhurtMemberAtFullHealth()
        {
            var (party, member, tree) = PartyWithTree();
            tree.Nodes[0].Modifiers = new List<StatModifier> { new StatModifier(StatType.Vitality, ModifierKind.Flat, 3) };
            party.State.GetSkillTree(member).Unlock(tree.Find("root"));

            var unit = party.CreateCombatant(member, 0);

            Assert.That(unit.Stats.MaxHealth, Is.GreaterThan(Combatant.ForPartyMember(member, _tuning, 0).Stats.MaxHealth));
            Assert.That(unit.Stats.CurrentHealth, Is.EqualTo(unit.Stats.MaxHealth));
        }

        [Test]
        public void PassiveNodes_AddTheirPassiveInCombat()
        {
            var (party, member, tree) = PartyWithTree();
            var passive = _assets.Create<PassiveData>();
            passive.ExtraAttackHits = 2;
            tree.Nodes[0].Kind = SkillNodeKind.Passive;
            tree.Nodes[0].Passive = passive;
            party.State.GetSkillTree(member).Unlock(tree.Find("root"));

            var unit = party.CreateCombatant(member, 0);

            Assert.That(unit.Stats.Passives, Does.Contain(passive));
            Assert.That(StatCalculator.GetAttackHitCount(member.BasicAttack, unit.Stats.Passives), Is.EqualTo(3));
            Assert.That(unit.Stats.FinalStats.Strength, Is.EqualTo(10), "A passive node grants no stats.");
        }

        [Test]
        public void SurgeNodes_UnlockTheSurgeForBattle()
        {
            var (party, member, tree) = PartyWithTree();
            var surge = _assets.Create<SurgeData>();
            surge.Owner = member;
            surge.UnlockSource = UnlockSource.SkillTree;
            member.Surge = surge;
            tree.Nodes[0].Kind = SkillNodeKind.Surge;
            tree.Nodes[0].Surge = surge;

            var locked = new ActionUnlocks();
            foreach (var action in party.State.SkillTreeActions) locked.Unlock(action);
            Assert.That(locked.IsUnlocked(surge, member), Is.False);

            party.State.GetSkillTree(member).Unlock(tree.Find("root"));
            var unlocks = new ActionUnlocks();
            foreach (var action in party.State.SkillTreeActions) unlocks.Unlock(action);

            Assert.That(unlocks.IsUnlocked(surge, member), Is.True);
        }
        #endregion
    }
}
