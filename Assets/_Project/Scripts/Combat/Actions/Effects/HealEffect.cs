using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    public class HealEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.Heal;

        public override void Apply(EffectContext context)
        {
            var target = context.TargetOrSource;
            if (target == null) return;

            target.Stats.Heal(context.ScaledValue);

            PlayFx(target, FxType.Heal);
            PlayAudio(context, AudioActionType.Heal);
        }
    }
}
