using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Actions;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using InElementum.Data.Stats;

namespace InElementum.Combat.Model
{
    /// <summary>
    /// Battle flow. Choosing an enemy action and checking the outcome are instant, so they have no state of their own:
    /// Setup → TimelineRunning → (a unit reaches Cast) AwaitingCommand, or the enemy picks and keeps running →
    /// (a cast completes) ResolvingAction → TimelineRunning | Victory | Defeat. The counter attacks and
    /// Super Ripostes a full parry earns resolve straight after the parried attack, so ResolvingAction can follow itself.
    /// </summary>
    public enum BattleState
    {
        Setup,
        TimelineRunning,
        AwaitingCommand,
        ResolvingAction,
        Victory,
        Defeat
    }

    /// <summary>
    /// The ability an enemy committed to at the Wait/Cast boundary, and its main target. Revealed to the player as it
    /// enters Cast, and never changed afterwards, except that Taunt can redirect a single-target attack.
    /// </summary>
    public sealed class EnemyIntent
    {
        internal EnemyIntent(Combatant enemy, EnemyAbilityData ability, Combatant target, bool isCounterCast)
        {
            Enemy = enemy;
            Ability = ability;
            Target = target;
            IsCounterCast = isCounterCast;
        }

        public Combatant Enemy { get; }
        public EnemyAbilityData Ability { get; }
        public Combatant Target { get; internal set; }
        /// <summary>Picked to fizzle a party member's cast, rather than from the enemy's usual pattern.</summary>
        public bool IsCounterCast { get; }

        public Element Element => Ability.Element;
        public DefenseRule DefenseRule => Ability.DefenseRule;
        public bool IsSteadfast => Ability.IsSteadfast;
        public bool IsGuard => Ability.IsGuard;
    }

    public enum InterruptReason
    {
        /// <summary>A landed hit whose element counters the cast.</summary>
        Fizzle,
        /// <summary>Every hit of an enemy attack was dodged or parried.</summary>
        Defended,
        /// <summary>The enemy's break meter emptied.</summary>
        Break
    }

    /// <summary>
    /// The whole battle as plain C#: parties, timeline, commands, action resolution, and the outcome.
    /// A MonoBehaviour drives it by calling <see cref="Tick"/>, answering prompts, and stepping each
    /// <see cref="ActionExecution"/>, and views subscribe to its events.
    /// </summary>
    public class BattleModel
    {
        private readonly List<Combatant> _party;
        private readonly List<Combatant> _enemies;
        private readonly List<Combatant> _participants = new List<Combatant>();
        private readonly Dictionary<Combatant, BattleCommand> _commands = new Dictionary<Combatant, BattleCommand>();
        private readonly Dictionary<Combatant, EnemyIntent> _intents = new Dictionary<Combatant, EnemyIntent>();
        /// <summary>Party members whose queued action is a Super Riposte.</summary>
        private readonly Dictionary<Combatant, PendingCounter> _superRipostes = new Dictionary<Combatant, PendingCounter>();
        /// <summary>Counter attacks and Super Ripostes waiting to strike at once, in order, after the parried attack finishes.</summary>
        private readonly List<PendingCounter> _pendingCounters = new List<PendingCounter>();
        private readonly PassiveRunner _passives;

        /// <summary>A parrier's strike back at the enemy whose attack they fully parried.</summary>
        private sealed class PendingCounter
        {
            public Combatant Member;
            public Combatant Enemy;
            public float Multiplier;
            /// <summary>The parrier was in Cast, so their queued action resolves now; otherwise a counter attack.</summary>
            public bool IsSuperRiposte;
        }

        public BattleModel(
            CombatTuning tuning,
            IEnumerable<Combatant> activeParty,
            IEnumerable<Combatant> enemies,
            BattleSide initiative,
            Inventory inventory = null,
            ActionUnlocks unlocks = null,
            IRandomSource random = null,
            ElementKnowledge knowledge = null)
        {
            Tuning = tuning != null ? tuning : throw new ArgumentNullException(nameof(tuning));
            _party = activeParty.Take(tuning.MaxActivePartySize).ToList();
            _enemies = enemies.ToList();
            Initiative = initiative;
            Inventory = inventory ?? new Inventory();
            Unlocks = unlocks ?? new ActionUnlocks();
            Random = random ?? new SystemRandomSource();
            Knowledge = knowledge ?? new ElementKnowledge();
            Timeline = new BattleTimeline(tuning);
            _passives = new PassiveRunner(this);

            if (_party.Count == 0) throw new ArgumentException("A battle needs at least one active party member.", nameof(activeParty));
            if (_enemies.Count == 0) throw new ArgumentException("A battle needs at least one enemy.", nameof(enemies));

            if (!EffectProcessor.IsInitialized) EffectProcessor.Initialize();
        }

