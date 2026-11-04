using System.Collections.Generic;
using InElementum.Core.Extensions;
using UnityEngine;

namespace InElementum.Data.Containers
{
    [CreateAssetMenu(fileName = "Enemy Encounter Pool", menuName = "In Elementum/Containers/EnemyEncounterPool", order = 5)]
    public class EnemyEncounterPoolData : ScriptableObject
    {
        [Header("Settings")]
        [SerializeField] private List<EnemyEncounterData> encounterPool;

        public List<EnemyEncounterData> EncounterPool => encounterPool;

        public EnemyEncounterData GetRandomEncounterData()
        {
            return encounterPool.RandomItem();
        }
    }
}