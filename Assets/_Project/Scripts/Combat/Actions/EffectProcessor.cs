using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Combat.Actions
{
    /// <summary>Finds every <see cref="EffectBase"/> by reflection, so a new effect only needs its class and enum value.</summary>
    public static class EffectProcessor
    {
        private static readonly Dictionary<EffectType, EffectBase> Effects = new Dictionary<EffectType, EffectBase>();

        public static bool IsInitialized { get; private set; }

        public static void Initialize()
        {
            Effects.Clear();

            var effectTypes = Assembly.GetAssembly(typeof(EffectBase)).GetTypes()
                .Where(t => typeof(EffectBase).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var effectType in effectTypes)
            {
                var effect = (EffectBase)Activator.CreateInstance(effectType);
                Effects.Add(effect.EffectType, effect);
            }

            IsInitialized = true;
        }

        public static EffectBase GetEffect(EffectType type)
        {
            if (!Effects.TryGetValue(type, out var effect))
            {
                Debug.LogError($"[EffectProcessor] No effect registered for EffectType '{type}'. Has the class been created?");
                return null;
            }

            return effect;
        }
    }
}
