using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Characters;
using InElementum.Data.Enums;

namespace InElementum.Data.Progression
{
    /// <summary>Two different party members, in either order: (A, B) equals (B, A).</summary>
    public readonly struct PartyPair : IEquatable<PartyPair>
    {
        public PartyPair(PartyMemberData a, PartyMemberData b)
        {
            A = a;
            B = b;
        }

        public PartyMemberData A { get; }
        public PartyMemberData B { get; }

        /// <summary>Both members set and different from each other.</summary>
        public bool IsValid => A != null && B != null && A != B;

        public bool Contains(PartyMemberData member) => member != null && (member == A || member == B);

        public bool Equals(PartyPair other) => (A == other.A && B == other.B) || (A == other.B && B == other.A);
        public override bool Equals(object obj) => obj is PartyPair other && Equals(other);

        // XOR is symmetric, so both orders hash alike.
        public override int GetHashCode() => (A != null ? A.GetHashCode() : 0) ^ (B != null ? B.GetHashCode() : 0);

        public override string ToString() =>
            $"{(A != null ? A.CharacterName : "?")} & {(B != null ? B.CharacterName : "?")}";
    }

    /// <summary>
    /// Relationship levels between pairs of party members, 0 to <see cref="MaxLevel"/>, default 0. Fusion Surges and
    /// relationship abilities query it. Gaining levels (Medium hub, cutscenes) comes later; for now the battle reports
    /// who fought together through <see cref="FoughtTogether"/>. Plain C#; persistence comes later.
    /// </summary>
    public class RelationshipService
    {
        public const int MaxLevel = 5;

        private readonly Dictionary<PartyPair, int> _levels = new Dictionary<PartyPair, int>();

        /// <summary>Debug override: when set, every valid pair reads this level instead of its own.</summary>
        public int? DebugLevel { get; set; }

        /// <summary>A pair won a battle together. Raised once per pair per victory, for later relationship gain.</summary>
        public event Action<PartyPair> FoughtTogether;

        public int GetLevel(PartyMemberData a, PartyMemberData b) => GetLevel(new PartyPair(a, b));

        public int GetLevel(PartyPair pair)
        {
            if (!pair.IsValid) return 0;
            if (DebugLevel.HasValue) return Clamp(DebugLevel.Value);
            return _levels.TryGetValue(pair, out var level) ? level : 0;
        }

        /// <summary>Sets a pair's level, clamped to 0..<see cref="MaxLevel"/>. Ignores invalid pairs.</summary>
        public void SetLevel(PartyMemberData a, PartyMemberData b, int level)
        {
            var pair = new PartyPair(a, b);
            if (pair.IsValid) _levels[pair] = Clamp(level);
        }

        public bool HasLevel(PartyMemberData a, PartyMemberData b, int level) => GetLevel(a, b) >= level;

        /// <summary>
        /// Raises <see cref="FoughtTogether"/> for every pair among <paramref name="members"/>, each once, in the order
        /// the members are given. Returns the pairs.
        /// </summary>
        public List<PartyPair> RecordFoughtTogether(IEnumerable<PartyMemberData> members)
        {
            var distinct = members.Where(member => member != null).Distinct().ToList();
            var pairs = new List<PartyPair>();

            for (var i = 0; i < distinct.Count; i++)
            {
                for (var j = i + 1; j < distinct.Count; j++)
                    pairs.Add(new PartyPair(distinct[i], distinct[j]));
            }

            foreach (var pair in pairs)
                FoughtTogether?.Invoke(pair);

            return pairs;
        }

        /// <summary>The relationship level an unlock source needs, or 0 if it is not a relationship source.</summary>
        public static int RequiredLevel(UnlockSource source)
        {
            switch (source)
            {
                case UnlockSource.RelationshipLevel3: return 3;
                case UnlockSource.RelationshipLevel5: return 5;
                default: return 0;
            }
        }

        private static int Clamp(int level) => Math.Max(0, Math.Min(MaxLevel, level));
    }
}
