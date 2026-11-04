using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    /// <summary>Picto and Lumina passives firing on combat events (2.7).</summary>
    public class PassiveTests
    {
        private readonly TestAssets _assets = new TestAssets();
        private readonly Dictionary<PartyMemberData, Loadout> _loadouts = new Dictionary<PartyMemberData, Loadout>();
        private CombatTuning _tuning;
        private List<PassiveActivation> _activations;

        [SetUp]
        public void SetUp()
        {
            _tuning = _assets.Tuning();
            _loadouts.Clear();
            _activations = new List<PassiveActivation>();
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        #region Helpers
        private PassiveData Passive(PassiveTrigger trigger, params ActionEffectData[] effects)
        {
            var passive = _assets.Create<PassiveData>();
            passive.DisplayName = trigger.ToString();
            passive.Trigger = trigger;
            passive.Effects = effects.ToList();
            return passive;
        }

        private PictoData Picto(PassiveData passive)
        {
            var picto = _assets.Create<PictoData>();
            picto.DisplayName = passive.DisplayName;
            picto.Passive = passive;
            picto.LuminaCost = 1;
            return picto;
        }

        /// <summary>The member wears a picto for each passive.</summary>
        private PartyMemberData Wearing(PartyMemberData member, params PassiveData[] passives)
        {
            var loadout = new Loadout(_tuning.PictoSlots);
            foreach (var passive in passives)
                Assert.That(loadout.Equip(Picto(passive)), Is.EqualTo(LoadoutError.None));

            _loadouts[member] = loadout;
            return member;
        }

        private Combatant Fighter(PartyMemberData member, int slot) =>
            Combatant.ForPartyMember(member, _tuning, slot, _loadouts.TryGetValue(member, out var loadout) ? loadout : null);

        private BattleModel Battle(BattleSide initiative, IEnumerable<PartyMemberData> party, IEnumerable<EnemyCharacterData> enemies)
        {
            var battle = new BattleModel(_tuning,
                party.Select(Fighter).ToList(),
                enemies.Select((enemy, slot) => Combatant.ForEnemy(enemy, _tuning, slot)).ToList(),
                initiative, new Inventory(), new ActionUnlocks(), new FixedRandom());
            battle.PassiveTriggered += _activations.Add;
            return battle;
        }

        private EnemyCharacterData Slime(int speed = 1, EnemyAbilityData ability = null, string name = "Slime") =>
            _assets.Enemy(name, speed, ability ?? TestAssets.EnemyHit(10));

        private static ActionEffectData Effect(EffectType type, ActionTargetType target, float value,
            StatusEffectType status = StatusEffectType.None, StatType stat = default) =>
            new ActionEffectData(type, target, value, status, stat);

        private static void Attack(BattleModel battle, Combatant target)
        {
            Assert.That(battle.Submit(BattleCommand.Attack(target)), Is.True);
            battle.Tick(0f);
            Assert.That(battle.State, Is.EqualTo(BattleState.ResolvingAction));
            battle.CurrentExecution.RunToEnd();
        }
        #endregion

        [Test]
        public void BattleStartPassiveAppliesWhenTheBattleBegins()
        {
            var stalwart = Passive(PassiveTrigger.OnBattleStart, Effect(EffectType.ModifyStat, ActionTargetType.Self, 2, stat: StatType.Defense));
            var battle = Battle(BattleSide.Party, new[] { Wearing(_assets.Member("Hero", 10), stalwart) }, new[] { Slime() });
            var hero = battle.Party[0];

            battle.Start();

            Assert.That(hero.Stats.FinalStats.Defense, Is.EqualTo(12));
            Assert.That(_activations.Count, Is.EqualTo(1));
            Assert.That(_activations[0].Owner, Is.SameAs(hero));
            Assert.That(_activations[0].Passive, Is.SameAs(stalwart));
            Assert.That(_activations[0].Trigger, Is.EqualTo(PassiveTrigger.OnBattleStart));
        }

        [Test]
        public void AlwaysPassiveEffectsApplyAsTheWearerJoins()
        {
            var passive = Passive(PassiveTrigger.Always, Effect(EffectType.RestoreAP, ActionTargetType.Self, 4));
            var battle = Battle(BattleSide.Party, new[] { Wearing(_assets.Member("Hero", 10), passive) }, new[] { Slime() });

            battle.Start();

            Assert.That(battle.Party[0].Stats.CurrentAp, Is.EqualTo(4));
        }

        [Test]
        public void PassiveWithoutEffectsNeverFires()
        {
            var twinStrike = Passive(PassiveTrigger.Always);
            twinStrike.ExtraAttackHits = 1;
            var battle = Battle(BattleSide.Party, new[] { Wearing(_assets.Member("Hero", 10), twinStrike) }, new[] { Slime() });

            battle.Start();

            Assert.That(_activations, Is.Empty);
        }

        [Test]
        public void TurnStartPassiveFiresEachTimeTheWearerReachesCast()
        {
            var secondWind = Passive(PassiveTrigger.OnTurnStart, Effect(EffectType.RestoreAP, ActionTargetType.Self, 1));
            var battle = Battle(BattleSide.Party, new[] { Wearing(_assets.Member("Hero", 10), secondWind) }, new[] { Slime() });
            var hero = battle.Party[0];

            battle.Start();
            Assert.That(battle.State, Is.EqualTo(BattleState.AwaitingCommand));
            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(1));

            battle.Submit(BattleCommand.Skip());
            battle.Tick(11f);

            Assert.That(battle.CurrentActor, Is.SameAs(hero));
            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(2));
        }

        [Test]
        public void TurnStartPassiveStillFiresWhenAStunCostsTheTurn()
        {
            var lifeblood = Passive(PassiveTrigger.OnTurnStart, Effect(EffectType.Heal, ActionTargetType.Self, 5));
            var battle = Battle(BattleSide.Party, new[] { Wearing(_assets.Member("Hero", 10), lifeblood) }, new[] { Slime() });
            var hero = battle.Party[0];
            hero.Stats.Damage(20);
            hero.Stats.ApplyStatus(StatusEffectType.Stun, 1);

            battle.Start();

            Assert.That(hero.Stats.CurrentHealth, Is.EqualTo(85));
            Assert.That(battle.State, Is.EqualTo(BattleState.TimelineRunning), "The stun still costs the turn.");
        }

        [Test]
        public void HitPassiveFiresForEachLandedHitAndAimsAtTheEnemyHit()
        {
            var kindling = Passive(PassiveTrigger.OnHit, Effect(EffectType.ApplyStatus, ActionTargetType.Enemy, 1, StatusEffectType.Burn));
            var hero = Wearing(_assets.Member("Hero", 10, _assets.Attack(10, hits: 3)), kindling);
            var battle = Battle(BattleSide.Party, new[] { hero }, new[] { Slime(name: "Left"), Slime(name: "Right") });
            battle.Start();
            var right = battle.Enemies[1];

            Attack(battle, right);

            Assert.That(_activations.Count, Is.EqualTo(3));
            Assert.That(_activations.All(activation => activation.Subject == right), Is.True);
            Assert.That(right.Stats.GetStatusValue(StatusEffectType.Burn), Is.EqualTo(3));
            Assert.That(battle.Enemies[0].Stats.GetStatusValue(StatusEffectType.Burn), Is.EqualTo(0));
        }

        [Test]
        public void KillPassiveFiresWhenTheWearersHitFellsAnEnemy()
        {
            var spoils = Passive(PassiveTrigger.OnKill, Effect(EffectType.RestoreAP, ActionTargetType.Self, 2));
            var hero = Wearing(_assets.Member("Hero", 10, _assets.Attack(60, hits: 2)), spoils);
            var battle = Battle(BattleSide.Party, new[] { hero }, new[] { Slime(), Slime() });
            battle.Start();

            Attack(battle, battle.Enemies[0]);

            Assert.That(battle.Enemies[0].IsAlive, Is.False);
            Assert.That(_activations.Count, Is.EqualTo(1), "Only the second hit fells it.");
            Assert.That(battle.Party[0].Stats.CurrentAp, Is.EqualTo(4), "2 AP from hits, 2 from the passive.");
        }

        [Test]
        public void PassiveEffectsDoNotSetOffOtherPassives()
        {
            var scorch = Passive(PassiveTrigger.OnHit, Effect(EffectType.Damage, ActionTargetType.Enemy, 100));
            var spoils = Passive(PassiveTrigger.OnKill, Effect(EffectType.RestoreAP, ActionTargetType.Self, 5));
            var hero = Wearing(_assets.Member("Hero", 10, _assets.Attack(1)), scorch, spoils);
            var battle = Battle(BattleSide.Party, new[] { hero }, new[] { Slime(), Slime() });
            battle.Start();

            Attack(battle, battle.Enemies[0]);

            Assert.That(battle.Enemies[0].IsAlive, Is.False, "The passive's damage fells it.");
            Assert.That(_activations.Single().Passive, Is.SameAs(scorch));
            Assert.That(_activations[0].Effects.Single().Killed, Is.True);
            Assert.That(battle.Party[0].Stats.CurrentAp, Is.EqualTo(1), "No OnKill from a passive's own damage.");
        }

        [Test]
        public void DodgePassiveFiresForEachDodgedHit()
        {
            var quickRecovery = Passive(PassiveTrigger.OnDodge, Effect(EffectType.RestoreAP, ActionTargetType.Self, 1));
            var battle = Battle(BattleSide.Enemy, new[] { Wearing(_assets.Member("Hero", 10), quickRecovery) },
                new[] { Slime(50, TestAssets.EnemyHit(10, 0f, 2)) });
            battle.Start();
            var execution = battle.CurrentExecution;

            execution.ExecuteNext(DefenseOutcome.Dodged);
            execution.ExecuteNext(DefenseOutcome.Hit);

            Assert.That(_activations.Count, Is.EqualTo(1));
            Assert.That(_activations[0].Subject, Is.SameAs(battle.Enemies[0]));
            Assert.That(battle.Party[0].Stats.CurrentAp, Is.EqualTo(2), "1 AP for the dodge, 1 from the passive.");
        }

        [Test]
        public void ParryPassiveFiresForEachParriedHit()
        {
            var charged = Passive(PassiveTrigger.OnParry, Effect(EffectType.FillSurgeGauge, ActionTargetType.Self, 10));
            var battle = Battle(BattleSide.Enemy, new[] { Wearing(_assets.Member("Hero", 10), charged) },
                new[] { Slime(50, TestAssets.EnemyHit(10, 0f, 2)) });
            battle.Start();
            var execution = battle.CurrentExecution;

            execution.ExecuteNext(DefenseOutcome.Parried);
            execution.ExecuteNext(DefenseOutcome.Parried);

            Assert.That(battle.Party[0].SurgeGauge.Current, Is.EqualTo(20));
            Assert.That(battle.Party[0].Stats.CurrentAp, Is.EqualTo(0), "Parries grant no AP.");
        }

        [Test]
        public void LandedEnemyHitsFireNoDefensePassives()
        {
            var quickRecovery = Passive(PassiveTrigger.OnDodge, Effect(EffectType.RestoreAP, ActionTargetType.Self, 1));
            var charged = Passive(PassiveTrigger.OnParry, Effect(EffectType.FillSurgeGauge, ActionTargetType.Self, 10));
            var battle = Battle(BattleSide.Enemy, new[] { Wearing(_assets.Member("Hero", 10), quickRecovery, charged) },
                new[] { Slime(50) });
            battle.Start();

            battle.CurrentExecution.RunToEnd();

            Assert.That(_activations, Is.Empty);
        }

        [Test]
        public void EquippedLuminaPassivesFireLikeWornPictos()
        {
            var member = _assets.Member("Hero", 10);
            var party = PartyState.FromMembers(new[] { member }, _tuning, _assets.Progression());
            var picto = Picto(Passive(PassiveTrigger.OnTurnStart, Effect(EffectType.RestoreAP, ActionTargetType.Self, 3)));
            party.Pictos.Add(picto);
            party.Pictos.Learn(picto);
            Assert.That(party.GetLoadout(member).EquipLumina(picto), Is.EqualTo(LoadoutError.None));
            _loadouts[member] = party.GetLoadout(member);
            var battle = Battle(BattleSide.Party, new[] { member }, new[] { Slime() });

            battle.Start();

            Assert.That(battle.Party[0].Stats.CurrentAp, Is.EqualTo(3));
        }

        [Test]
        public void IncapacitatedWearersFireNothing()
        {
            var spoils = Passive(PassiveTrigger.OnTurnStart, Effect(EffectType.RestoreAP, ActionTargetType.Self, 2));
            var battle = Battle(BattleSide.Party, new[] { _assets.Member("Hero", 10), Wearing(_assets.Member("Ally", 5), spoils) },
                new[] { Slime() });
            battle.Party[1].Stats.Damage(1000);

            battle.Start();
            battle.Submit(BattleCommand.Skip());
            battle.Tick(100f);

            Assert.That(_activations, Is.Empty);
        }
    }
}
