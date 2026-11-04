using System.Collections.Generic;
using InElementum.Combat.Actions;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Stats;

namespace InElementum.Combat.Model
{
    /// <summary>How an enemy attack's defense chain ended, decided when its last hit resolves.</summary>
    public enum ChainOutcome
    {
        /// <summary>Not an enemy attack, or its last hit has not resolved yet.</summary>
        None,
        /// <summary>At least one hit landed: the action resolves normally.</summary>
        Landed,
        /// <summary>Every hit dodged or parried, at least one dodged: cancelled, with interrupt knockback.</summary>
        Interrupted,
        /// <summary>
        /// Every hit parried: cancelled, and the enemy goes to the bar start. Each parrier strikes back at once:
        /// with a Super Riposte if they were in Cast, otherwise with a counter attack.
        /// </summary>
        Parried
    }

    /// <summary>One piece of a resolving action: a single hit or a single effect.</summary>
    public sealed class ExecutionStep
    {
        internal ExecutionStep(EffectType effectType, ActionTargetType targetType, float value)
        {
            EffectType = effectType;
            TargetType = targetType;
            Value = value;
        }

        public EffectType EffectType { get; }
        public ActionTargetType TargetType { get; }
        public float Value { get; }
        public StatusEffectType Status { get; internal set; }
        public StatType Stat { get; internal set; }
        public Element Element { get; internal set; }
        public int HitIndex { get; internal set; }

        /// <summary>Who strikes. Null means the action's actor; set for a guard counter-hit.</summary>
        public Combatant Attacker { get; internal set; }
        /// <summary>A fixed target, used by a guard counter-hit. Null means the step's target type decides.</summary>
        public Combatant FixedTarget { get; internal set; }

        /// <summary>A basic attack hit: each one that lands grants the attacker AP.</summary>
        public bool IsAttackHit { get; internal set; }
        /// <summary>
        /// An enemy hit on the party. The player gets one Dodge/Parry prompt for it, covering every target.
        /// Each dodge grants AP. Every hit's answer feeds the chain outcome, except guard counter-hits.
        /// </summary>
        public bool NeedsDefense { get; internal set; }
        /// <summary>Which inputs work against this hit. The forbidden one counts as a miss.</summary>
        public DefenseRule DefenseRule { get; internal set; }
        /// <summary>Extra seconds of wind-up before this hit lands, for the view's prompt timing.</summary>
        public float ImpactDelay { get; internal set; }
        /// <summary>A guarding enemy's answer to a basic-attack hit. It has no chain outcome.</summary>
        public bool IsGuardCounter { get; internal set; }

        public bool DealsDamage => EffectType == EffectType.Damage || EffectType == EffectType.LifeSteal;
    }

    /// <summary>What one step did to one target.</summary>
    public sealed class HitReport
    {
        public Combatant Target { get; internal set; }
        public int DamageDealt { get; internal set; }
        public ElementReaction Reaction { get; internal set; }
        public bool IsCrit { get; internal set; }
        public DefenseOutcome Defense { get; internal set; }
        /// <summary>The hit's element countered the target's cast, so the cast fizzled.</summary>
        public bool Fizzled { get; internal set; }
        /// <summary>Counter attack or Super Riposte damage on the parried enemy. It drains extra break.</summary>
        public bool IsCounter { get; internal set; }
        /// <summary>This hit emptied the target's break meter.</summary>
        public bool Broke { get; internal set; }
        /// <summary>The target was standing before this hit and is down after it.</summary>
        public bool Killed { get; internal set; }
        public int ApGained { get; internal set; }
    }

    public sealed class StepReport
    {
        internal StepReport(ActionExecution execution, ExecutionStep step)
        {
            Execution = execution;
            Step = step;
        }

        public ActionExecution Execution { get; }
        public ExecutionStep Step { get; }
        public List<HitReport> Hits { get; } = new List<HitReport>();
    }

