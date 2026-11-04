using System.Collections.Generic;
using InElementum.Combat.Model;
using InElementum.Data.Settings;
using NUnit.Framework;

namespace InElementum.Tests.EditMode
{
    public class TimelineTests
    {
        private sealed class Unit : ITimelineUnit
        {
            public Unit(string name, BattleSide side, int speed, int slot = 0)
            {
                Name = name;
                Side = side;
                Speed = speed;
                Slot = slot;
            }

            public string Name { get; }
            public int Speed { get; set; }
            public BattleSide Side { get; }
            public int Slot { get; }
            public override string ToString() => Name;
        }

        private readonly TestAssets _assets = new TestAssets();
        private CombatTuning _tuning;
        private BattleTimeline _timeline;

        [SetUp]
        public void SetUp()
        {
            _tuning = _assets.Tuning();
            _timeline = new BattleTimeline(_tuning);
        }

        [TearDown]
        public void TearDown() => _assets.DestroyAll();

        private TimelineEvent Next(float deltaTime = 1000f)
        {
            Assert.That(_timeline.Advance(deltaTime, out var timelineEvent, out _), Is.True, "Expected a timeline event.");
            return timelineEvent;
        }

        [Test]
        public void InitiatingSideActsFirstEvenWhenSlower()
        {
            var slow = new Unit("slow", BattleSide.Party, 5);
            var fast = new Unit("fast", BattleSide.Party, 8, 1);
            var enemy = new Unit("enemy", BattleSide.Enemy, 50);

            _timeline.SetupStart(new[] { slow, fast }, new[] { enemy }, BattleSide.Party);

            var first = Next(0f);
            Assert.That(first.Type, Is.EqualTo(TimelineEventType.ReachedCast));
            Assert.That(first.Unit, Is.EqualTo(fast));
        }

        [Test]
        public void EnemyInitiativeGivesEnemyFirstTurn()
        {
            var hero = new Unit("hero", BattleSide.Party, 50);
            var enemy = new Unit("enemy", BattleSide.Enemy, 1);

            _timeline.SetupStart(new[] { hero }, new[] { enemy }, BattleSide.Enemy);

            Assert.That(Next(0f).Unit, Is.EqualTo(enemy));
        }

        [Test]
        public void RestOfInitiatingSideStartsAheadOfTheOtherSide()
        {
            var lead = new Unit("lead", BattleSide.Party, 10);
            var second = new Unit("second", BattleSide.Party, 5, 1);
            var enemy = new Unit("enemy", BattleSide.Enemy, 5);

            _timeline.SetupStart(new[] { lead, second }, new[] { enemy }, BattleSide.Party);

            Assert.That(_timeline.Get(lead).Position, Is.EqualTo(100f));
            Assert.That(_timeline.Get(second).Position, Is.EqualTo(50f));
            Assert.That(_timeline.Get(enemy).Position, Is.EqualTo(0f));
        }

        [Test]
        public void FasterSideOnAverageGetsAHeadStart()
        {
            _tuning.AverageSpeedStartBonus = 0.2f;
            var hero = new Unit("hero", BattleSide.Party, 5);
            var fastEnemy = new Unit("fast", BattleSide.Enemy, 30);
            var slowEnemy = new Unit("slow", BattleSide.Enemy, 10, 1);

            _timeline.SetupStart(new[] { hero }, new[] { fastEnemy, slowEnemy }, BattleSide.Party);

            Assert.That(_timeline.Get(hero).Position, Is.EqualTo(100f), "Initiator still starts on the boundary.");
            Assert.That(_timeline.Get(fastEnemy).Position, Is.EqualTo(20f));
            Assert.That(_timeline.Get(slowEnemy).Position, Is.EqualTo(20f));
        }

        [Test]
        public void HeadStartNeverReachesTheBoundaryBeforeTheInitiator()
        {
            _tuning.AverageSpeedStartBonus = 1f;
            var hero = new Unit("hero", BattleSide.Party, 1);
            var enemy = new Unit("enemy", BattleSide.Enemy, 50);

            _timeline.SetupStart(new[] { hero }, new[] { enemy }, BattleSide.Party);

            Assert.That(Next(0f).Unit, Is.EqualTo(hero));
        }

