using System;

namespace InElementum.Data.Progression
{
    /// <summary>One party member's level, XP, and the Lumina and skill points their level grants. Plain C#; persistence comes later.</summary>
    public class CharacterProgress
    {
        private readonly ProgressionTuning _tuning;

        public CharacterProgress(ProgressionTuning tuning, int level = 1)
        {
            _tuning = tuning != null ? tuning : throw new ArgumentNullException(nameof(tuning));
            Level = Math.Min(Math.Max(1, level), tuning.MaxLevel);
        }

        public int Level { get; private set; }
        /// <summary>XP earned toward the next level.</summary>
        public int Xp { get; private set; }
        public int XpToNextLevel => _tuning.GetXpToNextLevel(Level);
        public bool IsMaxLevel => Level >= _tuning.MaxLevel;

        /// <summary>The character's Lumina point budget. Equipped Luminas spend it.</summary>
        public int LuminaPoints => _tuning.GetLuminaPoints(Level);

        /// <summary>Skill points earned so far. The skill tree spends them.</summary>
        public int SkillPoints => _tuning.GetSkillPoints(Level);

        /// <summary>Adds XP, levelling up as many times as it covers. Returns the levels gained.</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0 || IsMaxLevel) return 0;

            var gained = 0;
            Xp += amount;

            while (!IsMaxLevel && Xp >= XpToNextLevel)
            {
                Xp -= XpToNextLevel;
                Level++;
                gained++;
            }

            if (IsMaxLevel) Xp = 0;
            return gained;
        }
    }
}
