using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Characters;
using InElementum.Combat.Managers;
using InElementum.Combat.Model;
using InElementum.Core.UI;
using InElementum.Data.Actions;
using InElementum.Core.Managers;
using InElementum.Data.Enums;
using InElementum.Data.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Combat.UI
{
    /// <summary>
    /// Placeholder combat UI in UI Toolkit (look in <c>Resources/UI/Combat.uss</c>): the party stacked down from the
    /// top left, the turn order running left to right across the top, a context panel at the bottom center (the
    /// highlighted option, or the enemy being aimed at), and the command menu and timing prompts attached to the
    /// character they concern. Damaging actions in the menu are rated STRONG, WEAK, or NO EFFECT against the enemy's
    /// element once the party has discovered it, and each enemy's discovered weaknesses and resistances show under its
    /// health bar. Keyboard and gamepad input is read in Update from <see cref="CombatInput"/>; the mouse works
    /// through pointer events on the elements. The elements are built once and updated every frame in LateUpdate,
    /// after the battle camera has moved.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public partial class CombatHud : MonoBehaviour
    {
        private enum Menu
        {
            Root,
            Abilities,
            Items,
            Surges,
            Target
        }

        private sealed class Option
        {
            public string Title;
            /// <summary>Short note on the right of the row, such as an AP cost or a count.</summary>
            public string Meta;
            /// <summary>The description bar's text.</summary>
            public string Detail;
            /// <summary>The description bar's right-hand cell: cast speed, and QTE when there is one.</summary>
            public string Cast;
            /// <summary>Shown instead of acting when a disabled option is picked.</summary>
            public string Reason;
            /// <summary>The action behind the option, used to rate its element against the enemy.</summary>
            public CombatActionData Action;
            public bool Enabled = true;
            public Action Select;
            /// <summary>A button that picks this option straight from the root menu, shown as a keycap on the row.</summary>
            public CombatButton? Shortcut;
        }

        private const float ReferenceHeight = HudPanel.ReferenceHeight;
        private const int MaxVisibleRows = 6;
        private const float RowHeight = 30f;

        // Layout, in panel units (720 tall).
        private const float PartyLeft = 16f;
        private const float PartyTop = 72f;
        private const float ActorStepOut = 32f;
        private const float StepOutSeconds = 0.25f;
        private const float PartyWidth = 230f;
        private const float PartyEntryHeight = 74f;
        private const float PartyGap = 10f;
        private const float RailY = 56f;
        private const float EmblemSize = 28f;
        private const float ActiveEmblemSize = 36f;
        private const float EmblemStem = 6f;
        private const float RowGap = 4f;
        /// <summary>How far above the bottom edge the context panel starts.</summary>
        private const float ContextPanelRise = 130f;
        /// <summary>Gap the camera keeps between characters and the HUD.</summary>
        private const float CameraClearance = 12f;
        /// <summary>How big the ornamented corners of the Kenney frames draw, on large panels and on small boxes.</summary>
        private const float PanelCorner = 16f;
        private const float SmallCorner = 9f;

        // Palette: dark ink panels that fade out at their ends, thin silver and gilt rules, a blue sweep for focus.
        private static readonly Color PanelColor = HudColors.Panel;
        private static readonly Color MenuFill = HudColors.MenuFill;
        private static readonly Color MenuBorder = HudColors.Gilt;
        private static readonly Color StrongColor = new Color(1f, 0.8f, 0.25f);
        private static readonly Color WeakColor = new Color(0.62f, 0.68f, 0.78f);
        private static readonly Color ImmuneColor = new Color(0.85f, 0.4f, 0.4f);
        private static readonly Color PanelSoft = HudColors.PanelSoft;
        private static readonly Color Accent = HudColors.Accent;
        private static readonly Color TextColor = HudColors.Text;
        private static readonly Color Muted = HudColors.Muted;
        private static readonly Color HealthColor = HudColors.EnemyHealth;
        private static readonly Color PartyHealthColor = HudColors.PartyHealth;
        private static readonly Color Rail = HudColors.Rail;
        private static readonly Color CastRail = new Color(0.95f, 0.5f, 0.4f, 0.9f);
        private static readonly Color SweepColor = new Color(0.02f, 0.03f, 0.06f, 0.7f);
        private static readonly Color ActiveSweep = HudColors.ActiveSweep;
        private static readonly Color RowFill = new Color(0.07f, 0.08f, 0.11f, 0.84f);
        private static readonly Color RowEdge = new Color(0.82f, 0.84f, 0.88f, 0.55f);
        private static readonly Color TabColor = new Color(0.42f, 0.45f, 0.52f, 0.92f);
        private static readonly Color PipEmpty = new Color(0.25f, 0.27f, 0.33f, 0.9f);
        private static readonly Color ApColor = HudColors.Ap;
        private static readonly Color SurgeColor = HudColors.Surge;
        /// <summary>Marks every enemy in Cast: committed to its revealed intent.</summary>
        private static readonly Color RedZone = new Color(1f, 0.28f, 0.28f);
        private static readonly Color BreakColor = new Color(1f, 0.78f, 0.25f);
        private static readonly Color DodgeColor = new Color(0.45f, 0.6f, 1f);
        private static readonly Color ParryColor = Color.white;
        private static readonly Color QteZone = new Color(0.4f, 1f, 0.6f);

        private Menu _menu = Menu.Root;
        private Menu _menuBeforeTarget;
        private readonly List<Option> _options = new List<Option>();
        private int _cursor;
        private Combatant _menuActor;
        /// <summary>Bumped whenever the option list is rebuilt, so the menu rows know to rebind.</summary>
        private int _menuVersion;

        private List<Combatant> _targets = new List<Combatant>();
        private int _targetIndex;
        private Func<Combatant, BattleCommand> _commandForTarget;

        private string _message;
        private float _messageTime;
        private int _defeatCursor;

        // How far each party card has slid out, 0 (resting) to 1 (fully stepped out as the actor).
        private readonly Dictionary<Combatant, float> _stepOut = new Dictionary<Combatant, float>();

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _battle;
        private float _width = 1280f;

        private CombatManager Manager => CombatManager.Instance;
        private BattleModel Model => Manager != null ? Manager.Model : null;
        private CombatInput Input => Manager.Input;
        private string ConfirmKey => Input.Glyph(CombatButton.Confirm);
        private string CancelKey => Input.Glyph(CombatButton.Cancel);
        /// <summary>While a dodge, parry, or strike prompt is up, mouse buttons belong to it, not to the menus.</summary>
        private bool IsTimingPrompt => Manager.ActiveDefense != null || Manager.ActiveQte != null;
        private bool IsTargeting => Model.State == BattleState.AwaitingCommand && _menu == Menu.Target && _targets.Count > 0;
        private Combatant CurrentTarget => IsTargeting ? _targets[_targetIndex] : null;

        #region Lifecycle
        private void Awake()
        {
            _document = HudPanel.CreateDocument(transform, "Combat", HudPanel.Layer.Combat, "Combat");
            _root = _document.rootVisualElement;

            _battle = Hud.Box("cb-battle").Absolute();
            _battle.style.left = _battle.style.top = _battle.style.right = _battle.style.bottom = 0f;
            _root.Add(_battle);
            BuildBattle(_battle);
            BuildPopups(_root);
            BuildVictory(_root);
        }

        private void Start() => ReserveCameraSpace();
        #endregion

        #region Input
        private void Update()
        {
            ReserveCameraSpace();
            if (Model == null) return;

            SyncMenuActor();
            HandlePointer();

            switch (Model.State)
            {
                case BattleState.AwaitingCommand:
                    UpdateCommandMenu();
                    break;
                case BattleState.Victory:
                    if (Input.Confirm)
                    {
                        AudioManager.PlayUi(AudioActionType.MenuConfirm);
                        Manager.ContinueAfterVictory();
                    }
                    break;
                case BattleState.Defeat:
                    if (Input.Navigate.y != 0)
                    {
                        _defeatCursor = 1 - _defeatCursor;
                        AudioManager.PlayUi(AudioActionType.MenuMove);
                    }
                    if (Input.Confirm)
                    {
                        AudioManager.PlayUi(AudioActionType.MenuConfirm);
                        ChooseDefeatOption(_defeatCursor);
                    }
                    break;
            }
        }

        /// <summary>
        /// Tells the battle camera which screen edges the HUD covers, so overview and action shots keep characters
        /// clear of them: the party column on the left, the turn order on top, the context panel at the bottom. The
        /// column is measured with a card stepped out, and the panel counted even while hidden, so the shot doesn't
        /// shift as cards slide or turns change hands.
        /// </summary>
        private void ReserveCameraSpace()
        {
            var camera = Manager != null ? Manager.BattleCamera : null;
            if (camera == null || Screen.width <= 0 || Screen.height <= 0) return;

            var width = Screen.width * ReferenceHeight / Screen.height;
            if (Model != null && Model.State == BattleState.Victory)
            {
                FrameVictory(camera, width);
                return;
            }

            var left = (PartyLeft + ActorStepOut + PartyWidth + CameraClearance) / width;
            var top = (RailY + ActiveEmblemSize + EmblemStem + CameraClearance) / ReferenceHeight;
            var bottom = (ContextPanelRise + CameraClearance) / ReferenceHeight;
            camera.HudMargins = new Vector4(left, bottom, 0f, top);
        }

        /// <summary>Opens the root menu when a new character starts choosing, before any input is read.</summary>
        private void SyncMenuActor()
        {
            if (Model.State != BattleState.AwaitingCommand || Model.CurrentActor == _menuActor) return;
            _menuActor = Model.CurrentActor;
            OpenMenu(Menu.Root);
        }

        /// <summary>Right click steps back and the scroll wheel moves the cursor; clicks and hovers come from the elements.</summary>
        private void HandlePointer()
        {
            if (Input.RightClick && !IsTimingPrompt) Back();

            if (Input.Scroll != 0f && Model.State == BattleState.AwaitingCommand && _menu != Menu.Target && _options.Count > 0)
                _cursor = Wrap(_cursor + (Input.Scroll > 0f ? -1 : 1), _options.Count);
        }

        /// <summary>
        /// Wires <paramref name="element"/> to the mouse: moving over it calls <paramref name="hover"/>, clicking it
        /// calls <paramref name="click"/>. Clicks are ignored while a timing prompt owns the mouse buttons.
        /// </summary>
        private void Clickable(VisualElement element, Action click, Action hover = null)
        {
            element.pickingMode = PickingMode.Position;
            element.RegisterCallback<PointerMoveEvent>(_ =>
            {
                if (Model != null && !IsTimingPrompt) hover?.Invoke();
            });
            element.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0 || Model == null || IsTimingPrompt) return;
                if (click != null) AudioManager.PlayUi(AudioActionType.MenuConfirm);
                click?.Invoke();
            });
        }

        private void UpdateCommandMenu()
        {
            if (_menu == Menu.Target)
            {
                UpdateTargeting();
                return;
            }

            if (_options.Count == 0) return;
            if (_menu == Menu.Root && TryShortcut()) return;
            if (Input.Navigate.y != 0)
            {
                _cursor = Wrap(_cursor - Input.Navigate.y, _options.Count);
                AudioManager.PlayUi(AudioActionType.MenuMove);
            }

            if (Input.Confirm)
            {
                AudioManager.PlayUi(AudioActionType.MenuConfirm);
                Activate(_cursor);
            }
            else if (Input.Cancel)
            {
                AudioManager.PlayUi(AudioActionType.MenuBack);
                Back();
            }
        }

        private void UpdateTargeting()
        {
            var step = Input.Navigate.x != 0 ? Input.Navigate.x : -Input.Navigate.y;
            if (step != 0)
            {
                _targetIndex = Wrap(_targetIndex + step, _targets.Count);
                AudioManager.PlayUi(AudioActionType.MenuMove);
            }

            if (Input.Cancel)
            {
                AudioManager.PlayUi(AudioActionType.MenuBack);
                Back();
                return;
            }

            // The manager has already framed the actor's turn this frame; aiming narrows it to the actor and the
            // target, the same shot whether the target is an enemy, an ally, or the actor itself.
            var camera = Manager.BattleCamera;
            var actorView = Manager.GetView(Model.CurrentActor);
            var targetView = Manager.GetView(_targets[_targetIndex]);
            if (camera != null && actorView != null && targetView != null)
                camera.Frame(actorView.transform, targetView.transform);

            if (Input.Confirm)
            {
                AudioManager.PlayUi(AudioActionType.MenuConfirm);
                ConfirmTarget();
            }
        }

        /// <summary>Picks a root option by its shortcut button, if one was pressed this frame.</summary>
        private bool TryShortcut()
        {
            CombatButton? pressed = Input.Attack ? CombatButton.Attack
                : Input.OpenAbilities ? CombatButton.Abilities
                : Input.OpenItems ? CombatButton.Items
                : Input.OpenSurge ? CombatButton.Surge
                : Input.SkipTurn ? CombatButton.Skip
                : null;
            if (pressed == null) return false;

            var index = _options.FindIndex(option => option.Shortcut == pressed);
            if (index < 0) return false;
            AudioManager.PlayUi(AudioActionType.MenuConfirm);
            Activate(index);
            return true;
        }

        private void Activate(int index)
        {
            if (Model.State != BattleState.AwaitingCommand || index < 0 || index >= _options.Count) return;

            _cursor = index;
            var option = _options[index];
            if (option.Enabled) option.Select?.Invoke();
            else ShowMessage(option.Reason ?? option.Detail);
        }

        private void ConfirmTarget()
        {
            if (!IsTargeting) return;

            var command = _commandForTarget(_targets[_targetIndex]);
            if (!Model.Submit(command)) ShowMessage(Model.Validate(command));
        }

        /// <summary>Points the aim at <paramref name="unit"/>, and fires when <paramref name="confirm"/>. For mouse targeting.</summary>
        private void AimAt(Combatant unit, bool confirm)
        {
            if (!IsTargeting) return;
            var index = _targets.IndexOf(unit);
            if (index < 0) return;
            _targetIndex = index;
            if (confirm) ConfirmTarget();
        }

        /// <summary>Steps back one level: from aiming to the list it came from, or from a list to the root menu.</summary>
        private void Back()
        {
            if (Model.State != BattleState.AwaitingCommand) return;
            if (_menu == Menu.Target) OpenMenu(_menuBeforeTarget);
            else if (_menu != Menu.Root) OpenMenu(Menu.Root);
        }

        private void ChooseDefeatOption(int index)
        {
            _defeatCursor = index;
            if (index == 0) Manager.RestartBattle();
            else Manager.RespawnAtCheckpoint();
        }

        private void OpenMenu(Menu menu)
        {
            _menu = menu;
            _cursor = 0;
            _options.Clear();
            _menuVersion++;

            var actor = Model.CurrentActor;
            if (actor == null) return;

            switch (menu)
            {
                case Menu.Root:
                    var attack = actor.Member != null ? actor.Member.BasicAttack : null;
                    _options.Add(new Option
                    {
                        Title = "ATTACK",
                        Detail = attack == null ? "No basic attack assigned."
                            : actor.Weapon != null ? $"{actor.Weapon.DisplayName}, {actor.AttackPower:0.#} power per hit. Grants {Manager.Tuning.ApPerHit} AP per hit."
                            : $"{attack.DisplayName}. Grants {Manager.Tuning.ApPerHit} AP per hit.",
                        Cast = attack != null ? CastSpeed(attack) : null,
                        Action = attack,
                        Enabled = attack != null,
                        Select = () => Choose(attack, target => BattleCommand.Attack(target)),
                        Shortcut = CombatButton.Attack
                    });
                    _options.Add(new Option { Title = "ABILITIES", Detail = "Spend AP on elemental skills. AP spent charges your Surge.", Select = () => OpenMenu(Menu.Abilities), Shortcut = CombatButton.Abilities });
                    _options.Add(new Option { Title = "ITEMS", Detail = "Shared by the whole party.", Select = () => OpenMenu(Menu.Items), Shortcut = CombatButton.Items });
                    var surgeReady = Model.GetSurges(actor).Any() && actor.SurgeGauge.IsFull;
                    _options.Add(new Option { Title = "SURGE", Meta = surgeReady ? "READY" : null, Detail = "Your ultimate. Needs a full Surge gauge.", Select = () => OpenMenu(Menu.Surges), Shortcut = CombatButton.Surge });
                    _options.Add(new Option { Title = "SKIP", Detail = "Return to the start of the bar.", Select = () => Model.Submit(BattleCommand.Skip()), Shortcut = CombatButton.Skip });
                    break;

                case Menu.Abilities:
                    foreach (var ability in Model.GetAbilities(actor))
                    {
                        _options.Add(new Option
                        {
                            Title = ability.DisplayName,
                            Meta = $"{ability.ApCost} AP",
                            Detail = Description(ability),
                            Cast = CastSpeed(ability) + (ability.HasQte ? " · QTE" : string.Empty),
                            Reason = "Not enough AP.",
                            Action = ability,
                            Enabled = actor.Stats.CurrentAp >= ability.ApCost,
                            Select = () => Choose(ability, target => BattleCommand.Ability(ability, target))
                        });
                    }
                    break;

                case Menu.Items:
                    foreach (var pair in Model.Inventory.Items.Where(pair => pair.Value > 0))
                    {
                        var item = pair.Key;
                        _options.Add(new Option
                        {
                            Title = item.DisplayName,
                            Meta = $"×{pair.Value}",
                            Detail = Description(item),
                            Cast = CastSpeed(item),
                            Action = item,
                            Select = () => Choose(item, target => BattleCommand.Item(item, target))
                        });
                    }
                    break;

                case Menu.Surges:
                    foreach (var surge in Model.GetSurges(actor))
                    {
                        _options.Add(new Option
                        {
                            Title = surge.DisplayName,
                            Meta = surge.IsFusion ? "FUSION" : null,
                            Detail = Description(surge),
                            Cast = CastSpeed(surge),
                            Reason = "Surge gauge not full.",
                            Action = surge,
                            Enabled = actor.SurgeGauge.CanSpend(surge.GaugeCost),
                            Select = () => Choose(surge, target => BattleCommand.Surge(surge, target))
                        });
                    }
                    break;
            }

            if (_options.Count == 0 && menu != Menu.Root)
                _options.Add(new Option { Title = "(none)", Detail = menu == Menu.Surges ? "No Surge unlocked yet." : "Nothing here.", Enabled = false });
        }

        private void Choose(CombatActionData action, Func<Combatant, BattleCommand> makeCommand)
        {
            var needsTarget = action.TargetType == ActionTargetType.Enemy || action.TargetType == ActionTargetType.Ally;
            if (!needsTarget)
            {
                var command = makeCommand(null);
                if (!Model.Submit(command)) ShowMessage(Model.Validate(command));
                return;
            }

            _targets = Model.GetValidTargets(Model.CurrentActor, action).ToList();
            if (_targets.Count == 0)
            {
                ShowMessage("No valid target.");
                return;
            }

            _menuBeforeTarget = _menu;
            _menu = Menu.Target;
            _targetIndex = 0;
            _commandForTarget = makeCommand;
            _menuVersion++;
        }

        private void ShowMessage(string message)
        {
            _message = message;
            _messageTime = Time.time;
        }
        #endregion

        #region Refresh
        private void LateUpdate()
        {
            if (_root == null) return;
            var model = Model;
            _root.Show(model != null);
            if (model == null) return;

            _width = HudPanel.Width(_root);

            // The victory screen replaces the battle HUD; only popups still in flight finish over it.
            var victory = model.State == BattleState.Victory;
            _battle.Show(!victory);
            RefreshPopups();
            RefreshVictory(victory);
            if (victory) return;

            RefreshEnemyPlates();
            RefreshTargets();
            RefreshTurnOrder();
            RefreshContextPanel();
            RefreshBanner();
            RefreshParty();
            RefreshCommandMenu(model.State == BattleState.AwaitingCommand);
            RefreshCenterPanel();
            RefreshTimingPrompts();
            RefreshHints();
        }
        #endregion

        #region Helpers
        private static string CastSpeed(CombatActionData action)
        {
            var time = action.CastTime;
            var speed = time <= 0.01f ? "Instant" : time <= 0.75f ? "Fast" : time <= 1.5f ? "Normal" : "Slow";
            return $"{speed} {time:0.#}s";
        }

        /// <summary>
        /// How enemies react to <paramref name="action"/>'s element: against the enemy being aimed at, otherwise
        /// against every living enemy when they all react the same way. Null for actions that deal no damage, for
        /// neutral hits, when the enemies disagree, and until the party has discovered the element on every enemy
        /// rated.
        /// </summary>
        private ElementReaction? Matchup(Combatant actor, CombatActionData action)
        {
            var element = DamageElement(actor, action);
            if (element == Element.None) return null;

            var aimed = CurrentTarget;
            var against = aimed != null
                ? (aimed.Side == BattleSide.Enemy ? new[] { aimed } : Array.Empty<Combatant>())
                : Model.Enemies.Where(enemy => enemy.IsAlive).ToArray();
            if (against.Length == 0 || against.Any(enemy => !Model.IsElementKnown(enemy, element))) return null;

            var reaction = DamageCalculator.GetReaction(against[0].Stats.GetElementMultiplier(element));
            for (var i = 1; i < against.Length; i++)
            {
                if (DamageCalculator.GetReaction(against[i].Stats.GetElementMultiplier(element)) != reaction) return null;
            }

            return reaction == ElementReaction.Normal ? (ElementReaction?)null : reaction;
        }

        /// <summary>The element an action hits with, matching how the battle resolves it. None when it deals no damage.</summary>
        private static Element DamageElement(Combatant actor, CombatActionData action)
        {
            if (action == null) return Element.None;
            if (action is AttackData) return actor != null ? actor.AttackElement : action.Element;

            var damages = action.Effects.Any(effect => effect.EffectType == EffectType.Damage || effect.EffectType == EffectType.LifeSteal);
            return damages ? action.Element : Element.None;
        }

        /// <summary>
        /// The enemy's discovered elements that are not neutral, with its type's authored reaction to each. A Break's
        /// resistance drop does not change what shows here.
        /// </summary>
        private List<(Element element, ElementReaction reaction)> KnownReactions(Combatant enemy)
        {
            var known = new List<(Element, ElementReaction)>();
            if (enemy?.Enemy == null) return known;

            foreach (var element in Model.Knowledge.GetDiscovered(enemy.Enemy))
            {
                var reaction = DamageCalculator.GetReaction(ElementKnowledge.GetAffinity(enemy.Enemy, element));
                if (reaction != ElementReaction.Normal) known.Add((element, reaction));
            }

            return known;
        }

        /// <summary>
        /// The unit an intent is aimed at: its party target for a single-target effect, null when it hits the whole
        /// party, and the caster itself for abilities that only affect its own side.
        /// </summary>
        private static Combatant IntentAim(EnemyIntent intent)
        {
            if (intent.Ability.ActionList == null) return intent.Target;
            var targets = intent.Ability.ActionList.Where(action => action != null).Select(action => action.TargetType).ToList();
            if (targets.Any(t => t == ActionTargetType.Enemy || t == ActionTargetType.RandomEnemy)) return intent.Target;
            if (targets.Contains(ActionTargetType.AllEnemies)) return null;
            return intent.Enemy;
        }

        /// <summary>The intent's tags the badges don't already show: hit count, counter-cast, defense rule, Steadfast, Guard.</summary>
        private static string IntentNotes(EnemyIntent intent)
        {
            var notes = new List<string>();
            if (intent.Ability.HitCount > 1) notes.Add($"×{intent.Ability.HitCount}");
            if (intent.IsCounterCast) notes.Add("counter-cast");
            if (intent.DefenseRule == DefenseRule.DodgeOnly) notes.Add("dodge only");
            if (intent.DefenseRule == DefenseRule.ParryOnly) notes.Add("parry only");
            if (intent.IsSteadfast) notes.Add("Steadfast");
            if (intent.IsGuard) notes.Add("Guard");
            return string.Join(" · ", notes);
        }

        private static string Description(CombatActionData action)
        {
            var text = action.BuildDescription();
            return string.IsNullOrEmpty(text) ? action.DisplayName : text;
        }

        private static Color ElementColor(Combatant unit) => PlaceholderCharacter.ElementColor(unit.Data != null ? unit.Data.Element : Element.None);

        private static int Wrap(int value, int count) => count == 0 ? 0 : ((value % count) + count) % count;

        private static bool IsActing(TimelineEntry entry) => entry.Phase == TimelinePhase.Choosing || entry.Phase == TimelinePhase.Resolving;

        /// <summary>Keeps a block of the given size inside the screen, clear of the party, the turn order, and the context panel.</summary>
        private static Vector2 ClampToScreen(Vector2 origin, Vector2 size, float width)
        {
            const float minX = PartyLeft + PartyWidth + 50f;
            const float minY = 156f;
            return new Vector2(Mathf.Clamp(origin.x, minX, Mathf.Max(minX, width - size.x - 20f)),
                Mathf.Clamp(origin.y, minY, Mathf.Max(minY, ReferenceHeight - 140f - size.y)));
        }

        /// <summary>Keeps a timing prompt and its key entries clear of the screen edges and the HUD.</summary>
        private static Vector2 ClampPrompt(Vector2 center, float width) =>
            new Vector2(Mathf.Clamp(center.x, 320f, Mathf.Max(320f, width - 300f)), Mathf.Clamp(center.y, 190f, ReferenceHeight - 170f));

        /// <summary>A point <paramref name="heightOffset"/> world units above a view's feet, in panel units.</summary>
        private bool TryPanelPoint(CharacterBase view, float heightOffset, out Vector2 point)
        {
            point = default;
            if (view == null || !view.gameObject.activeInHierarchy) return false;
            return HudPanel.WorldToPanel(_root, view.transform.position + Vector3.up * heightOffset, out point);
        }

        /// <summary>The on-screen box a character stands in: feet to about two units up, a little narrower than tall.</summary>
        private bool TryCharacterRect(CharacterBase view, out Rect rect)
        {
            rect = default;
            if (!TryPanelPoint(view, 0f, out var feet) || !TryPanelPoint(view, 2f, out var head)) return false;

            var height = Mathf.Max(40f, feet.y - head.y);
            var halfWidth = Mathf.Max(24f, height * 0.4f);
            rect = new Rect(feet.x - halfWidth, feet.y - height, halfWidth * 2f, height);
            return true;
        }

        private VisualElement Plate(float corner = PanelCorner) => Plate(Hud.Box().Absolute(), corner);

        /// <summary>A dark Kenney panel framed in gilt, its corners <paramref name="corner"/> across.</summary>
        private static VisualElement Plate(VisualElement element, float corner = PanelCorner)
        {
            Hud.Frame(element, corner, MenuFill, MenuBorder);
            return element;
        }

        private static Label Text(VisualElement parent, string className, string text = "")
        {
            var label = Hud.Text(text, className).Absolute();
            parent.Add(label);
            return label;
        }

        private static float EaseOutCubic(float t) => Hud.EaseOutCubic(t);
        #endregion
    }
}
