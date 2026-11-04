using InElementum.Combat.Interfaces;
using InElementum.Combat.Model;
using InElementum.Core.Managers;
using InElementum.Data.Enums;

namespace InElementum.Combat.Actions
{
    public abstract class EffectBase
    {
        public abstract EffectType EffectType { get; }
        public abstract void Apply(EffectContext context);

        /// <summary>
        /// Runs the context through <see cref="DamageCalculator"/>, damages the target, shows the number and the
        /// element reaction, and records both on the context. Returns the damage dealt.
        /// </summary>
        protected static int DealDamage(EffectContext context)
        {
            var target = context.Target.Stats;
            var attacker = context.Source?.Stats;

            var request = DamageRequest.Create(context.Value, attacker?.FinalStats ?? default, target.FinalStats);
            request.Multiplier = context.Multiplier;
            request.FlatReduction = attacker?.AttackReduction ?? 0;
            request.ElementMultiplier = target.GetElementMultiplier(context.Element);
            request.IsCrit = context.IsCrit;

            var result = DamageCalculator.Calculate(request, target.Tuning);
            var dealt = result.Reaction == ElementReaction.Immune ? 0 : target.Damage(result.Amount, attacker);

            context.DamageDealt += dealt;
            context.Reaction = result.Reaction;

            SpawnText(context.Target, FormatDamage(dealt, result));
            return dealt;
        }

        public static string FormatDamage(int dealt, DamageResult result)
        {
            var text = result.IsCrit ? dealt + "!" : dealt.ToString();
            switch (result.Reaction)
            {
                case ElementReaction.Weak: return text + " Weak!";
                case ElementReaction.Resist: return text + " Resist";
                case ElementReaction.Immune: return "Immune";
                default: return text;
            }
        }

        protected static void PlayFx(ICombatant on, FxType fx)
        {
            var root = on?.ViewRoot;
            if (root != null && FxManager.Instance != null)
                FxManager.Instance.PlayFx(root, fx);
        }

        protected static void SpawnText(ICombatant on, string text)
        {
            var root = on?.TextRoot;
            if (root != null && FxManager.Instance != null)
                FxManager.Instance.SpawnFloatingText(root, text);
        }

        /// <summary>
        /// Plays the action's authored sound, or <paramref name="fallback"/>, the effect's own, when there is no
        /// action or it is left on Attack, the default. So a heal ability nobody gave a sound still sounds like a heal.
        /// </summary>
        protected static void PlayAudio(EffectContext context, AudioActionType fallback)
        {
            var authored = context.Action != null ? context.Action.AudioType : AudioActionType.Attack;
            AudioManager.Play(authored != AudioActionType.Attack ? authored : fallback);
        }
    }
}
