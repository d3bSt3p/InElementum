using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    /// <summary>Applies the authored status, replacing NueDeck's one class per status.</summary>
    public class ApplyStatusEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.ApplyStatus;

        public override void Apply(EffectContext context)
        {
            var target = context.TargetOrSource;
            if (target == null) return;

            target.Stats.ApplyStatus(context.Status, context.RoundedValue, context.Stat);

            PlayFx(target, GetFx(context.Status));
            PlayAudio(context, GetAudio(context.Status));
        }

        private static FxType GetFx(StatusEffectType status)
        {
            switch (status)
            {
                case StatusEffectType.Poison: return FxType.Poison;
                case StatusEffectType.Burn: return FxType.Burn;
                case StatusEffectType.Stun: return FxType.Stun;
                case StatusEffectType.Weakness:
                case StatusEffectType.Soft:
                case StatusEffectType.StatDebuff: return FxType.Debuff;
                default: return FxType.Buff;
            }
        }

        private static AudioActionType GetAudio(StatusEffectType status)
        {
            switch (status)
            {
                case StatusEffectType.Poison: return AudioActionType.Poison;
                case StatusEffectType.Burn: return AudioActionType.Burn;
                case StatusEffectType.Stun:
                case StatusEffectType.Weakness:
                case StatusEffectType.Soft:
                case StatusEffectType.StatDebuff: return AudioActionType.Debuff;
                default: return AudioActionType.Buff;
            }
        }
    }
}
