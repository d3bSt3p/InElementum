using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Actions;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Stats;
using UnityEngine;

namespace InElementum.Data.SkillTrees
{
    /// <summary>What unlocking a skill node grants.</summary>
    public enum SkillNodeKind
    {
        /// <summary>Permanent stat modifiers.</summary>
        StatBoost,
        /// <summary>Unlocks an ability for combat.</summary>
        Ability,
        /// <summary>Unlocks the character's Surge (or any Surge).</summary>
        Surge,
        /// <summary>A passive that is always in effect, like an equipped Lumina that costs no points.</summary>
        Passive
    }

    /// <summary>
    /// One node of a <see cref="SkillTreeData"/>. Sits on a grid cell, costs skill points, and unlocks once any one of
    /// its <see cref="Prerequisites"/> is unlocked (Child of Light-style paths). A node without prerequisites is a root
    /// and can be bought from the start.
    /// </summary>
    [Serializable]
    public class SkillNodeData
    {
        [SerializeField] private string id;
        [Tooltip("Shown in the menu. Empty uses the unlocked ability, Surge, or passive's name, or the stat boost.")]
        [SerializeField] private string displayName;
        [Tooltip("Shown in the menu. Empty describes what the node grants.")]
        [SerializeField] [TextArea] private string description;
        [SerializeField] private Sprite icon;
        [Tooltip("Grid cell: x to the right, y downward.")]
        [SerializeField] private Vector2Int position;
        [Tooltip("Major nodes draw as squares with a star, like Child of Light's big upgrades.")]
        [SerializeField] private bool isMajor;
        [SerializeField] [Min(0)] private int cost = 1;

        [Header("Unlocks")]
        [SerializeField] private SkillNodeKind kind;
        [SerializeField] private List<StatModifier> modifiers = new List<StatModifier>();
        [SerializeField] private AbilityData ability;
        [SerializeField] private SurgeData surge;
        [SerializeField] private PassiveData passive;

        [Tooltip("Ids of linked nodes. Any one of them unlocked makes this node available.")]
        [SerializeField] private List<string> prerequisites = new List<string>();

        public string Id { get => id; internal set => id = value; }
        public string DisplayNameOverride { get => displayName; internal set => displayName = value; }
        public string DescriptionOverride { get => description; internal set => description = value; }
        public Sprite Icon { get => icon; internal set => icon = value; }
        public Vector2Int Position { get => position; internal set => position = value; }
        public bool IsMajor { get => isMajor; internal set => isMajor = value; }
        public int Cost { get => cost; internal set => cost = value; }
        public SkillNodeKind Kind { get => kind; internal set => kind = value; }
        public List<StatModifier> Modifiers { get => modifiers; internal set => modifiers = value; }
        public AbilityData Ability { get => ability; internal set => ability = value; }
        public SurgeData Surge { get => surge; internal set => surge = value; }
        public PassiveData Passive { get => passive; internal set => passive = value; }
        public List<string> Prerequisites { get => prerequisites; internal set => prerequisites = value; }

        public bool IsRoot => prerequisites == null || prerequisites.Count == 0;

        /// <summary>The ability or Surge this node unlocks for combat, or null.</summary>
        public CombatActionData Action =>
            kind == SkillNodeKind.Ability ? ability : kind == SkillNodeKind.Surge ? (CombatActionData)surge : null;

        /// <summary>Stat modifiers this node grants. Empty unless it is a stat boost.</summary>
        public IEnumerable<StatModifier> GrantedModifiers =>
            kind == SkillNodeKind.StatBoost && modifiers != null ? modifiers : Enumerable.Empty<StatModifier>();

        public PassiveData GrantedPassive => kind == SkillNodeKind.Passive ? passive : null;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
                switch (kind)
                {
                    case SkillNodeKind.Ability: return ability != null ? ability.DisplayName : "Ability";
                    case SkillNodeKind.Surge: return surge != null ? surge.DisplayName : "Surge";
                    case SkillNodeKind.Passive: return passive != null ? passive.DisplayName : "Passive";
                    default:
                        var first = modifiers != null && modifiers.Count > 0 ? modifiers[0] : default;
                        return modifiers != null && modifiers.Count > 0 ? $"{first.Stat} Upgrade" : "Upgrade";
                }
            }
        }

        public string Description
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(description)) return description;
                switch (kind)
                {
                    case SkillNodeKind.Ability:
                        return ability != null ? $"Learn the ability {ability.DisplayName}. {ability.BuildDescription()}" : "Learn an ability.";
                    case SkillNodeKind.Surge:
                        return surge != null ? $"Unlock the Surge {surge.DisplayName}. {surge.BuildDescription()}" : "Unlock a Surge.";
                    case SkillNodeKind.Passive:
                        return passive != null ? passive.Description : "Gain a passive effect.";
                    default:
                        return modifiers == null || modifiers.Count == 0
                            ? "No effect."
                            : string.Join(" ", modifiers.Select(DescribeModifier));
                }
            }
        }

        public static string DescribeModifier(StatModifier modifier)
        {
            var amount = modifier.Kind == ModifierKind.Percent
                ? $"{Mathf.RoundToInt(Mathf.Abs(modifier.Value) * 100f)}%"
                : Mathf.RoundToInt(Mathf.Abs(modifier.Value)).ToString();
            var verb = modifier.Value >= 0f ? "Increase" : "Decrease";
            return $"{verb} {modifier.Stat} by {amount} permanently.";
        }

        /// <summary>A short random id, unique enough within one tree.</summary>
        public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 8);
    }
}