    /// <summary>
    /// A resolving action, run one step at a time so the view can animate each hit and show a defense prompt before it.
    /// Costs (AP, item, Surge gauge) are paid when resolution begins; if they can't be paid, the action fails.
    /// An enemy attack whose every hit is dodged or parried is cancelled once its last hit resolves.
    /// </summary>
    public sealed class ActionExecution
    {
        private readonly BattleModel _model;
        private readonly List<ExecutionStep> _steps = new List<ExecutionStep>();
        private int _nextStep;
        private bool _began;

        /// <summary>Index of the last step whose answer feeds the chain outcome, or -1.</summary>
        private int _lastChainStep = -1;
        private int _landedHits;
        private int _dodgedHits;
        private int _parriedHits;
        private readonly List<Combatant> _parriers = new List<Combatant>();

        private ActionExecution(BattleModel model, Combatant actor, Combatant chosenTarget)
        {
            _model = model;
            Actor = actor;
            ChosenTarget = chosenTarget;
        }

        public Combatant Actor { get; }
        public Combatant ChosenTarget { get; }
        /// <summary>Null for enemy actions.</summary>
        public BattleCommand Command { get; private set; }
        /// <summary>Null for party actions.</summary>
        public EnemyAbilityData EnemyAbility { get; private set; }

        public IReadOnlyList<ExecutionStep> Steps => _steps;
        /// <summary>Its cost could no longer be paid when it came to resolve, so it did nothing.</summary>
        public bool CostFailed { get; private set; }
        /// <summary>For enemy attacks: how the defense chain ended.</summary>
        public ChainOutcome Chain { get; private set; }
        /// <summary>A fully dodged or parried enemy attack: its remaining steps never run.</summary>
        public bool IsCancelled => Chain == ChainOutcome.Interrupted || Chain == ChainOutcome.Parried;
        public bool IsComplete => CostFailed || IsCancelled || _nextStep >= _steps.Count;
        public ExecutionStep NextStep => IsComplete ? null : _steps[_nextStep];

        /// <summary>
        /// The damage multiplier of a Super Riposte, earned by the actor parrying a full chain while casting this.
        /// 0 when it is not one.
        /// </summary>
        public float SuperRiposteMultiplier { get; private set; }
        public bool IsSuperRiposte => SuperRiposteMultiplier > 0f;

        /// <summary>
        /// The damage multiplier of a counter attack: the actor's basic attack, struck back at once after parrying a
        /// full chain while in Wait. Its hits grant no AP and draw no guard counter-hits, and the actor keeps its place
        /// on the bar. 0 when it is not one.
        /// </summary>
        public float CounterMultiplier { get; private set; }
        public bool IsCounter => CounterMultiplier > 0f;
        /// <summary>The parried enemy, for a counter attack or Super Riposte. Damage to it drains extra break.</summary>
        public Combatant CounterTarget { get; private set; }

        /// <summary>The ability's QTE, shown as it executes. Null when there is none.</summary>
        public QteData Qte => Command?.Action is AbilityData ability && ability.HasQte ? ability.Qte : null;
        public QteResult QteResult { get; private set; } = QteResult.None;

        public void SetQteResult(QteResult result) => QteResult = result;

        private float EffectMultiplier =>
            Qte != null && (QteResult == QteResult.Success || QteResult == QteResult.Perfect) ? Qte.SuccessMultiplier : 1f;

        #region Building
        internal static ActionExecution ForCommand(BattleModel model, Combatant actor, BattleCommand command,
            float superRiposte = 0f, Combatant counterTarget = null)
        {
            var execution = new ActionExecution(model, actor, command.Target)
            {
                Command = command,
                SuperRiposteMultiplier = superRiposte,
                CounterTarget = superRiposte > 0f ? counterTarget : null
            };

            if (command.Type == CommandType.Attack)
            {
                execution.AddAttackSteps(true);
            }
            else if (command.Action != null)
            {
                foreach (var effect in command.Action.Effects)
                    execution._steps.Add(FromEffect(effect, command.Action.Element, 0));
            }

            return execution;
        }

