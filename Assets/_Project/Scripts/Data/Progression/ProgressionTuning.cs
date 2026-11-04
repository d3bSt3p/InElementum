using System;
using UnityEngine;

namespace InElementum.Data.Progression
{
    /// <summary>Character levelling: the XP curve, and the Lumina points and skill points each level grants.</summary>
    [CreateAssetMenu(fileName = "Progression Tuning", menuName = "In Elementum/Settings/Progression Tuning", order = 3)]
    public class ProgressionTuning : ScriptableObject
    {
        [Header("Levels")]
        [SerializeField] [Min(1)] private int maxLevel = 99;
        [Tooltip("XP needed to go from level 1 to level 2.")]
        [SerializeField] [Min(1)] private int xpToLevelTwo = 50;
        [Tooltip("Each level needs this many times the XP of the one before.")]
        [SerializeField] [Min(1f)] private float xpGrowthPerLevel = 1.15f;

        [Header("Lumina points")]
        [Tooltip("Lumina points a character has at level 1.")]
        [SerializeField] [Min(0)] private int startingLuminaPoints = 5;
        [SerializeField] [Min(0)] private int luminaPointsPerLevel = 1;

        [Header("Skill points")]
        [Tooltip("Skill points a character has at level 1, to spend in their skill tree.")]
        [SerializeField] [Min(0)] private int startingSkillPoints = 2;
        [SerializeField] [Min(0)] private int skillPointsPerLevel = 1;

        public int MaxLevel { get => maxLevel; internal set => maxLevel = value; }
        public int XpToLevelTwo { get => xpToLevelTwo; internal set => xpToLevelTwo = value; }
        public float XpGrowthPerLevel { get => xpGrowthPerLevel; internal set => xpGrowthPerLevel = value; }
        public int StartingLuminaPoints { get => startingLuminaPoints; internal set => startingLuminaPoints = value; }
        public int LuminaPointsPerLevel { get => luminaPointsPerLevel; internal set => luminaPointsPerLevel = value; }
        public int StartingSkillPoints { get => startingSkillPoints; internal set => startingSkillPoints = value; }
        public int SkillPointsPerLevel { get => skillPointsPerLevel; internal set => skillPointsPerLevel = value; }

        /// <summary>XP needed to go from <paramref name="level"/> to the next. 0 at the level cap.</summary>
        public int GetXpToNextLevel(int level)
        {
            if (level >= maxLevel) return 0;

            var xp = xpToLevelTwo * Math.Pow(xpGrowthPerLevel, Math.Max(0, level - 1));
            return Math.Max(1, (int)Math.Round(xp, MidpointRounding.AwayFromZero));
        }

        public int GetLuminaPoints(int level) => startingLuminaPoints + Math.Max(0, level - 1) * luminaPointsPerLevel;

        /// <summary>Skill points earned by <paramref name="level"/>, spent or not.</summary>
        public int GetSkillPoints(int level) => startingSkillPoints + Math.Max(0, level - 1) * skillPointsPerLevel;
    }
}