        #region State
        public CombatTuning Tuning { get; }
        public BattleState State { get; private set; } = BattleState.Setup;
        public BattleSide Initiative { get; }
        public BattleTimeline Timeline { get; }
        public Inventory Inventory { get; }
        public ActionUnlocks Unlocks { get; }
        public IRandomSource Random { get; }
        /// <summary>Enemy weaknesses the party has found. Party hits with an element discover it for that enemy type.</summary>
        public ElementKnowledge Knowledge { get; }
        /// <summary>Relationship levels, from <see cref="Unlocks"/>. Null when the battle has none.</summary>
        public RelationshipService Relationships => Unlocks.Relationships;

        public IReadOnlyList<Combatant> Party => _party;
        public IReadOnlyList<Combatant> Enemies => _enemies;
        public IEnumerable<Combatant> Field => _party.Concat(_enemies);

        /// <summary>The party member waiting for a command, while <see cref="State"/> is AwaitingCommand.</summary>
        public Combatant CurrentActor { get; private set; }
        /// <summary>The action resolving, while <see cref="State"/> is ResolvingAction.</summary>
        public ActionExecution CurrentExecution { get; private set; }
        public BattleResult Result { get; private set; }

        public bool IsOver => State == BattleState.Victory || State == BattleState.Defeat;

        /// <summary>The enemy's revealed intent while it is in Cast, or null.</summary>
        public EnemyIntent GetIntent(Combatant enemy) => _intents.TryGetValue(enemy, out var intent) ? intent : null;
        public BattleCommand GetCommand(Combatant member) => _commands.TryGetValue(member, out var command) ? command : null;

        /// <summary>True if the member's queued action became a Super Riposte by parrying a full chain while in Cast.</summary>
        public bool IsSuperRiposte(Combatant member) => member != null && _superRipostes.ContainsKey(member);

        /// <summary>The living party member with Taunt, who draws enemy single-target attacks, or null.</summary>
        public Combatant Taunter => _party.FirstOrDefault(member => member.IsAlive && member.Stats.IsTaunting);

        public bool IsCasting(Combatant unit) =>
            unit != null && Timeline.Contains(unit) && Timeline.Get(unit).Phase == TimelinePhase.Casting;

        /// <summary>An enemy casting a Guard ability: it takes reduced damage and counters basic-attack hits.</summary>
        public bool IsGuarding(Combatant unit) => IsCasting(unit) && GetIntent(unit) is EnemyIntent intent && intent.IsGuard;

        /// <summary>
        /// The element of the action a unit is casting, or None if it is not casting. A Steadfast cast cannot fizzle.
        /// </summary>
        public Element GetCastElement(Combatant unit, out bool steadfast)
        {
            steadfast = false;
            if (!IsCasting(unit)) return Element.None;

            if (unit.Side == BattleSide.Enemy)
            {
                var intent = GetIntent(unit);
                if (intent == null) return Element.None;
                steadfast = intent.IsSteadfast;
                return intent.Element;
            }

            var command = GetCommand(unit);
            if (command == null) return Element.None;
            if (command.Type == CommandType.Attack) return unit.AttackElement;
            return command.Action != null ? command.Action.Element : Element.None;
        }
        #endregion

