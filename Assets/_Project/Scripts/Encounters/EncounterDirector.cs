using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Model;
using InElementum.Core.Managers;
using InElementum.Core.UI;
using InElementum.Data.Enums;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using InElementum.Exploration;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace InElementum.Encounters
{
    /// <summary>
    /// Moves between a room and combat. When an overworld enemy is struck or touches the player, it fades out,
    /// hides the room, and loads the combat scene on top with an <see cref="EncounterContext"/>. Afterwards it
    /// unloads combat and brings the room back: without the beaten enemy after a victory, or at the checkpoint
    /// after a defeat. Owns the room's <see cref="PartyService"/>, so the party carries from fight to fight.
    /// Lives outside the exploration root, so it keeps running while the room is hidden.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class EncounterDirector : MonoBehaviour, IEncounterHost
    {
        [Header("Scenes")]
        [Tooltip("Loaded additively for each fight. Must be in the build settings.")]
        [SerializeField] private string combatScene = "TestCombat";
        [Tooltip("Hidden during combat: the room, its camera, its light, and everything in it.")]
        [SerializeField] private GameObject explorationRoot;

        [Header("Party")]
        [Tooltip("The starting party and items, until a save system exists. Its encounter settings are ignored here.")]
        [SerializeField] private DebugBattleData startingParty;
        [SerializeField] private CombatTuning tuning;
        [Tooltip("Optional. Default levelling numbers are used when empty.")]
        [SerializeField] private ProgressionTuning progressionTuning;

        [Header("Room")]
        [SerializeField] private PlayerController2D player;
        [Tooltip("Where a defeated party respawns until another checkpoint is reached.")]
        [SerializeField] private Checkpoint startingCheckpoint;
        [Tooltip("Optional. Cuts the follow camera to the player after a respawn.")]
        [SerializeField] private ExplorationCamera explorationCamera;

        [Header("Transition")]
        [SerializeField] [Min(0f)] private float fadeDuration = 0.35f;
        [SerializeField] private Color fadeColor = Color.black;
        [Tooltip("Seconds after returning to the room when touching an enemy does nothing.")]
        [SerializeField] [Min(0f)] private float contactGraceTime = 1.5f;

        private readonly List<OverworldEnemy> _enemies = new List<OverworldEnemy>();
        private Checkpoint _checkpoint;
        private float _fade;
        private float _graceUntil;
        private VisualElement _fadeOverlay;

        public static EncounterDirector Instance { get; private set; }

        /// <summary>The party fighting from this room.</summary>
        public PartyService Party { get; private set; }
        public RoomSession Session { get; } = new RoomSession();
        /// <summary>Fading, loading, or fighting: the room is not in play.</summary>
        public bool Busy { get; private set; }
        /// <summary>The last finished battle's outcome, or null.</summary>
        public BattleResult LastResult { get; private set; }

        private void Awake()
        {
            Instance = this;

            if (tuning == null || startingParty == null)
                Debug.LogError("[EncounterDirector] Assign Combat Tuning and a Starting Party.", this);

            if (progressionTuning == null) progressionTuning = ScriptableObject.CreateInstance<ProgressionTuning>();
            if (tuning != null && startingParty != null)
                Party = PartyService.FromDebugBattle(startingParty, tuning, progressionTuning);

            if (player == null) player = FindAnyObjectByType<PlayerController2D>();
            _checkpoint = startingCheckpoint;
            EnsureExplorationUi();
            CreateFadeOverlay();

            // Enemies and the player pass through each other; contact is checked by overlap, not by collision.
            int playerLayer = LayerMask.NameToLayer("Player"), enemyLayer = LayerMask.NameToLayer("Enemy");
            if (playerLayer >= 0 && enemyLayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer);
                Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer);
            }
        }

        private void OnEnable()
        {
            OverworldEnemy.EncounterTriggered += OnEncounterTriggered;
            Checkpoint.Reached += OnCheckpointReached;
        }

        private void OnDisable()
        {
            OverworldEnemy.EncounterTriggered -= OnEncounterTriggered;
            Checkpoint.Reached -= OnCheckpointReached;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var root = explorationRoot != null ? explorationRoot.transform : null;
            _enemies.AddRange(root != null
                ? root.GetComponentsInChildren<OverworldEnemy>(true)
                : FindObjectsByType<OverworldEnemy>(FindObjectsInactive.Include));

            foreach (var enemy in _enemies.Where(enemy => Session.IsDefeated(enemy.EnemyId)))
                enemy.gameObject.SetActive(false);

            if (_checkpoint != null) _checkpoint.SetLit(true);
            PlayRoomAudio();
        }

        /// <summary>The room's music and ambience, at the start and on coming back from a fight.</summary>
        private static void PlayRoomAudio()
        {
            var audio = AudioManager.Ensure();
            audio.SetAmbienceSuspended(false);
            audio.PlayAmbience(AudioActionType.AmbienceExploration);
            audio.PlayMusic(AudioActionType.MusicExploration);
        }

        private void Update()
        {
            OverworldEnemy.ContactSuppressed = Busy || Time.time < _graceUntil;
        }

        #region Starting a fight
        private void OnEncounterTriggered(OverworldEnemy enemy, BattleSide initiative)
        {
            if (Busy || Party == null) return;
            if (enemy.Encounter == null)
            {
                Debug.LogWarning($"[EncounterDirector] {enemy.name} has no Encounter assigned.", enemy);
                return;
            }

            var context = new EncounterContext(enemy.Encounter, initiative)
            {
                Party = Party,
                Host = this,
                ReturnScene = gameObject.scene.name,
                ReturnPosition = player != null ? player.transform.position : Vector3.zero,
                OverworldEnemyId = enemy.EnemyId
            };

            StartCoroutine(EnterCombat(context));
        }

        private IEnumerator EnterCombat(EncounterContext context)
        {
            Busy = true;
            if (player != null) player.SetControlsEnabled(false);
            AudioManager.Play(AudioActionType.EncounterStart);

            yield return Fade(1f);
            if (explorationRoot != null) explorationRoot.SetActive(false);
            yield return LoadCombat(context);
            yield return Fade(0f);
        }

        private IEnumerator LoadCombat(EncounterContext context)
        {
            LastResult = null;
            EncounterContext.Pending = context;
            var load = SceneManager.LoadSceneAsync(combatScene, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"[EncounterDirector] Could not load '{combatScene}'. Is it in the build settings?", this);
                EncounterContext.Pending = null;
                yield return ReturnToRoom(null, false);
                yield break;
            }

            yield return load;
            var scene = SceneManager.GetSceneByName(combatScene);
            if (scene.IsValid()) SceneManager.SetActiveScene(scene);
        }

        private IEnumerator UnloadCombat()
        {
            var scene = SceneManager.GetSceneByName(combatScene);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            SceneManager.SetActiveScene(gameObject.scene);
        }
        #endregion

        #region IEncounterHost
        public void ContinueAfterVictory(EncounterContext context, BattleResult result)
        {
            LastResult = result;
            Session.MarkDefeated(context.OverworldEnemyId);
            StartCoroutine(LeaveCombat(context, false));
        }

        public void RestartBattle(EncounterContext context) => StartCoroutine(RestartRoutine(context));

        public void RespawnAtCheckpoint(EncounterContext context) => StartCoroutine(LeaveCombat(context, true));

        private IEnumerator RestartRoutine(EncounterContext context)
        {
            yield return Fade(1f);
            yield return UnloadCombat();
            yield return LoadCombat(context);
            yield return Fade(0f);
        }

        private IEnumerator LeaveCombat(EncounterContext context, bool respawn)
        {
            yield return Fade(1f);
            yield return UnloadCombat();
            yield return ReturnToRoom(context, respawn);
        }

        /// <summary>Brings the room back, after a fight or a failed load, and hands the controls back.</summary>
        private IEnumerator ReturnToRoom(EncounterContext context, bool respawn)
        {
            foreach (var enemy in _enemies)
            {
                if (Session.IsDefeated(enemy.EnemyId)) enemy.gameObject.SetActive(false);
                else if (respawn) enemy.ResetToStart();
            }

            if (explorationRoot != null) explorationRoot.SetActive(true);
            PlayRoomAudio();

            if (respawn)
            {
                Party.RestoreAll();
                if (player != null && _checkpoint != null) player.Warp(_checkpoint.SpawnPosition);
                if (explorationCamera != null) explorationCamera.Snap();
            }

            _graceUntil = Time.time + contactGraceTime;
            yield return Fade(0f);
            if (player != null) player.SetControlsEnabled(true);
            Busy = false;
        }
        #endregion

        /// <summary>
        /// Adds the exploration HUD here and the character switcher to the player when the scene lacks them, so rooms
        /// built before they existed still get them.
        /// </summary>
        private void EnsureExplorationUi()
        {
            if (GetComponent<ExplorationHud>() == null && FindAnyObjectByType<ExplorationHud>() == null) gameObject.AddComponent<ExplorationHud>();
            if (player != null && player.GetComponent<CharacterSwitcher>() == null) player.gameObject.AddComponent<CharacterSwitcher>();
        }

        private void OnCheckpointReached(Checkpoint checkpoint)
        {
            if (Busy) return;
            if (_checkpoint != null && _checkpoint != checkpoint) _checkpoint.SetLit(false);
            _checkpoint = checkpoint;
            checkpoint.SetLit(true);
            AudioManager.Play(AudioActionType.Checkpoint);
            Party?.RestoreAll();
            checkpoint.ShowRestored();
        }

        #region Fade
        private IEnumerator Fade(float target)
        {
            if (fadeDuration <= 0f)
            {
                _fade = target;
                yield break;
            }

            while (!Mathf.Approximately(_fade, target))
            {
                _fade = Mathf.MoveTowards(_fade, target, Time.unscaledDeltaTime / fadeDuration);
                yield return null;
            }
        }

        /// <summary>The fade's full-screen overlay, drawn over every other screen, combat's included.</summary>
        private void CreateFadeOverlay()
        {
            var document = HudPanel.CreateDocument(transform, "Fade", HudPanel.Layer.Fade);
            _fadeOverlay = Hud.Box("ie-fade");
            document.rootVisualElement.Add(_fadeOverlay);
            ApplyFade();
        }

        private void LateUpdate() => ApplyFade();

        private void ApplyFade()
        {
            if (_fadeOverlay == null) return;
            _fadeOverlay.style.backgroundColor = new Color(fadeColor.r, fadeColor.g, fadeColor.b, _fade);
            _fadeOverlay.Show(_fade > 0f);
        }
        #endregion
    }
}
