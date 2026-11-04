using System;
using System.Collections.Generic;
using System.Text;
using InElementum.Combat.Characters;
using InElementum.Core.Extensions;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Actions
{
    /// <summary>Anything a combatant can do on its turn. Replaces NueDeck's CardData.</summary>
    public abstract class CombatActionData : ScriptableObject
    {
        [Header("Profile")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;

        [Header("Description")]
        [SerializeField] private List<ActionDescriptionData> description = new List<ActionDescriptionData>();
        [SerializeField] private List<SpecialKeywords> specialKeywords = new List<SpecialKeywords>();

        [Header("Combat")]
        [SerializeField] private Element element;
        [SerializeField] private ActionTargetType targetType;
        [Tooltip("How long the action takes to cross the timeline's Cast section. Shown to the player as cast speed.")]
        [SerializeField] [Min(0f)] private float castTime = 1f;
        [SerializeField] private List<ActionEffectData> effects = new List<ActionEffectData>();

        [Header("Presentation")]
        [SerializeField] private AudioActionType audioType;
        [SerializeField] private FxType fxType;
        [SerializeField] private string animationKey;

        public string Id { get => id; internal set => id = value; }
        public string DisplayName { get => displayName; internal set => displayName = value; }
        public Sprite Icon { get => icon; internal set => icon = value; }
        public List<ActionDescriptionData> Description { get => description; internal set => description = value; }
        public List<SpecialKeywords> SpecialKeywords { get => specialKeywords; internal set => specialKeywords = value; }
        public Element Element { get => element; internal set => element = value; }
        public ActionTargetType TargetType { get => targetType; internal set => targetType = value; }
        public float CastTime { get => castTime; internal set => castTime = value; }
        public List<ActionEffectData> Effects { get => effects; internal set => effects = value; }
        public AudioActionType AudioType { get => audioType; internal set => audioType = value; }
        public FxType FxType { get => fxType; internal set => fxType = value; }
        public string AnimationKey { get => animationKey; internal set => animationKey = value; }

        /// <param name="viewer">Whose statuses scale the shown values. Null shows the authored values.</param>
        public string BuildDescription(CharacterStats viewer = null)
        {
            var text = new StringBuilder();
            foreach (var part in description)
                text.Append(part.GetText(effects, viewer));

            return text.ToString();
        }
    }

    [Serializable]
    public class ActionEffectData
    {
        [SerializeField] private EffectType effectType;
        [SerializeField] private ActionTargetType targetType;
        [SerializeField] private float value;
        [SerializeField] private float delay;
        [Tooltip("ApplyStatus only.")]
        [SerializeField] private StatusEffectType status;
        [Tooltip("ModifyStat, and ApplyStatus with StatBuff or StatDebuff.")]
        [SerializeField] private StatType stat;

        public ActionEffectData() { }

        public ActionEffectData(EffectType effectType, ActionTargetType targetType, float value,
            StatusEffectType status = StatusEffectType.None, StatType stat = default)
        {
            this.effectType = effectType;
            this.targetType = targetType;
            this.value = value;
            this.status = status;
            this.stat = stat;
        }

        public EffectType EffectType => effectType;
        public ActionTargetType TargetType => targetType;
        public float Value => value;
        public float Delay => delay;
        public StatusEffectType Status => status;
        public StatType Stat => stat;
    }

    /// <summary>One piece of an action's description: fixed text, or an effect's value scaled by a status.</summary>
    [Serializable]
    public class ActionDescriptionData
    {
        [Header("Text")]
        [SerializeField] private string descriptionText;
        [SerializeField] private bool enableOverrideColor;
        [SerializeField] private Color overrideColor = Color.black;

        [Header("Modifier")]
        [SerializeField] private bool useModifier;
        [SerializeField] private int modifiedEffectIndex;
        [SerializeField] private StatusEffectType modifierStatus;
        [SerializeField] private StatType modifierStat;
        [SerializeField] private bool usePrefixOnModifiedValue;
        [SerializeField] private string modifiedValuePrefix = "*";
        [SerializeField] private bool overrideColorOnValueScaled;

        public ActionDescriptionData() { }

        public ActionDescriptionData(string text)
        {
            descriptionText = text;
        }

        public static ActionDescriptionData ForEffectValue(int effectIndex,
            StatusEffectType scaleWith = StatusEffectType.None, StatType stat = default)
        {
            return new ActionDescriptionData
            {
                useModifier = true,
                modifiedEffectIndex = effectIndex,
                modifierStatus = scaleWith,
                modifierStat = stat
            };
        }

        public string GetText(IReadOnlyList<ActionEffectData> effects, CharacterStats viewer)
        {
            if (!useModifier)
                return enableOverrideColor && !string.IsNullOrEmpty(descriptionText)
                    ? ColorExtentions.ColorString(descriptionText, overrideColor)
                    : descriptionText;

            if (effects == null || effects.Count == 0) return string.Empty;

            var index = Mathf.Clamp(modifiedEffectIndex, 0, effects.Count - 1);
            var modifier = GetModifier(viewer);
            var value = effects[index].Value + modifier;

            var text = modifier != 0 && usePrefixOnModifiedValue
                ? modifiedValuePrefix + value
                : value.ToString();

            var colored = enableOverrideColor && (!overrideColorOnValueScaled || modifier != 0);
            return colored ? ColorExtentions.ColorString(text, overrideColor) : text;
        }

        private int GetModifier(CharacterStats viewer)
        {
            if (viewer == null) return 0;

            var amount = viewer.GetStatusValue(modifierStatus, modifierStat);
            var reduces = modifierStatus == StatusEffectType.StatDebuff || modifierStatus == StatusEffectType.Weakness;
            return reduces ? -amount : amount;
        }
    }
}
