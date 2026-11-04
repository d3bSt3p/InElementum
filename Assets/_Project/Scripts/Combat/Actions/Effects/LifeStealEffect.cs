using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    /// <summary>Damages the target and heals the source by the amount dealt.</summary>
    public class LifeStealEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.LifeSteal;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null || context.Source == null) return;

            var dealt = DealDamage(context);
            context.Source.Stats.Heal(dealt);

            PlayFx(context.Target, FxType.Attack);
            PlayFx(context.Source, FxType.Heal);
            PlayAudio(context, AudioActionType.Attack);
        }
    }
}