        [Test]
        public void FasterUnitsTakeMoreTurns()
        {
            var fast = new Unit("fast", BattleSide.Party, 20);
            var slow = new Unit("slow", BattleSide.Enemy, 10);
            _timeline.Add(fast);
            _timeline.Add(slow);

            var turns = new Dictionary<ITimelineUnit, int> { [fast] = 0, [slow] = 0 };
            var elapsed = 0f;
            while (elapsed < 99f)
            {
                if (!_timeline.Advance(99f - elapsed, out var timelineEvent, out var used))
                    break;

                elapsed += used;
                if (timelineEvent.Type == TimelineEventType.ReachedCast)
                {
                    turns[timelineEvent.Unit]++;
                    _timeline.BeginCast(timelineEvent.Unit, 0f);
                }
                else
                {
                    _timeline.ReturnToStart(timelineEvent.Unit);
                }
            }

            // 100 units of Wait at 20/s and 10/s: turns every 5 and every 10 seconds, up to 99 seconds.
            Assert.That(turns[fast], Is.EqualTo(19));
            Assert.That(turns[slow], Is.EqualTo(9));
        }

        [Test]
        public void CastTimeSetsHowLongCastTakes()
        {
            var caster = new Unit("caster", BattleSide.Party, 10);
            _timeline.Add(caster, 100f);
            Assert.That(Next(0f).Type, Is.EqualTo(TimelineEventType.ReachedCast));

            _timeline.BeginCast(caster, 2f);

            Assert.That(_timeline.Advance(1.9f, out _, out _), Is.False);
            Assert.That(_timeline.Get(caster).Position, Is.EqualTo(123.75f).Within(0.01f));

            Assert.That(_timeline.Advance(1f, out var done, out var used), Is.True);
            Assert.That(done.Type, Is.EqualTo(TimelineEventType.CastComplete));
            Assert.That(used, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(_timeline.Get(caster).Phase, Is.EqualTo(TimelinePhase.Resolving));
        }

        [Test]
        public void ZeroCastTimeCompletesImmediately()
        {
            var caster = new Unit("caster", BattleSide.Party, 10);
            _timeline.Add(caster, 100f);
            Next(0f);

            _timeline.BeginCast(caster, 0f);

            Assert.That(Next(0f).Type, Is.EqualTo(TimelineEventType.CastComplete));
        }

        [Test]
        public void OthersKeepMovingWhileAUnitCasts()
        {
            var caster = new Unit("caster", BattleSide.Party, 10);
            var other = new Unit("other", BattleSide.Enemy, 10);
            _timeline.Add(caster, 100f);
            _timeline.Add(other, 0f);
            Next(0f);
            _timeline.BeginCast(caster, 10f);

            _timeline.Advance(5f, out _, out _);

            Assert.That(_timeline.Get(other).Position, Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void InterruptCancelsCastAndKnocksBackToStart()
        {
            var caster = new Unit("caster", BattleSide.Enemy, 10);
            _timeline.Add(caster, 100f);
            Next(0f);
            _timeline.BeginCast(caster, 10f);
            _timeline.Advance(2f, out _, out _);

            Assert.That(_timeline.Interrupt(caster), Is.True);

            var entry = _timeline.Get(caster);
            Assert.That(entry.Phase, Is.EqualTo(TimelinePhase.Waiting));
            Assert.That(entry.Position, Is.EqualTo(0f));
        }

        [Test]
        public void InterruptKnockbackIsTunable()
        {
            _tuning.InterruptKnockback = 0.4f;
            var caster = new Unit("caster", BattleSide.Enemy, 10);
            _timeline.Add(caster, 100f);
            Next(0f);
            _timeline.BeginCast(caster, 10f);

            _timeline.Interrupt(caster);

            Assert.That(_timeline.Get(caster).Position, Is.EqualTo(60f).Within(0.01f));
        }

        [Test]
        public void OnlyCastingUnitsCanBeInterrupted()
        {
            var waiting = new Unit("waiting", BattleSide.Enemy, 10);
            var resolving = new Unit("resolving", BattleSide.Enemy, 10, 1);
            _timeline.Add(waiting, 30f);
            _timeline.Add(resolving, 100f);
            Next(0f);
            _timeline.BeginCast(resolving, 0f);
            Next(0f);

            Assert.That(_timeline.Interrupt(waiting), Is.False);
            Assert.That(_timeline.Get(waiting).Position, Is.EqualTo(30f));
            Assert.That(_timeline.Interrupt(resolving), Is.False);
        }

        [Test]
        public void TiesGoToHigherSpeedThenPartyThenSlot()
        {
            var slowFar = new Unit("slowFar", BattleSide.Enemy, 10);
            var fastNear = new Unit("fastNear", BattleSide.Enemy, 20, 1);
            _timeline.Add(slowFar, 50f);
            _timeline.Add(fastNear, 0f);

            // Both arrive at t = 5.
            Assert.That(Next().Unit, Is.EqualTo(fastNear));
            Assert.That(Next(0f).Unit, Is.EqualTo(slowFar));
        }

        [Test]
        public void EqualSpeedTiesGoToThePartyThenLowerSlot()
        {
            var enemy = new Unit("enemy", BattleSide.Enemy, 10);
            var partySlot1 = new Unit("p1", BattleSide.Party, 10, 1);
            var partySlot0 = new Unit("p0", BattleSide.Party, 10);
            _timeline.Add(enemy);
            _timeline.Add(partySlot1);
            _timeline.Add(partySlot0);

            Assert.That(Next().Unit, Is.EqualTo(partySlot0));
            Assert.That(Next(0f).Unit, Is.EqualTo(partySlot1));
            Assert.That(Next(0f).Unit, Is.EqualTo(enemy));
        }

        [Test]
        public void RemovedUnitsLeaveAndReturnAtTheStart()
        {
            var unit = new Unit("unit", BattleSide.Party, 10);
            _timeline.Add(unit, 70f);

            _timeline.Remove(unit);
            Assert.That(_timeline.Contains(unit), Is.False);

            _timeline.Add(unit);
            Assert.That(_timeline.Get(unit).Position, Is.EqualTo(0f));
        }

        [Test]
        public void SpeedChangesApplyImmediately()
        {
            var unit = new Unit("unit", BattleSide.Party, 10);
            _timeline.Add(unit);
            _timeline.Advance(5f, out _, out _);

            unit.Speed = 50;

            Assert.That(_timeline.Advance(10f, out var reached, out var used), Is.True);
            Assert.That(reached.Unit, Is.EqualTo(unit));
            Assert.That(used, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void DefaultInterruptKnockbackIsHalfTheWaitSection()
        {
            var tuning = _assets.Create<CombatTuning>();
            Assert.That(tuning.InterruptKnockback, Is.EqualTo(0.5f));
        }

        [Test]
        public void KnockBackWorksFromResolving()
        {
            _tuning.InterruptKnockback = 0.5f;
            var unit = new Unit("unit", BattleSide.Enemy, 10);
            _timeline.Add(unit, 100f);
            Next(0f);
            _timeline.BeginCast(unit, 0f);
            Assert.That(Next(0f).Type, Is.EqualTo(TimelineEventType.CastComplete));

            _timeline.KnockBack(unit);

            Assert.That(_timeline.Get(unit).Phase, Is.EqualTo(TimelinePhase.Waiting));
            Assert.That(_timeline.Get(unit).Position, Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void FrozenUnitsHoldStillWhileOthersMoveAndRaiseUnfrozen()
        {
            var frozen = new Unit("frozen", BattleSide.Enemy, 10);
            var other = new Unit("other", BattleSide.Party, 1);
            _timeline.Add(frozen, 20f);
            _timeline.Add(other);

            _timeline.Freeze(frozen, 3f);
            Assert.That(_timeline.Get(frozen).IsFrozen, Is.True);

            var thaw = Next();
            Assert.That(thaw.Type, Is.EqualTo(TimelineEventType.Unfrozen));
            Assert.That(thaw.Unit, Is.EqualTo(frozen));
            Assert.That(_timeline.Get(frozen).Position, Is.EqualTo(20f), "A frozen unit does not move.");
            Assert.That(_timeline.Get(other).Position, Is.EqualTo(3f).Within(0.01f), "Everyone else keeps moving.");
            Assert.That(_timeline.Get(frozen).IsFrozen, Is.False);

            // Then it moves again: 80 units at 10 per second.
            Assert.That(_timeline.Advance(1000f, out var reached, out var used), Is.True);
            Assert.That(reached.Unit, Is.EqualTo(frozen));
            Assert.That(used, Is.EqualTo(8f).Within(0.01f));
        }

        [Test]
        public void TimeToResolveCoversOnlyCastingUnits()
        {
            var caster = new Unit("caster", BattleSide.Party, 10);
            var waiting = new Unit("waiting", BattleSide.Enemy, 1);
            _timeline.Add(caster, 100f);
            _timeline.Add(waiting);
            Next(0f);
            _timeline.BeginCast(caster, 4f);
            _timeline.Advance(1f, out _, out _);

            Assert.That(_timeline.GetTimeToResolve(caster), Is.EqualTo(3f).Within(0.01f));
            Assert.That(_timeline.GetTimeToResolve(waiting), Is.EqualTo(float.PositiveInfinity));
        }
    }
}
