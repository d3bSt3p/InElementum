using System.Collections.Generic;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Settings;
using InElementum.Encounters;
using InElementum.Menus;
using NUnit.Framework;
using UnityEngine;

namespace InElementum.Tests.EditMode
{
    /// <summary>The menu's rules: cursor movement, equipping a picto into a chosen slot, and using items outside combat.</summary>
    public class MenuTests
    {
        private TestAssets _assets;
        private CombatTuning _tuning;

        [SetUp]
        public void SetUp()
        {
            _assets = new TestAssets();
            _tuning = _assets.Tuning();
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        #region Navigation
        [Test]
        public void Nearest_FollowsTheRowBeforeCuttingDiagonally()
        {
            var points = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 1), new Vector2(2, 0), new Vector2(0, 1) };

            Assert.That(MenuNavigation.Nearest(points, 0, Vector2Int.right), Is.EqualTo(2));
            Assert.That(MenuNavigation.Nearest(points, 0, new Vector2Int(0, 1)), Is.EqualTo(3), "Down is +y on screen.");
        }

        [Test]
        public void Nearest_StaysPutWhenNothingLiesThatWay()
        {
            var points = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0) };

            Assert.That(MenuNavigation.Nearest(points, 0, Vector2Int.left), Is.EqualTo(0));
        }

        [Test]
        public void Wrap_LoopsAroundBothEnds()
        {
            Assert.That(MenuNavigation.Wrap(0, -1, 4), Is.EqualTo(3));
            Assert.That(MenuNavigation.Wrap(3, 1, 4), Is.EqualTo(0));
            Assert.That(MenuNavigation.Wrap(0, 1, 0), Is.EqualTo(-1));
        }
        #endregion

        #region Picto slots
        private PictoData Picto(string name)
        {
            var picto = _assets.Create<PictoData>();
            picto.DisplayName = name;
            picto.Passive = _assets.Create<PassiveData>();
            return picto;
        }

        [Test]
        public void EquipAt_ReplacesTheChosenSlot_AndKeepsTheOthersInPlace()
        {
            var inventory = new PictoInventory();
            var member = _assets.Member("A", 10);
            var loadout = inventory.CreateLoadout(member, 3);
            var a = Picto("A");
            var b = Picto("B");
            var c = Picto("C");
            var d = Picto("D");
            foreach (var picto in new[] { a, b, c, d }) inventory.Add(picto);
            loadout.Equip(a);
            loadout.Equip(b);
            loadout.Equip(c);

            Assert.That(loadout.EquipAt(1, d), Is.EqualTo(LoadoutError.None));

            Assert.That(loadout.Pictos, Is.EqualTo(new[] { a, d, c }));
            Assert.That(inventory.GetWearer(b), Is.Null);
        }

        [Test]
        public void EquipAt_TakesAPictoFromAnotherWearer_WhenAsked()
        {
            var inventory = new PictoInventory();
            var first = inventory.CreateLoadout(_assets.Member("A", 10), 3);
            var second = inventory.CreateLoadout(_assets.Member("B", 10), 3);
            var picto = Picto("Shared");
            inventory.Add(picto);
            first.Equip(picto);

            Assert.That(second.EquipAt(0, picto), Is.EqualTo(LoadoutError.WornByAnother));
            Assert.That(second.EquipAt(0, picto, takeFromWearer: true), Is.EqualTo(LoadoutError.None));

            Assert.That(inventory.GetWearer(picto), Is.SameAs(second));
            Assert.That(first.Pictos, Is.Empty);
        }

        [Test]
        public void EquipAt_ChangesNothingWhenRefused()
        {
            var inventory = new PictoInventory();
            var loadout = inventory.CreateLoadout(_assets.Member("A", 10), 3);
            var worn = Picto("Worn");
            var other = Picto("Other");
            var copy = Picto("Copy");
            copy.Passive = worn.Passive;
            foreach (var picto in new[] { worn, other, copy }) inventory.Add(picto);
            loadout.Equip(worn);
            loadout.Equip(other);

            Assert.That(loadout.EquipAt(1, copy), Is.EqualTo(LoadoutError.DuplicateEffect));

            Assert.That(loadout.Pictos, Is.EqualTo(new[] { worn, other }), "The replaced picto goes back in its slot.");
        }
        #endregion

        #region Items
        private ItemData Potion(bool usableOutsideCombat = true)
        {
            var item = _assets.Create<ItemData>();
            item.DisplayName = "Potion";
            item.StackMax = 9;
            item.UsableOutsideCombat = usableOutsideCombat;
            item.Effects = new List<ActionEffectData> { new ActionEffectData(EffectType.Heal, ActionTargetType.Ally, 30) };
            return item;
        }

        private (PartyService party, PartyMemberData member) HurtParty(int damage)
        {
            var party = new PartyService(_tuning, _assets.Progression());
            var member = _assets.Member("A", 10);
            party.AddMember(member, PartyRole.Active);
            var unit = party.CreateCombatant(member, 0);
            unit.Stats.Damage(damage);
            party.ApplyBattle(new BattleResult(BattleOutcome.Victory, new BattleRewards(), new[] { unit }), null);
            return (party, member);
        }

        [Test]
        public void UseItem_HealsAndSpendsOne()
        {
            var (party, member) = HurtParty(50);
            var potion = Potion();
            party.Items.Add(potion, 2);

            Assert.That(party.UseItem(potion, member), Is.True);

            Assert.That(party.GetHealth(member), Is.EqualTo(80));
            Assert.That(party.Items.GetCount(potion), Is.EqualTo(1));
        }

        [Test]
        public void UseItem_ForgetsHealthOnceFull()
        {
            var (party, member) = HurtParty(10);
            var potion = Potion();
            party.Items.Add(potion);

            party.UseItem(potion, member);

            Assert.That(party.GetHealth(member), Is.Null);
        }

        [Test]
        public void UseItem_IsRefusedAtFullHealth_OrForBattleOnlyItems()
        {
            var (party, member) = HurtParty(0);
            var potion = Potion();
            var battleOnly = Potion(usableOutsideCombat: false);
            party.Items.Add(potion);
            party.Items.Add(battleOnly);

            Assert.That(party.UseItem(potion, member), Is.False, "Already at full health.");
            Assert.That(party.Items.GetCount(potion), Is.EqualTo(1));

            var (hurtParty, hurt) = HurtParty(40);
            hurtParty.Items.Add(battleOnly);
            Assert.That(hurtParty.UseItem(battleOnly, hurt), Is.False);
        }
        #endregion
    }
}
