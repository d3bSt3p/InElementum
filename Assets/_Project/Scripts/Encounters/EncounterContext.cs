using System.Collections.Generic;
using InElementum.Combat.Model;
using InElementum.Data.Characters;
using InElementum.Data.Containers;
using UnityEngine;

namespace InElementum.Encounters
{
    /// <summary>
    /// What a battle is about to be: who is fought, who struck first, and, when exploration started it, where to go
    /// back to. Exploration sets <see cref="Pending"/> before loading combat; the combat scene consumes it on start.
    /// </summary>
    public sealed class EncounterContext
    {
        public EncounterContext(IEnumerable<EnemyCharacterData> enemies, BattleSide initiative)
        {
            Enemies = new List<EnemyCharacterData>(enemies);
            Initiative = initiative;
        }

        public EncounterContext(EncounterData encounter, BattleSide initiative)
            : this(encounter != null ? encounter.Enemies : new List<EnemyCharacterData>(), initiative)
        {
            Encounter = encounter;
        }

        public IReadOnlyList<EnemyCharacterData> Enemies { get; }
        public BattleSide Initiative { get; }

        /// <summary>The authored encounter, for its backdrop. Null for a debug battle.</summary>
        public EncounterData Encounter { get; }

        /// <summary>Who fights, and where the outcome is written. Null makes combat use its debug party.</summary>
        public PartyService Party { get; set; }

        /// <summary>What to tell when the battle is over. Null when combat was launched directly.</summary>
        public IEncounterHost Host { get; set; }

        /// <summary>The scene to go back to. Combat loads on top of it, so it is still loaded.</summary>
        public string ReturnScene { get; set; }
        /// <summary>Where the player stood when the fight started.</summary>
        public Vector3 ReturnPosition { get; set; }
        /// <summary>The overworld enemy fought, removed from the room on victory.</summary>
        public string OverworldEnemyId { get; set; }

        /// <summary>The encounter the next combat scene should run. Null when combat is launched directly.</summary>
        public static EncounterContext Pending { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Pending = null;

        /// <summary>Returns and clears <see cref="Pending"/>.</summary>
        public static EncounterContext Consume()
        {
            var context = Pending;
            Pending = null;
            return context;
        }
    }

    /// <summary>Whoever started a battle and takes the player back afterwards: the exploration side of combat.</summary>
    public interface IEncounterHost
    {
        /// <summary>The player dismissed the victory screen.</summary>
        void ContinueAfterVictory(EncounterContext context, BattleResult result);

        /// <summary>The player chose Restart Battle after a defeat.</summary>
        void RestartBattle(EncounterContext context);

        /// <summary>The player chose Respawn at Last Checkpoint after a defeat.</summary>
        void RespawnAtCheckpoint(EncounterContext context);
    }
}
