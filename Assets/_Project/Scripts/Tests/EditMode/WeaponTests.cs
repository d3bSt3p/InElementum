using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Model;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using InElementum.Data.Stats;
using InElementum.Data.Weapons;
using InElementum.Encounters;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class WeaponTests
    {
        private readonly TestAssets _assets = new TestAssets();
        private CombatTuning _tuning;
        private ProgressionTuning _progression;

        [SetUp]
        public void SetUp()
        {
            _tuning = _assets.Tuning();
            _tuning.SetScaling(ScalingGrade.S, 1f);
            _tuning.SetScaling(ScalingGrade.C, 0.35f);
            _tuning.SetScaling(ScalingGrade.E, 0.1f);
            _progression = _assets.Progression();
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private WeaponData Weapon(string name, float basePower, Element element = Element.None, PartyMemberData wielder = null,
            params WeaponScaling[] scaling)
        {
            var weapon = _assets.Create<WeaponData>();
            weapon.DisplayName = name;
            weapon.BasePower = basePower;
            weapon.PowerPerLevel = 2f;
            weapon.MaxLevel = 5;
            weapon.Element = element;
            weapon.Wielder = wielder;
            weapon.Scaling = scaling.ToList();
            return weapon;
        }

        #region Power
        [Test]
        public void Power_IsBasePlusLevelsPlusAttributesTimesTheirGrade()
        {
            var weapon = Weapon("Blade", 3f, Element.None, null,
                new WeaponScaling(StatType.Strength, ScalingGrade.C), new WeaponScaling(StatType.Luck, ScalingGrade.S));
            var stats = new StatBlock(10, 10, 10, 10, 4);

            // 3 + 10 * 0.35 + 4 * 1 = 10.5, rounded away from zero.
            Assert.That(WeaponMath.GetPower(weapon, 1, stats, _tuning), Is.EqualTo(11));
            // Two levels up add 2 each.
            Assert.That(WeaponMath.GetPower(weapon, 3, stats, _tuning), Is.EqualTo(15));
        }

        [Test]
        public void Power_ClampsTheLevelToTheWeaponsRange()
        {
            var weapon = Weapon("Blade", 3f);
            var stats = new StatBlock(10, 10, 10, 10, 10);

            Assert.That(WeaponMath.GetPower(weapon, 0, stats, _tuning), Is.EqualTo(3));
            Assert.That(WeaponMath.GetPower(weapon, 99, stats, _tuning), Is.EqualTo(3 + 2 * 4));
        }

        [Test]
        public void Power_GradesChangeHowMuchAnAttributeAdds()
        {
            var stats = new StatBlock(10, 10, 20, 10, 10);
            var high = Weapon("High", 0f, Element.None, null, new WeaponScaling(StatType.Strength, ScalingGrade.S));
            var low = Weapon("Low", 0f, Element.None, null, new WeaponScaling(StatType.Strength, ScalingGrade.E));

            Assert.That(WeaponMath.GetPower(high, 1, stats, _tuning), Is.EqualTo(20));
            Assert.That(WeaponMath.GetPower(low, 1, stats, _tuning), Is.EqualTo(2));
            Assert.That(high.GetGrade(StatType.Strength), Is.EqualTo(ScalingGrade.S));
            Assert.That(high.GetGrade(StatType.Luck), Is.Null);
        }
        #endregion

        #region Inventory
        [Test]
        public void Equip_NeedsAnOwnedWeaponTheMemberMayWield()
        {
            var hero = _assets.Member("Hero", 10);
            var other = _assets.Member("Other", 10);
            var theirs = Weapon("Theirs", 3f, Element.None, other);
            var unowned = Weapon("Unowned", 3f);
            var weapons = new WeaponInventory();
            weapons.Add(theirs);

            Assert.That(weapons.Equip(hero, unowned), Is.EqualTo(WeaponError.NotOwned));
            Assert.That(weapons.Equip(hero, theirs), Is.EqualTo(WeaponError.WrongWielder));
            Assert.That(weapons.Equip(other, theirs), Is.EqualTo(WeaponError.None));
            Assert.That(weapons.GetWeapon(other), Is.SameAs(theirs));
            Assert.That(weapons.UsableBy(hero), Is.Empty);
        }

        [Test]
        public void Equip_ASharedWeaponHasOneHolder()
        {
            var hero = _assets.Member("Hero", 10);
            var other = _assets.Member("Other", 10);
            var shared = Weapon("Shared", 3f);
            var weapons = new WeaponInventory();
            weapons.Add(shared);
            weapons.Equip(other, shared);

            Assert.That(weapons.Equip(hero, shared), Is.EqualTo(WeaponError.HeldByAnother));
            Assert.That(weapons.GetWeapon(other), Is.SameAs(shared));

            Assert.That(weapons.Equip(hero, shared, takeFromHolder: true), Is.EqualTo(WeaponError.None));
            Assert.That(weapons.GetHolder(shared), Is.SameAs(hero));
            Assert.That(weapons.GetWeapon(other), Is.Null);
        }

        [Test]
        public void Equip_ReplacesTheHeldWeapon()
        {
            var hero = _assets.Member("Hero", 10);
            var first = Weapon("First", 3f, Element.None, hero);
            var second = Weapon("Second", 3f, Element.None, hero);
            var weapons = new WeaponInventory();
            weapons.Add(first);
            weapons.Add(second);

            weapons.Equip(hero, first);
            weapons.Equip(hero, second);

            Assert.That(weapons.GetWeapon(hero), Is.SameAs(second));
            Assert.That(weapons.GetHolder(first), Is.Null);
        }

        [Test]
        public void Levels_StartAt1AndClampToTheMax()
        {
            var weapon = Weapon("Blade", 3f);
            var weapons = new WeaponInventory();

            Assert.That(weapons.SetLevel(weapon, 3), Is.False, "Only owned weapons level up.");
            weapons.Add(weapon);
            Assert.That(weapons.GetLevel(weapon), Is.EqualTo(1));
            weapons.SetLevel(weapon, 99);
            Assert.That(weapons.GetLevel(weapon), Is.EqualTo(5));
        }

        [Test]
        public void AddMember_BringsStartingWeaponsAndHoldsTheFirstFreeOne()
        {
            var hero = _assets.Member("Hero", 10);
            var other = _assets.Member("Other", 10);
            var shared = Weapon("Shared", 3f);
            var own = Weapon("Own", 3f, Element.None, other);
            hero.StartingWeapons = new List<WeaponData> { shared };
            other.StartingWeapons = new List<WeaponData> { shared, own };

            var party = PartyState.FromMembers(new[] { hero, other }, _tuning, _progression);

            Assert.That(party.Weapons.Owned, Is.EquivalentTo(new[] { shared, own }));
            Assert.That(party.Weapons.GetWeapon(hero), Is.SameAs(shared));
            Assert.That(party.Weapons.GetWeapon(other), Is.SameAs(own), "The shared weapon was taken, so they hold their next one.");
        }
        #endregion

        #region Combat
        [Test]
        public void HeldWeaponSetsTheAttacksPowerAndElement()
        {
            var hero = _assets.Member("Hero", 10, _assets.Attack(10));
            var weapon = Weapon("Flame", 20f, Element.Fire, hero);
            var enemy = _assets.Enemy("Slime", 1, TestAssets.EnemyHit(10));
            enemy.Affinities = new List<ElementAffinity> { new ElementAffinity(Element.Fire, 2f) };

            var unit = Combatant.ForPartyMember(hero, _tuning, 0, weapon: weapon);
            var battle = new BattleModel(_tuning, new[] { unit }, new[] { Combatant.ForEnemy(enemy, _tuning, 0) },
                BattleSide.Party, new Inventory(), new ActionUnlocks(), new FixedRandom());
            battle.Start();
            var slime = battle.Enemies[0];

            Assert.That(unit.AttackElement, Is.EqualTo(Element.Fire));
            Assert.That(unit.AttackPower, Is.EqualTo(20f));
            Assert.That(battle.Submit(BattleCommand.Attack(slime)), Is.True);
            battle.Tick(0f);
            battle.CurrentExecution.RunToEnd();

            // 20 power, doubled by the Fire weakness.
            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(60));
        }

        [Test]
        public void WithoutAWeaponTheAttackUsesItsOwnPowerAndElement()
        {
            var attack = _assets.Attack(10);
            attack.Element = Element.Water;
            var hero = _assets.Member("Hero", 10, attack);

            var unit = Combatant.ForPartyMember(hero, _tuning, 0);

            Assert.That(unit.Weapon, Is.Null);
            Assert.That(unit.AttackPower, Is.EqualTo(10f));
            Assert.That(unit.AttackElement, Is.EqualTo(Element.Water));
        }

        [Test]
        public void PartyService_GivesBattleUnitsTheWeaponTheyHoldAtItsLevel()
        {
            var hero = _assets.Member("Hero", 10);
            var first = Weapon("First", 3f, Element.None, hero);
            var second = Weapon("Second", 5f, Element.Wind, hero);
            hero.StartingWeapons = new List<WeaponData> { first, second };
            var service = new PartyService(_tuning, _progression);
            service.AddMember(hero, PartyRole.Active);

            Assert.That(service.CreateCombatant(hero, 0).Weapon, Is.SameAs(first));

            service.State.Weapons.Equip(hero, second);
            service.State.Weapons.SetLevel(second, 2);
            var unit = service.CreateCombatant(hero, 0);

            Assert.That(unit.Weapon, Is.SameAs(second));
            Assert.That(unit.WeaponLevel, Is.EqualTo(2));
            Assert.That(unit.AttackElement, Is.EqualTo(Element.Wind));
            Assert.That(unit.AttackPower, Is.EqualTo(7f));
        }
        #endregion
    }
}
