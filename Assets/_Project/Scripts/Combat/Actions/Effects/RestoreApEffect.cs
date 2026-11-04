using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    public class RestoreApEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.RestoreAP;

        public override void Apply(EffectContext context)
        {
            var target = context.TargetOrSource;
            if (target == null) return;

            target.Stats.GainAp(context.RoundedValue);

            PlayFx(target, FxType.Buff);
            PlayAudio(context, AudioActionType.Drink);
        }
    }
}
