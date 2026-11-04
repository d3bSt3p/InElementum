using System;

namespace InElementum.Combat.Model
{
    /// <summary>One party member's Surge gauge. It charges as that character spends AP; a full gauge unlocks their ultimate.</summary>
    public class SurgeGauge
    {
        public SurgeGauge(int max)
        {
            Max = Math.Max(1, max);
        }

        public int Max { get; }
        public int Current { get; private set; }
        public bool IsFull => Current >= Max;

        public Action<int, int> OnChanged;

        public void Add(int value)
        {
            Current = Math.Min(Math.Max(0, Current + value), Max);
            OnChanged?.Invoke(Current, Max);
        }

        public bool CanSpend(float fraction) => Current >= GetCost(fraction);

        /// <summary>Spends a fraction of the gauge, where 1 means the full gauge. Fails if the gauge holds too little.</summary>
        public bool TrySpend(float fraction)
        {
            var cost = GetCost(fraction);
            if (Current < cost) return false;

            Current -= cost;
            OnChanged?.Invoke(Current, Max);
            return true;
        }

        private int GetCost(float fraction) => (int)Math.Ceiling(Max * (double)fraction);

        public void Reset()
        {
            Current = 0;
            OnChanged?.Invoke(Current, Max);
        }
    }
}
