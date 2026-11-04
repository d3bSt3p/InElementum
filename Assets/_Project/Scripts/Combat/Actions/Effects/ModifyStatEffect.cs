using System;
using InElementum.Data.Enums;

namespace InElementum.Combat.Actions.Effects
{
    /// <summary>A positive value buffs the authored stat by that many points. A negative value debuffs it.</summary>
    public class ModifyStatEffect : EffectBase
    {
        public override EffectType EffectType => EffectType.ModifyStat;

        public override void Apply(EffectContext context)
        {
            var target = context.TargetOrSource;
            if (target == null) return;

            var isBuff = context.RoundedValue >= 0;
            target.Stats.ApplyStatus(
                isBuff ? StatusEffectType.StatBuff : StatusEffectType.StatDebuff,
                Math.Abs(context.RoundedValue),
                context.Stat);

            PlayFx(target, isBuff ? FxType.Buff : FxType.Debuff);
            PlayAudio(context, isBuff ? AudioActionType.Buff : AudioActionType.Debuff);
        }
    }
}
