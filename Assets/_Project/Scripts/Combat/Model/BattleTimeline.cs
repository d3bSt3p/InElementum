using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Settings;

namespace InElementum.Combat.Model
{
    public enum BattleSide
    {
        Party,
        Enemy
    }

    public enum TimelinePhase
    {
        /// <summary>Crossing the Wait section at a rate set by Speed.</summary>
        Waiting,
        /// <summary>Paused at the Wait/Cast boundary while an action is chosen.</summary>
        Choosing,
        /// <summary>Crossing the Cast section at a rate set by the chosen action's cast time.</summary>
        Casting,
        /// <summary>Parked at the end of the bar while its action resolves. Cannot be interrupted.</summary>
        Resolving
    }

    public enum TimelineEventType
    {
        ReachedCast,
        CastComplete,
        /// <summary>A frozen unit's freeze ran out, so it moves again.</summary>
        Unfrozen
    }

    /// <summary>What the timeline needs from a unit. Speed is read live, so buffs and debuffs apply immediately.</summary>
    public interface ITimelineUnit
    {
        int Speed { get; }
        BattleSide Side { get; }
        int Slot { get; }
    }

    public sealed class TimelineEntry
    {
        internal TimelineEntry(ITimelineUnit unit, float position)
        {
            Unit = unit;
            Position = position;
        }

        public ITimelineUnit Unit { get; }
        public float Position { get; internal set; }
        public TimelinePhase Phase { get; internal set; }
        public float CastTime { get; internal set; }
        /// <summary>Timeline seconds left frozen in place, as when Broken. 0 when moving.</summary>
        public float FrozenTime { get; internal set; }
        public bool IsFrozen => FrozenTime > 0f;
        internal bool PendingUnfreeze { get; set; }
    }

    public readonly struct TimelineEvent
    {
        public TimelineEvent(TimelineEventType type, ITimelineUnit unit)
        {
            Type = type;
            Unit = unit;
        }

        public TimelineEventType Type { get; }
        public ITimelineUnit Unit { get; }
    }

    /// <summary>
    /// Child of Light-style turn order: one shared bar with a long Wait section and a short Cast section, and no rounds.
    /// Units cross Wait at a rate set by Speed, stop at the boundary to choose an action, then cross Cast at a rate set by
    /// the action's cast time. An interrupt cancels a cast and knocks the unit back; a freeze holds it in place.
    /// </summary>
    public class BattleTimeline
    {
        /// <summary>Positions this close to a boundary count as on it.</summary>
        private const float Epsilon = 0.0001f;
        /// <summary>Keeps head-started units just short of the boundary, so the initiator always acts first.</summary>
        private const float BoundaryGap = 0.01f;

        private readonly CombatTuning _tuning;
        private readonly List<TimelineEntry> _entries = new List<TimelineEntry>();

        public BattleTimeline(CombatTuning tuning)
        {
            _tuning = tuning != null ? tuning : throw new ArgumentNullException(nameof(tuning));
        }

        public float WaitLength => _tuning.WaitLength;
        public float CastLength => _tuning.CastLength;
        public float EndPosition => WaitLength + CastLength;
        public IReadOnlyList<TimelineEntry> Entries => _entries;

        #region Membership
        public bool Contains(ITimelineUnit unit) => Find(unit) != null;

        public TimelineEntry Get(ITimelineUnit unit) =>
            Find(unit) ?? throw new ArgumentException("Unit is not on the timeline.", nameof(unit));

        /// <summary>Puts a unit on the bar, at the start unless given a position. Used for battle start and revives.</summary>
        public void Add(ITimelineUnit unit, float position = 0f)
        {
            if (Contains(unit)) return;
            _entries.Add(new TimelineEntry(unit, Clamp(position, 0f, WaitLength)));
        }

        public void Remove(ITimelineUnit unit) => _entries.RemoveAll(entry => entry.Unit == unit);

        private TimelineEntry Find(ITimelineUnit unit)
        {
            foreach (var entry in _entries)
            {
                if (entry.Unit == unit) return entry;
            }

            return null;
        }
        #endregion