        #region Events
        public event Action<BattleState> StateChanged;
        /// <summary>A party member reached Cast and is waiting for a command.</summary>
        public event Action<Combatant> TurnStarted;
        public event Action<Combatant, BattleCommand> CommandChosen;
        /// <summary>An enemy entered Cast: its intent (ability, target, element, defense rule, Steadfast, Guard) is now public.</summary>
        public event Action<EnemyIntent> IntentRevealed;
        /// <summary>Taunt redirected a revealed intent to a new target.</summary>
        public event Action<EnemyIntent> IntentRetargeted;
        /// <summary>A unit lost its turn: it was stunned, or the player chose Skip.</summary>
        public event Action<Combatant> TurnSkipped;
        /// <summary>A unit's action was cancelled and it was knocked back.</summary>
        public event Action<Combatant, InterruptReason> Interrupted;
        public event Action<ActionExecution> ActionStarted;
        public event Action<StepReport> StepResolved;
        /// <summary>The last hit of an enemy attack resolved; see <see cref="ActionExecution.Chain"/>.</summary>
        public event Action<ActionExecution> ChainResolved;
        /// <summary>
        /// A parrier in Wait (first) earned a counter attack on the enemy it parried (second). It strikes once the
        /// parried attack finishes.
        /// </summary>
        public event Action<Combatant, Combatant> CounterEarned;
        /// <summary>A party member's queued action became a Super Riposte. It resolves once the parried attack finishes.</summary>
        public event Action<Combatant> SuperRiposteEarned;
        public event Action<Combatant> Broken;
        /// <summary>A Broken enemy's stun ended and its break meter refilled.</summary>
        public event Action<Combatant> BreakRecovered;
        public event Action<ActionExecution> ActionFinished;
        /// <summary>A worn picto's or equipped Lumina's passive fired and applied its effects.</summary>
        public event Action<PassiveActivation> PassiveTriggered;
        public event Action<BattleResult> BattleEnded;
        #endregion

        #region Flow
        public void Start()
        {
            if (State != BattleState.Setup) return;

            _participants.AddRange(_party.Where(member => member.IsAlive));
            Timeline.SetupStart(_party.Where(c => c.IsAlive), _enemies.Where(c => c.IsAlive), Initiative);
            SetState(BattleState.TimelineRunning);

            foreach (var member in _party.ToList())
                EnterBattle(member);
            SyncTimeline();

            if (!EvaluateOutcome()) Tick(0f);
        }

        /// <summary>Advances the timeline by <paramref name="deltaTime"/> seconds. Does nothing outside TimelineRunning.</summary>
        public void Tick(float deltaTime)
        {
            var remaining = Math.Max(0f, deltaTime);

            // Each pass either uses up the time or handles one event; the guard only stops a runaway loop.
            for (var guard = 0; guard < 10000 && State == BattleState.TimelineRunning; guard++)
            {
                if (Timeline.Advance(remaining, out var timelineEvent, out var used))
                {
                    remaining -= used;
                    Handle(timelineEvent);
                    continue;
                }

                remaining -= used;
                if (remaining <= 0f) break;
            }
        }

        private void Handle(TimelineEvent timelineEvent)
        {
            var unit = (Combatant)timelineEvent.Unit;

            switch (timelineEvent.Type)
            {
                case TimelineEventType.ReachedCast:
                    StartTurn(unit);
                    break;
                case TimelineEventType.CastComplete:
                    BeginResolution(unit);
                    break;
                case TimelineEventType.Unfrozen:
                    RecoverFromBreak(unit);
                    break;
            }
        }

        private void StartTurn(Combatant unit)
        {
            // Taunt lasts until the taunter's next turn.
            if (unit.Stats.IsTaunting) unit.Stats.ClearStatus(new StatusKey(StatusEffectType.Taunt));

            unit.Stats.TriggerAllStatus();
            SyncTimeline();

            if (!unit.IsAlive)
            {
                EvaluateOutcome();
                return;
            }

            // A stunned unit's turn still starts, so turn-start passives fire before it is lost.
            if (_passives.Fire(unit, PassiveTrigger.OnTurnStart))
            {
                SyncTimeline();
                if (EvaluateOutcome() || !unit.IsAlive) return;
            }

            if (unit.Stats.IsStunned)
            {
                Timeline.ReturnToStart(unit);
                TurnSkipped?.Invoke(unit);
                return;
            }

            if (unit.Side == BattleSide.Party)
            {
                CurrentActor = unit;
                SetState(BattleState.AwaitingCommand);
                TurnStarted?.Invoke(unit);
            }
            else
            {
                ChooseEnemyAction(unit);
            }
        }

