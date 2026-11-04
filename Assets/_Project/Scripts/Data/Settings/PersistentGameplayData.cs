using System;
using System.Collections.Generic;
using InElementum.Combat.Characters;
using UnityEngine;

namespace InElementum.Data.Settings
{
    [Serializable]
    public class PersistentGameplayData
    {
        private readonly GameplayData _gameplayData;

        [SerializeField] private int currentGold;
        [SerializeField] private List<AllyBase> allyList;
        [SerializeField] private int currentStageId;
        [SerializeField] private int currentEncounterId;
        [SerializeField] private bool isFinalEncounter;
        [SerializeField] private List<AllyHealthData> allyHealthDataDataList;

        public PersistentGameplayData(GameplayData gameplayData)
        {
            _gameplayData = gameplayData;

            InitData();
        }

        public void SetAllyHealthData(string id, int newCurrentHealth, int newMaxHealth)
        {
            var data = allyHealthDataDataList.Find(x => x.CharacterId == id);
            var newData = new AllyHealthData
            {
                CharacterId = id,
                CurrentHealth = newCurrentHealth,
                MaxHealth = newMaxHealth
            };

            if (data != null)
                allyHealthDataDataList.Remove(data);

            allyHealthDataDataList.Add(newData);
        }

        private void InitData()
        {
            AllyList = new List<AllyBase>(_gameplayData.InitalAllyList);
            CurrentEncounterId = 0;
            CurrentStageId = 0;
            CurrentGold = _gameplayData.StartingGold;
            IsFinalEncounter = false;
            allyHealthDataDataList = new List<AllyHealthData>();
        }

        #region Encapsulation
        public List<AllyBase> AllyList
        {
            get => allyList;
            set => allyList = value;
        }

        public int CurrentStageId
        {
            get => currentStageId;
            set => currentStageId = value;
        }

        public int CurrentEncounterId
        {
            get => currentEncounterId;
            set => currentEncounterId = value;
        }

        public bool IsFinalEncounter
        {
            get => isFinalEncounter;
            set => isFinalEncounter = value;
        }

        public List<AllyHealthData> AllyHealthDataList
        {
            get => allyHealthDataDataList;
            set => allyHealthDataDataList = value;
        }

        public int CurrentGold
        {
            get => currentGold;
            set => currentGold = value;
        }
        #endregion
    }
}
