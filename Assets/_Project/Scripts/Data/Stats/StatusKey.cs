using System;
using InElementum.Data.Enums;

namespace InElementum.Data.Stats
{
    /// <summary>Identifies a status on a character. StatBuff and StatDebuff exist once per stat.</summary>
    public readonly struct StatusKey : IEquatable<StatusKey>
    {
        public StatusKey(StatusEffectType type, StatType stat = default)
        {
            Type = type;
            Stat = IsKeyedByStat(type) ? stat : default;
        }

        public StatusEffectType Type { get; }
        public StatType Stat { get; }
        public bool IsStatKeyed => IsKeyedByStat(Type);

        public static bool IsKeyedByStat(StatusEffectType type) =>
            type == StatusEffectType.StatBuff || type == StatusEffectType.StatDebuff;

        public bool Equals(StatusKey other) => Type == other.Type && Stat == other.Stat;
        public override bool Equals(object obj) => obj is StatusKey other && Equals(other);
        public override int GetHashCode() => ((int)Type * 397) ^ (int)Stat;
        public override string ToString() => IsStatKeyed ? $"{Type}({Stat})" : Type.ToString();
    }
}