        #region Setup
        /// <summary>
        /// Places both sides for the first turn. The initiating side's fastest unit starts on the Cast boundary, so it
        /// acts first, and the rest of that side start at <see cref="CombatTuning.InitiatorStartPosition"/>. The side with
        /// the higher average Speed gets <see cref="CombatTuning.AverageSpeedStartBonus"/>. Individual Speed then sets rates.
        /// </summary>
        public void SetupStart(IEnumerable<ITimelineUnit> party, IEnumerable<ITimelineUnit> enemies, BattleSide initiative)
        {
            _entries.Clear();

            var partyList = party.ToList();
            var enemyList = enemies.ToList();
            var initiators = initiative == BattleSide.Party ? partyList : enemyList;
            var first = initiators.OrderBy(unit => unit, TurnOrder).FirstOrDefault();

            var partyBonus = 0f;
            var enemyBonus = 0f;
            var partyAverage = AverageSpeed(partyList);
            var enemyAverage = AverageSpeed(enemyList);
            if (partyAverage > enemyAverage) partyBonus = _tuning.AverageSpeedStartBonus * WaitLength;
            else if (enemyAverage > partyAverage) enemyBonus = _tuning.AverageSpeedStartBonus * WaitLength;

            foreach (var unit in partyList.Concat(enemyList))
            {
                if (unit == first)
                {
                    _entries.Add(new TimelineEntry(unit, WaitLength));
                    continue;
                }

                var start = unit.Side == initiative ? _tuning.InitiatorStartPosition * WaitLength : 0f;
                start += unit.Side == BattleSide.Party ? partyBonus : enemyBonus;
                _entries.Add(new TimelineEntry(unit, Clamp(start, 0f, WaitLength - BoundaryGap)));
            }
        }

        private static float AverageSpeed(List<ITimelineUnit> units) =>
            units.Count == 0 ? 0f : (float)units.Average(unit => unit.Speed);
        #endregion

        #region Running
        /// <summary>Units per second through the Wait section.</summary>
        public float GetWaitRate(ITimelineUnit unit) =>
            _tuning.BaseWaitRate + Math.Max(0, unit.Speed) * _tuning.WaitRatePerSpeed;

        /// <summary>
        /// Moves every unit forward by up to <paramref name="deltaTime"/> seconds, stopping at the first unit to reach a
        /// boundary. Returns true with that event and the time used; false if no unit reached one in time.
        /// Units reaching a boundary in the same tick fire one per call, in <see cref="TurnOrder"/>.
        /// </summary>
        public bool Advance(float deltaTime, out TimelineEvent timelineEvent, out float timeUsed)
        {
            timelineEvent = default;
            timeUsed = 0f;

            if (TryGetPendingEvent(out timelineEvent)) return true;

            var step = Math.Max(0f, deltaTime);
            var reachesBoundary = false;
            foreach (var entry in _entries)
            {
                var time = TimeToBoundary(entry);
                if (time <= step)
                {
                    step = time;
                    reachesBoundary = true;
                }
            }

            foreach (var entry in _entries)
                Move(entry, step);

            timeUsed = step;
            return reachesBoundary && TryGetPendingEvent(out timelineEvent);
        }

        /// <summary>Starts crossing the Cast section. A cast time of 0 completes on the next advance.</summary>
        public void BeginCast(ITimelineUnit unit, float castTime)
        {
            var entry = Get(unit);
            entry.Phase = TimelinePhase.Casting;
            entry.Position = WaitLength;
            entry.CastTime = Math.Max(0f, castTime);
        }

        /// <summary>Sends a unit back to the start of the bar, after its action resolves or when it skips.</summary>
        public void ReturnToStart(ITimelineUnit unit)
        {
            var entry = Find(unit);
            if (entry == null) return;

            entry.Phase = TimelinePhase.Waiting;
            entry.Position = 0f;
            entry.CastTime = 0f;
        }

        /// <summary>
        /// Skips the rest of a casting unit's Cast so its action resolves now, as a Super Riposte does. Returns false,
        /// changing nothing, if the unit is not mid-Cast.
        /// </summary>
        public bool ResolveNow(ITimelineUnit unit)
        {
            var entry = Find(unit);
            if (entry == null || entry.Phase != TimelinePhase.Casting) return false;

            entry.Phase = TimelinePhase.Resolving;
            entry.Position = EndPosition;
            return true;
        }

        /// <summary>
        /// Cancels a casting unit's action and knocks it back. Returns false, changing nothing, if the unit is not
        /// mid-Cast. A unit that has finished casting is resolving; see <see cref="KnockBack"/>.
        /// </summary>
        public bool Interrupt(ITimelineUnit unit)
        {
            var entry = Find(unit);
            if (entry == null || entry.Phase != TimelinePhase.Casting) return false;

            KnockBack(entry);
            return true;
        }

        /// <summary>
        /// Sends a unit to the interrupt knockback point from any phase, such as an enemy whose resolving attack was
        /// fully defended.
        /// </summary>
        public void KnockBack(ITimelineUnit unit)
        {
            var entry = Find(unit);
            if (entry != null) KnockBack(entry);
        }

        private void KnockBack(TimelineEntry entry)
        {
            entry.Phase = TimelinePhase.Waiting;
            entry.Position = Clamp(WaitLength * (1f - _tuning.InterruptKnockback), 0f, WaitLength - BoundaryGap);
            entry.CastTime = 0f;
        }

