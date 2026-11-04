using System;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Stats
{
    [Serializable]
    public struct StatModifier
    {
        [SerializeField] private StatType stat;
        [SerializeField] private ModifierKind kind;
        [Tooltip("Flat: stat points. Percent: a fraction, so 0.25 means +25%.")]
        [SerializeField] private float value;

        public StatModifier(StatType stat, ModifierKind kind, float value)
        {
            this.stat = stat;
            this.kind = kind;
            this.value = value;
        }

        public StatType Stat => stat;
        public ModifierKind Kind => kind;
        public float Value => value;
    }
}
