using System;
using InElementum.Data.Enums;
using InElementum.Data.Settings;

namespace InElementum.Combat.Model
{
    public enum DefenseInput
    {
        None,
        Dodge,
        Parry
    }

    public enum DefenseOutcome
    {
        Hit,
        Dodged,
        Parried
    }

    /// <summary>Judges timed inputs. Plain C#: the prompt on screen is a separate view.</summary>
    public static class QteJudge
    {
        /// <param name="offset">Input time minus target time, in seconds. Null when nothing was pressed.</param>
        /// <param name="window">Seconds either side of the target time that still succeed.</param>
        public static QteResult Judge(float? offset, float window, float perfectFraction)
        {
            if (offset == null) return QteResult.Fail;

            var distance = Math.Abs(offset.Value);
            if (distance > window) return QteResult.Fail;
            return distance <= window * perfectFraction ? QteResult.Perfect : QteResult.Success;
        }

        /// <summary>
        /// Judges a Dodge or Parry pressed <paramref name="offset"/> seconds from a hit's impact.
        /// Parry uses the tighter window. Either input works on any prompt.
        /// </summary>
        public static DefenseOutcome JudgeDefense(DefenseInput input, float? offset, CombatTuning tuning)
        {
            switch (input)
            {
                case DefenseInput.Dodge:
                    return Judge(offset, tuning.DodgeWindow, tuning.PerfectWindowFraction) == QteResult.Fail
                        ? DefenseOutcome.Hit
                        : DefenseOutcome.Dodged;
                case DefenseInput.Parry:
                    return Judge(offset, tuning.ParryWindow, tuning.PerfectWindowFraction) == QteResult.Fail
                        ? DefenseOutcome.Hit
                        : DefenseOutcome.Parried;
                default:
                    return DefenseOutcome.Hit;
            }
        }
    }
}
