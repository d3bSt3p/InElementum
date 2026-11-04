using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Actions;
using InElementum.Combat.Characters;
using InElementum.Combat.Model;
using InElementum.Core.Managers;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using InElementum.Encounters;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace InElementum.Combat.Managers
{
    /// <summary>A defense prompt in progress: the player presses Dodge or Parry around <see cref="ImpactTime"/>.</summary>
    public sealed class DefensePrompt
    {
        public Combatant Attacker { get; internal set; }
        /// <summary>Which inputs work. The forbidden one counts as a miss.</summary>
        public DefenseRule Rule { get; internal set; }
        /// <summary>A guarding enemy's answer to a basic-attack hit, outside any chain.</summary>
        public bool IsGuardCounter { get; internal set; }
        /// <summary>
        /// This hit's place in the attack, from 1. Dodging or parrying all <see cref="HitCount"/> hits cancels the
        /// attack; parrying them all earns a counter.
        /// </summary>
        public int HitNumber { get; internal set; }
        public int HitCount { get; internal set; }
        public float ShownAt { get; internal set; }
        public float ImpactTime { get; internal set; }
        public DefenseInput Input { get; internal set; }
    }

    /// <summary>An ability QTE in progress: the player presses QTE as the marker crosses the target zone.</summary>
    public sealed class AbilityQtePrompt
    {
        public string ActionName { get; internal set; }
        public float StartTime { get; internal set; }
        public float Duration { get; internal set; }
        /// <summary>Seconds either side of the midpoint that succeed.</summary>
        public float HalfWindow { get; internal set; }
        public QteResult Result { get; internal set; }
        public float TargetTime => StartTime + Duration * 0.5f;
    }

    /// <summary>How hard a popup hits: plain words, a damage number, a weakness hit, or a crit.</summary>
    public enum PopupStyle
    {
        Info,
        Damage,
        Weak,
        Crit
    }

    public readonly struct FeedbackPopup
    {
        public FeedbackPopup(int id, Vector3 origin, string text, Color color, PopupStyle style, float drift, float time)
        {
            Id = id;
            Origin = origin;
            Text = text;
            Color = color;
            Style = style;
            Drift = drift;
            Time = time;
        }

        /// <summary>Unique and increasing, so a higher id is a newer popup.</summary>
        public int Id { get; }

        /// <summary>World point the popup starts from, fixed when it spawns so it does not slide with the character.</summary>
        public Vector3 Origin { get; }
        public string Text { get; }
        public Color Color { get; }
        public PopupStyle Style { get; }
        /// <summary>Sideways drift, -1 to 1, so popups on the same target fan out.</summary>
        public float Drift { get; }
        public float Time { get; }
    }

    /// <summary>A shock ring bursting at a world point, such as where a parry meets the blow.</summary>
    public readonly struct ImpactRing
    {
        public ImpactRing(Vector3 origin, Color color, float time)
        {
            Origin = origin;
            Color = color;
            Time = time;
        }

        /// <summary>World point the ring bursts from, fixed when it spawns.</summary>
        public Vector3 Origin { get; }
        public Color Color { get; }
        public float Time { get; }
    }

    /// <summary>
    /// Drives a <see cref="BattleModel"/> in the scene: builds the battle, ticks the timeline, plays each action's
    /// steps with their defense and ability QTEs, and ends the battle. All rules live in the model; this only times
    /// and presents them. Launch it directly with a <see cref="DebugBattleData"/>, or from exploration through
    /// <see cref="EncounterContext.Pending"/>.
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        /// <summary>Raised when a battle ends. Exploration (Phase 3) listens to return to the room.</summary>
        public static event Action<BattleResult> CombatEnded;
        /// <summary>Raised when the player picks Respawn at Last Checkpoint after a defeat.</summary>
        public static event Action RespawnRequested;

        /// <summary>The last encounter fought, so Restart Battle can run it again.</summary>
        private static EncounterContext _lastContext;

        /// <summary>
        /// The debug party, used when no encounter brings its own. Kept across scene reloads so victories add up to
        /// levels and learned Luminas. Only victories change it, so Restart Battle still starts from the pre-battle state.
        /// </summary>
        private static PartyService _debugParty;
        private static DebugBattleData _debugPartySource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _debugParty = null;
            _debugPartySource = null;
            _lastContext = null;
        }

        [Header("Setup")]
        [SerializeField] private CombatTuning tuning;
        [Tooltip("Optional. Default levelling numbers are used when empty.")]
        [SerializeField] private ProgressionTuning progressionTuning;
        [SerializeField] private DebugBattleData debugBattle;
        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("Optional. Found on, or added to, the main camera when empty.")]
        [SerializeField] private CombatCamera battleCamera;

        [Header("Layout")]
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Vector3 partyOrigin = new Vector3(-5f, -2f, 0f);
        [SerializeField] private Vector3 enemyOrigin = new Vector3(2.5f, -2f, 0f);
        [SerializeField] private Vector3 slotSpacing = new Vector3(1.6f, 0.35f, 0f);

        [Header("Pacing (seconds)")]
        [Tooltip("Multiplies how fast the Wait/Cast timeline runs.")]
        [SerializeField] [Min(0.1f)] private float timelineSpeed = 1f;
        [SerializeField] [Min(0f)] private float moveDuration = 0.2f;
        [SerializeField] [Min(0f)] private float stepPause = 0.3f;
        [Tooltip("Time from a defense prompt appearing to the hit landing.")]
        [SerializeField] [Min(0.1f)] private float defenseLeadTime = 0.8f;
        [SerializeField] [Min(0.2f)] private float abilityQteDuration = 1.2f;
        [SerializeField] [Min(0.1f)] private float popupLifetime = 1.2f;
        [Tooltip("How long the camera holds on the actor before an action plays out.")]
        [SerializeField] [Min(0f)] private float windUpDuration = 0.45f;
        [SerializeField] [Min(0.1f)] private float bannerLifetime = 1.8f;

        [Header("Dodge")]
        [Tooltip("How far a dodging character darts away from the attacker.")]
        [SerializeField] [Min(0f)] private float dodgeDistance = 0.6f;
        [SerializeField] [Min(0.01f)] private float dodgeDashDuration = 0.08f;
        [Tooltip("How long the character stays out after the hit lands before sliding home.")]
        [SerializeField] [Min(0f)] private float dodgeHoldAfterImpact = 0.12f;
        [SerializeField] [Min(0.01f)] private float dodgeReturnDuration = 0.15f;
        [SerializeField] private Color dodgeAfterimageColor = new Color(0.45f, 0.6f, 1f, 0.6f);

        private readonly Dictionary<Combatant, CharacterBase> _views = new Dictionary<Combatant, CharacterBase>();
        private readonly List<FeedbackPopup> _popups = new List<FeedbackPopup>();
        private int _nextPopupId;
        private readonly List<ImpactRing> _impactRings = new List<ImpactRing>();
        private readonly List<string> _log = new List<string>();
        private string _banner;
        private float _bannerTime = float.NegativeInfinity;

        #region Public state
        public BattleModel Model { get; private set; }
        public CombatInput Input { get; private set; }
        public CombatTuning Tuning => tuning;
        /// <summary>The party's levels, pictos, and Luminas.</summary>
        public PartyState Party => PartyService?.State;
        /// <summary>Who fights and where the outcome goes: the encounter's party, else the Debug Battle's.</summary>
        public PartyService PartyService { get; private set; }
        /// <summary>True when exploration started this battle, so the victory screen leads back to it.</summary>
        public bool ReturnsToExploration => _lastContext?.Host != null;
        /// <summary>Levels, new pictos, and learned Luminas from the last victory. Null until one.</summary>
        public VictoryReport LastVictory { get; private set; }
        public bool IsPlayingAction { get; private set; }
        public DefensePrompt ActiveDefense { get; private set; }
        public AbilityQtePrompt ActiveQte { get; private set; }
        public IReadOnlyList<FeedbackPopup> Popups => _popups;
        public IReadOnlyList<ImpactRing> ImpactRings => _impactRings;
        public IReadOnlyList<string> Log => _log;
        public float PopupLifetime => popupLifetime;
        /// <summary>How long an impact ring takes to burst out and fade, matching a damage popup's ring.</summary>
        public const float ImpactRingLifetime = 0.25f;
        public CombatCamera BattleCamera => battleCamera;
        /// <summary>The action playing out now, or null between actions.</summary>
        public ActionExecution CurrentExecution { get; private set; }
        /// <summary>The headline for the current action, such as "Shade Wisp uses Gloom Lance.", while it is fresh.</summary>
        public string Banner => Time.time - _bannerTime < bannerLifetime ? _banner : null;

        public CharacterBase GetView(Combatant combatant) => combatant != null && _views.TryGetValue(combatant, out var view) ? view : null;
        #endregion

        #region Setup
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Input = new CombatInput(inputActions);
            if (characterRoot == null) characterRoot = transform;
        }

        private void Start()
        {
            if (tuning == null || debugBattle == null)
            {
                Debug.LogError("[CombatManager] Assign Combat Tuning and a Debug Battle.", this);
                enabled = false;
                return;
            }

            BuildBattle(EncounterContext.Consume() ?? new EncounterContext(debugBattle.Enemies, debugBattle.Initiative));
            SetUpCamera();

            // The room's ambience waits under the battle music; exploration brings it back on the way out.
            var audio = AudioManager.Ensure();
            audio.SetAmbienceSuspended(true);
            audio.PlayMusic(AudioActionType.MusicBattle);

            Model.Start();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Input?.Dispose();
            Instance = null;
        }

        private void BuildBattle(EncounterContext context)
        {
            _lastContext = context;

            if (context.Party == null && (_debugParty == null || _debugPartySource != debugBattle))
            {
                if (progressionTuning == null) progressionTuning = ScriptableObject.CreateInstance<ProgressionTuning>();
                _debugParty = PartyService.FromDebugBattle(debugBattle, tuning, progressionTuning);
                _debugPartySource = debugBattle;
            }

            PartyService = context.Party ?? _debugParty;
            LastVictory = null;

            // The battle spends from a copy, written back to the party only on victory. A debug battle keeps its
            // levels and pictos between fights but starts each one at full health with the Debug Battle's items.
            Inventory inventory;
            if (context.Party == null)
            {
                PartyService.RestoreAll();
                inventory = new Inventory();
                foreach (var stack in debugBattle.StartingItems.Where(stack => stack.Item != null))
                    inventory.Add(stack.Item, stack.Count);
            }
            else
            {
                inventory = PartyService.Items.Clone();
            }
            // Skill trees unlock abilities and Surges. Unlock Everything and relationship levels stay debug overrides
            // until the Medium exists.
            Party.Relationships.DebugLevel = debugBattle.OverrideRelationships ? debugBattle.RelationshipLevel : (int?)null;

            var unlocks = new ActionUnlocks { UnlockEverything = debugBattle.UnlockEverything, Relationships = Party.Relationships };
            foreach (var surge in debugBattle.FusionSurges)
                unlocks.AddFusionSurge(surge);
            foreach (var action in Party.SkillTreeActions)
                unlocks.Unlock(action);

            var party = PartyService.Active.Where(member => member != null)
                .Select((member, slot) => PartyService.CreateCombatant(member, slot)).ToList();
            var enemies = context.Enemies.Where(enemy => enemy != null)
                .Select((enemy, slot) => Combatant.ForEnemy(enemy, tuning, slot)).ToList();

            foreach (var member in party)
                member.Stats.GainAp(debugBattle.StartingAp);

            Model = new BattleModel(tuning, party, enemies, context.Initiative, inventory, unlocks, knowledge: Party.Elements);

            foreach (var member in party) Spawn(member);
            foreach (var enemy in enemies) Spawn(enemy);

            Model.ActionStarted += execution => StartCoroutine(PlayAction(execution));
            Model.IntentRevealed += OnEnemyIntent;
            Model.IntentRetargeted += OnIntentRetargeted;
            Model.Interrupted += OnInterrupted;
            Model.TurnSkipped += unit => AddLog($"{unit.Name} loses the turn.");
            Model.CommandChosen += (unit, command) => AddLog($"{unit.Name} readies {Describe(unit, command)}.");
            Model.StepResolved += OnStepResolved;
            Model.CounterEarned += OnCounterEarned;
            Model.SuperRiposteEarned += OnSuperRiposteEarned;
            Model.Broken += OnBroken;
            Model.BreakRecovered += unit => AddLog($"{unit.Name} recovers from the break.");
            Model.PassiveTriggered += OnPassiveTriggered;
            Model.BattleEnded += OnBattleEnded;
        }

        private void SetUpCamera()
        {
            if (battleCamera == null && Camera.main != null)
            {
                var main = Camera.main.gameObject;
                battleCamera = main.GetComponent<CombatCamera>();
                if (battleCamera == null) battleCamera = main.AddComponent<CombatCamera>();
            }

            if (battleCamera == null) return;

            var encounter = _lastContext?.Encounter;
            var cameraComponent = battleCamera.GetComponent<Camera>();
            if (encounter != null && cameraComponent != null) cameraComponent.backgroundColor = encounter.BackdropColor;

            RefreshOverview();
            battleCamera.ShowOverview();
            battleCamera.Snap();
        }

        private void RefreshOverview()
        {
            if (battleCamera != null) battleCamera.SetOverviewSubjects(_views.Values.Select(view => view.transform));
        }

        private void Spawn(Combatant combatant)
        {
            CharacterBase view;
            var prefab = combatant.Data != null ? combatant.Data.Prefab : null;
            if (prefab != null)
                view = Instantiate(prefab, characterRoot);
            else
                view = PlaceholderCharacter.Create(combatant.Name, combatant.Data != null ? combatant.Data.Element : Element.None,
                    combatant.Side == BattleSide.Enemy, characterRoot, combatant.Data != null ? combatant.Data.SpriteSet : null);

            var origin = combatant.Side == BattleSide.Party ? partyOrigin : enemyOrigin;
            var spacing = combatant.Side == BattleSide.Party ? new Vector3(-slotSpacing.x, slotSpacing.y, 0f) : slotSpacing;
            view.transform.position = origin + spacing * combatant.Slot;
            view.Bind(combatant);
            _views[combatant] = view;
        }
        #endregion

        #region Loop
        private void Update()
        {
            Input.Update();
            PrunePopups();

            if (Model != null && Model.State == BattleState.TimelineRunning)
                Model.Tick(Time.deltaTime * timelineSpeed);

            UpdateCamera();
        }

        /// <summary>
        /// The resting shot between actions: whoever is choosing framed with the enemies they face, the same shot an
        /// enemy's attack gets, otherwise the whole field. <see cref="PlayAction"/> takes over while an action plays,
        /// and the HUD narrows to the target while aiming.
        /// </summary>
        private void UpdateCamera()
        {
            if (battleCamera == null || Model == null || IsPlayingAction) return;

            var actor = Model.State == BattleState.AwaitingCommand ? Model.CurrentActor : null;
            var actorView = GetView(actor);
            if (actorView == null)
            {
                battleCamera.ShowOverview();
                return;
            }

            var frame = new List<Transform> { actorView.transform };
            frame.AddRange(LivingViews(Model.Enemies));
            battleCamera.Frame(frame);
        }

        private IEnumerable<Transform> LivingViews(IEnumerable<Combatant> units) =>
            units.Where(unit => unit.IsAlive).Select(GetView).Where(view => view != null).Select(view => view.transform);

        private void ShowBanner(string text)
        {
            _banner = text;
            _bannerTime = Time.time;
        }

        /// <summary>
        /// The views an action lands on, framed with the actor: its chosen target, or everyone on the side it aims
        /// at. Party and enemy actions get the same treatment.
        /// </summary>
        private List<Transform> ActionFrame(ActionExecution execution)
        {
            var frame = new List<Transform>();
            var actorView = GetView(execution.Actor);
            if (actorView != null) frame.Add(actorView.transform);

            var targetView = GetView(execution.ChosenTarget);
            if (targetView != null && targetView != actorView)
            {
                frame.Add(targetView.transform);
                return frame;
            }

            var aim = AimOf(execution);
            if (aim == ActionTargetType.Self) return frame;

            var party = execution.Actor.Side == BattleSide.Party;
            var aimsAtAllies = aim == ActionTargetType.Ally || aim == ActionTargetType.AllAllies || aim == ActionTargetType.RandomAlly;
            frame.AddRange(LivingViews(party == aimsAtAllies ? Model.Party : Model.Enemies).Where(view => view != frame.FirstOrDefault()));
            return frame;
        }

        /// <summary>Who an action aims at: its first effect that reaches the other side, else its declared target.</summary>
        private static ActionTargetType AimOf(ActionExecution execution)
        {
            if (execution.EnemyAbility != null)
            {
                var actions = execution.EnemyAbility.ActionList;
                if (actions == null || actions.Count == 0) return ActionTargetType.Enemy;
                var hostile = actions.FirstOrDefault(action => IsOpponentAim(action.TargetType));
                return hostile != null ? hostile.TargetType : actions[0].TargetType;
            }

            var command = execution.Command;
            if (command == null) return ActionTargetType.Enemy;
            if (command.Type == CommandType.Attack) return ActionTargetType.Enemy;
            return command.Action != null ? command.Action.TargetType : ActionTargetType.Enemy;
        }

        private static bool IsOpponentAim(ActionTargetType type) =>
            type == ActionTargetType.Enemy || type == ActionTargetType.AllEnemies || type == ActionTargetType.RandomEnemy;

        private IEnumerator PlayAction(ActionExecution execution)
        {
            IsPlayingAction = true;
            CurrentExecution = execution;
            var actorView = GetView(execution.Actor);
            var targetView = GetView(execution.ChosenTarget);
            AddLog($"{execution.Actor.Name} uses {DescribeExecution(execution)}.");
            ShowBanner($"{execution.Actor.Name} uses {DescribeExecution(execution)}.");

            // Hold on the actor for a beat, then pull back to take in the actor and whoever it hits.
            if (battleCamera != null && actorView != null)
            {
                battleCamera.Focus(actorView.transform, execution.Actor.Side == BattleSide.Party ? 0.4f : 0.6f, 1.1f);
                yield return new WaitForSeconds(windUpDuration);
            }

            if (battleCamera != null) battleCamera.Frame(ActionFrame(execution));
            if (actorView != null) yield return actorView.Advance(targetView, moveDuration);

            if (execution.CostFailed)
            {
                Popup(execution.Actor, "Failed", Color.gray);
                AddLog("...but it fails: its cost can no longer be paid.");
            }
            else if (execution.Qte != null)
            {
                yield return RunAbilityQte(execution);
            }

            var chainHits = execution.Steps.Count(step => step.NeedsDefense && !step.IsGuardCounter);
            var hitNumber = 0;
            while (!execution.IsComplete)
            {
                var step = execution.NextStep;
                var defense = DefenseOutcome.Hit;
                if (step.NeedsDefense && !step.IsGuardCounter) hitNumber++;
                if (step.NeedsDefense && Model.Party.Any(member => member.IsAlive) && (step.Attacker ?? execution.Actor).IsAlive)
                {
                    var prompt = new DefensePrompt
                    {
                        Attacker = step.Attacker ?? execution.Actor,
                        Rule = step.DefenseRule,
                        IsGuardCounter = step.IsGuardCounter,
                        HitNumber = step.IsGuardCounter ? 1 : hitNumber,
                        HitCount = step.IsGuardCounter ? 1 : chainHits
                    };
                    yield return RunDefense(prompt, DefenderViews(execution, step), step.ImpactDelay);
                    defense = QteJudge.JudgeDefense(prompt.Input,
                        prompt.Input == DefenseInput.None ? (float?)null : _defenseOffset, tuning);
                }

                // Swing first, so the hit lands on the strike's impact frame. A defended hit has already waited out its
                // telegraph, so it lands at once rather than giving the defender extra time.
                var striker = step.DealsDamage && !execution.IsCancelled ? GetView(step.Attacker ?? execution.Actor) : null;
                if (striker != null)
                {
                    var lead = striker.PlayStrike(StrikeAnimation(execution, step));
                    if (lead > 0f && !step.NeedsDefense) yield return new WaitForSeconds(lead);
                }

                execution.ExecuteNext(defense);
                yield return new WaitForSeconds(stepPause);
            }

            if (actorView != null) yield return actorView.Return(moveDuration);

            CurrentExecution = null;
            IsPlayingAction = false;
            Model.FinishAction();
        }

        private float _defenseOffset;

        /// <summary>The strike pose for one hit: a counter's lunge, or the move the actor chose.</summary>
        private static CharacterAnimation StrikeAnimation(ActionExecution execution, ExecutionStep step)
        {
            if (execution.IsCounter || (step.Attacker != null && step.Attacker != execution.Actor)) return CharacterAnimation.Counter;
            switch (execution.Command?.Type)
            {
                case CommandType.Ability: return CharacterAnimation.Ability;
                case CommandType.Surge: return CharacterAnimation.Surge;
                default: return CharacterAnimation.Attack;
            }
        }

        /// <summary>
        /// The party views a hit is coming for: its chosen target for a single-target hit, otherwise every living
        /// party member, since random and all-party hits are not resolved until they land.
        /// </summary>
        private List<CharacterBase> DefenderViews(ActionExecution execution, ExecutionStep step)
        {
            IEnumerable<Combatant> units = Model.Party;
            var chosen = execution.ChosenTarget;
            if (step.FixedTarget != null)
                units = new[] { step.FixedTarget };
            else if (Model.Taunter != null && (step.TargetType == ActionTargetType.Enemy || step.TargetType == ActionTargetType.RandomEnemy))
                units = new[] { Model.Taunter };
            else if (step.TargetType == ActionTargetType.Enemy && chosen != null && chosen.Side == BattleSide.Party)
                units = new[] { chosen };

            return units.Where(unit => unit.IsAlive).Select(GetView)
                .Where(view => view != null && view.isActiveAndEnabled).ToList();
        }

        /// <summary>Darts each defender away from the attacker the moment Dodge is pressed, telegraphing it.</summary>
        private void PlayDodges(DefensePrompt prompt, IReadOnlyList<CharacterBase> defenders)
        {
            var attackerView = GetView(prompt.Attacker);
            var holdUntil = Mathf.Max(prompt.ImpactTime, Time.time) + dodgeHoldAfterImpact;
            foreach (var view in defenders)
            {
                // Away from the attacker horizontally; the party stands on the left, so this is a leftward dart.
                var away = attackerView != null && attackerView.transform.position.x < view.transform.position.x ? 1f : -1f;
                view.PlayDodge(new Vector3(away * dodgeDistance, 0f, 0f), dodgeDashDuration, holdUntil,
                    dodgeReturnDuration, dodgeAfterimageColor);
            }
        }

        /// <param name="extraDelay">The hit's authored extra wind-up, for an uneven rhythm.</param>
        private IEnumerator RunDefense(DefensePrompt prompt, IReadOnlyList<CharacterBase> defenders, float extraDelay)
        {
            prompt.ShownAt = Time.time;
            prompt.ImpactTime = Time.time + defenseLeadTime + Mathf.Max(0f, extraDelay);
            ActiveDefense = prompt;

            // The first press counts, so mashing can't fish for the window.
            var close = prompt.ImpactTime + Mathf.Max(tuning.DodgeWindow, tuning.ParryWindow);
            while (Time.time < close)
            {
                if (prompt.Input == DefenseInput.None)
                {
                    if (Input.Parry) prompt.Input = DefenseInput.Parry;
                    else if (Input.Dodge) prompt.Input = DefenseInput.Dodge;

                    if (prompt.Input != DefenseInput.None)
                    {
                        _defenseOffset = Time.time - prompt.ImpactTime;
                        if (prompt.Input == DefenseInput.Dodge) PlayDodges(prompt, defenders);
                        break;
                    }
                }

                yield return null;
            }

            // Hold until the impact moment, so an early press still lands with the hit.
            while (Time.time < prompt.ImpactTime) yield return null;
            ActiveDefense = null;
        }

        private IEnumerator RunAbilityQte(ActionExecution execution)
        {
            var prompt = new AbilityQtePrompt
            {
                ActionName = execution.Command.Action.DisplayName,
                StartTime = Time.time,
                Duration = abilityQteDuration,
                HalfWindow = execution.Qte.TimingWindow * 0.5f,
                Result = QteResult.None
            };
            ActiveQte = prompt;

            float? offset = null;
            while (Time.time < prompt.StartTime + prompt.Duration)
            {
                if (Input.Qte)
                {
                    offset = Time.time - prompt.TargetTime;
                    break;
                }

                yield return null;
            }

            prompt.Result = QteJudge.Judge(offset, prompt.HalfWindow, tuning.PerfectWindowFraction);
            execution.SetQteResult(prompt.Result);
            AudioManager.Play(prompt.Result == QteResult.Fail ? AudioActionType.QteFail : AudioActionType.QteSuccess);
            Popup(execution.Actor, prompt.Result.ToString(), prompt.Result == QteResult.Fail ? Color.gray : Color.cyan);
            yield return new WaitForSeconds(0.25f);
            ActiveQte = null;
        }
        #endregion

        #region Model events
        private void OnEnemyIntent(EnemyIntent intent)
        {
            if (GetView(intent.Enemy) is EnemyBase enemyView) enemyView.ShowIntent(intent);
            AudioManager.Play(intent.Enemy.Enemy != null ? intent.Enemy.Enemy.Voice : null);
            var traits = DescribeIntentTraits(intent);
            var target = intent.Target != null ? $" at {intent.Target.Name}" : string.Empty;
            AddLog($"{intent.Enemy.Name} begins {intent.Ability.Name}{target}{(traits.Length > 0 ? $" ({traits})" : string.Empty)}.");
        }

        /// <summary>The revealed intent's tags: counter-cast, element, defense rule, Steadfast, Guard.</summary>
        public static string DescribeIntentTraits(EnemyIntent intent)
        {
            var traits = new List<string>();
            if (intent.IsCounterCast) traits.Add("counter-cast");
            if (intent.Element != Element.None) traits.Add(intent.Element.ToString());
            if (intent.DefenseRule == DefenseRule.DodgeOnly) traits.Add("dodge only");
            if (intent.DefenseRule == DefenseRule.ParryOnly) traits.Add("parry only");
            if (intent.IsSteadfast) traits.Add("Steadfast");
            if (intent.IsGuard) traits.Add("Guard");
            return string.Join(", ", traits);
        }

        private void OnIntentRetargeted(EnemyIntent intent)
        {
            if (GetView(intent.Enemy) is EnemyBase enemyView) enemyView.ShowIntent(intent);
            AddLog($"{intent.Enemy.Name} turns on {intent.Target.Name}!");
        }

        private void OnInterrupted(Combatant unit, InterruptReason reason)
        {
            switch (reason)
            {
                case InterruptReason.Fizzle:
                    Popup(unit, "Fizzled!", new Color(1f, 0.6f, 0.2f));
                    AddLog($"{unit.Name}'s cast fizzles!");
                    break;
                case InterruptReason.Defended:
                    Popup(unit, "Interrupted!", new Color(1f, 0.6f, 0.2f));
                    AddLog($"{unit.Name}'s attack is turned aside!");
                    break;
                default:
                    AddLog($"{unit.Name}'s cast is broken off.");
                    break;
            }

            if (GetView(unit) is EnemyBase enemyView) enemyView.ClearIntent();
        }

        private void OnCounterEarned(Combatant member, Combatant enemy)
        {
            Popup(member, "Counter!", Color.cyan);
            AddLog($"{member.Name} counters {enemy.Name}!");
        }

        private void OnSuperRiposteEarned(Combatant member)
        {
            Popup(member, "Super Riposte!", new Color(1f, 0.85f, 0.2f));
            AddLog($"{member.Name} counters at once with a Super Riposte!");
        }

        private void OnBroken(Combatant enemy)
        {
            Popup(enemy, "BREAK!", new Color(1f, 0.85f, 0.2f), PopupStyle.Crit);
            AudioManager.Play(AudioActionType.Break);
            AddLog($"{enemy.Name} is Broken!");
            if (battleCamera != null) battleCamera.Punch(1.6f);
            if (GetView(enemy) is EnemyBase enemyView) enemyView.ClearIntent();
        }

        private void OnStepResolved(StepReport report)
        {
            if (battleCamera != null && report.Step.DealsDamage && report.Hits.Any(hit => hit.DamageDealt > 0))
                battleCamera.Punch(report.Hits.Any(hit => hit.IsCrit || hit.IsCounter) ? 1.6f : 1f);

            foreach (var hit in report.Hits)
            {
                var target = hit.Target;
                if (report.Step.NeedsDefense && hit.Defense == DefenseOutcome.Dodged)
                {
                    Popup(target, hit.ApGained > 0 ? $"Dodge +{hit.ApGained} AP" : "Dodge", Color.cyan);
                    AudioManager.Play(AudioActionType.Dodge);
                    continue;
                }

                if (report.Step.NeedsDefense && hit.Defense == DefenseOutcome.Parried)
                {
                    if (TryContactPoint(report.Execution.Actor, target, out var contact)) ImpactAt(contact, Color.cyan);
                    Popup(target, "Parry!", Color.cyan);
                    AudioManager.Play(AudioActionType.Block);
                    var parrier = GetView(target);
                    if (parrier != null) parrier.PlayStrike(CharacterAnimation.Parry);
                    continue;
                }

                if (report.Step.DealsDamage)
                {
                    var text = EffectBase.FormatDamage(hit.DamageDealt, new DamageResult(hit.DamageDealt, hit.Reaction, hit.IsCrit));
                    if (hit.IsCounter) text = (report.Execution.IsSuperRiposte ? "Super Riposte " : "Counter ") + text;
                    Popup(target, text, DamageColor(hit), hit.IsCounter ? PopupStyle.Crit : DamageStyle(hit));

                    var view = GetView(target);
                    if (view != null)
                    {
                        StartCoroutine(view.Flash(new Color(1f, 0.5f, 0.5f), 0.12f));
                        if (hit.DamageDealt > 0) view.PlayHurt();
                    }
                }
                else
                {
                    Popup(target, report.Step.EffectType == EffectType.ApplyStatus ? report.Step.Status.ToString() : report.Step.EffectType.ToString(),
                        new Color(0.6f, 1f, 0.6f));
                }

                if (hit.Killed) PlayDownedSound(target);
                if (!target.IsAlive) AddLog($"{target.Name} is down.");
            }
        }

        /// <summary>An enemy's own cry when it goes down, else the shared enemy death sound.</summary>
        private static void PlayDownedSound(Combatant unit)
        {
            if (unit.Side != BattleSide.Enemy) return;
            var voice = unit.Enemy != null ? unit.Enemy.Voice : null;
            if (voice != null) AudioManager.Play(voice);
            else AudioManager.Play(AudioActionType.EnemyDeath);
        }

        private void OnPassiveTriggered(PassiveActivation activation)
        {
            var name = activation.Passive.DisplayName;
            if (string.IsNullOrEmpty(name)) name = activation.Passive.name;
            Popup(activation.Owner, name, PassiveColor);

            foreach (var effect in activation.Effects)
            {
                if (effect.EffectType == EffectType.Damage || effect.EffectType == EffectType.LifeSteal)
                {
                    Popup(effect.Target, effect.DamageDealt.ToString(), Color.white, PopupStyle.Damage);
                    if (effect.Killed)
                    {
                        PlayDownedSound(effect.Target);
                        AddLog($"{effect.Target.Name} is down.");
                    }
                }
                else if (effect.Target != activation.Owner)
                {
                    Popup(effect.Target, effect.EffectType == EffectType.ApplyStatus ? effect.Status.ToString() : effect.EffectType.ToString(),
                        new Color(0.6f, 1f, 0.6f));
                }
            }

            AddLog($"{activation.Owner.Name}'s {name}: {DescribePassiveEffects(activation)}.");
        }

        /// <summary>Gilt, for passive callouts, so they read apart from damage and status popups.</summary>
        private static readonly Color PassiveColor = new Color(0.97f, 0.85f, 0.56f);

        private static string DescribePassiveEffects(PassiveActivation activation)
        {
            return string.Join(", ", activation.Effects.Select(effect =>
            {
                var what = effect.EffectType == EffectType.ApplyStatus ? $"{effect.Status} {effect.Value}"
                    : effect.EffectType == EffectType.Damage || effect.EffectType == EffectType.LifeSteal ? $"{effect.DamageDealt} damage"
                    : $"{effect.EffectType} {effect.Value}";
                return effect.Target == activation.Owner ? what : $"{what} on {effect.Target.Name}";
            }));
        }

        private void OnBattleEnded(BattleResult result)
        {
            AddLog(result.Outcome == BattleOutcome.Victory ? "Victory!" : "Defeat...");

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopMusic();
                AudioManager.Instance.PlayOneShot(result.Outcome == BattleOutcome.Victory ? AudioActionType.Victory : AudioActionType.Defeat);
                if (result.Outcome == BattleOutcome.Victory) AudioManager.Instance.PlayOneShot(AudioActionType.Coin);
            }

            if (result.Outcome == BattleOutcome.Victory)
                LastVictory = PartyService.ApplyBattle(result, Model.Inventory);

            LastResult = result;
            CombatEnded?.Invoke(result);
        }
        #endregion

        #region End of battle
        /// <summary>The finished battle's result. Null while it is still being fought.</summary>
        public BattleResult LastResult { get; private set; }

        private bool _leaving;

        /// <summary>
        /// Leaves the victory screen: back to exploration when it started the battle, otherwise the same encounter
        /// again, as the combat test scene has nowhere else to go.
        /// </summary>
        public void ContinueAfterVictory()
        {
            if (_leaving) return;
            if (!ReturnsToExploration)
            {
                RestartBattle();
                return;
            }

            _leaving = true;
            _lastContext.Host.ContinueAfterVictory(_lastContext, LastResult);
        }

        /// <summary>Runs the same encounter again from its pre-battle state.</summary>
        public void RestartBattle()
        {
            if (_leaving) return;
            _leaving = true;

            if (ReturnsToExploration)
            {
                _lastContext.Host.RestartBattle(_lastContext);
                return;
            }

            EncounterContext.Pending = _lastContext;
            SceneManager.LoadScene(gameObject.scene.name);
        }

        /// <summary>
        /// Sends the party back to the last checkpoint: through the exploration host when it started the battle,
        /// else through <see cref="RespawnRequested"/>. With neither, as in the combat test scene, the battle restarts.
        /// </summary>
        public void RespawnAtCheckpoint()
        {
            if (ReturnsToExploration)
            {
                if (_leaving) return;
                _leaving = true;
                _lastContext.Host.RespawnAtCheckpoint(_lastContext);
            }
            else if (RespawnRequested != null) RespawnRequested.Invoke();
            else RestartBattle();
        }
        #endregion

        #region Feedback
        public void Popup(Combatant on, string text, Color color, PopupStyle style = PopupStyle.Info)
        {
            var view = GetView(on);
            if (view == null) return;

            var drift = UnityEngine.Random.Range(0.3f, 1f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            _popups.Add(new FeedbackPopup(_nextPopupId++, view.PopupOrigin, text, color, style, drift, Time.time));
        }

        /// <summary>Bursts a shock ring at <paramref name="origin"/>, a world point.</summary>
        public void ImpactAt(Vector3 origin, Color color) => _impactRings.Add(new ImpactRing(origin, color, Time.time));

        /// <summary>
        /// Where a blow from <paramref name="attacker"/> meets <paramref name="defender"/>: chest height, halfway
        /// between them. The attacker has stepped in by then, so this is the gap they strike across. With no
        /// attacker view, the defender's chest. False when the defender has no view.
        /// </summary>
        private bool TryContactPoint(Combatant attacker, Combatant defender, out Vector3 point)
        {
            point = default;
            var defenderView = GetView(defender);
            if (defenderView == null) return false;

            var attackerView = GetView(attacker);
            var position = attackerView != null && attackerView != defenderView
                ? Vector3.Lerp(defenderView.transform.position, attackerView.transform.position, 0.5f)
                : defenderView.transform.position;
            point = position + Vector3.up;
            return true;
        }

        private static PopupStyle DamageStyle(HitReport hit)
        {
            if (hit.Reaction == ElementReaction.Immune) return PopupStyle.Info;
            if (hit.IsCrit) return PopupStyle.Crit;
            return hit.Reaction == ElementReaction.Weak ? PopupStyle.Weak : PopupStyle.Damage;
        }

        private static Color DamageColor(HitReport hit)
        {
            switch (hit.Reaction)
            {
                case ElementReaction.Weak: return new Color(1f, 0.85f, 0.2f);
                case ElementReaction.Resist: return new Color(0.7f, 0.75f, 0.85f);
                case ElementReaction.Immune: return Color.gray;
                default: return hit.IsCrit ? new Color(1f, 0.45f, 0.25f) : Color.white;
            }
        }

        public void AddLog(string line)
        {
            _log.Add(line);
            if (_log.Count > 8) _log.RemoveAt(0);
        }

        private void PrunePopups()
        {
            _popups.RemoveAll(popup => Time.time - popup.Time > popupLifetime);
            _impactRings.RemoveAll(ring => Time.time - ring.Time > ImpactRingLifetime);
        }

        public static string Describe(Combatant actor, BattleCommand command)
        {
            if (command.Type == CommandType.Attack) return actor.Member?.BasicAttack != null ? actor.Member.BasicAttack.DisplayName : "Attack";
            return command.Action != null ? command.Action.DisplayName : command.Type.ToString();
        }

        private static string DescribeExecution(ActionExecution execution)
        {
            if (execution.Command == null) return execution.EnemyAbility?.Name;
            var name = Describe(execution.Actor, execution.Command);
            return execution.IsCounter ? $"a counter attack ({name})" : name;
        }
        #endregion
    }
}