        private void ChooseEnemyAction(Combatant enemy)
        {
            var abilities = enemy.Enemy != null ? enemy.Enemy.EnemyAbilityList : null;
            if (abilities == null || abilities.Count == 0)
            {
                Timeline.ReturnToStart(enemy);
                TurnSkipped?.Invoke(enemy);
                return;
            }

            var isCounterCast = TryCounterCast(abilities, out var ability, out var target);
            if (!isCounterCast)
            {
                var index = enemy.Enemy.FollowAbilityPattern
                    ? enemy.AbilityUseCount % abilities.Count
                    : Random.Range(0, abilities.Count);
                ability = abilities[index];
                enemy.AbilityUseCount++;
                target = RandomLiving(_party);
            }

            foreach (var action in ability.ActionList)
                action.RollValue();

            var taunter = Taunter;
            if (taunter != null && ability.IsSingleTargetAttack) target = taunter;

            var intent = new EnemyIntent(enemy, ability, target, isCounterCast);
            _intents[enemy] = intent;

            Timeline.BeginCast(enemy, ability.CastTime);
            IntentRevealed?.Invoke(intent);
        }

        /// <summary>
        /// Picks a damaging ability whose element counters a party member's cast, if its own cast would land first,
        /// and aims it at that member. Party members are checked in slot order, abilities in list order. A counter-cast
        /// does not advance the enemy's pattern.
        /// </summary>
        private bool TryCounterCast(IReadOnlyList<EnemyAbilityData> abilities, out EnemyAbilityData ability, out Combatant target)
        {
            foreach (var member in _party.OrderBy(member => member.Slot))
            {
                if (!member.IsAlive) continue;

                var cast = GetCastElement(member, out _);
                if (cast == Element.None) continue;

                var memberTimeLeft = Timeline.GetTimeToResolve(member);
                foreach (var candidate in abilities)
                {
                    if (candidate == null || !candidate.DealsDamage || !Tuning.Counters(candidate.Element, cast)) continue;
                    if (candidate.CastTime >= memberTimeLeft) continue;

                    ability = candidate;
                    target = member;
                    return true;
                }
            }

            ability = null;
            target = null;
            return false;
        }

        private void BeginResolution(Combatant unit)
        {
            ActionExecution execution;
            if (unit.Side == BattleSide.Party)
            {
                if (!_commands.TryGetValue(unit, out var command))
                {
                    Timeline.ReturnToStart(unit);
                    return;
                }

                _commands.Remove(unit);
                _superRipostes.TryGetValue(unit, out var superRiposte);
                _superRipostes.Remove(unit);
                execution = ActionExecution.ForCommand(this, unit, command, superRiposte?.Multiplier ?? 0f, superRiposte?.Enemy);
            }
            else
            {
                if (!_intents.TryGetValue(unit, out var intent))
                {
                    Timeline.ReturnToStart(unit);
                    return;
                }

                _intents.Remove(unit);
                execution = ActionExecution.ForEnemy(this, intent);
            }

            StartExecution(execution);
        }

        private void StartExecution(ActionExecution execution)
        {
            CurrentExecution = execution;
            SetState(BattleState.ResolvingAction);
            execution.Begin();
            ActionStarted?.Invoke(execution);
        }

        /// <summary>
        /// Call once every step of <see cref="CurrentExecution"/> has run and its animations are done. An enemy whose
        /// attack was dodged and parried takes interrupt knockback. A counter attacker keeps its place on the bar.
        /// Every other actor, a fully parried enemy included, returns to the bar start. Any counter attack or Super
        /// Riposte the attack earned then starts resolving, raising <see cref="ActionStarted"/> again.
        /// </summary>
        public void FinishAction()
        {
            if (State != BattleState.ResolvingAction) return;

            var execution = CurrentExecution;
            CurrentExecution = null;
            PlaceAfterAction(execution);
            ActionFinished?.Invoke(execution);

            SyncTimeline();
            if (EvaluateOutcome())
            {
                _pendingCounters.Clear();
                return;
            }

            if (!TryStartCounter()) SetState(BattleState.TimelineRunning);
        }

        private void PlaceAfterAction(ActionExecution execution)
        {
            if (execution.IsCounter) return;

            if (execution.Chain == ChainOutcome.Interrupted) Timeline.KnockBack(execution.Actor);
            else Timeline.ReturnToStart(execution.Actor);
        }

