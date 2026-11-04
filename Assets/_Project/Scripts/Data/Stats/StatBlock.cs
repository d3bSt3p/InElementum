using System;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Stats
{
    /// <summary>The five GDD stats. A value type, so runtime copies never write back to authored data.</summary>
    [Serializable]
    public struct StatBlock
    {
        [SerializeField] private int vitality;
        [SerializeField] private int defense;
        [SerializeField] private int strength;
        [SerializeField] private int speed;
        [SerializeField] private int luck;

        public StatBlock(int vitality, int defense, int strength, int speed, int luck)
        {
            this.vitality = vitality;
            this.defense = defense;
            this.strength = strength;
            this.speed = speed;
            this.luck = luck;
        }

        public int Vitality => vitality;
        public int Defense => defense;
        public int Strength => strength;
        public int Speed => speed;
        public int Luck => luck;

        public int this[StatType stat]
        {
            get
            {
                switch (stat)
                {
                    case StatType.Vitality: return vitality;
                    case StatType.Defense: return defense;
                    case StatType.Strength: return strength;
                    case StatType.Speed: return speed;
                    case StatType.Luck: return luck;
                    default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
                }
            }
            set
            {
                switch (stat)
                {
                    case StatType.Vitality: vitality = value; break;
                    case StatType.Defense: defense = value; break;
                    case StatType.Strength: strength = value; break;
                    case StatType.Speed: speed = value; break;
                    case StatType.Luck: luck = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
                }
            }
        }
    }
}
