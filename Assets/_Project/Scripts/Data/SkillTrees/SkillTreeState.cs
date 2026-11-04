using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Actions;
using InElementum.Data.Pictos;
using InElementum.Data.Progression;
using InElementum.Data.Stats;

namespace InElementum.Data.SkillTrees
{
    /// <summary>Why a skill node could not be unlocked. <see cref="None"/> means it can be.</summary>
    public enum SkillUnlockError
    {
        None,
        Missing,
        AlreadyUnlocked,
        /// <summary>None of its prerequisites is unlocked yet.</summary>
        NotReachable,
        NotEnoughSkillPoints
    }

    /// <summary>How a node looks in the menu.</summary>
    public enum SkillNodeState
    {
        Unlocked,
        /// <summary>Reachable from an unlocked node; buyable once there are points.</summary>
        Available,
        Locked
    }

    /// <summary>
    /// One character's progress through their <see cref="SkillTreeData"/>: which nodes are unlocked, and the skill points
    /// left from <see cref="CharacterProgress.SkillPoints"/>. Plain C#; persistence comes with the save system.
    /// </summary>
    public sealed class SkillTreeState
    {
        private readonly HashSet<string> _unlocked = new HashSet<string>();

        public SkillTreeState(SkillTreeData tree, CharacterProgress progress)
        {
            Tree = tree != null ? tree : throw new ArgumentNullException(nameof(tree));
            Progress = progress;
        }

        public SkillTreeData Tree { get; }
        /// <summary>Supplies the skill point budget. Null means no points.</summary>
        public CharacterProgress Progress { get; }

        public IEnumerable<string> UnlockedIds => _unlocked;
        public IEnumerable<SkillNodeData> UnlockedNodes => Tree.Nodes.Where(node => node != null && _unlocked.Contains(node.Id));

        public int SkillPoints => Progress?.SkillPoints ?? 0;
        public int SkillPointsSpent => UnlockedNodes.Sum(node => node.Cost);
        public int SkillPointsLeft => SkillPoints - SkillPointsSpent;

        /// <summary>Permanent stat modifiers from unlocked stat boosts.</summary>
        public IEnumerable<StatModifier> Modifiers => UnlockedNodes.SelectMany(node => node.GrantedModifiers);

        /// <summary>Passives from unlocked passive nodes.</summary>
        public IEnumerable<PassiveData> Passives =>
            UnlockedNodes.Select(node => node.GrantedPassive).Where(passive => passive != null);

        /// <summary>Abilities and Surges unlocked through the tree.</summary>
        public IEnumerable<CombatActionData> UnlockedActions =>
            UnlockedNodes.Select(node => node.Action).Where(action => action != null);

        public bool IsUnlocked(SkillNodeData node) => node != null && _unlocked.Contains(node.Id);

        /// <summary>A root, or linked to an unlocked node.</summary>
        public bool IsReachable(SkillNodeData node) =>
            node != null && (node.IsRoot || node.Prerequisites.Any(id => _unlocked.Contains(id)));

        public SkillNodeState GetState(SkillNodeData node)
        {
            if (IsUnlocked(node)) return SkillNodeState.Unlocked;
            return IsReachable(node) ? SkillNodeState.Available : SkillNodeState.Locked;
        }

        public SkillUnlockError CanUnlock(SkillNodeData node)
        {
            if (node == null || !Tree.Nodes.Contains(node)) return SkillUnlockError.Missing;
            if (IsUnlocked(node)) return SkillUnlockError.AlreadyUnlocked;
            if (!IsReachable(node)) return SkillUnlockError.NotReachable;
            if (node.Cost > SkillPointsLeft) return SkillUnlockError.NotEnoughSkillPoints;
            return SkillUnlockError.None;
        }

        public SkillUnlockError Unlock(SkillNodeData node)
        {
            var error = CanUnlock(node);
            if (error == SkillUnlockError.None) _unlocked.Add(node.Id);
            return error;
        }
    }
}