        /// <summary>
        /// Starts the next waiting counter attack, if its parrier and enemy are both still standing, or Super Riposte,
        /// if its earner is still casting it. Returns true if one started.
        /// </summary>
        private bool TryStartCounter()
        {
            while (_pendingCounters.Count > 0)
            {
                var counter = _pendingCounters[0];
                _pendingCounters.RemoveAt(0);
                var member = counter.Member;
                if (!member.IsAlive) continue;

                if (counter.IsSuperRiposte)
                {
                    if (!_superRipostes.ContainsKey(member) || !Timeline.ResolveNow(member)) continue;
                    BeginResolution(member);
                    return true;
                }

                if (!counter.Enemy.IsAlive || member.Member?.BasicAttack == null) continue;
                StartExecution(ActionExecution.ForCounter(this, member, counter.Enemy, counter.Multiplier));
                return true;
            }

            return false;
        }
        #endregion

        #region Player commands
        /// <summary>Abilities the actor can see in the Abilities menu: default ones plus those unlocked.</summary>
        public IEnumerable<AbilityData> GetAbilities(Combatant actor)
        {
            var member = actor?.Member;
            if (member == null) return Enumerable.Empty<AbilityData>();
            return member.Abilities.Where(ability => ability != null && Unlocks.IsUnlocked(ability, member));
        }

        /// <summary>
        /// The actor's unlocked personal Surge and the unlocked Fusion Surges they are part of, known or explicitly
        /// unlocked. The partner does not need to be in battle.
        /// </summary>
        public IEnumerable<SurgeData> GetSurges(Combatant actor)
        {
            var member = actor?.Member;
            if (member == null) yield break;

            if (member.Surge != null && Unlocks.IsUnlocked(member.Surge, member))
                yield return member.Surge;

            var fusions = Unlocks.FusionSurges.Concat(Unlocks.Unlocked.OfType<SurgeData>()).Distinct();
            foreach (var surge in fusions)
            {
                if (surge != member.Surge && surge.IsFusion && surge.IsOwnedBy(member) && Unlocks.IsUnlocked(surge, member))
                    yield return surge;
            }
        }

        public IEnumerable<Combatant> GetValidTargets(Combatant actor, CombatActionData action)
        {
            if (action == null) return Enumerable.Empty<Combatant>();

            var revives = action.Effects.Any(effect => effect.EffectType == EffectType.Revive);
            switch (action.TargetType)
            {
                case ActionTargetType.Enemy:
                    return Opponents(actor).Where(c => c.IsAlive);
                case ActionTargetType.Ally:
                    return Allies(actor).Where(c => revives ? !c.IsAlive : c.IsAlive);
                default:
                    return Enumerable.Empty<Combatant>();
            }
        }

        public bool CanSubmit(BattleCommand command) => Validate(command) == null;

        /// <summary>Why the command can't be used right now, or null if it can.</summary>
        public string Validate(BattleCommand command)
        {
            if (State != BattleState.AwaitingCommand || CurrentActor == null) return "Not waiting for a command.";
            if (command == null) return "No command.";

            var actor = CurrentActor;
            switch (command.Type)
            {
                case CommandType.Skip:
                    return null;
                case CommandType.Attack:
                    var attack = actor.Member != null ? actor.Member.BasicAttack : null;
                    if (attack == null) return "No basic attack.";
                    return ValidateTarget(actor, attack, command.Target);
                case CommandType.Ability:
                    if (!(command.Action is AbilityData ability) || !GetAbilities(actor).Contains(ability)) return "Ability not available.";
                    if (actor.Stats.CurrentAp < ability.ApCost) return "Not enough AP.";
                    return ValidateTarget(actor, ability, command.Target);
                case CommandType.Item:
                    if (!(command.Action is ItemData item) || Inventory.GetCount(item) <= 0) return "No item left.";
                    return ValidateTarget(actor, item, command.Target);
                case CommandType.Surge:
                    if (!(command.Action is SurgeData surge) || !GetSurges(actor).Contains(surge)) return "Surge not unlocked.";
                    if (!actor.SurgeGauge.CanSpend(surge.GaugeCost)) return "Surge gauge not full.";
                    return ValidateTarget(actor, surge, command.Target);
                default:
                    return "Unknown command.";
            }
        }

        private string ValidateTarget(Combatant actor, CombatActionData action, Combatant target)
        {
            if (action.TargetType != ActionTargetType.Enemy && action.TargetType != ActionTargetType.Ally) return null;
            return GetValidTargets(actor, action).Contains(target) ? null : "Invalid target.";
        }

