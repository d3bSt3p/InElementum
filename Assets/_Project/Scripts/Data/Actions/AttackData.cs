using UnityEngine;

namespace InElementum.Data.Actions
{
    /// <summary>
    /// A basic attack. Each hit deals <see cref="PowerPerHit"/> damage and grants AP from CombatTuning.
    /// The inherited effects are extras applied on each hit, such as a status.
    /// </summary>
    [CreateAssetMenu(fileName = "Attack", menuName = "In Elementum/Actions/Attack", order = 0)]
    public class AttackData : CombatActionData
    {
        [Header("Attack")]
        [SerializeField] [Min(1)] private int baseHitCount = 1;
        [SerializeField] [Min(0f)] private float powerPerHit = 1f;

        public int BaseHitCount { get => baseHitCount; internal set => baseHitCount = value; }
        public float PowerPerHit { get => powerPerHit; internal set => powerPerHit = value; }
    }
}
