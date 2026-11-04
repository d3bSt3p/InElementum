using System.Collections.Generic;
using InElementum.Data.Actions;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.SkillTrees;
using InElementum.Data.Weapons;
using UnityEngine;

namespace InElementum.Data.Characters
{
    [CreateAssetMenu(fileName = "Party Member", menuName = "In Elementum/Characters/Party Member", order = 0)]
    public class PartyMemberData : CharacterDataBase
    {
        [Header("Combat")]
        [SerializeField] private AttackData basicAttack;
        [SerializeField] private List<AbilityData> abilities = new List<AbilityData>();
        [SerializeField] private SurgeData surge;
        [SerializeField] [Min(0)] private int maxAp = 10;
        [Tooltip("Pictos this character joins the party wearing. A picto has one wearer, so give each character different ones.")]
        [SerializeField] private List<PictoData> defaultPictos = new List<PictoData>();
        [Tooltip("Weapons this character brings when they join the party. They start holding the first one.")]
        [SerializeField] private List<WeaponData> startingWeapons = new List<WeaponData>();
        [Tooltip("The character's skill tree: the source of their Surge and of abilities that do not start unlocked.")]
        [SerializeField] private SkillTreeData skillTree;

        [Header("Exploration")]
        [SerializeField] private ExplorationAbility explorationAbility;
        [Tooltip("Played when the player switches to this character while exploring. Empty uses the Character Switch Feedback's default.")]
        [SerializeField] private AudioClip switchSound;

        public AttackData BasicAttack { get => basicAttack; internal set => basicAttack = value; }
        public List<AbilityData> Abilities { get => abilities; internal set => abilities = value; }
        public SurgeData Surge { get => surge; internal set => surge = value; }
        public int MaxAp { get => maxAp; internal set => maxAp = value; }
        public List<PictoData> DefaultPictos { get => defaultPictos; internal set => defaultPictos = value; }
        public List<WeaponData> StartingWeapons { get => startingWeapons; internal set => startingWeapons = value; }
        public SkillTreeData SkillTree { get => skillTree; internal set => skillTree = value; }
        public ExplorationAbility ExplorationAbility { get => explorationAbility; internal set => explorationAbility = value; }
        public AudioClip SwitchSound { get => switchSound; internal set => switchSound = value; }
    }
}