        /// <summary>
        /// Gives the waiting actor its command. The actor starts crossing Cast at the action's cast time; costs are paid
        /// when it resolves. Skip sends the actor back to the start of the bar instead.
        /// </summary>
        public bool Submit(BattleCommand command)
        {
            if (Validate(command) != null) return false;

            var actor = CurrentActor;
            CurrentActor = null;

            if (command.Type == CommandType.Skip)
            {
                Timeline.ReturnToStart(actor);
                TurnSkipped?.Invoke(actor);
            }
            else
            {
                var action = command.Type == CommandType.Attack ? actor.Member.BasicAttack : command.Action;
                _commands[actor] = command;
                Timeline.BeginCast(actor, action.CastTime);
            }

            CommandChosen?.Invoke(actor, command);
            SetState(BattleState.TimelineRunning);
            return true;
        }
        #endregion

        #region Resolution helpers
        internal IEnumerable<Combatant> ResolveTargets(Combatant actor, ActionTargetType type, Combatant chosen, bool targetsDowned)
        {
            bool Eligible(Combatant c) => targetsDowned ? !c.IsAlive : c.IsAlive;

            // Taunt draws every single-target enemy attack, random ones included.
            if (actor.Side == BattleSide.Enemy && !targetsDowned
                && (type == ActionTargetType.Enemy || type == ActionTargetType.RandomEnemy)
                && Taunter is Combatant taunter)
                return new[] { taunter };

            switch (type)
            {
                case ActionTargetType.Self:
                    return Eligible(actor) ? new[] { actor } : Array.Empty<Combatant>();
                case ActionTargetType.AllEnemies:
                    return Opponents(actor).Where(Eligible).ToList();
                case ActionTargetType.AllAllies:
                    return Allies(actor).Where(Eligible).ToList();
                case ActionTargetType.Enemy:
                case ActionTargetType.RandomEnemy:
                    return Single(Opponents(actor), type == ActionTargetType.Enemy ? chosen : null, Eligible);
                case ActionTargetType.Ally:
                case ActionTargetType.RandomAlly:
                    return Single(Allies(actor), type == ActionTargetType.Ally ? chosen : null, Eligible);
                default:
                    return Array.Empty<Combatant>();
            }
        }

        /// <summary>The chosen target if still eligible, otherwise a random eligible one, so a dead target doesn't waste the action.</summary>
        private IEnumerable<Combatant> Single(IEnumerable<Combatant> group, Combatant chosen, Func<Combatant, bool> eligible)
        {
            var candidates = group.Where(eligible).ToList();
            if (chosen != null && candidates.Contains(chosen)) return new[] { chosen };
            return candidates.Count == 0 ? Array.Empty<Combatant>() : new[] { candidates[Random.Range(0, candidates.Count)] };
        }

        private IEnumerable<Combatant> Opponents(Combatant actor) => actor.Side == BattleSide.Party ? _enemies : _party;
        private IEnumerable<Combatant> Allies(Combatant actor) => actor.Side == BattleSide.Party ? _party : _enemies;

        private Combatant RandomLiving(IReadOnlyList<Combatant> group)
        {
            var living = group.Where(c => c.IsAlive).ToList();
            return living.Count == 0 ? null : living[Random.Range(0, living.Count)];
        }

        /// <summary>Cancels a casting unit's action and knocks it back. Status damage never calls this.</summary>
        internal bool TryInterrupt(Combatant target, InterruptReason reason)
        {
            if (target == null || !target.IsAlive || !Timeline.Interrupt(target)) return false;

            ForgetQueuedAction(target);
            Interrupted?.Invoke(target, reason);
            return true;
        }

        /// <summary>
        /// Fizzles the target's cast if <paramref name="hitElement"/> counters it. Non-elemental and Steadfast casts
        /// cannot fizzle. The same rule applies to both sides.
        /// </summary>
        internal bool TryFizzle(Combatant target, Element hitElement)
        {
            if (hitElement == Element.None || target == null || !target.IsAlive) return false;

            var cast = GetCastElement(target, out var steadfast);
            if (steadfast || !Tuning.Counters(hitElement, cast)) return false;

            return TryInterrupt(target, InterruptReason.Fizzle);
        }

