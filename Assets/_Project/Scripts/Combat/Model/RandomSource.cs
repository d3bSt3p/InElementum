using System;

namespace InElementum.Combat.Model
{
    /// <summary>Randomness for combat rules, injectable so tests can fix the rolls.</summary>
    public interface IRandomSource
    {
        /// <summary>A value in [0, 1).</summary>
        float Value();

        /// <summary>An integer in [minInclusive, maxExclusive). Returns minInclusive when the range is empty.</summary>
        int Range(int minInclusive, int maxExclusive);
    }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource() : this(Environment.TickCount) { }

        public SystemRandomSource(int seed)
        {
            _random = new Random(seed);
        }

        public float Value() => (float)_random.NextDouble();

        public int Range(int minInclusive, int maxExclusive) =>
            maxExclusive <= minInclusive ? minInclusive : _random.Next(minInclusive, maxExclusive);
    }
}
