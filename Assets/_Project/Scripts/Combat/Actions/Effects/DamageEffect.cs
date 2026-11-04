using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    public class DamageEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.Damage;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null) return;

            DealDamage(context);

            PlayFx(context.Target, FxType.Attack);
            PlayAudio(context, AudioActionType.Attack);
        }
    }
}
