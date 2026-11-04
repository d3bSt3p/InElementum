using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Settings;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class BattleModelTests
    {
        private readonly TestAssets _assets = new TestAssets();
        private CombatTuning _tuning;
        private Inventory _inventory;
        private ActionUnlocks _unlocks;

        [SetUp]
        public void SetUp()
        {
            _tuning = _assets.Tuning();
            _inventory = new Inventory();
            _unlocks = new ActionUnlocks();
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private BattleModel Battle(BattleSide initiative, IEnumerable<PartyMemberData> party, IEnumerable<EnemyCharacterData> enemies)
        {
            var active = party.Select((member, slot) => Combatant.ForPartyMember(member, _tuning, slot)).ToList();
            var foes = enemies.Select((enemy, slot) => Combatant.ForEnemy(enemy, _tuning, slot)).ToList();

            return new BattleModel(_tuning, active, foes, initiative, _inventory, _unlocks, new FixedRandom());
        }

        private BattleModel OneOnOne(BattleSide initiative, PartyMemberData hero, EnemyCharacterData enemy) =>
            Battle(initiative, new[] { hero }, new[] { enemy });

        private EnemyCharacterData Slime(int speed = 1, EnemyAbilityData ability = null) =>
            _assets.Enemy("Slime", speed, ability ?? TestAssets.EnemyHit(10));

        private static ActionEffectData Effect(EffectType type, ActionTargetType target, float value) =>
            new ActionEffectData(type, target, value);

        /// <summary>Submits the command, lets a zero cast time complete, and resolves every step with no defense input.</summary>
        private static void Resolve(BattleModel battle, BattleCommand command)
        {
            Assert.That(battle.Submit(command), Is.True, battle.Validate(command));
            battle.Tick(0f);
            Assert.That(battle.State, Is.EqualTo(BattleState.ResolvingAction));
            battle.CurrentExecution.RunToEnd();
        }

        #region Flow
        [Test]
        public void PartyInitiativeOpensWithAPartyCommand()
        {
            var battle = OneOnOne(BattleSide.Party, _assets.Member("Hero", 10), Slime());

            battle.Start();

            Assert.That(battle.State, Is.EqualTo(BattleState.AwaitingCommand));
            Assert.That(battle.CurrentActor, Is.SameAs(battle.Party[0]));
        }

        [Test]
        public void EnemyInitiativeOpensWithAnEnemyAction()
        {
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 50), Slime());

            battle.Start();

            Assert.That(battle.State, Is.EqualTo(BattleState.ResolvingAction));
            Assert.That(battle.CurrentExecution.Actor, Is.SameAs(battle.Enemies[0]));
        }

        [Test]
        public void ActivePartyIsCapped()
        {
            var members = Enumerable.Range(0, 6).Select(i => _assets.Member("M" + i, 10)).ToList();
            var battle = Battle(BattleSide.Party, members, new[] { Slime() });

            Assert.That(battle.Party.Count, Is.EqualTo(4));
        }

        [Test]
        public void SkipSendsTheActorBackToTheStart()
        {
            var battle = OneOnOne(BattleSide.Party, _assets.Member("Hero", 10), Slime());
            battle.Start();
            var hero = battle.Party[0];

            Assert.That(battle.Submit(BattleCommand.Skip()), Is.True);

            Assert.That(battle.State, Is.EqualTo(BattleState.TimelineRunning));
            Assert.That(battle.Timeline.Get(hero).Position, Is.EqualTo(0f));
        }

        [Test]
        public void StunnedUnitLosesItsTurn()
        {
            var battle = OneOnOne(BattleSide.Party, _assets.Member("Hero", 10), Slime());
            var hero = battle.Party[0];
            hero.Stats.ApplyStatus(StatusEffectType.Stun, 1);
            Combatant skipped = null;
            battle.TurnSkipped += unit => skipped = unit;

            battle.Start();

            Assert.That(skipped, Is.SameAs(hero));
            Assert.That(battle.State, Is.EqualTo(BattleState.TimelineRunning));
            Assert.That(battle.Timeline.Get(hero).Position, Is.EqualTo(0f));
        }
        #endregion

        #region Player actions
        [Test]
        public void AttackDamagesTheTargetAndGrantsApPerHit()
        {
            var battle = OneOnOne(BattleSide.Party, _assets.Member("Hero", 10, _assets.Attack(10, 3)), Slime());
            battle.Start();
            var hero = battle.Party[0];
            var slime = battle.Enemies[0];

            Resolve(battle, BattleCommand.Attack(slime));
            battle.FinishAction();

            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(70));
            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(3));
            Assert.That(hero.SurgeGauge.Current, Is.EqualTo(0), "Attacks do not charge the Surge gauge.");
            Assert.That(battle.State, Is.EqualTo(BattleState.TimelineRunning));
            Assert.That(battle.Timeline.Get(hero).Position, Is.EqualTo(0f));
        }

        [Test]
        public void AbilityCostsApAtResolutionAndChargesTheCastersSurgeGauge()
        {
            var ability = _assets.Action<AbilityData>(ActionTargetType.Enemy, 0f, Effect(EffectType.Damage, ActionTargetType.Enemy, 7));
            ability.ApCost = 3;
            var member = _assets.Member("Hero", 10);
            member.Abilities = new List<AbilityData> { ability };
            var battle = OneOnOne(BattleSide.Party, member, Slime());
            battle.Start();
            var hero = battle.Party[0];
            var slime = battle.Enemies[0];
            var command = BattleCommand.Ability(ability, slime);

            Assert.That(battle.Validate(command), Is.EqualTo("Not enough AP."));

            hero.Stats.GainAp(5);
            Assert.That(battle.Submit(command), Is.True);
            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(5), "AP is paid when the action resolves.");

            battle.Tick(0f);
            battle.CurrentExecution.RunToEnd();

            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(2));
            Assert.That(hero.SurgeGauge.Current, Is.EqualTo(30));
            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(93));
        }

        [Test]
        public void SkillTreeAbilitiesStayHiddenUntilUnlocked()
        {
            var ability = _assets.Action<AbilityData>(ActionTargetType.Enemy, 0f);
            ability.UnlockSource = UnlockSource.SkillTree;
            var member = _assets.Member("Hero", 10);
            member.Abilities = new List<AbilityData> { ability };
            var battle = OneOnOne(BattleSide.Party, member, Slime());
            var hero = battle.Party[0];

            Assert.That(battle.GetAbilities(hero), Is.Empty);

            _unlocks.Unlock(ability);
            Assert.That(battle.GetAbilities(hero), Is.EquivalentTo(new[] { ability }));
        }

        [Test]
        public void SuccessfulAbilityQteAppliesItsMultiplier()
        {
            var ability = _assets.Action<AbilityData>(ActionTargetType.Enemy, 0f, Effect(EffectType.Damage, ActionTargetType.Enemy, 7));
            ability.ApCost = 0;
            ability.HasQte = true;
            ability.Qte = new QteData(QteType.TimedPress, 0.5f, 2f);
            var member = _assets.Member("Hero", 10);
            member.Abilities = new List<AbilityData> { ability };
            var battle = OneOnOne(BattleSide.Party, member, Slime());
            battle.Start();
            var slime = battle.Enemies[0];

            battle.Submit(BattleCommand.Ability(ability, slime));
            battle.Tick(0f);
            Assert.That(battle.CurrentExecution.Qte, Is.Not.Null);
            battle.CurrentExecution.SetQteResult(QteResult.Success);
            battle.CurrentExecution.RunToEnd();

            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(86));
        }

        [Test]
        public void SurgeNeedsAnUnlockAndAFullGaugeAndEmptiesIt()
        {
            var member = _assets.Member("Hero", 10);
            var surge = _assets.Action<SurgeData>(ActionTargetType.Enemy, 0f, Effect(EffectType.Damage, ActionTargetType.Enemy, 50));
            surge.UnlockSource = UnlockSource.SkillTree;
            surge.Owner = member;
            member.Surge = surge;
            var battle = OneOnOne(BattleSide.Party, member, Slime());
            battle.Start();
            var hero = battle.Party[0];
            var slime = battle.Enemies[0];

            Assert.That(battle.GetSurges(hero), Is.Empty, "Surges are not unlocked from the start.");

            _unlocks.UnlockEverything = true;
            Assert.That(battle.GetSurges(hero), Is.EquivalentTo(new[] { surge }));
            Assert.That(battle.Validate(BattleCommand.Surge(surge, slime)), Is.EqualTo("Surge gauge not full."));

            hero.SurgeGauge.Add(100);
            Resolve(battle, BattleCommand.Surge(surge, slime));

            Assert.That(hero.SurgeGauge.Current, Is.EqualTo(0));
            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(50));
        }

        [Test]
        public void ItemsComeFromTheSharedInventoryAndCanReviveAnAlly()
        {
            var item = _assets.Action<ItemData>(ActionTargetType.Ally, 0f, Effect(EffectType.Revive, ActionTargetType.Ally, 50));
            _inventory.Add(item);
            var battle = Battle(BattleSide.Party, new[] { _assets.Member("Hero", 10), _assets.Member("Ally", 5) }, new[] { Slime() });
            var hero = battle.Party[0];
            var ally = battle.Party[1];
            ally.Stats.Damage(1000);
            battle.Start();

            Assert.That(battle.Timeline.Contains(ally), Is.False);
            Assert.That(battle.GetValidTargets(hero, item), Is.EquivalentTo(new[] { ally }));

            Resolve(battle, BattleCommand.Item(item, ally));

            Assert.That(ally.IsAlive, Is.True);
            Assert.That(ally.Stats.CurrentHealth, Is.EqualTo(50));
            Assert.That(_inventory.GetCount(item), Is.EqualTo(0));
            Assert.That(battle.Timeline.Get(ally).Position, Is.EqualTo(0f), "Revived units re-enter at the start.");
        }
        #endregion

        #region Interrupts and defense
        private AttackData ElementalAttack(Element element, float power = 10f, int hits = 1, float castTime = 0f)
        {
            var attack = _assets.Attack(power, hits, castTime);
            attack.Element = element;
            return attack;
        }

        /// <summary>
        /// Enemy initiative with a slow enemy casting <paramref name="ability"/> (put a long cast time on it), then time
        /// runs until the hero's turn, with the enemy still mid-Cast.
        /// </summary>
        private BattleModel HeroTurnWhileEnemyCasts(EnemyAbilityData ability, AttackData attack, EnemyCharacterData enemy = null)
        {
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10, attack), enemy ?? Slime(1, ability));
            battle.Start();
            battle.Tick(10f);
            Assert.That(battle.State, Is.EqualTo(BattleState.AwaitingCommand));
            Assert.That(battle.IsCasting(battle.Enemies[0]), Is.True);
            return battle;
        }

        [Test]
        public void IntentIsRevealedWhenTheEnemyEntersCast()
        {
            var ability = TestAssets.EnemyHit(10, 20f, 2, Element.Wind);
            ability.IsSteadfast = true;
            ability.DefenseRule = DefenseRule.ParryOnly;
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, ability));
            EnemyIntent revealed = null;
            battle.IntentRevealed += intent => revealed = intent;

            battle.Start();

            var slime = battle.Enemies[0];
            Assert.That(revealed, Is.SameAs(battle.GetIntent(slime)));
            Assert.That(revealed.Target, Is.SameAs(battle.Party[0]));
            Assert.That(revealed.Element, Is.EqualTo(Element.Wind));
            Assert.That(revealed.DefenseRule, Is.EqualTo(DefenseRule.ParryOnly));
            Assert.That(revealed.IsSteadfast, Is.True);
            Assert.That(battle.Timeline.Get(slime).Phase, Is.EqualTo(TimelinePhase.Casting));
        }

        [Test]
        public void CounterElementHitFizzlesAnEnemyCast()
        {
            var battle = HeroTurnWhileEnemyCasts(TestAssets.EnemyHit(10, 20f, 1, Element.Wind), ElementalAttack(Element.Fire));
            var slime = battle.Enemies[0];
            var reason = (InterruptReason?)null;
            battle.Interrupted += (unit, why) => reason = why;
            StepReport report = null;
            battle.StepResolved += step => report = step;

            Resolve(battle, BattleCommand.Attack(slime));

            Assert.That(reason, Is.EqualTo(InterruptReason.Fizzle));
            Assert.That(report.Hits[0].Fizzled, Is.True);
            Assert.That(battle.GetIntent(slime), Is.Null);
            Assert.That(battle.Timeline.Get(slime).Phase, Is.EqualTo(TimelinePhase.Waiting));
            Assert.That(battle.Timeline.Get(slime).Position, Is.EqualTo(0f));
        }

        [TestCase(Element.None, Element.Wind)]
        [TestCase(Element.Water, Element.Wind)]
        [TestCase(Element.Fire, Element.None)]
        [TestCase(Element.Fire, Element.Fire)]
        public void PlainAndNonCounterHitsDoNotInterruptACaster(Element hit, Element cast)
        {
            var battle = HeroTurnWhileEnemyCasts(TestAssets.EnemyHit(10, 20f, 1, cast), ElementalAttack(hit));
            var slime = battle.Enemies[0];
            var interrupted = false;
            battle.Interrupted += (unit, why) => interrupted = true;

            Resolve(battle, BattleCommand.Attack(slime));

            Assert.That(interrupted, Is.False);
            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(90));
            Assert.That(battle.GetIntent(slime), Is.Not.Null);
            Assert.That(battle.IsCasting(slime), Is.True);
        }

        [Test]
        public void SteadfastCastsCannotFizzle()
        {
            var ability = TestAssets.EnemyHit(10, 20f, 1, Element.Wind);
            ability.IsSteadfast = true;
            var battle = HeroTurnWhileEnemyCasts(ability, ElementalAttack(Element.Fire));
            var slime = battle.Enemies[0];

            Resolve(battle, BattleCommand.Attack(slime));

            Assert.That(battle.IsCasting(slime), Is.True);
            Assert.That(battle.GetIntent(slime), Is.Not.Null);
        }

        [TestCase(Element.Water, DefenseOutcome.Hit, true)]
        [TestCase(Element.Water, DefenseOutcome.Dodged, false)]
        [TestCase(Element.Water, DefenseOutcome.Parried, false)]
        [TestCase(Element.None, DefenseOutcome.Hit, false)]
        [TestCase(Element.Wind, DefenseOutcome.Hit, false)]
        public void EnemyHitsFizzleAPartyCastOnlyWhenTheyLandAndCounterIt(Element hitElement, DefenseOutcome defense, bool expectFizzle)
        {
            var member = _assets.Member("Hero", 10, ElementalAttack(Element.Fire, 10f, 1, 10f));
            var battle = OneOnOne(BattleSide.Party, member, Slime(100, TestAssets.EnemyHit(10, 0f, 1, hitElement)));
            var hero = battle.Party[0];
            battle.Start();
            battle.Submit(BattleCommand.Attack(battle.Enemies[0]));

            battle.Tick(2f);
            Assert.That(battle.State, Is.EqualTo(BattleState.ResolvingAction));
            var report = battle.CurrentExecution.ExecuteNext(defense);

            Assert.That(report.Hits[0].Fizzled, Is.EqualTo(expectFizzle));
            Assert.That(battle.Timeline.Get(hero).Phase, Is.EqualTo(expectFizzle ? TimelinePhase.Waiting : TimelinePhase.Casting));
            Assert.That(battle.GetCommand(hero) == null, Is.EqualTo(expectFizzle));
        }

        [Test]
        public void DodgeAvoidsDamageAndGrantsApPerHitDodged()
        {
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, TestAssets.EnemyHit(10, 0f, 3)));
            battle.Start();
            var hero = battle.Party[0];
            var execution = battle.CurrentExecution;
            Assert.That(execution.Steps.Count(step => step.NeedsDefense), Is.EqualTo(3), "One prompt per hit.");

            var report = execution.ExecuteNext(DefenseOutcome.Dodged);
            Assert.That(report.Hits[0].ApGained, Is.EqualTo(1));
            Assert.That(hero.Stats.CurrentHealth, Is.EqualTo(100));

            execution.ExecuteNext(DefenseOutcome.Dodged);
            Assert.That(hero.Stats.CurrentHealth, Is.EqualTo(100));
            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(2));
            Assert.That(hero.SurgeGauge.Current, Is.EqualTo(0), "Dodges do not charge the Surge gauge.");

            execution.ExecuteNext(DefenseOutcome.Hit);
            Assert.That(hero.Stats.CurrentHealth, Is.EqualTo(90));
            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(2));
        }

        [TestCase(DefenseOutcome.Parried, DefenseOutcome.Hit, DefenseOutcome.Parried)]
        [TestCase(DefenseOutcome.Hit, DefenseOutcome.Dodged, DefenseOutcome.Dodged)]
        public void AnyLandedHitLetsTheAttackResolveNormally(DefenseOutcome first, DefenseOutcome second, DefenseOutcome third)
        {
            _tuning.InterruptKnockback = 0.5f;
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, TestAssets.EnemyHit(10, 0f, 3)));
            battle.Start();
            var slime = battle.Enemies[0];
            var execution = battle.CurrentExecution;

            execution.ExecuteNext(first);
            execution.ExecuteNext(second);
            Assert.That(execution.Chain, Is.EqualTo(ChainOutcome.None), "The outcome waits for the last hit.");
            execution.ExecuteNext(third);
            battle.FinishAction();

            Assert.That(execution.Chain, Is.EqualTo(ChainOutcome.Landed));
            Assert.That(battle.Timeline.Get(slime).Position, Is.EqualTo(0f));
        }

        [TestCase(DefenseOutcome.Dodged, DefenseOutcome.Dodged, DefenseOutcome.Dodged)]
        [TestCase(DefenseOutcome.Parried, DefenseOutcome.Dodged, DefenseOutcome.Parried)]
        public void DodgedOrMixedChainInterruptsTheEnemy(DefenseOutcome first, DefenseOutcome second, DefenseOutcome third)
        {
            _tuning.InterruptKnockback = 0.5f;
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, TestAssets.EnemyHit(10, 0f, 3)));
            battle.Start();
            var slime = battle.Enemies[0];
            var execution = battle.CurrentExecution;
            var reason = (InterruptReason?)null;
            battle.Interrupted += (unit, why) => reason = why;

            execution.ExecuteNext(first);
            execution.ExecuteNext(second);
            execution.ExecuteNext(third);

            Assert.That(execution.Chain, Is.EqualTo(ChainOutcome.Interrupted));
            Assert.That(reason, Is.EqualTo(InterruptReason.Defended));
            Assert.That(battle.Party[0].Stats.CurrentHealth, Is.EqualTo(100));

            battle.FinishAction();
            Assert.That(battle.Timeline.Get(slime).Phase, Is.EqualTo(TimelinePhase.Waiting));
            Assert.That(battle.Timeline.Get(slime).Position, Is.EqualTo(50f).Within(0.01f), "Interrupt knockback, not the bar start.");
        }

        [TestCase(DefenseOutcome.Dodged, true)]
        [TestCase(DefenseOutcome.Hit, false)]
        public void CleanDefenseCancelsTheRestOfTheAttack(DefenseOutcome defense, bool cancelled)
        {
            var ability = TestAssets.EnemyHit(10);
            ability.ActionList.Add(new EnemyActionData(EffectType.ApplyStatus, ActionTargetType.Enemy, 2, 2, StatusEffectType.Weakness));
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, ability));
            battle.Start();
            var hero = battle.Party[0];
            var execution = battle.CurrentExecution;

            execution.ExecuteNext(defense);

            Assert.That(execution.IsComplete, Is.EqualTo(cancelled));
            execution.RunToEnd();
            Assert.That(hero.Stats.GetStatusValue(StatusEffectType.Weakness) > 0, Is.EqualTo(!cancelled));
        }

        [Test]
        public void FullParryCancelsTheAttackSendsTheEnemyToTheStartAndCountersAtOnce()
        {
            _tuning.InterruptKnockback = 0.5f;
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10, _assets.Attack(10, 3)),
                Slime(1, TestAssets.EnemyHit(10, 5f, 3)));
            battle.Start();
            var hero = battle.Party[0];
            var slime = battle.Enemies[0];
            battle.Tick(5f);
            Assert.That(battle.Timeline.Get(hero).Position, Is.EqualTo(50f).Within(0.01f));
            var execution = battle.CurrentExecution;
            (Combatant member, Combatant enemy) countered = default;
            battle.CounterEarned += (member, enemy) => countered = (member, enemy);
            var counterHits = new List<bool>();
            battle.StepResolved += step => counterHits.AddRange(step.Hits.Where(hit => hit.Target == slime).Select(hit => hit.IsCounter));

            execution.ExecuteNext(DefenseOutcome.Parried);
            execution.ExecuteNext(DefenseOutcome.Parried);
            Assert.That(countered, Is.EqualTo(default((Combatant, Combatant))), "The counter waits for the last hit.");
            execution.ExecuteNext(DefenseOutcome.Parried);

            Assert.That(execution.Chain, Is.EqualTo(ChainOutcome.Parried));
            Assert.That(countered, Is.EqualTo((hero, slime)));
            Assert.That(hero.Stats.CurrentHealth, Is.EqualTo(100));
            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(0), "Parries grant no AP.");

            battle.FinishAction();
            Assert.That(battle.Timeline.Get(slime).Phase, Is.EqualTo(TimelinePhase.Waiting));
            Assert.That(battle.Timeline.Get(slime).Position, Is.EqualTo(0f), "The enemy goes to the bar start.");
            Assert.That(battle.State, Is.EqualTo(BattleState.ResolvingAction), "No timeline time passes.");
            var counter = battle.CurrentExecution;
            Assert.That(counter.IsCounter, Is.True);
            Assert.That(counter.Actor, Is.SameAs(hero));
            counter.RunToEnd();

            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(55), "The basic attack's 3 hits of 10, each times 1.5.");
            Assert.That(counterHits, Is.EqualTo(new[] { true, true, true }));
            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(0), "Counter hits grant no AP.");

            battle.FinishAction();
            Assert.That(battle.State, Is.EqualTo(BattleState.TimelineRunning));
            Assert.That(battle.Timeline.Get(hero).Position, Is.EqualTo(50f).Within(0.01f), "The counter does not move the parrier.");
            Assert.That(battle.Timeline.Get(slime).Position, Is.EqualTo(0f));
        }

        [Test]
        public void EveryParrierOfAFullyParriedAreaAttackCounters()
        {
            var sweep = TestAssets.EnemyHit(10, 5f);
            sweep.ActionList[0] = new EnemyActionData(EffectType.Damage, ActionTargetType.AllEnemies, 10, 10);
            var battle = Battle(BattleSide.Enemy, new[] { _assets.Member("Hero", 10), _assets.Member("Ally", 5) },
                new[] { Slime(1, sweep) });
            var hero = battle.Party[0];
            var ally = battle.Party[1];
            var slime = battle.Enemies[0];
            var counters = new List<Combatant>();
            battle.CounterEarned += (member, _) => counters.Add(member);
            battle.Start();
            battle.Tick(5f);
            Assert.That(battle.CurrentExecution.ChosenTarget, Is.SameAs(hero));

            battle.CurrentExecution.ExecuteNext(DefenseOutcome.Parried);
            Assert.That(counters, Is.EqualTo(new[] { hero, ally }));

            foreach (var parrier in counters)
            {
                battle.FinishAction();
                Assert.That(battle.CurrentExecution.Actor, Is.SameAs(parrier));
                battle.CurrentExecution.RunToEnd();
            }

            battle.FinishAction();
            Assert.That(battle.State, Is.EqualTo(BattleState.TimelineRunning));
            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(70), "Two counters of 10 times 1.5.");
            Assert.That(battle.Timeline.Get(slime).Position, Is.EqualTo(0f));
            Assert.That(battle.Timeline.Get(hero).Position, Is.EqualTo(50f).Within(0.01f), "Parriers keep their places.");
            Assert.That(battle.Timeline.Get(ally).Position, Is.EqualTo(25f).Within(0.01f));
        }

        [Test]
        public void ParryingAFullChainWhileCastingCountersAtOnceWithASuperRiposte()
        {
            var member = _assets.Member("Hero", 10, _assets.Attack(10, 1, 3f));
            var battle = OneOnOne(BattleSide.Party, member, Slime(50, TestAssets.EnemyHit(10)));
            var hero = battle.Party[0];
            var slime = battle.Enemies[0];
            battle.Start();
            battle.Submit(BattleCommand.Attack(slime));
            var started = new List<Combatant>();
            battle.ActionStarted += action => started.Add(action.Actor);
            var countered = false;
            battle.CounterEarned += (_, __) => countered = true;

            battle.Tick(2f);
            Assert.That(battle.CurrentExecution.Actor, Is.SameAs(slime));
            battle.CurrentExecution.ExecuteNext(DefenseOutcome.Parried);
            Assert.That(battle.IsSuperRiposte(hero), Is.True);
            Assert.That(countered, Is.False, "A caster strikes back with its Super Riposte instead.");
            battle.FinishAction();

            Assert.That(battle.Timeline.Get(slime).Position, Is.EqualTo(0f), "The enemy goes to the bar start.");
            Assert.That(battle.State, Is.EqualTo(BattleState.ResolvingAction), "No timeline time passes.");
            var execution = battle.CurrentExecution;
            Assert.That(started, Is.EqualTo(new[] { slime, hero }));
            Assert.That(execution.IsSuperRiposte, Is.True);
            Assert.That(execution.IsCounter, Is.False);
            execution.RunToEnd();

            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(80), "Times 2.");
            Assert.That(battle.IsSuperRiposte(hero), Is.False);

            battle.FinishAction();
            Assert.That(battle.State, Is.EqualTo(BattleState.TimelineRunning));
            Assert.That(battle.Timeline.Get(hero).Position, Is.EqualTo(0f));
        }

        [Test]
        public void ParryingWhileNotCastingEarnsNoSuperRiposte()
        {
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, TestAssets.EnemyHit(10)));
            battle.Start();
            battle.CurrentExecution.ExecuteNext(DefenseOutcome.Parried);

            Assert.That(battle.IsSuperRiposte(battle.Party[0]), Is.False);
        }

        [TestCase(DefenseRule.DodgeOnly, DefenseOutcome.Parried, DefenseOutcome.Hit)]
        [TestCase(DefenseRule.DodgeOnly, DefenseOutcome.Dodged, DefenseOutcome.Dodged)]
        [TestCase(DefenseRule.ParryOnly, DefenseOutcome.Dodged, DefenseOutcome.Hit)]
        [TestCase(DefenseRule.ParryOnly, DefenseOutcome.Parried, DefenseOutcome.Parried)]
        [TestCase(DefenseRule.Any, DefenseOutcome.Parried, DefenseOutcome.Parried)]
        public void DefenseRulesTurnTheForbiddenInputIntoAMiss(DefenseRule rule, DefenseOutcome input, DefenseOutcome expected)
        {
            var ability = TestAssets.EnemyHit(10);
            ability.DefenseRule = rule;
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, ability));
            battle.Start();
            Assert.That(battle.CurrentExecution.NextStep.DefenseRule, Is.EqualTo(rule));

            var report = battle.CurrentExecution.ExecuteNext(input);

            Assert.That(report.Hits[0].Defense, Is.EqualTo(expected));
            Assert.That(battle.Party[0].Stats.CurrentHealth, Is.EqualTo(expected == DefenseOutcome.Hit ? 90 : 100));
        }

        [Test]
        public void HitDelaysReachEachStep()
        {
            var ability = TestAssets.EnemyHit(10, 0f, 3);
            ability.HitDelays = new List<float> { 0f, 0.4f };
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, ability));
            battle.Start();

            Assert.That(battle.CurrentExecution.Steps.Select(step => step.ImpactDelay), Is.EqualTo(new[] { 0f, 0.4f, 0f }));
        }
        #endregion

        #region Taunt
        [Test]
        public void EnemySingleTargetAttacksGoToTheTaunter()
        {
            var battle = Battle(BattleSide.Enemy, new[] { _assets.Member("Hero", 10), _assets.Member("Tank", 10) },
                new[] { Slime(1, TestAssets.EnemyHit(10)) });
            var tank = battle.Party[1];
            tank.Stats.ApplyStatus(StatusEffectType.Taunt, 1);

            battle.Start();

            Assert.That(battle.CurrentExecution.ChosenTarget, Is.SameAs(tank));
            battle.CurrentExecution.RunToEnd();
            Assert.That(tank.Stats.CurrentHealth, Is.EqualTo(90));
            Assert.That(battle.Party[0].Stats.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void TauntRetargetsAnIntentAlreadyInCast()
        {
            var provoke = _assets.Action<AbilityData>(ActionTargetType.Ally, 0f,
                new ActionEffectData(EffectType.ApplyStatus, ActionTargetType.Ally, 1, StatusEffectType.Taunt));
            provoke.ApCost = 0;
            var hero = _assets.Member("Hero", 10);
            hero.Abilities = new List<AbilityData> { provoke };
            var battle = Battle(BattleSide.Enemy, new[] { hero, _assets.Member("Tank", 5) },
                new[] { Slime(1, TestAssets.EnemyHit(10, 20f)) });
            var tank = battle.Party[1];
            var slime = battle.Enemies[0];
            EnemyIntent retargeted = null;
            battle.IntentRetargeted += intent => retargeted = intent;
            battle.Start();
            Assert.That(battle.GetIntent(slime).Target, Is.SameAs(battle.Party[0]));

            battle.Tick(10f);
            Resolve(battle, BattleCommand.Ability(provoke, tank));

            Assert.That(retargeted, Is.SameAs(battle.GetIntent(slime)));
            Assert.That(battle.GetIntent(slime).Target, Is.SameAs(tank));
        }

        [Test]
        public void TauntEndsWhenTheTauntersNextTurnStarts()
        {
            var battle = OneOnOne(BattleSide.Party, _assets.Member("Hero", 10), Slime());
            var hero = battle.Party[0];
            hero.Stats.ApplyStatus(StatusEffectType.Taunt, 1);
            Assert.That(battle.Taunter, Is.SameAs(hero));

            battle.Start();

            Assert.That(hero.Stats.IsTaunting, Is.False);
            Assert.That(battle.Taunter, Is.Null);
        }

        [Test]
        public void TaunterDodgesEarnBonusAp()
        {
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, TestAssets.EnemyHit(10)));
            var hero = battle.Party[0];
            hero.Stats.ApplyStatus(StatusEffectType.Taunt, 1);
            battle.Start();

            var report = battle.CurrentExecution.ExecuteNext(DefenseOutcome.Dodged);

            Assert.That(report.Hits[0].ApGained, Is.EqualTo(2));
        }

        [Test]
        public void TaunterParriesEarnABiggerCounter()
        {
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, TestAssets.EnemyHit(10)));
            battle.Party[0].Stats.ApplyStatus(StatusEffectType.Taunt, 1);
            battle.Start();

            battle.CurrentExecution.ExecuteNext(DefenseOutcome.Parried);
            battle.FinishAction();

            Assert.That(battle.CurrentExecution.CounterMultiplier, Is.EqualTo(2f));
        }
        #endregion

        #region Break
        private EnemyCharacterData Elite(int breakMeter, EnemyAbilityData ability)
        {
            var enemy = Slime(1, ability);
            enemy.BreakMeter = breakMeter;
            enemy.Affinities = new List<ElementAffinity> { new ElementAffinity(Element.Fire, 0.5f) };
            return enemy;
        }

        [Test]
        public void DamageDrainsTheBreakMeter()
        {
            var battle = OneOnOne(BattleSide.Party, _assets.Member("Hero", 10), Elite(100, TestAssets.EnemyHit(10)));
            battle.Start();
            var elite = battle.Enemies[0];

            Resolve(battle, BattleCommand.Attack(elite));

            Assert.That(elite.BreakMax, Is.EqualTo(100));
            Assert.That(elite.BreakMeter, Is.EqualTo(90));
        }

        [Test]
        public void HitsDuringCastDrainExtraBreak()
        {
            var battle = HeroTurnWhileEnemyCasts(null, _assets.Attack(10), Elite(100, TestAssets.EnemyHit(10, 20f)));
            var elite = battle.Enemies[0];

            Resolve(battle, BattleCommand.Attack(elite));

            Assert.That(elite.BreakMeter, Is.EqualTo(85));
        }

        [Test]
        public void CountersDrainExtraBreak()
        {
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Elite(100, TestAssets.EnemyHit(10)));
            battle.Start();
            var elite = battle.Enemies[0];
            battle.CurrentExecution.ExecuteNext(DefenseOutcome.Parried);
            battle.FinishAction();

            Assert.That(battle.CurrentExecution.IsCounter, Is.True);
            battle.CurrentExecution.RunToEnd();

            Assert.That(elite.Stats.CurrentHealth, Is.EqualTo(85));
            Assert.That(elite.BreakMeter, Is.EqualTo(70), "15 damage times the counter break multiplier of 2.");
        }

        [Test]
        public void SuperRipostesDrainExtraBreakOnTheParriedEnemy()
        {
            var fastElite = Elite(100, TestAssets.EnemyHit(10));
            fastElite.BaseStats = new InElementum.Data.Stats.StatBlock(10, 10, 10, 50, 10);
            var battle = OneOnOne(BattleSide.Party, _assets.Member("Hero", 10, _assets.Attack(10, 1, 3f)), fastElite);
            battle.Start();
            var elite = battle.Enemies[0];
            battle.Submit(BattleCommand.Attack(elite));
            battle.Tick(2f);
            battle.CurrentExecution.ExecuteNext(DefenseOutcome.Parried);
            battle.FinishAction();

            Assert.That(battle.CurrentExecution.IsSuperRiposte, Is.True);
            battle.CurrentExecution.RunToEnd();

            Assert.That(elite.Stats.CurrentHealth, Is.EqualTo(80));
            Assert.That(elite.BreakMeter, Is.EqualTo(60), "20 damage times the counter break multiplier of 2.");
        }

        [Test]
        public void EmptyMeterBreaksCancelsTheCastFreezesAndDropsResistancesUntilItRefills()
        {
            var battle = HeroTurnWhileEnemyCasts(null, _assets.Attack(10), Elite(10, TestAssets.EnemyHit(10, 20f)));
            var elite = battle.Enemies[0];
            var reason = (InterruptReason?)null;
            Combatant broken = null;
            Combatant recovered = null;
            battle.Interrupted += (unit, why) => reason = why;
            battle.Broken += unit => broken = unit;
            battle.BreakRecovered += unit => recovered = unit;

            Resolve(battle, BattleCommand.Attack(elite));
            battle.FinishAction();

            Assert.That(broken, Is.SameAs(elite));
            Assert.That(elite.IsBroken, Is.True);
            Assert.That(reason, Is.EqualTo(InterruptReason.Break));
            Assert.That(battle.GetIntent(elite), Is.Null);
            Assert.That(battle.Timeline.Get(elite).IsFrozen, Is.True);
            Assert.That(elite.Stats.GetElementMultiplier(Element.Fire), Is.EqualTo(1f), "Resistances count as normal.");

            var position = battle.Timeline.Get(elite).Position;
            battle.Tick(4.9f);
            Assert.That(battle.Timeline.Get(elite).Position, Is.EqualTo(position), "Frozen in place.");
            Assert.That(recovered, Is.Null);

            battle.Tick(0.2f);
            Assert.That(recovered, Is.SameAs(elite));
            Assert.That(elite.IsBroken, Is.False);
            Assert.That(elite.BreakMeter, Is.EqualTo(10), "The meter refills.");
            Assert.That(elite.Stats.GetElementMultiplier(Element.Fire), Is.EqualTo(0.5f));
        }

        [Test]
        public void EnemiesWithoutAMeterNeverBreak()
        {
            var battle = OneOnOne(BattleSide.Party, _assets.Member("Hero", 10, _assets.Attack(50)), Slime());
            battle.Start();
            var slime = battle.Enemies[0];

            Resolve(battle, BattleCommand.Attack(slime));

            Assert.That(slime.CanBreak, Is.False);
            Assert.That(slime.IsBroken, Is.False);
        }
        #endregion

        #region Enemy counter-casting
        [TestCase(2f, true)]
        [TestCase(12f, false)]
        public void EnemiesCounterCastOnlyWhenTheirCastWouldLandFirst(float counterCastTime, bool expectCounterCast)
        {
            var normal = TestAssets.EnemyHit(5, 1f);
            var counter = TestAssets.EnemyHit(5, counterCastTime, 1, Element.Water);
            var member = _assets.Member("Hero", 10, ElementalAttack(Element.Fire, 10f, 1, 10f));
            var battle = OneOnOne(BattleSide.Party, member, _assets.Enemy("Slime", 50, normal, counter));
            var hero = battle.Party[0];
            var slime = battle.Enemies[0];
            battle.Start();
            battle.Submit(BattleCommand.Attack(slime));

            // The slime reaches Cast at 2 s, with 8 s of the hero's cast left.
            battle.Tick(2f);

            var intent = battle.GetIntent(slime);
            Assert.That(intent, Is.Not.Null);
            Assert.That(intent.IsCounterCast, Is.EqualTo(expectCounterCast));
            Assert.That(intent.Ability, Is.SameAs(expectCounterCast ? counter : normal));
            if (expectCounterCast)
            {
                Assert.That(intent.Target, Is.SameAs(hero));
                Assert.That(slime.AbilityUseCount, Is.EqualTo(0), "A counter-cast does not advance the pattern.");
            }
        }
        #endregion

        #region Guard
        private static EnemyAbilityData GuardAbility(int counterPower, float castTime = 20f)
        {
            return new EnemyAbilityData
            {
                Name = "Guard",
                CastTime = castTime,
                IsGuard = true,
                GuardCounterPower = counterPower,
                ActionList = new List<EnemyActionData>()
            };
        }

        [Test]
        public void GuardReducesDamageAndCountersEachBasicAttackHit()
        {
            var battle = HeroTurnWhileEnemyCasts(GuardAbility(7), _assets.Attack(10, 2));
            var hero = battle.Party[0];
            var slime = battle.Enemies[0];
            Assert.That(battle.IsGuarding(slime), Is.True);
            var position = battle.Timeline.Get(slime).Position;
            battle.Submit(BattleCommand.Attack(slime));
            battle.Tick(0f);
            var execution = battle.CurrentExecution;

            execution.ExecuteNext();
            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(95), "Half damage while guarding.");

            var counter = execution.NextStep;
            Assert.That(counter.IsGuardCounter, Is.True);
            Assert.That(counter.NeedsDefense, Is.True);
            Assert.That(counter.Attacker, Is.SameAs(slime));
            execution.ExecuteNext(DefenseOutcome.Hit);
            Assert.That(hero.Stats.CurrentHealth, Is.EqualTo(93));

            execution.ExecuteNext();
            var ap = hero.Stats.CurrentAp;
            execution.ExecuteNext(DefenseOutcome.Dodged);
            Assert.That(hero.Stats.CurrentAp, Is.EqualTo(ap + 1), "Guard counter-hits are defended as normal.");

            Assert.That(execution.IsComplete, Is.True);
            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(90));
            Assert.That(execution.Chain, Is.EqualTo(ChainOutcome.None), "Guard counter-hits have no chain outcome.");
            Assert.That(battle.IsCasting(slime), Is.True, "Guard counter-hits do not move the enemy.");
            Assert.That(battle.Timeline.Get(slime).Position, Is.EqualTo(position));
        }

        [Test]
        public void GuardDoesNotCounterAbilities()
        {
            var ability = _assets.Action<AbilityData>(ActionTargetType.Enemy, 0f,
                new ActionEffectData(EffectType.Damage, ActionTargetType.Enemy, 10));
            ability.ApCost = 0;
            var member = _assets.Member("Hero", 10);
            member.Abilities = new List<AbilityData> { ability };
            var battle = OneOnOne(BattleSide.Enemy, member, Slime(1, GuardAbility(7)));
            battle.Start();
            battle.Tick(10f);
            var slime = battle.Enemies[0];

            Resolve(battle, BattleCommand.Ability(ability, slime));

            Assert.That(battle.CurrentExecution.Steps.Any(step => step.IsGuardCounter), Is.False);
            Assert.That(slime.Stats.CurrentHealth, Is.EqualTo(95));
        }
        #endregion

        #region Outcomes
        [Test]
        public void DefeatedEnemiesLeaveTheTimelineAndVictoryGrantsRewards()
        {
            var enemy = Slime();
            enemy.Rewards = new RewardTable { Experience = 15, CurrencyMin = 5, CurrencyMax = 9 };
            var battle = OneOnOne(BattleSide.Party, _assets.Member("Hero", 10, _assets.Attack(200)), enemy);
            BattleResult ended = null;
            battle.BattleEnded += result => ended = result;
            battle.Start();
            var slime = battle.Enemies[0];

            Resolve(battle, BattleCommand.Attack(slime));
            Assert.That(battle.Timeline.Contains(slime), Is.False);

            battle.FinishAction();

            Assert.That(battle.State, Is.EqualTo(BattleState.Victory));
            Assert.That(ended, Is.SameAs(battle.Result));
            Assert.That(ended.Rewards.Experience, Is.EqualTo(15));
            Assert.That(ended.Rewards.Currency, Is.EqualTo(5));
            Assert.That(ended.Participants, Is.EquivalentTo(new[] { battle.Party[0] }));
        }

        [Test]
        public void FallenPartyIsDefeated()
        {
            var battle = OneOnOne(BattleSide.Enemy, _assets.Member("Hero", 10), Slime(1, TestAssets.EnemyHit(500)));
            battle.Start();
            battle.CurrentExecution.RunToEnd();

            battle.FinishAction();

            Assert.That(battle.State, Is.EqualTo(BattleState.Defeat));
            Assert.That(battle.Result.Outcome, Is.EqualTo(BattleOutcome.Defeat));
        }
        #endregion
    }
}
