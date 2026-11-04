using System;
using System.Collections.Generic;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Weapons
{
    /// <summary>How strongly a weapon's power grows with one attribute, from E (barely) to S (most).</summary>
    public enum ScalingGrade
    {
        E,
        D,
        C,
        B,
        A,
        S
    }

    /// <summary>One attribute a weapon scales with, and how strongly.</summary>
    [Serializable]
    public struct WeaponScaling
    {
        [SerializeField] private StatType stat;
        [SerializeField] private ScalingGrade grade;

        public WeaponScaling(StatType stat, ScalingGrade grade)
        {
            this.stat = stat;
            this.grade = grade;
        }

        public StatType Stat => stat;
        public ScalingGrade Grade => grade;
    }

    /// <summary>
    /// A weapon: what a character's basic attack hits with. Its element is the attack's element (None hits without
    /// one), and its power is each hit's power: <see cref="BasePower"/>, plus <see cref="PowerPerLevel"/> per upgrade
    /// level, plus the wielder's attributes times the grade of each <see cref="Scaling"/> entry (coefficients in
    /// CombatTuning). See <see cref="WeaponMath"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "In Elementum/Weapons/Weapon", order = 0)]
    public class WeaponData : ScriptableObject
    {
        [Header("Profile")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] [TextArea] private string description;
        [Tooltip("The one character who can wield this weapon. Empty lets anyone wield it.")]
        [SerializeField] private PartyMemberData wielder;

        [Header("Attack")]
        [Tooltip("The basic attack's element while this weapon is equipped. None hits without an element.")]
        [SerializeField] private Element element;
        [Tooltip("Power of each basic attack hit at level 1, before attribute scaling.")]
        [SerializeField] [Min(0f)] private float basePower = 3f;
        [Tooltip("Power added per upgrade level above 1.")]
        [SerializeField] [Min(0f)] private float powerPerLevel = 1f;
        [Tooltip("Highest upgrade level.")]
        [SerializeField] [Min(1)] private int maxLevel = 10;
        [Tooltip("Attributes that add to this weapon's power, and how strongly.")]
        [SerializeField] private List<WeaponScaling> scaling = new List<WeaponScaling>();

        public string Id { get => id; internal set => id = value; }
        public string DisplayName { get => displayName; internal set => displayName = value; }
        public Sprite Icon { get => icon; internal set => icon = value; }
        public string Description { get => description; internal set => description = value; }
        public PartyMemberData Wielder { get => wielder; internal set => wielder = value; }
        public Element Element { get => element; internal set => element = value; }
        public float BasePower { get => basePower; internal set => basePower = value; }
        public float PowerPerLevel { get => powerPerLevel; internal set => powerPerLevel = value; }
        public int MaxLevel { get => maxLevel; internal set => maxLevel = value; }
        public List<WeaponScaling> Scaling { get => scaling; internal set => scaling = value; }

        /// <summary>Whether <paramref name="member"/> may wield this weapon.</summary>
        public bool CanBeWieldedBy(PartyMemberData member) => member != null && (wielder == null || wielder == member);

        /// <summary>The grade this weapon scales <paramref name="stat"/> with, or null when it does not.</summary>
        public ScalingGrade? GetGrade(StatType stat)
        {
            foreach (var entry in scaling)
                if (entry.Stat == stat) return entry.Grade;
            return null;
        }
    }
}
