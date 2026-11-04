using InElementum.Combat.Interfaces;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Combat.Actions
{
    /// <summary>Everything an effect needs to apply itself once.</summary>
    public sealed class EffectContext
    {
        public EffectContext(ICombatant source, ICombatant target, float value)
        {
            Source = source;
            Target = target;
            Value = value;
        }

        public ICombatant Source { get; }
        public ICombatant Target { get; }
        public float Value { get; }

        /// <summary>The authored action behind this effect. Null for enemy abilities.</summary>
        public CombatActionData Action { get; set; }
        public StatusEffectType Status { get; set; }
        public StatType Stat { get; set; }
        public Element Element { get; set; }
        public int HitIndex { get; set; }
        public bool IsCrit { get; set; }
        public QteResult QteResult { get; set; }
        /// <summary>Situational damage multiplier, such as a QTE bonus or a Perilous attack.</summary>
        public float Multiplier { get; set; } = 1f;
        /// <summary>The gauge FillSurgeGauge charges: the target's, or the source's when there is no target.</summary>
        public SurgeGauge SurgeGauge { get; set; }

        /// <summary>Written by damage effects: total damage dealt, and the target's reaction to the element.</summary>
        public int DamageDealt { get; set; }
        public ElementReaction Reaction { get; set; }

        public int RoundedValue => Mathf.RoundToInt(Value);
        /// <summary>The value after <see cref="Multiplier"/>, for effects a QTE can strengthen, such as healing.</summary>
        public int ScaledValue => Mathf.RoundToInt(Value * Multiplier);
        public ICombatant TargetOrSource => Target ?? Source;

        public static EffectContext From(ActionEffectData data, ICombatant source, ICombatant target, CombatActionData action = null)
        {
            return new EffectContext(source, target, data.Value)
            {
                Action = action,
                Status = data.Status,
                Stat = data.Stat,
                Element = action != null ? action.Element : Element.None
            };
        }
    }
}
