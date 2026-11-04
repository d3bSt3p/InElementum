using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    public class FillSurgeGaugeEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.FillSurgeGauge;

        public override void Apply(EffectContext context)
        {
            if (context.SurgeGauge == null) return;

            context.SurgeGauge.Add(context.RoundedValue);

            PlayAudio(context, AudioActionType.Buff);
        }
    }
}
