using InElementum.Combat.Actions;
using InElementum.Data.Containers;
using InElementum.Data.Settings;
using UnityEngine;

namespace InElementum.Core.Managers
{
    [DefaultExecutionOrder(-10)]
    public class GameManager : MonoBehaviour
    {
        public GameManager() { }
        public static GameManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private GameplayData gameplayData;
        [SerializeField] private CombatTuning combatTuning;
        [SerializeField] private EnemyEncounterData enemyEncounterData;
        [SerializeField] private BossEncounterData bossEncounterData;
        [SerializeField] private SceneData sceneData;

        #region Cache
        public SceneData SceneData => sceneData;
        public EnemyEncounterData EnemyEncounterData { get => enemyEncounterData; set => enemyEncounterData = value; }
        public BossEncounterData BossEncounterData { get => bossEncounterData; set => bossEncounterData = value; }
        public GameplayData GameplayData => gameplayData;
        public CombatTuning CombatTuning => combatTuning;
        public PersistentGameplayData PersistentGameplayData { get; private set; }

        /// <summary>
        /// When set, CombatManager will use this encounter instead of resolving one from encounter data.
        /// Cleared after use.
        /// </summary>
        public EnemyEncounter OverrideEncounter { get; set; }
        #endregion

        #region Setup
        private void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }

            transform.parent = null;
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EffectProcessor.Initialize();
            InitGameplayData();
        }
        #endregion

        #region Public Methods
        public void InitGameplayData()
        {
            PersistentGameplayData = new PersistentGameplayData(gameplayData);
        }

        public void OnExitApp() { }
        #endregion
    }
}
