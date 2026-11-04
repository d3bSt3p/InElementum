using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Core.Extensions;
using UnityEngine;

namespace InElementum.Data.Containers
{
    [CreateAssetMenu(fileName = "Enemy Encounter Data", menuName = "In Elementum/Containers/EnemyEncounterData", order = 4)]
    public class EnemyEncounterData : ScriptableObject
    {
        [Header("Settings")]
        [SerializeField] private bool encounterRandomlyAtStage;
        [SerializeField] private List<EnemyEncounterStage> enemyEncounterList;

        public bool EncounterRandomlyAtStage => encounterRandomlyAtStage;
        public List<EnemyEncounterStage> EnemyEncounterList => enemyEncounterList;

        public EnemyEncounter GetEnemyEncounter(int stageId = 0, int encounterId = 0)
        {
            var selectedStage = EnemyEncounterList.FirstOrDefault(x => x.StageId == stageId);

            if (selectedStage == null)
            {
                Debug.LogError($"[EnemyEncounterData] No stage found with StageId: {stageId}");
                return null;
            }

            if (selectedStage.EnemyEncounterList == null || selectedStage.EnemyEncounterList.Count == 0)
            {
                Debug.LogError($"[EnemyEncounterData] Stage {stageId} has no encounters assigned.");
                return null;
            }

            if (EncounterRandomlyAtStage)
                return selectedStage.EnemyEncounterList.RandomItem();

            var safeIndex = encounterId % selectedStage.EnemyEncounterList.Count;
            return selectedStage.EnemyEncounterList[safeIndex] ?? selectedStage.EnemyEncounterList.RandomItem();
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Encounter Data")]
#endif
        public bool ValidateData()
        {
            if (EnemyEncounterList == null || EnemyEncounterList.Count == 0)
            {
                Debug.LogError($"[EnemyEncounterData] '{name}': No stages configured.", this);
                return false;
            }

            var isValid = true;

            foreach (var stage in EnemyEncounterList)
            {
                if (stage.EnemyEncounterList == null || stage.EnemyEncounterList.Count == 0)
                {
                    Debug.LogError($"[EnemyEncounterData] '{name}': Stage '{stage.Name}' (StageId: {stage.StageId}) has no encounters.", this);
                    isValid = false;
                    continue;
                }

                foreach (var encounter in stage.EnemyEncounterList)
                {
                    if (encounter.EnemyList == null || encounter.EnemyList.Count == 0)
                    {
                        Debug.LogError($"[EnemyEncounterData] '{name}': Stage '{stage.Name}' has an encounter with no enemies assigned.", this);
                        isValid = false;
                        continue;
                    }

                    foreach (var enemy in encounter.EnemyList)
                    {
                        if (enemy == null)
                        {
                            Debug.LogError($"[EnemyEncounterData] '{name}': Stage '{stage.Name}' has a null EnemyCharacterData entry.", this);
                            isValid = false;
                        }
                        else if (enemy.EnemyPrefab == null)
                        {
                            Debug.LogError($"[EnemyEncounterData] '{name}': Enemy '{enemy.name}' in stage '{stage.Name}' has no prefab assigned.", this);
                            isValid = false;
                        }
                    }
                }
            }

            if (isValid)
                Debug.Log($"[EnemyEncounterData] '{name}': All stages validated successfully.");

            return isValid;
        }
    }

    [Serializable]
    public class EnemyEncounterStage
    {
        [SerializeField] private string name;
        [SerializeField] private int stageId;
        [SerializeField] private List<EnemyEncounter> enemyEncounterList;

        public string Name => name;
        public int StageId => stageId;
        public List<EnemyEncounter> EnemyEncounterList => enemyEncounterList;
    }

    [Serializable]
    public class EnemyEncounter : EncounterBase
    {
        [SerializeField] private List<EnemyCharacterData> enemyList;
        public List<EnemyCharacterData> EnemyList => enemyList;
    }

    [Serializable]
    public abstract class EncounterBase
    {
        [SerializeField] private BackgroundTypes targetBackgroundType;
        public BackgroundTypes TargetBackgroundType => targetBackgroundType;
    }
}