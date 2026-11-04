using InElementum.Data.Characters;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Actions
{
    [CreateAssetMenu(fileName = "Surge", menuName = "In Elementum/Actions/Surge", order = 3)]
    public class SurgeData : CombatActionData
    {
        [Header("Surge")]
        [SerializeField] private PartyMemberData owner;
        [Tooltip("Set only for a Fusion Surge. The partner does not need to be in the active party.")]
        [SerializeField] private PartyMemberData fusionPartner;
        [Tooltip("Fraction of the gauge spent, where 1 is the full gauge.")]
        [SerializeField] [Range(0f, 1f)] private float gaugeCost = 1f;
        [Tooltip("Surges come from the skill tree, or relationships for Fusion Surges. Never Default outside debugging.")]
        [SerializeField] private UnlockSource unlockSource = UnlockSource.SkillTree;

        public PartyMemberData Owner { get => owner; internal set => owner = value; }
        public PartyMemberData FusionPartner { get => fusionPartner; internal set => fusionPartner = value; }
        public float GaugeCost { get => gaugeCost; internal set => gaugeCost = value; }
        public UnlockSource UnlockSource { get => unlockSource; internal set => unlockSource = value; }

        public bool IsFusion => fusionPartner != null;
        public bool StartsUnlocked => unlockSource == UnlockSource.Default;
        public bool IsOwnedBy(PartyMemberData member) => member != null && (member == owner || member == fusionPartner);
    }
}
