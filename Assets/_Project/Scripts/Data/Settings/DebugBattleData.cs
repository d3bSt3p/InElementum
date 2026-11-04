using System;
using System.Collections.Generic;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using UnityEngine;
using UnityEngine.Serialization;

namespace InElementum.Data.Settings
{
    /// <summary>
    /// A battle setup for launching the combat scene directly, without exploration. Also stands in for the party
    /// service until Phase 3 adds one.
    /// </summary>
    [CreateAssetMenu(fileName = "Debug Battle", menuName = "In Elementum/Settings/Debug Battle", order = 2)]
    public class DebugBattleData : ScriptableObject
    {
        [Header("Party")]
        [Tooltip("Up to CombatTuning's max active party size. Fewer is fine.")]
        [SerializeField] private List<PartyMemberData> activeParty = new List<PartyMemberData>();
        [Tooltip("In the party but not taken into battle. They can be swapped in from the party menu.")]
        [FormerlySerializedAs("backupParty")]
        [SerializeField] private List<PartyMemberData> benchedParty = new List<PartyMemberData>();
        [SerializeField] private List<ItemStack> startingItems = new List<ItemStack>();
        [Tooltip("Every Fusion Surge the party could learn. Each shows for its two owners once unlocked.")]
        [SerializeField] private List<SurgeData> fusionSurges = new List<SurgeData>();

        [Header("Encounter")]
        [Tooltip("Used only when no encounter is pending from exploration.")]
        [SerializeField] private List<EnemyCharacterData> enemies = new List<EnemyCharacterData>();
        [SerializeField] private BattleSide initiative = BattleSide.Party;

        [Header("Debug")]
        [Tooltip("Unlocks every ability and Surge, so Surges can be tested before the skill tree exists.")]
        [SerializeField] private bool unlockEverything = true;
        [Tooltip("AP each party member starts with.")]
        [SerializeField] [Min(0)] private int startingAp;
        [Tooltip("Reads every relationship pair as Relationship Level below, so Fusion Surges and relationship abilities can be tested.")]
        [SerializeField] private bool overrideRelationships;
        [SerializeField] [Range(0, 5)] private int relationshipLevel = 5;

        public List<PartyMemberData> ActiveParty { get => activeParty; internal set => activeParty = value; }
        public List<PartyMemberData> BenchedParty { get => benchedParty; internal set => benchedParty = value; }
        public List<ItemStack> StartingItems { get => startingItems; internal set => startingItems = value; }
        public List<EnemyCharacterData> Enemies { get => enemies; internal set => enemies = value; }
        public BattleSide Initiative { get => initiative; internal set => initiative = value; }
        public bool UnlockEverything { get => unlockEverything; internal set => unlockEverything = value; }
        public int StartingAp { get => startingAp; internal set => startingAp = value; }
        public List<SurgeData> FusionSurges { get => fusionSurges; internal set => fusionSurges = value; }
        public bool OverrideRelationships { get => overrideRelationships; internal set => overrideRelationships = value; }
        public int RelationshipLevel { get => relationshipLevel; internal set => relationshipLevel = value; }
    }

    [Serializable]
    public class ItemStack
    {
        [SerializeField] private ItemData item;
        [SerializeField] [Min(1)] private int count = 1;

        public ItemStack() { }

        public ItemStack(ItemData item, int count)
        {
            this.item = item;
            this.count = count;
        }

        public ItemData Item => item;
        public int Count => count;
    }
}
