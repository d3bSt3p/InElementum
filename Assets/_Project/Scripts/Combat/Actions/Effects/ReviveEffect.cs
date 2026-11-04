using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    /// <summary>Revives an incapacitated target with the value as its health. Standing targets are unaffected.</summary>
    public class ReviveEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.Revive;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null) return;
            if (!context.Target.Stats.Revive(context.RoundedValue)) return;

            PlayFx(context.Target, FxType.Heal);
            PlayAudio(context, AudioActionType.Heal);
        }
    }
}