        /// <summary>
        /// Holds a unit in place for <paramref name="seconds"/> of timeline time, while everyone else moves. A longer
        /// freeze already running is kept. An <see cref="TimelineEventType.Unfrozen"/> event fires when it ends.
        /// </summary>
        public void Freeze(ITimelineUnit unit, float seconds)
        {
            var entry = Find(unit);
            if (entry == null || seconds <= 0f) return;

            entry.FrozenTime = Math.Max(entry.FrozenTime, seconds);
            entry.PendingUnfreeze = false;
        }

        /// <summary>Seconds until a casting unit's action resolves, or infinity if it is not casting.</summary>
        public float GetTimeToResolve(ITimelineUnit unit)
        {
            var entry = Find(unit);
            if (entry == null || entry.Phase != TimelinePhase.Casting) return float.PositiveInfinity;
            return entry.IsFrozen ? entry.FrozenTime + CastTimeLeft(entry) : CastTimeLeft(entry);
        }

        private float CastTimeLeft(TimelineEntry entry) =>
            entry.CastTime <= 0f ? 0f : (EndPosition - entry.Position) / (CastLength / entry.CastTime);

        private float TimeToBoundary(TimelineEntry entry)
        {
            if (entry.IsFrozen) return entry.FrozenTime;

            switch (entry.Phase)
            {
                case TimelinePhase.Waiting:
                    var waitRate = GetWaitRate(entry.Unit);
                    return waitRate <= 0f ? float.PositiveInfinity : (WaitLength - entry.Position) / waitRate;
                case TimelinePhase.Casting:
                    return CastTimeLeft(entry);
                default:
                    return float.PositiveInfinity;
            }
        }

        private void Move(TimelineEntry entry, float time)
        {
            if (entry.IsFrozen)
            {
                entry.FrozenTime -= time;
                if (entry.FrozenTime > Epsilon) return;

                entry.FrozenTime = 0f;
                entry.PendingUnfreeze = true;
                return;
            }

            switch (entry.Phase)
            {
                case TimelinePhase.Waiting:
                    entry.Position = Math.Min(WaitLength, entry.Position + GetWaitRate(entry.Unit) * time);
                    if (WaitLength - entry.Position <= Epsilon) entry.Position = WaitLength;
                    break;
                case TimelinePhase.Casting:
                    entry.Position = entry.CastTime <= 0f
                        ? EndPosition
                        : Math.Min(EndPosition, entry.Position + CastLength / entry.CastTime * time);
                    if (EndPosition - entry.Position <= Epsilon) entry.Position = EndPosition;
                    break;
            }
        }

        private bool TryGetPendingEvent(out TimelineEvent timelineEvent)
        {
            TimelineEntry next = null;
            foreach (var entry in _entries)
            {
                if (!IsAtBoundary(entry)) continue;
                if (next == null || TurnOrder.Compare(entry.Unit, next.Unit) < 0) next = entry;
            }

            if (next == null)
            {
                timelineEvent = default;
                return false;
            }

            if (next.PendingUnfreeze)
            {
                next.PendingUnfreeze = false;
                timelineEvent = new TimelineEvent(TimelineEventType.Unfrozen, next.Unit);
            }
            else if (next.Phase == TimelinePhase.Waiting)
            {
                next.Phase = TimelinePhase.Choosing;
                timelineEvent = new TimelineEvent(TimelineEventType.ReachedCast, next.Unit);
            }
            else
            {
                next.Phase = TimelinePhase.Resolving;
                next.Position = EndPosition;
                timelineEvent = new TimelineEvent(TimelineEventType.CastComplete, next.Unit);
            }

            return true;
        }

        private bool IsAtBoundary(TimelineEntry entry)
        {
            if (entry.PendingUnfreeze) return true;
            if (entry.IsFrozen) return false;

            switch (entry.Phase)
            {
                case TimelinePhase.Waiting: return entry.Position >= WaitLength;
                case TimelinePhase.Casting: return entry.CastTime <= 0f || entry.Position >= EndPosition;
                default: return false;
            }
        }
        #endregion

        /// <summary>Tie-break for units acting in the same tick: higher Speed, then the party, then slot order.</summary>
        public static readonly IComparer<ITimelineUnit> TurnOrder = Comparer<ITimelineUnit>.Create((a, b) =>
        {
            var bySpeed = b.Speed.CompareTo(a.Speed);
            if (bySpeed != 0) return bySpeed;

            var bySide = a.Side.CompareTo(b.Side);
            return bySide != 0 ? bySide : a.Slot.CompareTo(b.Slot);
        });

        private static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);
    }
}
