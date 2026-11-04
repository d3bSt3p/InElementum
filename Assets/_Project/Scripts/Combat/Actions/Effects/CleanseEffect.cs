using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    public class CleanseEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.Cleanse;

        public override void Apply(EffectContext context)
        {
            var target = context.TargetOrSource;
            if (target == null) return;

            target.Stats.ClearNegativeStatus();

            PlayFx(target, FxType.Buff);
            PlayAudio(context, AudioActionType.Buff);
        }
    }
}