        /// <summary>A counter attack on <paramref name="enemy"/>: the member's basic attack, each hit times <paramref name="multiplier"/>.</summary>
        internal static ActionExecution ForCounter(BattleModel model, Combatant member, Combatant enemy, float multiplier)
        {
            var execution = new ActionExecution(model, member, enemy)
            {
                Command = BattleCommand.Attack(enemy),
                CounterMultiplier = multiplier,
                CounterTarget = enemy
            };

            execution.AddAttackSteps(false);
            return execution;
        }

        /// <param name="grantsAp">False for a counter attack, whose hits grant no AP and draw no guard counter-hits.</param>
        private void AddAttackSteps(bool grantsAp)
        {
            var attack = Actor.Member.BasicAttack;
            var hits = StatCalculator.GetAttackHitCount(attack, Actor.Stats.Passives);
            // The held weapon sets each hit's power and element.
            var element = Actor.AttackElement;
            var power = Actor.AttackPower;

            for (var i = 0; i < hits; i++)
            {
                _steps.Add(new ExecutionStep(EffectType.Damage, attack.TargetType, power)
                {
                    Element = element,
                    HitIndex = i,
                    IsAttackHit = grantsAp
                });

                foreach (var extra in attack.Effects)
                    _steps.Add(FromEffect(extra, element, i));
            }
        }

        internal static ActionExecution ForEnemy(BattleModel model, EnemyIntent intent)
        {
            var execution = new ActionExecution(model, intent.Enemy, intent.Target) { EnemyAbility = intent.Ability };
            var ability = intent.Ability;

            foreach (var action in ability.ActionList)
            {
                var isHit = (action.EffectType == EffectType.Damage || action.EffectType == EffectType.LifeSteal)
                            && TargetsOpponents(action.TargetType);
                var count = isHit ? ability.HitCount : 1;

                for (var i = 0; i < count; i++)
                {
                    execution._steps.Add(new ExecutionStep(action.EffectType, action.TargetType, action.ActionValue)
                    {
                        Status = action.Status,
                        Stat = action.Stat,
                        Element = ability.Element,
                        HitIndex = i,
                        NeedsDefense = isHit,
                        DefenseRule = ability.DefenseRule,
                        ImpactDelay = isHit ? ability.GetHitDelay(i) : 0f
                    });
                    if (isHit) execution._lastChainStep = execution._steps.Count - 1;
                }
            }

            return execution;
        }

        private static ExecutionStep FromEffect(ActionEffectData effect, Element element, int hitIndex)
        {
            return new ExecutionStep(effect.EffectType, effect.TargetType, effect.Value)
            {
                Status = effect.Status,
                Stat = effect.Stat,
                Element = element,
                HitIndex = hitIndex
            };
        }

        private static bool TargetsOpponents(ActionTargetType type) =>
            type == ActionTargetType.Enemy || type == ActionTargetType.AllEnemies || type == ActionTargetType.RandomEnemy;
        #endregion

        #region Running
        /// <summary>Pays the action's cost. Called by the model as resolution begins.</summary>
        internal void Begin()
        {
            if (_began) return;
            _began = true;

            if (Command == null) return;

            switch (Command.Action)
            {
                case AbilityData ability:
                    if (!Actor.Stats.TrySpendAp(ability.ApCost))
                    {
                        CostFailed = true;
                        return;
                    }

                    Actor.SurgeGauge?.Add(ability.ApCost * _model.Tuning.SurgeChargePerAp);
                    break;
                case ItemData item:
                    if (!_model.Inventory.TryConsume(item)) CostFailed = true;
                    break;
                case SurgeData surge:
                    if (Actor.SurgeGauge == null || !Actor.SurgeGauge.TrySpend(surge.GaugeCost)) CostFailed = true;
                    break;
            }
        }

