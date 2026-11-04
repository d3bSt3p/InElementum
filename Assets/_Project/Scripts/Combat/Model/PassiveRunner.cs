using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Actions;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;

namespace InElementum.Combat.Model
{
    /// <summary>One passive firing: whose it was, what set it off, and what each of its effects did.</summary>
    public sealed class PassiveActivation
    {
        internal PassiveActivation(Combatant owner, PassiveData passive, PassiveTrigger trigger, Combatant subject)
        {
            Owner = owner;
            Passive = passive;
            Trigger = trigger;
            Subject = subject;
        }

        /// <summary>The party member wearing the picto or Lumina.</summary>
        public Combatant Owner { get; }
        public PassiveData Passive { get; }
        public PassiveTrigger Trigger { get; }
        /// <summary>The enemy the trigger concerns: the one hit, or the attacker dodged or parried. Null otherwise.</summary>
        public Combatant Subject { get; }
        /// <summary>One entry per effect per target, in effect order.</summary>
        public List<PassiveEffectReport> Effects { get; } = new List<PassiveEffectReport>();
    }

    public sealed class PassiveEffectReport
    {
        public EffectType EffectType { get; internal set; }
        public StatusEffectType Status { get; internal set; }
        public Combatant Target { get; internal set; }
        public int Value { get; internal set; }
        /// <summary>Damage dealt, for damaging effects.</summary>
        public int DamageDealt { get; internal set; }
        public bool Killed { get; internal set; }
    }

    /// <summary>
    /// Fires the triggered passives of a party member's worn pictos and equipped Luminas. Effects go through the same
    /// <see cref="EffectProcessor"/> pipeline as actions, from the wearer, with no element, crit, or multiplier, so they
    /// never fizzle a cast or drain break. A passive's effects never set off another passive.
    /// </summary>
    internal sealed class PassiveRunner
    {
        private readonly BattleModel _model;
        private bool _running;

        public PassiveRunner(BattleModel model)
        {
            _model = model;
        }

        /// <summary>
        /// Fires every passive of <paramref name="owner"/> with this trigger. <paramref name="subject"/> is the enemy
        /// the trigger concerns, aimed at by single-enemy effects. Returns true if any effect ran.
        /// </summary>
        public bool Fire(Combatant owner, PassiveTrigger trigger, Combatant subject = null)
        {
            if (_running || owner == null || !owner.IsAlive) return false;

            var passives = owner.Stats.Passives
                .Where(passive => passive.Trigger == trigger && passive.Effects != null && passive.Effects.Count > 0)
                .ToList();
            if (passives == null || passives.Count == 0) return false;

            var fired = false;
            _running = true;
            try
            {
                foreach (var passive in passives)
                {
                    if (!owner.IsAlive) break;

                    var activation = Apply(owner, passive, trigger, subject);
                    if (activation.Effects.Count == 0) continue;

                    fired = true;
                    _model.RaisePassiveTriggered(activation);
                }
            }
            finally
            {
                _running = false;
            }

            return fired;
        }

        private PassiveActivation Apply(Combatant owner, PassiveData passive, PassiveTrigger trigger, Combatant subject)
        {
            var activation = new PassiveActivation(owner, passive, trigger, subject);

            foreach (var effect in passive.Effects)
            {
                if (effect == null) continue;

                var implementation = EffectProcessor.GetEffect(effect.EffectType);
                if (implementation == null) continue;

                foreach (var target in Targets(owner, effect.TargetType, subject, effect.EffectType == EffectType.Revive))
                {
                    var wasAlive = target.IsAlive;
                    var context = new EffectContext(owner, target, effect.Value)
                    {
                        Status = effect.Status,
                        Stat = effect.Stat,
                        Element = Element.None,
                        SurgeGauge = target.SurgeGauge ?? owner.SurgeGauge
                    };

                    implementation.Apply(context);
                    activation.Effects.Add(new PassiveEffectReport
                    {
                        EffectType = effect.EffectType,
                        Status = effect.Status,
                        Target = target,
                        Value = context.RoundedValue,
                        DamageDealt = context.DamageDealt,
                        Killed = wasAlive && !target.IsAlive
                    });
                }
            }

            return activation;
        }

        /// <summary>
        /// Who a passive effect reaches. Self is the wearer, and Ally picks the wearer too. Enemy is the trigger's
        /// subject if it is still standing, nobody if it has fallen, and a random enemy when the trigger has no subject.
        /// Random and All targets work as they do for actions.
        /// </summary>
        private IEnumerable<Combatant> Targets(Combatant owner, ActionTargetType type, Combatant subject, bool revives)
        {
            switch (type)
            {
                case ActionTargetType.Enemy:
                    if (subject != null)
                        return subject.IsAlive && owner.IsOpponentOf(subject) ? new[] { subject } : Array.Empty<Combatant>();
                    return _model.ResolveTargets(owner, ActionTargetType.RandomEnemy, null, false);
                case ActionTargetType.Ally:
                    return _model.ResolveTargets(owner, ActionTargetType.Ally, owner, revives);
                default:
                    return _model.ResolveTargets(owner, type, null, revives);
            }
        }
    }
}
