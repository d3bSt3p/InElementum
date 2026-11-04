using System.Collections.Generic;
using InElementum.Combat.Characters;
using UnityEngine;

namespace InElementum.Data.Settings
{
    [CreateAssetMenu(fileName = "Gameplay Data", menuName = "In Elementum/Settings/GameplayData", order = 0)]
    public class GameplayData : ScriptableObject
    {
        [Header("Gameplay Settings")]
        [SerializeField] private List<AllyBase> initalAllyList;

        [Header("Economy")]
        [SerializeField] private int startingGold = 100;

        #region Encapsulation
        public List<AllyBase> InitalAllyList => initalAllyList;
        public int StartingGold => startingGold;
        #endregion
    }
}