        /// <summary>
        /// Runs the next step. <paramref name="defense"/> is the player's judged Dodge/Parry for steps that
        /// <see cref="ExecutionStep.NeedsDefense"/>; it is ignored otherwise. An input the step's defense rule forbids
        /// counts as a miss.
        /// </summary>
        public StepReport ExecuteNext(DefenseOutcome defense = DefenseOutcome.Hit)
        {
            if (IsComplete || !Actor.IsAlive)
            {
                _nextStep = _steps.Count;
                return null;
            }

            var index = _nextStep++;
            var step = _steps[index];
            var source = step.Attacker ?? Actor;
            var report = new StepReport(this, step);
            defense = Enforce(step.DefenseRule, defense);

            if (source.IsAlive)
            {
                foreach (var target in Targets(step, source))
                {
                    var hit = new HitReport { Target = target, Defense = DefenseOutcome.Hit };
                    report.Hits.Add(hit);

                    if (step.NeedsDefense)
                        ApplyDefendedHit(step, source, target, defense, hit);
                    else
                        ApplyStep(step, source, target, hit, index);
                }

                if (step.NeedsDefense && !step.IsGuardCounter && report.Hits.Count > 0)
                    RecordChainHit(defense, report.Hits);
            }

            if (index == _lastChainStep) ResolveChain();

            _model.AfterStep(report);
            return report;
        }

        /// <summary>Runs every remaining step. <paramref name="defense"/> answers each defense prompt; null means no input.</summary>
        public void RunToEnd(System.Func<ExecutionStep, DefenseOutcome> defense = null)
        {
            while (!IsComplete)
            {
                var step = NextStep;
                if (ExecuteNext(step.NeedsDefense && defense != null ? defense(step) : DefenseOutcome.Hit) == null) break;
            }
        }

        /// <summary>The forbidden input counts as a miss: no Parry against DodgeOnly, no Dodge against ParryOnly.</summary>
        public static DefenseOutcome Enforce(DefenseRule rule, DefenseOutcome defense)
        {
            if (rule == DefenseRule.DodgeOnly && defense == DefenseOutcome.Parried) return DefenseOutcome.Hit;
            if (rule == DefenseRule.ParryOnly && defense == DefenseOutcome.Dodged) return DefenseOutcome.Hit;
            return defense;
        }

        private IEnumerable<Combatant> Targets(ExecutionStep step, Combatant source)
        {
            if (step.FixedTarget != null)
                return step.FixedTarget.IsAlive ? new[] { step.FixedTarget } : System.Array.Empty<Combatant>();

            return _model.ResolveTargets(source, step.TargetType, ChosenTarget, step.EffectType == EffectType.Revive);
        }

        private void ApplyStep(ExecutionStep step, Combatant source, Combatant target, HitReport hit, int index)
        {
            var context = CreateContext(step, source, target);
            var wasCasting = false;
            var isCounter = false;
            var wasAlive = target.IsAlive;

            if (step.DealsDamage)
            {
                context.IsCrit = DamageCalculator.RollCrit(source.Stats.FinalStats, _model.Tuning, _model.Random);
                hit.IsCrit = context.IsCrit;
                wasCasting = _model.IsCasting(target);
                context.Multiplier *= DamageMultiplier(source, target, out isCounter);
            }

            EffectProcessor.GetEffect(step.EffectType)?.Apply(context);
            hit.DamageDealt = context.DamageDealt;
            hit.Reaction = context.Reaction;
            hit.Killed = wasAlive && !target.IsAlive;

            if (!step.DealsDamage) return;

            _model.DiscoverElement(source, target, step.Element);
            hit.IsCounter = isCounter;
            hit.Fizzled = _model.TryFizzle(target, step.Element);
            if (source.Side == BattleSide.Party && target.Side == BattleSide.Enemy)
                hit.Broke = _model.DrainBreak(target, hit.DamageDealt, wasCasting, isCounter);

            if (!step.IsAttackHit) return;

            hit.ApGained = _model.Tuning.ApPerHit;
            source.Stats.GainAp(hit.ApGained);

            // A guarding enemy answers each basic-attack hit with a counter-hit, played right after it.
            if (target.IsAlive && _model.IsGuarding(target))
                _steps.Insert(index + 1, GuardCounter(target, source));
        }

