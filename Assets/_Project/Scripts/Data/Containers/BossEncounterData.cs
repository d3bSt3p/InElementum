using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Core.Extensions;
using UnityEngine;

namespace InElementum.Data.Containers
{
    [CreateAssetMenu(fileName = "Boss Encounter Data", menuName = "In Elementum/Containers/BossEncounterData", order = 5)]
    public class BossEncounterData : ScriptableObject
    {
        [Header("Settings")]
        [SerializeField] private List<BossEncounterStage> bossEncounterList;

        public List<BossEncounterStage> BossEncounterList => bossEncounterList;

        public EnemyEncounter GetBossEncounter(int stageId = 0)
        {
            var selectedStage = BossEncounterList.First(x => x.StageId == stageId);
            return selectedStage.BossEncounterList.RandomItem();
        }
    }

    [Serializable]
    public class BossEncounterStage
    {
        [SerializeField] private string name;
        [SerializeField] private int stageId;
        [SerializeField] private List<EnemyEncounter> bossEncounterList;

        public string Name => name;
        public int StageId => stageId;
        public List<EnemyEncounter> BossEncounterList => bossEncounterList;
    }
}  