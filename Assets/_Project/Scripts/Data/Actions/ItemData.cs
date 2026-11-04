using UnityEngine;

namespace InElementum.Data.Actions
{
    [CreateAssetMenu(fileName = "Item", menuName = "In Elementum/Actions/Item", order = 2)]
    public class ItemData : CombatActionData
    {
        [Header("Item")]
        [SerializeField] [Min(1)] private int stackMax = 99;
        [SerializeField] private bool usableOutsideCombat;

        public int StackMax { get => stackMax; internal set => stackMax = value; }
        public bool UsableOutsideCombat { get => usableOutsideCombat; internal set => usableOutsideCombat = value; }
    }
}