        private ExecutionStep GuardCounter(Combatant guard, Combatant attacker)
        {
            var ability = _model.GetIntent(guard).Ability;
            return new ExecutionStep(EffectType.Damage, ActionTargetType.Enemy, ability.GuardCounterPower)
            {
                Attacker = guard,
                FixedTarget = attacker,
                Element = ability.Element,
                NeedsDefense = true,
                DefenseRule = ability.DefenseRule,
                IsGuardCounter = true
            };
        }

        /// <summary>
        /// Situational damage multiplier for a hit: Super Riposte or counter attack for a party action, and Guard
        /// reduction on a guarding enemy. <paramref name="isCounter"/> marks the actor's hits on the parried enemy.
        /// </summary>
        private float DamageMultiplier(Combatant source, Combatant target, out bool isCounter)
        {
            var multiplier = IsSuperRiposte ? SuperRiposteMultiplier : IsCounter ? CounterMultiplier : 1f;
            isCounter = CounterTarget != null && source == Actor && target == CounterTarget;

            if (target.Side == BattleSide.Enemy && _model.IsGuarding(target))
                multiplier *= _model.Tuning.GuardDamageMultiplier;

            return multiplier;
        }

        private void ApplyDefendedHit(ExecutionStep step, Combatant source, Combatant target, DefenseOutcome defense, HitReport hit)
        {
            hit.Defense = defense;

            switch (defense)
            {
                case DefenseOutcome.Dodged:
                    hit.ApGained = _model.Tuning.ApPerDodge + (target.Stats.IsTaunting ? _model.Tuning.TauntDodgeApBonus : 0);
                    target.Stats.GainAp(hit.ApGained);
                    return;
                case DefenseOutcome.Parried:
                    return;
                default:
                    // Enemy hits never crit.
                    var context = CreateContext(step, source, target);
                    EffectProcessor.GetEffect(step.EffectType)?.Apply(context);
                    hit.DamageDealt = context.DamageDealt;
                    hit.Reaction = context.Reaction;
                    hit.Killed = !target.IsAlive;
                    hit.Fizzled = _model.TryFizzle(target, step.Element);
                    return;
            }
        }

        private void RecordChainHit(DefenseOutcome defense, List<HitReport> hits)
        {
            switch (defense)
            {
                case DefenseOutcome.Dodged:
                    _dodgedHits++;
                    break;
                case DefenseOutcome.Parried:
                    _parriedHits++;
                    foreach (var hit in hits)
                    {
                        if (!_parriers.Contains(hit.Target)) _parriers.Add(hit.Target);
                    }

                    break;
                default:
                    _landedHits++;
                    break;
            }
        }

        /// <summary>
        /// Decides the chain outcome once the last hit resolves. Any landed hit lets the action resolve normally. A
        /// clean defense cancels the remaining steps; steps that already ran stay applied.
        /// </summary>
        private void ResolveChain()
        {
            if (_landedHits + _dodgedHits + _parriedHits == 0) return;

            if (_landedHits > 0) Chain = ChainOutcome.Landed;
            else Chain = _dodgedHits > 0 ? ChainOutcome.Interrupted : ChainOutcome.Parried;

            _model.ResolveChain(this, _parriers);
        }

        private EffectContext CreateContext(ExecutionStep step, Combatant source, Combatant target)
        {
            return new EffectContext(source, target, step.Value)
            {
                Action = source != Actor ? null : Command?.Action ?? (Command?.Type == CommandType.Attack ? Actor.Member.BasicAttack : null),
                Status = step.Status,
                Stat = step.Stat,
                Element = step.Element,
                HitIndex = step.HitIndex,
                QteResult = source != Actor ? QteResult.None : QteResult,
                Multiplier = source != Actor ? 1f : EffectMultiplier,
                SurgeGauge = (target ?? source).SurgeGauge
            };
        }
        #endregion

        public override string ToString() =>
            $"{Actor.Name}: {(Command != null ? Command.ToString() : EnemyAbility?.Name)}";
    }
}