        /// <summary>
        /// Drains an enemy's break meter by the damage dealt, more if it was in Cast or the hit was a counter. At 0 the
        /// enemy is Broken: its cast is cancelled with interrupt knockback, it freezes on the bar, and its resistances
        /// drop. Returns true if this broke it.
        /// </summary>
        internal bool DrainBreak(Combatant target, int damage, bool wasCasting, bool isCounter)
        {
            if (target == null || !target.CanBreak || target.IsBroken || !target.IsAlive || damage <= 0) return false;

            var drain = damage * (wasCasting ? Tuning.CastBreakMultiplier : 1f) * (isCounter ? Tuning.CounterBreakMultiplier : 1f);
            target.BreakMeter = Math.Max(0, target.BreakMeter - (int)Math.Round(drain, MidpointRounding.AwayFromZero));
            if (target.BreakMeter > 0) return false;

            target.IsBroken = true;
            target.Stats.IgnoreResistances = true;
            if (Timeline.Interrupt(target))
            {
                ForgetQueuedAction(target);
                Interrupted?.Invoke(target, InterruptReason.Break);
            }

            Broken?.Invoke(target);
            if (Tuning.BreakStunDuration > 0f) Timeline.Freeze(target, Tuning.BreakStunDuration);
            else RecoverFromBreak(target);
            return true;
        }

        private void RecoverFromBreak(Combatant unit)
        {
            if (!unit.IsBroken) return;

            unit.IsBroken = false;
            unit.Stats.IgnoreResistances = false;
            unit.BreakMeter = unit.BreakMax;
            BreakRecovered?.Invoke(unit);
        }

        /// <summary>
        /// Applies an enemy attack's chain outcome once its last hit resolves. A fully parried attack sends the enemy
        /// to the bar start when it finishes, and every parrier strikes back at once, the main target first, then by
        /// slot. A parrier in Cast turns its queued action into a Super Riposte; one in Wait earns a counter attack
        /// with its basic attack. A taunter's parry adds <see cref="CombatTuning.TauntCounterBonus"/> to its multiplier.
        /// </summary>
        internal void ResolveChain(ActionExecution execution, IReadOnlyCollection<Combatant> parriers)
        {
            var enemy = execution.Actor;
            switch (execution.Chain)
            {
                case ChainOutcome.Interrupted:
                    Interrupted?.Invoke(enemy, InterruptReason.Defended);
                    break;
                case ChainOutcome.Parried:
                    // The main target is who the attack was aimed at, or the first parrier if it was redirected.
                    var mainTarget = parriers.Contains(execution.ChosenTarget) ? execution.ChosenTarget : parriers.FirstOrDefault();
                    var ordered = parriers.OrderBy(parrier => parrier == mainTarget ? 0 : 1).ThenBy(parrier => parrier.Slot);
                    foreach (var parrier in ordered)
                    {
                        if (!parrier.IsAlive) continue;

                        var bonus = parrier.Stats.IsTaunting ? Tuning.TauntCounterBonus : 0f;
                        if (IsCasting(parrier) && _commands.ContainsKey(parrier))
                        {
                            var superRiposte = new PendingCounter
                            {
                                Member = parrier, Enemy = enemy, Multiplier = Tuning.SuperRiposteMultiplier + bonus, IsSuperRiposte = true
                            };
                            _superRipostes[parrier] = superRiposte;
                            _pendingCounters.Add(superRiposte);
                            SuperRiposteEarned?.Invoke(parrier);
                        }
                        else if (enemy.IsAlive && !IsCasting(parrier) && parrier.Member?.BasicAttack != null)
                        {
                            _pendingCounters.Add(new PendingCounter
                            {
                                Member = parrier, Enemy = enemy, Multiplier = Tuning.CounterMultiplier + bonus
                            });
                            CounterEarned?.Invoke(parrier, enemy);
                        }
                    }

                    break;
            }

            ChainResolved?.Invoke(execution);
        }

        internal void AfterStep(StepReport report)
        {
            SyncTimeline();
            RetargetToTaunter();
            StepResolved?.Invoke(report);

            if (FireHitPassives(report))
            {
                SyncTimeline();
                RetargetToTaunter();
            }
        }

