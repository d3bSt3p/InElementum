using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    public class IncreaseMaxHealthEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.IncreaseMaxHealth;

        public override void Apply(EffectContext context)
        {
            var target = context.TargetOrSource;
            if (target == null) return;

            target.Stats.IncreaseMaxHealth(context.RoundedValue);

            PlayFx(target, FxType.Buff);
            PlayAudio(context, AudioActionType.Buff);
        }
    }
}
