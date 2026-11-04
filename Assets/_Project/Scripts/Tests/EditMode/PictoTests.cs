using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Characters;
using InElementum.Combat.Model;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using InElementum.Data.Stats;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class PictoTests
    {
        private readonly TestAssets _assets = new TestAssets();
        private CombatTuning _tuning;
        private ProgressionTuning _progression;
        private int _pictoCount;

        [SetUp]
        public void SetUp()
        {
            _tuning = _assets.Tuning();
            _progression = _assets.Progression();
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        #region Helpers
        private static StatModifier Flat(StatType stat, float value) => new StatModifier(stat, ModifierKind.Flat, value);
        private static StatModifier Percent(StatType stat, float value) => new StatModifier(stat, ModifierKind.Percent, value);

        private PassiveData Passive(int extraAttackHits = 0)
        {
            var passive = _assets.Create<PassiveData>();
            passive.ExtraAttackHits = extraAttackHits;
            return passive;
        }

        /// <summary>A picto with its own passive, mastered after 2 victories, costing 2 Lumina points.</summary>
        private PictoData Picto(params StatModifier[] modifiers) => Picto(Passive(), modifiers);

        private PictoData Picto(PassiveData passive, params StatModifier[] modifiers)
        {
            var picto = _assets.Create<PictoData>();
            picto.DisplayName = $"Picto {++_pictoCount}";
            picto.Modifiers = modifiers.ToList();
            picto.Passive = passive;
            picto.BattlesToMaster = 2;
            picto.LuminaCost = 2;
            return picto;
        }

        /// <summary>A party of the given members, at level 1 with 5 Lumina points, who own <paramref name="pictos"/>.</summary>
        private PartyState Party(IEnumerable<PartyMemberData> members, params PictoData[] pictos)
        {
            var party = PartyState.FromMembers(members, _tuning, _progression);
            foreach (var picto in pictos)
                party.Pictos.Add(picto);

            return party;
        }

        private PartyMemberData Member(string name = "Hero") => _assets.Member(name, 10);
        #endregion

        #region Stats
        [Test]
        public void FinalStatsCombineEveryWornPicto()
        {
            var loadout = new Loadout(3);
            loadout.Equip(Picto(Flat(StatType.Strength, 5)));
            loadout.Equip(Picto(Flat(StatType.Defense, 3)));
            loadout.Equip(Picto(Percent(StatType.Strength, 0.5f)));

            var result = StatCalculator.GetFinalStats(new StatBlock(10, 10, 10, 10, 10), loadout);

            Assert.That(result.Strength, Is.EqualTo(23)); // (10 + 5) * 1.5 rounds up from 22.5
            Assert.That(result.Defense, Is.EqualTo(13));
            Assert.That(result.Speed, Is.EqualTo(10));
        }

        [Test]
        public void LuminasGiveTheirPassiveButNoStats()
        {
            var hero = Member();
            var strong = Picto(Flat(StatType.Strength, 5));
            var party = Party(new[] { hero }, strong);
            party.Pictos.Learn(strong);
            var loadout = party.GetLoadout(hero);

            Assert.That(loadout.EquipLumina(strong), Is.EqualTo(LoadoutError.None));

            Assert.That(StatCalculator.GetFinalStats(new StatBlock(10, 10, 10, 10, 10), loadout).Strength, Is.EqualTo(10));
            Assert.That(loadout.Passives, Is.EqualTo(new[] { strong.Passive }));
        }

        [Test]
        public void FinalStatsIncludeStatusesAndMealBuff()
        {
            var result = StatCalculator.GetFinalStats(new StatBlock(10, 10, 10, 10, 10), null,
                new[] { Flat(StatType.Luck, 2) }, new[] { Flat(StatType.Luck, 3) });

            Assert.That(result.Luck, Is.EqualTo(15));
        }

        [Test]
        public void CharacterStatsApplyLoadoutToFinalStatsAndHealth()
        {
            var loadout = new Loadout(3);
            loadout.Equip(Picto(Flat(StatType.Vitality, 5)));
            var stats = new CharacterStats(new StatBlock(10, 10, 10, 10, 10), _tuning);

            stats.SetLoadout(loadout);

            Assert.That(stats.FinalStats.Vitality, Is.EqualTo(15));
            Assert.That(stats.MaxHealth, Is.EqualTo(150));
        }

        [Test]
        public void AttackHitCountIsBasePlusExtraHitsFromPictosAndLuminas()
        {
            var hero = Member();
            var twin = Picto(Passive(extraAttackHits: 1));
            var triple = Picto(Passive(extraAttackHits: 2));
            var party = Party(new[] { hero }, twin, triple);
            party.Pictos.Learn(triple);
            var loadout = party.GetLoadout(hero);
            var attack = _assets.Attack(10, hits: 2);

            Assert.That(StatCalculator.GetAttackHitCount(attack, (Loadout)null), Is.EqualTo(2));

            loadout.Equip(twin);
            Assert.That(StatCalculator.GetAttackHitCount(attack, loadout), Is.EqualTo(3), "Worn picto.");

            loadout.EquipLumina(triple);
            Assert.That(StatCalculator.GetAttackHitCount(attack, loadout), Is.EqualTo(5), "Worn picto plus Lumina.");
        }

        [Test]
        public void BasicAttackInBattleUsesExtraHitsFromTheLoadout()
        {
            var hero = _assets.Member("Hero", 10, _assets.Attack(10, hits: 1));
            var loadout = new Loadout(3);
            loadout.Equip(Picto(Passive(extraAttackHits: 2)));
            var enemy = _assets.Enemy("Slime", 1, TestAssets.EnemyHit(10));
            var battle = new BattleModel(_tuning,
                new[] { Combatant.ForPartyMember(hero, _tuning, 0, loadout) },
                new[] { Combatant.ForEnemy(enemy, _tuning, 0) }, BattleSide.Party, new Inventory(), new ActionUnlocks(), new FixedRandom());
            battle.Start();

            battle.Submit(BattleCommand.Attack(battle.Enemies[0]));
            battle.Tick(0f);
            battle.CurrentExecution.RunToEnd();

            Assert.That(battle.Enemies[0].Stats.CurrentHealth, Is.EqualTo(70), "Three hits of 10.");
            Assert.That(battle.Party[0].Stats.CurrentAp, Is.EqualTo(3));
        }
        #endregion

        #region Wearing pictos
        [Test]
        public void PictoSlotsAreLimitedByTuning()
        {
            var loadout = new Loadout(_tuning.PictoSlots);

            for (var i = 0; i < 3; i++)
                Assert.That(loadout.Equip(Picto()), Is.EqualTo(LoadoutError.None));

            Assert.That(loadout.Equip(Picto()), Is.EqualTo(LoadoutError.SlotsFull));
            Assert.That(loadout.Pictos.Count, Is.EqualTo(3));
        }

        [Test]
        public void EachPictoHasOneWearer()
        {
            var phoenix = Member("Phoenix");
            var terra = Member("Terra");
            var picto = Picto();
            var party = Party(new[] { phoenix, terra }, picto);

            Assert.That(party.GetLoadout(phoenix).Equip(picto), Is.EqualTo(LoadoutError.None));
            Assert.That(party.GetLoadout(terra).Equip(picto), Is.EqualTo(LoadoutError.WornByAnother));
            Assert.That(party.Pictos.GetWearer(picto), Is.SameAs(party.GetLoadout(phoenix)));

            party.GetLoadout(phoenix).Unequip(picto);

            Assert.That(party.GetLoadout(terra).Equip(picto), Is.EqualTo(LoadoutError.None));
            Assert.That(party.Pictos.GetWearer(picto), Is.SameAs(party.GetLoadout(terra)));
        }

        [Test]
        public void OnlyOwnedPictosCanBeWornFromTheInventory()
        {
            var hero = Member();
            var party = Party(new[] { hero });
            var picto = Picto();

            Assert.That(party.GetLoadout(hero).Equip(picto), Is.EqualTo(LoadoutError.NotOwned));
            Assert.That(party.Pictos.Add(picto), Is.True);
            Assert.That(party.Pictos.Add(picto), Is.False, "Owning a picto twice adds nothing.");
            Assert.That(party.GetLoadout(hero).Equip(picto), Is.EqualTo(LoadoutError.None));
            Assert.That(party.GetLoadout(hero).Equip(picto), Is.EqualTo(LoadoutError.AlreadyEquipped));
        }

        [Test]
        public void TwoSourcesOfTheSamePassiveAreRejected()
        {
            var shared = Passive();
            var first = Picto(shared);
            var second = Picto(shared);
            var loadout = new Loadout(3);

            Assert.That(loadout.Equip(first), Is.EqualTo(LoadoutError.None));
            Assert.That(loadout.Equip(second), Is.EqualTo(LoadoutError.DuplicateEffect));
        }

        [Test]
        public void StatOnlyPictosWithNoPassiveStack()
        {
            var loadout = new Loadout(3);

            Assert.That(loadout.Equip(Picto(null, Flat(StatType.Luck, 1))), Is.EqualTo(LoadoutError.None));
            Assert.That(loadout.Equip(Picto(null, Flat(StatType.Luck, 1))), Is.EqualTo(LoadoutError.None));
            Assert.That(loadout.Passives, Is.Empty);
        }

        [Test]
        public void NewMembersWearTheirDefaultPictosUnlessAnotherMemberAlreadyDoes()
        {
            var shared = Picto();
            var own = Picto();
            var phoenix = Member("Phoenix");
            var terra = Member("Terra");
            phoenix.DefaultPictos = new List<PictoData> { shared };
            terra.DefaultPictos = new List<PictoData> { shared, own };

            var party = Party(new[] { phoenix, terra });

            Assert.That(party.GetLoadout(phoenix).Pictos, Is.EqualTo(new[] { shared }));
            Assert.That(party.GetLoadout(terra).Pictos, Is.EqualTo(new[] { own }));
            Assert.That(party.Pictos.Owned, Is.EquivalentTo(new[] { shared, own }));
        }

        [Test]
        public void StandaloneDefaultLoadoutWearsDefaultsUpToTheSlotCount()
        {
            var hero = Member();
            hero.DefaultPictos = new List<PictoData> { Picto(), Picto(), Picto(), Picto() };

            var loadout = Loadout.FromDefaults(hero, _tuning);

            Assert.That(loadout.Pictos, Is.EqualTo(hero.DefaultPictos.Take(3)));
        }
        #endregion

        #region Mastery
        [Test]
        public void MasteryReachingTheThresholdLearnsTheLuminaPartyWide()
        {
            var phoenix = Member("Phoenix");
            var terra = Member("Terra");
            var picto = Picto();
            var party = Party(new[] { phoenix, terra }, picto);
            party.GetLoadout(phoenix).Equip(picto);

            Assert.That(party.Pictos.RecordVictory(new[] { phoenix }), Is.Empty);
            Assert.That(party.Pictos.GetMastery(picto), Is.EqualTo(1));
            Assert.That(party.Pictos.IsLearned(picto), Is.False);
            Assert.That(party.GetLoadout(terra).EquipLumina(picto), Is.EqualTo(LoadoutError.NotLearned));

            Assert.That(party.Pictos.RecordVictory(new[] { phoenix }), Is.EqualTo(new[] { picto }));
            Assert.That(party.Pictos.IsLearned(picto), Is.True);
            Assert.That(party.GetLoadout(terra).EquipLumina(picto), Is.EqualTo(LoadoutError.None), "Terra never wore it.");

            Assert.That(party.Pictos.RecordVictory(new[] { phoenix }), Is.Empty, "A Lumina is learned once.");
        }

        [Test]
        public void OnlyPictosWornByParticipantsGainMastery()
        {
            var fighter = Member("Fighter");
            var benched = Member("Benched");
            var fought = Picto();
            var idle = Picto();
            var spare = Picto();
            var party = Party(new[] { fighter, benched }, fought, idle, spare);
            party.GetLoadout(fighter).Equip(fought);
            party.GetLoadout(benched).Equip(idle);

            party.Pictos.RecordVictory(new[] { fighter });

            Assert.That(party.Pictos.GetMastery(fought), Is.EqualTo(1));
            Assert.That(party.Pictos.GetMastery(idle), Is.EqualTo(0));
            Assert.That(party.Pictos.GetMastery(spare), Is.EqualTo(0), "Owned but unworn.");
        }
        #endregion

        #region Luminas
        [Test]
        public void LuminaPointsLimitEquippedLuminasAndGrowWithLevel()
        {
            var hero = Member();
            var first = Picto();
            var second = Picto();
            first.LuminaCost = 3;
            second.LuminaCost = 3;
            var party = Party(new[] { hero }, first, second);
            party.Pictos.Learn(first);
            party.Pictos.Learn(second);
            var loadout = party.GetLoadout(hero);

            Assert.That(loadout.LuminaPoints, Is.EqualTo(5));
            Assert.That(loadout.EquipLumina(first), Is.EqualTo(LoadoutError.None));
            Assert.That(loadout.LuminaPointsLeft, Is.EqualTo(2));
            Assert.That(loadout.EquipLumina(second), Is.EqualTo(LoadoutError.NotEnoughLuminaPoints));

            party.GetProgress(hero).AddXp(100);

            Assert.That(loadout.LuminaPoints, Is.EqualTo(6));
            Assert.That(loadout.EquipLumina(second), Is.EqualTo(LoadoutError.None));
            Assert.That(loadout.LuminaPointsLeft, Is.EqualTo(0));

            loadout.UnequipLumina(first);
            Assert.That(loadout.LuminaPointsLeft, Is.EqualTo(3));
        }

        [Test]
        public void ThereIsNoSlotLimitOnLuminasOnlyPoints()
        {
            var hero = Member();
            var party = Party(new[] { hero });
            var loadout = party.GetLoadout(hero);

            for (var i = 0; i < 5; i++)
            {
                var picto = Picto();
                picto.LuminaCost = 1;
                party.Pictos.Add(picto);
                party.Pictos.Learn(picto);
                Assert.That(loadout.EquipLumina(picto), Is.EqualTo(LoadoutError.None));
            }

            Assert.That(loadout.Luminas.Count, Is.GreaterThan(_tuning.PictoSlots));
        }

        [Test]
        public void CannotEquipTheLuminaOfAWornPicto()
        {
            var hero = Member();
            var picto = Picto();
            var party = Party(new[] { hero }, picto);
            party.Pictos.Learn(picto);
            var loadout = party.GetLoadout(hero);
            loadout.Equip(picto);

            Assert.That(loadout.EquipLumina(picto), Is.EqualTo(LoadoutError.PictoWorn));

            loadout.Unequip(picto);
            Assert.That(loadout.EquipLumina(picto), Is.EqualTo(LoadoutError.None));
            Assert.That(loadout.Equip(picto), Is.EqualTo(LoadoutError.DuplicateEffect), "Its Lumina already gives the passive.");
        }

        [Test]
        public void StandaloneLoadoutsCannotEquipLuminas()
        {
            var picto = Picto();

            Assert.That(new Loadout(3).EquipLumina(picto), Is.EqualTo(LoadoutError.NotLearned));
        }
        #endregion

        #region Progression
        [Test]
        public void XpLevelsUpAndCarriesTheRemainder()
        {
            var progress = new CharacterProgress(_progression);

            Assert.That(progress.AddXp(90), Is.EqualTo(0));
            Assert.That(progress.AddXp(230), Is.EqualTo(3), "320 XP crosses three 100 XP levels.");
            Assert.That(progress.Level, Is.EqualTo(4));
            Assert.That(progress.Xp, Is.EqualTo(20));
            Assert.That(progress.LuminaPoints, Is.EqualTo(8));
        }

        [Test]
        public void XpStopsAtTheLevelCap()
        {
            _progression.MaxLevel = 3;
            var progress = new CharacterProgress(_progression);

            Assert.That(progress.AddXp(10000), Is.EqualTo(2));
            Assert.That(progress.Level, Is.EqualTo(3));
            Assert.That(progress.Xp, Is.EqualTo(0));
            Assert.That(progress.XpToNextLevel, Is.EqualTo(0));
            Assert.That(progress.AddXp(500), Is.EqualTo(0));
        }

        [Test]
        public void XpCurveGrowsEachLevel()
        {
            _progression.XpToLevelTwo = 100;
            _progression.XpGrowthPerLevel = 1.5f;

            Assert.That(_progression.GetXpToNextLevel(1), Is.EqualTo(100));
            Assert.That(_progression.GetXpToNextLevel(2), Is.EqualTo(150));
            Assert.That(_progression.GetXpToNextLevel(3), Is.EqualTo(225));
        }

        [Test]
        public void VictoryGrantsXpMasteryAndPictoDrops()
        {
            var hero = Member("Hero");
            var benched = Member("Benched");
            var worn = Picto();
            worn.BattlesToMaster = 1;
            hero.DefaultPictos = new List<PictoData> { worn };
            var owned = Picto();
            var drop = Picto();
            var party = Party(new[] { hero, benched }, owned);

            var report = party.ApplyVictory(new[] { hero }, 150, new[] { drop, owned });

            Assert.That(party.GetProgress(hero).Level, Is.EqualTo(2));
            Assert.That(party.GetProgress(benched).Level, Is.EqualTo(1), "Only participants gain XP.");
            Assert.That(report.LevelUps.Select(x => (x.Member, x.Level, x.LevelsGained)), Is.EqualTo(new[] { (hero, 2, 1) }));
            Assert.That(report.LearnedLuminas, Is.EqualTo(new[] { worn }));
            Assert.That(report.NewPictos, Is.EqualTo(new[] { drop }), "Pictos already owned are not new.");
            Assert.That(party.Pictos.Owns(drop), Is.True);
            Assert.That(party.Pictos.GetMastery(drop), Is.EqualTo(0), "Drops arrive after mastery is counted.");
        }

        [Test]
        public void BattleRewardsIncludeEachEnemysPictoDrop()
        {
            var drop = Picto();
            var enemy = _assets.Enemy("Boss", 1);
            enemy.Rewards = new RewardTable { PictoDrop = drop };

            var rewards = BattleRewards.Roll(new[] { Combatant.ForEnemy(enemy, _tuning, 0) }, new FixedRandom());

            Assert.That(rewards.Pictos, Is.EqualTo(new[] { drop }));
        }
        #endregion
    }
}
