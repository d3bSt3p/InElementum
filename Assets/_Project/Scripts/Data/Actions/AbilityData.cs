using System;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Actions
{
    [CreateAssetMenu(fileName = "Ability", menuName = "In Elementum/Actions/Ability", order = 1)]
    public class AbilityData : CombatActionData
    {
        [Header("Ability")]
        [SerializeField] [Min(0)] private int apCost = 1;
        [SerializeField] private UnlockSource unlockSource;
        [Tooltip("RelationshipLevel3 and RelationshipLevel5 only: the partner whose relationship with the ability's user unlocks it.")]
        [SerializeField] private PartyMemberData relationshipPartner;

        [Header("Quick Time Event")]
        [SerializeField] private bool hasQte;
        [SerializeField] private QteData qte = new QteData();

        public int ApCost { get => apCost; internal set => apCost = value; }
        public UnlockSource UnlockSource { get => unlockSource; internal set => unlockSource = value; }
        public PartyMemberData RelationshipPartner { get => relationshipPartner; internal set => relationshipPartner = value; }
        public bool HasQte { get => hasQte; internal set => hasQte = value; }
        public QteData Qte { get => qte; internal set => qte = value; }
    }

    [Serializable]
    public class QteData
    {
        [SerializeField] private QteType type;
        [Tooltip("Seconds the player has to succeed.")]
        [SerializeField] [Min(0f)] private float timingWindow = 0.5f;
        [Tooltip("Multiplies the ability's effect when the QTE succeeds.")]
        [SerializeField] [Min(0f)] private float successMultiplier = 1.5f;

        public QteData() { }

        public QteData(QteType type, float timingWindow, float successMultiplier)
        {
            this.type = type;
            this.timingWindow = timingWindow;
            this.successMultiplier = successMultiplier;
        }

        public QteType Type => type;
        public float TimingWindow => timingWindow;
        public float SuccessMultiplier => successMultiplier;
    }
}