        /// <summary>
        /// Fires the passives a step earned: OnHit for each party hit that lands on an enemy and OnKill for each it
        /// fells, and OnDodge or OnParry for each party member who defended an enemy hit, guard counter-hits included.
        /// Returns true if any fired.
        /// </summary>
        private bool FireHitPassives(StepReport report)
        {
            var step = report.Step;
            var source = step.Attacker ?? report.Execution.Actor;
            var fired = false;

            foreach (var hit in report.Hits)
            {
                var target = hit.Target;
                if (step.NeedsDefense && target.Side == BattleSide.Party)
                {
                    if (hit.Defense == DefenseOutcome.Dodged) fired |= _passives.Fire(target, PassiveTrigger.OnDodge, source);
                    else if (hit.Defense == DefenseOutcome.Parried) fired |= _passives.Fire(target, PassiveTrigger.OnParry, source);
                    continue;
                }

                if (!step.DealsDamage || source.Side != BattleSide.Party || target.Side != BattleSide.Enemy) continue;

                fired |= _passives.Fire(source, PassiveTrigger.OnHit, target);
                if (hit.Killed) fired |= _passives.Fire(source, PassiveTrigger.OnKill);
            }

            return fired;
        }

        /// <summary>A member joins the fight: their Always and OnBattleStart passives fire.</summary>
        private void EnterBattle(Combatant member)
        {
            _passives.Fire(member, PassiveTrigger.Always);
            _passives.Fire(member, PassiveTrigger.OnBattleStart);
        }

        internal void RaisePassiveTriggered(PassiveActivation activation) => PassiveTriggered?.Invoke(activation);

        /// <summary>Records that a party hit's element landed on an enemy, so its reaction shows from now on.</summary>
        internal void DiscoverElement(Combatant source, Combatant target, Element element)
        {
            if (source.Side == BattleSide.Party && target.Enemy != null) Knowledge.Discover(target.Enemy, element);
        }

        /// <summary>
        /// True if the party has discovered how <paramref name="enemy"/>'s type reacts to <paramref name="element"/>.
        /// None always counts as known, since it has no reaction.
        /// </summary>
        public bool IsElementKnown(Combatant enemy, Element element) =>
            element == Element.None || (enemy?.Enemy != null && Knowledge.IsDiscovered(enemy.Enemy, element));

        /// <summary>Points revealed single-target attacks at the taunter, if there is one.</summary>
        private void RetargetToTaunter()
        {
            var taunter = Taunter;
            if (taunter == null) return;

            foreach (var intent in _intents.Values.ToList())
            {
                if (intent.Target == taunter || !intent.Ability.IsSingleTargetAttack) continue;
                intent.Target = taunter;
                IntentRetargeted?.Invoke(intent);
            }
        }

        private void ForgetQueuedAction(Combatant unit)
        {
            _commands.Remove(unit);
            _intents.Remove(unit);
            _superRipostes.Remove(unit);
        }

        /// <summary>Takes incapacitated units off the bar and puts revived ones back at its start.</summary>
        private void SyncTimeline()
        {
            foreach (var unit in Field)
            {
                var onTimeline = Timeline.Contains(unit);
                if (unit.IsAlive && !onTimeline)
                {
                    Timeline.Add(unit);
                }
                else if (!unit.IsAlive && onTimeline)
                {
                    Timeline.Remove(unit);
                    ForgetQueuedAction(unit);
                    if (CurrentActor == unit) CurrentActor = null;
                }
            }
        }

        /// <summary>Ends the battle if a side is down. Returns true if the flow stopped.</summary>
        private bool EvaluateOutcome()
        {
            var partyDown = _party.All(member => !member.IsAlive);
            var enemiesDown = _enemies.All(enemy => !enemy.IsAlive);

            if (partyDown)
            {
                End(BattleOutcome.Defeat);
                return true;
            }

            if (enemiesDown)
            {
                End(BattleOutcome.Victory);
                return true;
            }

            if (State == BattleState.AwaitingCommand && CurrentActor == null)
                SetState(BattleState.TimelineRunning);

            return false;
        }

        private void End(BattleOutcome outcome)
        {
            if (IsOver) return;

            CurrentActor = null;
            CurrentExecution = null;
            var rewards = outcome == BattleOutcome.Victory ? BattleRewards.Roll(_enemies, Random) : null;
            Result = new BattleResult(outcome, rewards, _participants.ToList());

            SetState(outcome == BattleOutcome.Victory ? BattleState.Victory : BattleState.Defeat);
            if (outcome == BattleOutcome.Victory && Relationships != null)
                Result.FoughtTogether.AddRange(Relationships.RecordFoughtTogether(_participants.Select(member => member.Member)));
            BattleEnded?.Invoke(Result);
        }

        private void SetState(BattleState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
        #endregion
    }
}
