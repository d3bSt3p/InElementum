using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    public class StunEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.Stun;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null) return;

            context.Target.Stats.ApplyStatus(StatusEffectType.Stun, context.RoundedValue);

            PlayFx(context.Target, FxType.Stun);
            PlayAudio(context, AudioActionType.Debuff);
        }
    }
}
