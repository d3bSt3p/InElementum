using System.Collections.Generic;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Progression;

namespace InElementum.Combat.Model
{
    /// <summary>
    /// Which abilities and Surges the party has unlocked. Actions with <see cref="UnlockSource.Default"/> start unlocked.
    /// Relationship actions unlock once their pair reaches the level in <see cref="Relationships"/>: a Fusion Surge's
    /// owner and partner, or an ability's user and its <see cref="AbilityData.RelationshipPartner"/>. Everything else
    /// must be unlocked, which the skill tree will do (Phase 4.2). Plain C#; persistence comes later.
    /// </summary>
    public class ActionUnlocks
    {
        private readonly HashSet<CombatActionData> _unlocked = new HashSet<CombatActionData>();
        private readonly List<SurgeData> _fusionSurges = new List<SurgeData>();

        /// <summary>Debug override: treats every ability and Surge as unlocked, so Surges can be tested before the skill tree.</summary>
        public bool UnlockEverything { get; set; }

        /// <summary>Relationship levels for relationship-sourced actions. Null locks them all.</summary>
        public RelationshipService Relationships { get; set; }

        public IEnumerable<CombatActionData> Unlocked => _unlocked;

        /// <summary>
        /// Every Fusion Surge in the game, unlocked or not. A Fusion Surge belongs to two members, so it is found here
        /// rather than on either member's data.
        /// </summary>
        public IReadOnlyList<SurgeData> FusionSurges => _fusionSurges;

        public void Unlock(CombatActionData action)
        {
            if (action != null) _unlocked.Add(action);
        }

        public void Lock(CombatActionData action) => _unlocked.Remove(action);

        /// <summary>Makes a Fusion Surge known, so members can find it once it is unlocked.</summary>
        public void AddFusionSurge(SurgeData surge)
        {
            if (surge != null && surge.IsFusion && !_fusionSurges.Contains(surge)) _fusionSurges.Add(surge);
        }

        /// <param name="user">Who would use the action. Relationship abilities need it to find their pair.</param>
        public bool IsUnlocked(CombatActionData action, PartyMemberData user = null)
        {
            if (action == null) return false;
            if (UnlockEverything || _unlocked.Contains(action)) return true;

            switch (action)
            {
                case AbilityData ability:
                    if (ability.UnlockSource == UnlockSource.Default) return true;
                    return MeetsRelationship(ability.UnlockSource, user, ability.RelationshipPartner);
                case SurgeData surge:
                    if (surge.StartsUnlocked) return true;
                    return surge.IsFusion && MeetsRelationship(surge.UnlockSource, surge.Owner, surge.FusionPartner);
                default:
                    return true;
            }
        }

        private bool MeetsRelationship(UnlockSource source, PartyMemberData a, PartyMemberData b)
        {
            var level = RelationshipService.RequiredLevel(source);
            return level > 0 && Relationships != null && new PartyPair(a, b).IsValid && Relationships.HasLevel(a, b, level);
        }
    }
}
