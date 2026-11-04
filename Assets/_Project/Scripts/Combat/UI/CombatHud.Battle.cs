using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Managers;
using InElementum.Combat.Model;
using InElementum.Core.UI;
using InElementum.Data.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Combat.UI
{
    /// <summary>The battle HUD's elements: built once in <see cref="BuildBattle"/>, then refreshed every frame.</summary>
    public partial class CombatHud
    {
        #region Elements
        // Layers, back to front.
        private VisualElement _plateLayer;
        private VisualElement _targetLayer;
        private VisualElement _turnLayer;
        private VisualElement _contextPlate;
        private VisualElement _bannerPlate;
        private VisualElement _partyLayer;
        private VisualElement _menuLayer;
        private VisualElement _centerLayer;
        private HintBar _hints;
        private VisualElement _promptLayer;

        // Turn order.
        private VisualElement _railCloud;
        private VisualElement _castWash;
        private readonly VisualElement[] _railLines = new VisualElement[4];
        private Diamond _railStart;
        private Diamond _railEndRing;
        private Diamond _railEnd;
        private Label _castMark;
        private Label _waitLabel;
        private Label _actLabel;
        private readonly Dictionary<Combatant, (VisualElement stem, Emblem emblem)> _timelineIcons = new Dictionary<Combatant, (VisualElement, Emblem)>();

        // Context panel and banner.
        private VisualElement _enemyInfo;
        private Label _enemyName;
        private AffinityRow _enemyAffinities;
        private Label _enemyHealth;
        private HudBar _enemyBar;
        private VisualElement _enemyCast;
        private Label _enemyIntent;
        private VisualElement _description;
        private VisualElement _castCell;
        private Label _castValue;
        private Label _detail;
        private Label _bannerText;

        // Targets.
        private Brackets _targetBrackets;
        private readonly List<VisualElement> _targetBoxes = new List<VisualElement>();

        // Command menu.
        private VisualElement _menuFrame;
        private VisualElement _menuTab;
        private VisualElement _menuTabLine;
        private Label _menuTabLabel;
        private Label _menuApLabel;
        private PipRow _menuPips;
        private readonly List<MenuRow> _menuRows = new List<MenuRow>();
        private Label _menuPointer;
        private Label _menuUp;
        private Label _menuDown;
        private int _menuFirst;
        private bool _menuAiming;
        private VisualElement _messagePlate;
        private Label _messageText;

        // Defeat.
        private VisualElement _centerPlate;
        private Label _centerTitle;
        private Label _centerBody;
        private readonly List<MenuRow> _defeatRows = new List<MenuRow>();
        private Label _defeatPointer;

        // Timing prompts.
        private VisualElement _ringDisc;
        private VisualElement _ringBase;
        private VisualElement _ringClosing;
        private VisualElement _ringEmphasis;
        private KeyPrompt _ringKey;
        private Label _ringGlyph;
        private Label _promptTitle;
        private Label _promptResult;
        private readonly List<PromptEntry> _promptEntries = new List<PromptEntry>();

        private readonly List<PartyCard> _cards = new List<PartyCard>();
        private readonly List<EnemyPlate> _plates = new List<EnemyPlate>();

        private sealed class MenuRow
        {
            public VisualElement Root;
            public VisualElement Sweep;
            public VisualElement BevelTop;
            public VisualElement BevelLeft;
            public KeyPrompt Key;
            public Label Title;
            public AffinityChip Chip;
            public Label Meta;
        }

        private sealed class PromptEntry
        {
            public VisualElement Root;
            public VisualElement Sweep;
            public VisualElement Bar;
            public KeyPrompt Key;
            public Label Title;
        }

        private sealed class PartyCard
        {
            public Combatant Unit;
            public VisualElement Sweep;
            public VisualElement SweepLine;
            public Brackets Brackets;
            public VisualElement Card;
            public Emblem Emblem;
            public Label Name;
            public PipRow Pips;
            public Label ApTag;
            public Label Hp;
            public Label HpMax;
            public Label HpTag;
            public HudBar HpBar;
            public Label SurgeTag;
            public HudBar SurgeBar;
        }

        private sealed class EnemyPlate
        {
            public Combatant Unit;
            public VisualElement Root;
            public HudBar Bar;
            public Label Name;
            public VisualElement BreakBack;
            public HudBar Break;
            public AffinityRow Affinities;
            public Label Broken;
            public Emblem IntentTarget;
            public Diamond AllBadge;
            public Label AllLabel;
            public Label Arrow;
            public Diamond ElementBack;
            public Diamond ElementFill;
            public Label Notes;
        }
        #endregion

        #region Build
        private void BuildBattle(VisualElement root)
        {
            _plateLayer = Layer(root);
            _targetLayer = Layer(root);
            _turnLayer = Layer(root);
            BuildTurnOrder();
            BuildContextPanel(root);
            BuildBanner(root);
            _partyLayer = Layer(root);
            _menuLayer = Layer(root);
            BuildCommandMenu();
            _centerLayer = Layer(root);
            BuildCenterPanel();
            _hints = new HintBar();
            root.Add(_hints);
            _promptLayer = Hud.Box().Absolute();
            root.Add(_promptLayer);
            BuildTimingPrompt();
        }

        private static VisualElement Layer(VisualElement parent)
        {
            var layer = Hud.Box("cb-layer").Absolute();
            layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0f;
            parent.Add(layer);
            return layer;
        }

        private void BuildTurnOrder()
        {
            _railCloud = Hud.Tinted(UiTextures.SoftBox, SweepColor);
            _castWash = Hud.Tinted(UiTextures.SoftBox, CastRail.Alpha(0.16f / CastRail.a));
            _turnLayer.Add(_railCloud);
            _turnLayer.Add(_castWash);
            for (var i = 0; i < _railLines.Length; i++)
            {
                _railLines[i] = Hud.Fill(i % 2 == 0 ? Rail : CastRail);
                _turnLayer.Add(_railLines[i]);
            }

            _railStart = new Diamond(10f).Absolute();
            _railStart.Fill = Rail;
            _railEndRing = new Diamond(20f).Absolute();
            _railEndRing.SetRim(2f, Accent);
            _railEnd = new Diamond(9f).Absolute();
            _railEnd.Fill = Accent;
            _turnLayer.Add(_railStart);
            _turnLayer.Add(_railEndRing);
            _turnLayer.Add(_railEnd);

            _castMark = Text(_turnLayer, "cb-stat", "»");
            _castMark.style.color = CastRail;
            _castMark.style.unityTextAlign = TextAnchor.MiddleCenter;
            _waitLabel = Text(_turnLayer, "cb-tag", "WAIT");
            _waitLabel.style.color = Muted;
            _waitLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _actLabel = Text(_turnLayer, "cb-caps", "ACT");
            _actLabel.style.color = Accent;
            _actLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        }

        private void BuildContextPanel(VisualElement root)
        {
            _contextPlate = Plate();
            root.Add(_contextPlate);

            _enemyInfo = Hud.Box().Absolute();
            _enemyInfo.Place(new Rect(6f, 4f, 640f, 66f));
            var heading = Hud.Box("cb-row").Absolute();
            heading.Place(new Rect(16f, 6f, 420f, 24f));
            _enemyName = Hud.Text(string.Empty, "cb-title");
            _enemyAffinities = new AffinityRow();
            _enemyAffinities.style.position = Position.Relative;
            _enemyAffinities.style.marginLeft = 14f;
            heading.Add(_enemyName);
            heading.Add(_enemyAffinities);
            _enemyInfo.Add(heading);
            _enemyHealth = Text(_enemyInfo, "cb-label");
            _enemyHealth.Place(new Rect(640f - 176f, 6f, 160f, 24f));
            _enemyHealth.style.unityTextAlign = TextAnchor.MiddleRight;
            _enemyBar = new HudBar();
            _enemyBar.Place(new Rect(16f, 33f, 608f, 6f));
            _enemyInfo.Add(_enemyBar);
            _enemyCast = Hud.Fill(Accent);
            _enemyInfo.Add(_enemyCast);
            _enemyIntent = Text(_enemyInfo, "cb-small");
            _enemyIntent.Place(new Rect(16f, 44f, 608f, 18f));
            _enemyIntent.style.color = RedZone;
            _enemyIntent.style.unityTextAlign = TextAnchor.MiddleLeft;
            _contextPlate.Add(_enemyInfo);

            _description = Hud.Box().Absolute();
            _description.Place(new Rect(6f, 4f, 640f, 66f));
            _castCell = Hud.Box().Absolute();
            _castCell.Place(new Rect(640f - 132f, 0f, 132f, 66f));
            var divider = Hud.Fill(MenuBorder);
            divider.Place(new Rect(0f, 10f, 1f, 46f));
            _castCell.Add(divider);
            var castTag = Text(_castCell, "cb-tag", "CAST");
            castTag.Place(new Rect(0f, 10f, 132f, 14f));
            castTag.style.color = Muted;
            castTag.style.unityTextAlign = TextAnchor.MiddleCenter;
            _castValue = Text(_castCell, "cb-small");
            _castValue.Place(new Rect(0f, 26f, 132f, 24f));
            _castValue.style.unityTextAlign = TextAnchor.MiddleCenter;
            _description.Add(_castCell);
            _detail = Text(_description, "cb-label");
            _detail.AddToClassList("cb-wrap");
            _detail.style.unityTextAlign = TextAnchor.MiddleLeft;
            _contextPlate.Add(_description);
        }

        private void BuildBanner(VisualElement root)
        {
            _bannerPlate = Plate(SmallCorner);
            _bannerText = Text(_bannerPlate, "cb-label");
            _bannerText.Place(new Rect(10f, 2f, 400f, 32f));
            _bannerText.style.unityTextAlign = TextAnchor.MiddleCenter;
            root.Add(_bannerPlate);
        }

        private void BuildCommandMenu()
        {
            _menuFrame = Hud.Box().Absolute();
            Hud.Frame(_menuFrame, SmallCorner, PanelSoft, MenuBorder, small: true);
            _menuLayer.Add(_menuFrame);
            _menuTab = Hud.Tinted(UiTextures.FadeLeft, TabColor);
            _menuTabLine = Hud.Tinted(UiTextures.FadeLeft, MenuBorder);
            _menuLayer.Add(_menuTab);
            _menuLayer.Add(_menuTabLine);
            _menuTabLabel = Text(_menuLayer, "cb-item");
            _menuTabLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _menuApLabel = Text(_menuLayer, "cb-tag", "AP");
            _menuApLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            _menuPips = new PipRow();
            _menuLayer.Add(_menuPips);

            for (var i = 0; i < MaxVisibleRows; i++)
            {
                var row = CreateMenuRow();
                var slot = i;
                Clickable(row.Root, () =>
                {
                    if (!_menuAiming) Activate(_menuFirst + slot);
                }, () =>
                {
                    if (!_menuAiming && _menuFirst + slot < _options.Count) _cursor = _menuFirst + slot;
                });
                _menuRows.Add(row);
                _menuLayer.Add(row.Root);
            }

            _menuPointer = Text(_menuLayer, "cb-caps", "▶");
            _menuPointer.style.color = Accent;
            _menuPointer.style.unityTextAlign = TextAnchor.MiddleCenter;
            _menuUp = Text(_menuLayer, "cb-small", "▲");
            _menuDown = Text(_menuLayer, "cb-small", "▼");
            _menuUp.style.color = _menuDown.style.color = Accent;

            _messagePlate = Plate(SmallCorner);
            _messageText = Text(_messagePlate, "cb-small");
            _messageText.style.color = new Color(1f, 0.55f, 0.45f);
            _messageText.style.unityTextAlign = TextAnchor.MiddleCenter;
            _menuLayer.Add(_messagePlate);
        }

        /// <summary>
        /// One bar of a menu: a dark fill with a light bevel along its top and left edges and a corner tick at the
        /// bottom right, its keycap, title, element rating, and note in a row. The selected bar gets a blue sweep and
        /// a gilt bevel.
        /// </summary>
        private MenuRow CreateMenuRow()
        {
            var root = Hud.Box("cb-menu-row").Absolute();
            root.style.backgroundColor = RowFill;
            var sweep = Hud.Tinted(UiTextures.FadeRight, ActiveSweep);
            sweep.style.left = sweep.style.top = sweep.style.right = sweep.style.bottom = 0f;
            var bevelTop = Hud.Tinted(UiTextures.FadeRight, RowEdge);
            bevelTop.style.left = bevelTop.style.top = bevelTop.style.right = 0f;
            bevelTop.style.height = 1f;
            var bevelLeft = Hud.Fill(RowEdge);
            bevelLeft.style.left = bevelLeft.style.top = bevelLeft.style.bottom = 0f;
            bevelLeft.style.width = 1f;
            var tickA = Hud.Fill(RowEdge);
            tickA.style.right = 0f;
            tickA.style.bottom = 0f;
            tickA.style.width = 7f;
            tickA.style.height = 1f;
            var tickB = Hud.Fill(RowEdge);
            tickB.style.right = 0f;
            tickB.style.bottom = 0f;
            tickB.style.width = 1f;
            tickB.style.height = 7f;
            root.Add(sweep);
            root.Add(bevelTop);
            root.Add(bevelLeft);
            root.Add(tickA);
            root.Add(tickB);

            var key = new KeyPrompt(20f).Classes("cb-menu-row__key");
            var title = Hud.Text(string.Empty, "cb-item", "cb-menu-row__title");
            var chip = new AffinityChip();
            var meta = Hud.Text(string.Empty, "cb-small", "cb-menu-row__meta");
            root.Add(key);
            root.Add(title);
            root.Add(chip);
            root.Add(meta);
            return new MenuRow { Root = root, Sweep = sweep, BevelTop = bevelTop, BevelLeft = bevelLeft, Key = key, Title = title, Chip = chip, Meta = meta };
        }

        private void BuildCenterPanel()
        {
            _centerPlate = Plate(20f);
            _centerTitle = Text(_centerPlate, "cb-number");
            _centerTitle.Place(new Rect(0f, 16f, 520f, 36f));
            _centerTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            AddDividerPair(_centerPlate, new Vector2(260f, 56.5f), 260f - 70f, 11f, 22f, MenuBorder);
            var jewel = new Diamond(9f).Absolute();
            jewel.Fill = Accent;
            jewel.CenterOn(new Vector2(260f, 56.5f));
            _centerPlate.Add(jewel);
            _centerBody = Text(_centerPlate, "cb-label");
            _centerBody.AddToClassList("cb-wrap");
            _centerBody.Place(new Rect(30f, 68f, 460f, 124f));
            _centerBody.style.unityTextAlign = TextAnchor.UpperCenter;
            _centerLayer.Add(_centerPlate);

            string[] choices = { "Restart Battle", "Respawn at Last Checkpoint" };
            for (var i = 0; i < choices.Length; i++)
            {
                var row = CreateMenuRow();
                row.Title.text = choices[i];
                row.Title.style.unityTextAlign = TextAnchor.MiddleCenter;
                row.Key.Show(false);
                row.Chip.Show(false);
                row.Meta.Show(false);
                var index = i;
                Clickable(row.Root, () => ChooseDefeatOption(index), () => _defeatCursor = index);
                _defeatRows.Add(row);
                _centerLayer.Add(row.Root);
            }

            _defeatPointer = Text(_centerLayer, "cb-caps", "▶");
            _defeatPointer.style.color = Accent;
            _defeatPointer.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        /// <summary>
        /// Two Kenney fading dividers meeting at <paramref name="center"/>, ornaments inward, <paramref name="gap"/>
        /// apart; a plain fading rule without the art.
        /// </summary>
        private static void AddDividerPair(VisualElement parent, Vector2 center, float halfWidth, float height, float gap, Color color)
        {
            var art = UiArt.Instance;
            if (art == null)
            {
                var rule = Hud.Tinted(UiTextures.FadeBoth, color);
                rule.Place(new Rect(center.x - halfWidth, center.y, halfWidth * 2f, 1f));
                parent.Add(rule);
                return;
            }

            const int dividerHeight = 22;
            var large = height * Screen.height / ReferenceHeight > dividerHeight * 1.25f;
            var factor = large ? 2 : 1;
            var texture = UiArt.Pick(art.dividerFades, art.dividerFadesDouble, art.dividerStyle, large);
            var y = center.y - height * 0.5f;
            for (var side = 0; side < 2; side++)
            {
                var divider = Hud.Box().Absolute();
                var x = side == 0 ? center.x - gap * 0.5f - halfWidth : center.x + gap * 0.5f;
                divider.Place(new Rect(x, y, halfWidth, height));
                divider.style.backgroundImage = new StyleBackground(texture);
                divider.style.unityBackgroundImageTintColor = color;
                divider.style.unitySliceLeft = 2 * factor;
                divider.style.unitySliceRight = 18 * factor;
                divider.style.unitySliceScale = height / (dividerHeight * factor);
                // The art ends in its ornament on the right; the right-hand divider is mirrored to face inward.
                if (side == 1) divider.style.scale = new Scale(new Vector3(-1f, 1f, 1f));
                parent.Add(divider);
            }
        }

        private void BuildTimingPrompt()
        {
            _ringDisc = Ring(64f, 0f, PanelColor);
            _ringDisc.style.backgroundColor = PanelColor;
            _ringBase = Ring(64f, 3f, TextColor);
            _ringClosing = Ring(64f, 3f, RedZone);
            _ringEmphasis = Ring(60f, 3f, RedZone);
            _promptLayer.Add(_ringDisc);
            _promptLayer.Add(_ringBase);
            _promptLayer.Add(_ringClosing);
            _promptLayer.Add(_ringEmphasis);
            _ringKey = new KeyPrompt(34f).Absolute();
            _promptLayer.Add(_ringKey);
            _ringGlyph = Text(_promptLayer, "cb-number");
            _ringGlyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            _promptTitle = Text(_promptLayer, "cb-title");
            _promptResult = Text(_promptLayer, "cb-title");
            _promptResult.style.unityTextAlign = TextAnchor.MiddleCenter;

            for (var i = 0; i < 2; i++)
            {
                var root = Hud.Box("cb-prompt-entry").Absolute();
                root.style.transformOrigin = new TransformOrigin(Length.Percent(0f), Length.Percent(50f));
                root.style.rotate = new Rotate(new Angle(7f, AngleUnit.Degree));
                var sweep = Hud.Tinted(UiTextures.FadeRight, ActiveSweep);
                sweep.style.left = sweep.style.top = sweep.style.right = sweep.style.bottom = 2f;
                var bar = Hud.Fill(Accent);
                bar.style.left = bar.style.top = bar.style.bottom = 0f;
                bar.style.width = 4f;
                var key = new KeyPrompt(22f).Classes("cb-prompt-entry__key");
                var title = Hud.Text(string.Empty, "cb-title", "cb-prompt-entry__title");
                root.Add(sweep);
                root.Add(bar);
                root.Add(key);
                root.Add(title);
                _promptLayer.Add(root);
                _promptEntries.Add(new PromptEntry { Root = root, Sweep = sweep, Bar = bar, Key = key, Title = title });
            }
        }

        /// <summary>An absolute circle <paramref name="size"/> across: a ring when <paramref name="width"/> is above zero.</summary>
        private static VisualElement Ring(float size, float width, Color color)
        {
            var ring = Hud.Box().Absolute();
            SizeRing(ring, Vector2.zero, size);
            if (width > 0f) Hud.SetBorder(ring, width, color);
            return ring;
        }

        private static void SizeRing(VisualElement ring, Vector2 center, float size)
        {
            ring.Place(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size));
            Hud.SetRadius(ring, size * 0.5f);
        }
        #endregion

        #region Turn order
        /// <summary>
        /// The Wait/Cast bar, laid across the top to the right of the party. Units travel left to right and act at
        /// the right end; the stretch past the chevrons is the cast zone. Party icons ride above the rail, enemies
        /// below it, each on a short stem.
        /// </summary>
        private void RefreshTurnOrder()
        {
            var timeline = Model.Timeline;
            var left = PartyLeft + PartyWidth + 90f;
            var right = _width - 70f;
            var length = right - left;
            var castX = right - length * timeline.CastLength / timeline.EndPosition;

            _railCloud.Place(new Rect(left - 90f, RailY - 56f, length + 180f, 112f));
            _castWash.Place(new Rect(castX, RailY - 40f, right - castX, 80f));
            _railLines[0].Place(new Rect(left, RailY - 2f, castX - left, 1f));
            _railLines[1].Place(new Rect(castX, RailY - 2f, right - castX, 1f));
            _railLines[2].Place(new Rect(left, RailY + 1f, castX - left, 1f));
            _railLines[3].Place(new Rect(castX, RailY + 1f, right - castX, 1f));
            _railStart.CenterOn(new Vector2(left, RailY));
            _railEndRing.CenterOn(new Vector2(right, RailY));
            _railEnd.CenterOn(new Vector2(right, RailY));
            _castMark.Place(new Rect(castX - 26f, RailY - 13f, 26f, 24f));
            _waitLabel.Place(new Rect(left - 66f, RailY - 9f, 56f, 18f));
            _actLabel.Place(new Rect(right + 14f, RailY - 9f, 50f, 18f));

            var seen = new HashSet<Combatant>();
            VisualElement actingIcon = null, actingStem = null;
            foreach (var entry in timeline.Entries)
            {
                var unit = (Combatant)entry.Unit;
                seen.Add(unit);
                if (!_timelineIcons.TryGetValue(unit, out var icon))
                {
                    icon = (Hud.Fill(Rail), new Emblem(EmblemSize).Absolute());
                    _turnLayer.Add(icon.stem);
                    _turnLayer.Add(icon.emblem);
                    _timelineIcons[unit] = icon;
                }

                var x = left + length * entry.Position / timeline.EndPosition;
                var isParty = unit.Side == BattleSide.Party;
                var active = IsActing(entry);
                var size = active ? ActiveEmblemSize : EmblemSize;
                var lift = size * 0.5f + EmblemStem;
                var center = new Vector2(x, isParty ? RailY - lift : RailY + lift);

                var rim = active ? Accent : entry.Phase == TimelinePhase.Casting ? CastRail : isParty ? ApColor : RedZone;
                icon.stem.Place(new Rect(x - 0.5f, Mathf.Min(RailY, center.y), 1f, lift));
                icon.stem.style.backgroundColor = rim;
                icon.emblem.Size = size;
                icon.emblem.SetCharacter(unit.Name, unit.Data != null ? unit.Data.Portrait : null, ElementColor(unit));
                icon.emblem.SetRim(rim);
                icon.emblem.CenterOn(center);
                icon.stem.Show(true);
                icon.emblem.Show(true);
                if (active)
                {
                    actingIcon = icon.emblem;
                    actingStem = icon.stem;
                }
            }

            // Whoever is acting draws last, so it sits on top of anyone sharing its spot.
            actingStem?.BringToFront();
            actingIcon?.BringToFront();

            foreach (var pair in _timelineIcons)
            {
                if (seen.Contains(pair.Key)) continue;
                pair.Value.stem.Show(false);
                pair.Value.emblem.Show(false);
            }
        }
        #endregion

        #region Context panel and banner
        /// <summary>
        /// The context panel at the bottom center, shown only while a party member is choosing. Aiming at an enemy,
        /// it shows that enemy's name, health, and what it is readying; otherwise it explains the highlighted
        /// option. Between turns it is hidden.
        /// </summary>
        private void RefreshContextPanel()
        {
            var box = new Rect(_width * 0.5f - 320f, ReferenceHeight - ContextPanelRise, 640f, 66f);
            _contextPlate.Place(new Rect(box.x - 6f, box.y - 4f, box.width + 12f, box.height + 8f));

            var awaiting = Model.State == BattleState.AwaitingCommand;
            var enemy = awaiting && IsTargeting && CurrentTarget.Side == BattleSide.Enemy ? CurrentTarget : null;
            var option = awaiting && enemy == null && _cursor < _options.Count && !string.IsNullOrEmpty(_options[_cursor].Detail) ? _options[_cursor] : null;
            _contextPlate.Show(enemy != null || option != null);
            _enemyInfo.Show(enemy != null);
            _description.Show(option != null);

            if (enemy != null) RefreshEnemyInfo(enemy);
            else if (option != null) RefreshDescription(option);
        }

        private void RefreshEnemyInfo(Combatant enemy)
        {
            _enemyName.text = enemy.Name;
            _enemyAffinities.Set(KnownReactions(enemy), StrongColor, WeakColor, ImmuneColor);
            _enemyHealth.text = $"{enemy.Stats.CurrentHealth} / {enemy.Stats.MaxHealth}";
            _enemyBar.Set(enemy.Stats.CurrentHealth, enemy.Stats.MaxHealth, HealthColor);

            // A thin line under the bar: how far through its cast the enemy is.
            var entry = Model.Timeline.Contains(enemy) ? Model.Timeline.Get(enemy) : null;
            var casting = entry != null && entry.Phase == TimelinePhase.Casting && Model.Timeline.CastLength > 0f;
            _enemyCast.Show(casting);
            if (casting)
            {
                var progress = Mathf.Clamp01((entry.Position - Model.Timeline.WaitLength) / Model.Timeline.CastLength);
                _enemyCast.Place(new Rect(16f, 41f, 608f * progress, 2f));
            }

            var intent = Model.GetIntent(enemy);
            _enemyIntent.Show(intent != null);
            if (intent == null) return;

            var hits = intent.Ability.HitCount > 1 ? $" ×{intent.Ability.HitCount}" : string.Empty;
            var target = intent.Target != null ? $"  →  {intent.Target.Name}" : string.Empty;
            var traits = CombatManager.DescribeIntentTraits(intent);
            if (traits.Length > 0) traits = $"  [{traits}]";
            _enemyIntent.text = $"Casting  {intent.Ability.Name}{hits}{target}{traits}";
        }

        /// <summary>The highlighted option explained inside the context panel, with its cast speed in a cell on the right.</summary>
        private void RefreshDescription(Option option)
        {
            var textWidth = 640f - 32f;
            var hasCast = !string.IsNullOrEmpty(option.Cast);
            _castCell.Show(hasCast);
            if (hasCast)
            {
                _castValue.text = option.Cast;
                textWidth -= 132f;
            }

            _detail.Place(new Rect(16f, 2f, textWidth, 62f));
            _detail.text = option.Enabled || string.IsNullOrEmpty(option.Reason) ? option.Detail : $"{option.Detail}  ({option.Reason})";
            _detail.style.color = option.Enabled ? TextColor : Muted;
        }

        /// <summary>The current action's headline, under the turn order.</summary>
        private void RefreshBanner()
        {
            var banner = Manager.Banner;
            _bannerPlate.Show(!string.IsNullOrEmpty(banner));
            if (string.IsNullOrEmpty(banner)) return;

            _bannerPlate.Place(new Rect(_width * 0.5f - 210f, 112f, 420f, 36f));
            _bannerText.text = banner;
        }
        #endregion

        #region Enemy plates and targets
        /// <summary>Slim health bars under each enemy's feet, with the break meter, discovered weaknesses, and revealed intent.</summary>
        private void RefreshEnemyPlates()
        {
            var enemies = Model.Enemies;
            while (_plates.Count < enemies.Count) _plates.Add(CreatePlate());

            for (var i = 0; i < _plates.Count; i++)
            {
                var plate = _plates[i];
                var enemy = i < enemies.Count ? enemies[i] : null;
                var shown = enemy != null && enemy.IsAlive && TryPanelPoint(Manager.GetView(enemy), 0f, out var feet);
                plate.Root.Show(shown);
                if (!shown) continue;

                plate.Unit = enemy;
                TryPanelPoint(Manager.GetView(enemy), 0f, out feet);
                plate.Root.Move(feet);
                plate.Bar.Set(enemy.Stats.CurrentHealth, enemy.Stats.MaxHealth, HealthColor);
                plate.Name.text = enemy.Name;

                var line = 30f;
                plate.BreakBack.Show(enemy.CanBreak);
                plate.Break.Show(enemy.CanBreak);
                if (enemy.CanBreak)
                {
                    plate.BreakBack.Place(new Rect(-51f, line + 1f, 102f, 5f));
                    plate.Break.Place(new Rect(-50f, line + 2f, 100f, 3f));
                    plate.Break.Set(enemy.BreakMeter, enemy.BreakMax, BreakColor);
                    line += 7f;
                }

                var known = KnownReactions(enemy);
                plate.Affinities.Show(known.Count > 0);
                if (known.Count > 0)
                {
                    plate.Affinities.Set(known, StrongColor, WeakColor, ImmuneColor);
                    plate.Affinities.Move(new Vector2(-known.Count * AffinityRow.ChipWidth * 0.5f, line - 1f));
                    line += 15f;
                }

                plate.Broken.Show(enemy.IsBroken);
                if (enemy.IsBroken)
                {
                    plate.Broken.Place(new Rect(-80f, line, 160f, 16f));
                    line += 14f;
                }

                RefreshIntent(plate, Model.GetIntent(enemy), line + IntentIconSize * 0.5f + 6f);
            }
        }

        private const float IntentIconSize = 24f;

        private EnemyPlate CreatePlate()
        {
            var plate = new EnemyPlate { Root = Hud.Box().Absolute() };
            var root = plate.Root;
            var back = Hud.Fill(PanelColor);
            back.Place(new Rect(-51f, 9f, 102f, 6f));
            root.Add(back);
            plate.Bar = new HudBar();
            plate.Bar.Place(new Rect(-50f, 10f, 100f, 4f));
            root.Add(plate.Bar);
            plate.Name = Text(root, "cb-small");
            plate.Name.Place(new Rect(-80f, 14f, 160f, 16f));
            plate.Name.style.unityTextAlign = TextAnchor.UpperCenter;
            plate.BreakBack = Hud.Fill(PanelColor);
            root.Add(plate.BreakBack);
            plate.Break = new HudBar();
            root.Add(plate.Break);
            plate.Affinities = new AffinityRow();
            root.Add(plate.Affinities);
            plate.Broken = Text(root, "cb-small", "BROKEN");
            plate.Broken.style.color = BreakColor;
            plate.Broken.style.unityTextAlign = TextAnchor.UpperCenter;

            plate.IntentTarget = new Emblem(IntentIconSize).Absolute();
            plate.AllBadge = new Diamond(IntentIconSize + 8f).Absolute();
            plate.AllBadge.Fill = PanelColor;
            plate.AllBadge.SetRim(2f, RedZone);
            plate.AllLabel = Text(root, "cb-tag", "ALL");
            plate.AllLabel.style.color = RedZone;
            plate.AllLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            plate.Arrow = Text(root, "cb-caps", "›");
            plate.Arrow.style.color = RedZone;
            plate.Arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            plate.ElementBack = new Diamond(IntentIconSize + 8f).Absolute();
            plate.ElementBack.Fill = PanelColor;
            plate.ElementBack.SetRim(2f, RedZone);
            plate.ElementFill = new Diamond(IntentIconSize * 0.75f).Absolute();
            plate.Notes = Text(root, "cb-small");
            plate.Notes.style.color = RedZone;
            plate.Notes.style.unityTextAlign = TextAnchor.UpperCenter;
            root.Add(plate.IntentTarget);
            root.Add(plate.AllBadge);
            plate.AllLabel.BringToFront();
            root.Add(plate.ElementBack);
            root.Add(plate.ElementFill);
            _plateLayer.Add(root);
            return plate;
        }

        /// <summary>
        /// An enemy's revealed intent as two badges centered on y = <paramref name="centerY"/>: who it is aiming at,
        /// then the element it attacks with. Hit count and special rules sit in a small line underneath.
        /// </summary>
        private void RefreshIntent(EnemyPlate plate, EnemyIntent intent, float centerY)
        {
            var shown = intent != null;
            var aim = shown ? IntentAim(intent) : null;
            plate.IntentTarget.Show(shown && aim != null);
            plate.AllBadge.Show(shown && aim == null);
            plate.AllLabel.Show(shown && aim == null);
            plate.Arrow.Show(shown);
            plate.ElementBack.Show(shown);
            plate.ElementFill.Show(shown);
            plate.Notes.Show(shown);
            if (!shown) return;

            // Each badge's backing is 8 units wider than its icon.
            var offset = (IntentIconSize + 8f + 10f) * 0.5f;
            var targetCenter = new Vector2(-offset, centerY);
            var elementCenter = new Vector2(offset, centerY);
            if (aim != null)
            {
                plate.IntentTarget.SetCharacter(aim.Name, aim.Data != null ? aim.Data.Portrait : null, ElementColor(aim));
                plate.IntentTarget.SetRim(RedZone);
                plate.IntentTarget.CenterOn(targetCenter);
            }
            else
            {
                plate.AllBadge.CenterOn(targetCenter);
                plate.AllLabel.Place(new Rect(targetCenter.x - IntentIconSize * 0.5f, centerY - IntentIconSize * 0.5f, IntentIconSize, IntentIconSize));
            }

            plate.Arrow.Place(new Rect(-8f, centerY - 9f, 16f, 18f));
            plate.ElementBack.CenterOn(elementCenter);
            plate.ElementFill.Fill = Characters.PlaceholderCharacter.ElementColor(intent.Element);
            plate.ElementFill.CenterOn(elementCenter);

            plate.Notes.text = IntentNotes(intent);
            plate.Notes.Place(new Rect(-110f, centerY + IntentIconSize * 0.5f + 5f, 220f, 16f));
        }

        /// <summary>
        /// While aiming: corner brackets around the current target, and a clickable area over every valid target so
        /// the mouse can hover to aim and click to confirm.
        /// </summary>
        private void RefreshTargets()
        {
            if (_targetBrackets == null)
            {
                _targetBrackets = new Brackets();
                _targetLayer.Add(_targetBrackets);
            }

            var aiming = IsTargeting;
            var count = aiming ? _targets.Count : 0;
            while (_targetBoxes.Count < count)
            {
                var box = Hud.Box().Absolute();
                Clickable(box, () => AimAt(box.userData as Combatant, true), () => AimAt(box.userData as Combatant, false));
                _targetLayer.Insert(0, box);
                _targetBoxes.Add(box);
            }

            var bracketed = false;
            for (var i = 0; i < _targetBoxes.Count; i++)
            {
                var box = _targetBoxes[i];
                var shown = i < count && TryCharacterRect(Manager.GetView(_targets[i]), out var rect);
                box.Show(shown);
                if (!shown) continue;

                TryCharacterRect(Manager.GetView(_targets[i]), out rect);
                box.userData = _targets[i];
                box.Place(rect);
                if (i != _targetIndex) continue;

                var pulse = 4f + 3f * Mathf.Sin(Time.time * 6f);
                _targetBrackets.Set(new Rect(rect.x - pulse, rect.y - pulse, rect.width + pulse * 2f, rect.height + pulse * 2f), Accent, 16f, 3f);
                bracketed = true;
            }

            _targetBrackets.Show(bracketed);
        }
        #endregion

        #region Party
        /// <summary>
        /// The party, stacked down from the top left. Idle entries have no backing; the acting member's entry
        /// steps out toward the field on a blue sweep, and an ally being aimed at gets brackets.
        /// </summary>
        private void RefreshParty()
        {
            if (_cards.Count != Model.Party.Count || _cards.Where((card, i) => card.Unit != Model.Party[i]).Any())
            {
                _partyLayer.Clear();
                _cards.Clear();
                foreach (var member in Model.Party) _cards.Add(CreateCard(member));
            }

            var y = PartyTop;
            foreach (var card in _cards)
            {
                var member = card.Unit;
                var isActor = member == Model.CurrentActor || (Manager.CurrentExecution != null && Manager.CurrentExecution.Actor == member);
                var isTarget = member == CurrentTarget;

                _stepOut.TryGetValue(member, out var step);
                step = Mathf.MoveTowards(step, isActor ? 1f : 0f, Time.unscaledDeltaTime / StepOutSeconds);
                _stepOut[member] = step;

                var eased = 1f - (1f - step) * (1f - step);
                var rect = new Rect(PartyLeft + ActorStepOut * eased, y, PartyWidth, PartyEntryHeight);
                var tint = member.IsAlive ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                y += PartyEntryHeight + PartyGap;

                // Only the acting member's entry gets a backing sweep, fading in as it slides out; the rest float bare.
                card.Sweep.Show(step > 0f);
                card.SweepLine.Show(step > 0f);
                if (step > 0f)
                {
                    var sweep = new Rect(0f, rect.y - 3f, rect.xMax + 50f, rect.height + 6f);
                    card.Sweep.Place(sweep);
                    card.Sweep.style.unityBackgroundImageTintColor = ActiveSweep.Alpha(eased);
                    card.SweepLine.Place(new Rect(sweep.x, sweep.yMax, sweep.width, 1f));
                    card.SweepLine.style.unityBackgroundImageTintColor = Accent.Alpha(eased);
                }

                card.Brackets.Show(isTarget);
                if (isTarget) card.Brackets.Set(new Rect(rect.x - 8f, rect.y - 5f, rect.width + 16f, rect.height + 10f), Accent, 12f, 2f);

                card.Card.Place(rect);
                card.Emblem.SetCharacter(member.Name, member.Data != null ? member.Data.Portrait : null, ElementColor(member) * tint);
                card.Emblem.SetRim(isActor ? Accent : Rail);
                card.Emblem.SetTint(tint);

                var text = TextColor * tint;
                card.Name.text = member.IsAlive ? member.Name : $"{member.Name} · down";
                card.Name.style.color = text;

                const float rowWidth = PartyWidth - 48f;
                var pipStep = Mathf.Min(12f, (rowWidth - 40f) / Mathf.Max(1, member.Stats.MaxAp));
                card.Pips.Set(member.Stats.MaxAp, member.Stats.CurrentAp, pipStep, ApColor * tint, PipEmpty);
                card.ApTag.style.color = ApColor * tint;

                card.HpTag.style.color = text;
                card.Hp.text = member.Stats.CurrentHealth.ToString("N0");
                card.Hp.style.color = text;
                card.HpMax.text = $"/ {member.Stats.MaxHealth:N0}";
                card.HpMax.style.color = text;
                card.HpBar.Set(member.Stats.CurrentHealth, member.Stats.MaxHealth, PartyHealthColor * tint);

                // Surge: a thinner second row, stepped in like a sub-stat. It pulses when full.
                var gauge = member.SurgeGauge;
                var surgeColor = gauge.IsFull ? Color.Lerp(SurgeColor, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 5f)) : SurgeColor;
                card.SurgeTag.text = gauge.IsFull ? "READY" : "SURGE";
                card.SurgeTag.style.color = surgeColor * tint;
                card.SurgeBar.Set(gauge.Current, gauge.Max, surgeColor * tint);
            }
        }

        private PartyCard CreateCard(Combatant member)
        {
            const float textX = 48f;
            const float rowWidth = PartyWidth - textX;
            var card = new PartyCard
            {
                Unit = member,
                Sweep = Hud.Tinted(UiTextures.FadeRight, ActiveSweep),
                SweepLine = Hud.Tinted(UiTextures.FadeRight, Accent),
                Brackets = new Brackets(),
                Card = Hud.Box("cb-card").Absolute()
            };
            _partyLayer.Add(card.Sweep);
            _partyLayer.Add(card.SweepLine);
            _partyLayer.Add(card.Brackets);
            _partyLayer.Add(card.Card);

            // Ally targets can be picked from their card too.
            Clickable(card.Card, () => AimAt(member, true), () => AimAt(member, false));

            var root = card.Card;
            card.Emblem = new Emblem(32f).Absolute();
            card.Emblem.CenterOn(new Vector2(20f, 30f));
            root.Add(card.Emblem);

            card.Name = Text(root, "cb-title");
            card.Name.Place(new Rect(textX, 0f, rowWidth, 22f));
            card.Name.style.unityTextAlign = TextAnchor.MiddleLeft;

            card.Pips = new PipRow();
            card.Pips.Move(new Vector2(textX + 5f, 27f));
            root.Add(card.Pips);
            card.ApTag = Text(root, "cb-tag", "AP");
            card.ApTag.Place(new Rect(PartyWidth - 34f, 19f, 30f, 16f));
            card.ApTag.style.unityTextAlign = TextAnchor.MiddleRight;

            // HP: the current value large and the maximum small, over a thin bar.
            var hp = Hud.Box("cb-row", "cb-hp").Absolute();
            hp.Place(new Rect(textX, 31f, rowWidth, 24f));
            card.HpTag = Hud.Text("HP", "cb-tag", "cb-hp__tag");
            card.Hp = Hud.Text(string.Empty, "cb-stat");
            card.HpMax = Hud.Text(string.Empty, "cb-small", "cb-hp__max");
            hp.Add(card.HpTag);
            hp.Add(card.Hp);
            hp.Add(card.HpMax);
            root.Add(hp);
            card.HpBar = new HudBar();
            card.HpBar.Place(new Rect(textX, 56f, rowWidth - 8f, 3f));
            root.Add(card.HpBar);

            card.SurgeTag = Text(root, "cb-tag");
            card.SurgeTag.Place(new Rect(textX + 10f, 60f, 44f, 14f));
            card.SurgeTag.style.unityTextAlign = TextAnchor.MiddleLeft;
            card.SurgeBar = new HudBar();
            card.SurgeBar.Place(new Rect(textX + 50f, 66f, rowWidth - 58f, 3f));
            root.Add(card.SurgeBar);
            return card;
        }
        #endregion

        #region Command menu
        /// <summary>
        /// The command menu: an upright box beside the acting character; the context panel describes the
        /// highlighted option. While aiming, the box shrinks to the chosen action above the character's head, clear
        /// of the allies it might be aimed at, and its element rating follows the target.
        /// </summary>
        private void RefreshCommandMenu(bool awaiting)
        {
            var actor = awaiting ? Model.CurrentActor : null;
            _menuLayer.Show(actor != null);
            if (actor == null) return;

            var aiming = _menu == Menu.Target;
            if (!TryPanelPoint(Manager.GetView(actor), aiming ? 2.3f : 1.1f, out var anchor))
                anchor = new Vector2(_width * 0.3f, ReferenceHeight * 0.5f);

            var box = RefreshMenuBox(actor, anchor, aiming ? _menuBeforeTarget : _menu, aiming);

            var showMessage = !string.IsNullOrEmpty(_message) && Time.time - _messageTime < 2f;
            _messagePlate.Show(showMessage);
            if (!showMessage) return;

            // Clear of the menu's frame, which reaches 33 units above its rows.
            var note = new Rect(box.x, box.y - 66f, Mathf.Max(box.width, 300f), 24f);
            _messagePlate.Place(new Rect(note.x - 8f, note.y - 3f, note.width + 16f, note.height + 6f));
            _messageText.Place(new Rect(8f, 3f, note.width, note.height));
            _messageText.text = _message;
        }

        /// <summary>
        /// One list of the command menu: a tab with the name and AP pips on top, then a separate bar per option. The
        /// highlighted bar gets a blue sweep, with a pointer beside it. Rows scroll once there are more than
        /// <see cref="MaxVisibleRows"/>. When <paramref name="aiming"/>, only the chosen row shows, above
        /// <paramref name="anchor"/>, and it can't be clicked. Returns the box.
        /// </summary>
        private Rect RefreshMenuBox(Combatant actor, Vector2 anchor, Menu list, bool aiming)
        {
            const float tabHeight = 22f;
            var boxWidth = list == Menu.Root ? 200f : 300f;
            var first = aiming ? _cursor : Mathf.Clamp(_cursor - MaxVisibleRows / 2, 0, Mathf.Max(0, _options.Count - MaxVisibleRows));
            var count = aiming ? Mathf.Min(1, _options.Count) : Mathf.Min(MaxVisibleRows, _options.Count);
            var boxHeight = Mathf.Max(0f, count * RowHeight - RowGap);
            _menuFirst = first;
            _menuAiming = aiming;

            var wanted = aiming
                ? new Vector2(anchor.x - 40f, anchor.y - boxHeight - 30f)
                : new Vector2(anchor.x + 60f, anchor.y - boxHeight * 0.5f - tabHeight);
            var origin = ClampToScreen(wanted, new Vector2(boxWidth + 24f, boxHeight + tabHeight + 4f), _width);
            var box = new Rect(origin.x, origin.y + tabHeight + 4f, boxWidth, boxHeight);

            // Tab: whose turn it is, or which list this is, with the actor's AP on the right. It fades in from the
            // left and is underlined in gilt.
            var tab = new Rect(box.x, box.y - tabHeight - 4f, boxWidth, tabHeight);
            _menuFrame.Place(new Rect(box.x - 7f, tab.y - 7f, box.width + 14f, box.yMax - tab.y + 14f));
            _menuTab.Place(tab);
            _menuTabLine.Place(new Rect(tab.x, tab.yMax, tab.width, 1f));
            _menuTabLabel.Place(new Rect(tab.x + 6f, tab.y, boxWidth, tab.height));
            _menuTabLabel.text = list == Menu.Root ? actor.Name : list.ToString();

            var pips = actor.Stats.MaxAp;
            var pipStep = Mathf.Min(11f, boxWidth * 0.45f / Mathf.Max(1, pips));
            var pipsX = tab.xMax - 8f - pips * pipStep;
            _menuApLabel.Place(new Rect(pipsX - 30f, tab.y, 26f, tab.height));
            _menuPips.Move(new Vector2(pipsX + pipStep * 0.5f, tab.center.y));
            _menuPips.Set(pips, actor.Stats.CurrentAp, pipStep, ApColor, PipEmpty);

            var pointerShown = false;
            for (var slot = 0; slot < _menuRows.Count; slot++)
            {
                var row = _menuRows[slot];
                var i = first + slot;
                var shown = slot < count && i < _options.Count;
                row.Root.Show(shown);
                if (!shown) continue;

                var option = _options[i];
                var selected = i == _cursor;
                var rect = new Rect(box.x, box.y + slot * RowHeight, box.width, RowHeight - RowGap);
                row.Root.Place(rect);
                row.Root.pickingMode = aiming ? PickingMode.Ignore : PickingMode.Position;
                SetRowSelected(row, selected);
                if (selected)
                {
                    _menuPointer.Place(new Rect(box.x - 24f, rect.y, 20f, rect.height));
                    pointerShown = true;
                }

                // The shortcut's keycap sits before the title, while its button is bound on the current scheme.
                var shortcut = option.Shortcut != null ? Input.Glyph(option.Shortcut.Value) : null;
                row.Key.Show(shortcut != null);
                if (shortcut != null) row.Key.Set(shortcut, Input.UsingGamepad);

                row.Title.text = option.Title;
                row.Title.style.color = option.Enabled ? TextColor : Muted;

                row.Meta.Show(!string.IsNullOrEmpty(option.Meta));
                row.Meta.text = option.Meta ?? string.Empty;
                row.Meta.style.color = option.Meta == "READY" ? SurgeColor : !option.Enabled ? Muted : selected ? TextColor : ApColor;

                var rating = Matchup(actor, option.Action);
                row.Chip.Show(rating != null);
                if (rating != null) row.Chip.Set(rating.Value, StrongColor, WeakColor, ImmuneColor);
            }

            _menuPointer.Show(pointerShown);
            _menuUp.Show(!aiming && first > 0);
            _menuDown.Show(!aiming && first + count < _options.Count);
            _menuUp.Place(new Rect(box.xMax + 2f, box.y, 16f, 12f));
            _menuDown.Place(new Rect(box.xMax + 2f, box.yMax - 12f, 16f, 12f));
            return box;
        }

        private static void SetRowSelected(MenuRow row, bool selected)
        {
            row.Sweep.Show(selected);
            var bevel = selected ? Accent : RowEdge;
            row.BevelTop.style.unityBackgroundImageTintColor = bevel;
            row.BevelLeft.style.backgroundColor = bevel;
        }
        #endregion

        #region Defeat
        private void RefreshCenterPanel()
        {
            var defeat = Model.State == BattleState.Defeat;
            _centerLayer.Show(defeat);
            if (!defeat) return;

            _centerPlate.Place(new Rect(_width * 0.5f - 260f, 220f, 520f, 200f));
            _centerTitle.text = "Defeat";
            _centerBody.text = string.Empty;

            _defeatPointer.Show(true);
            for (var i = 0; i < _defeatRows.Count; i++)
            {
                var row = _defeatRows[i];
                row.Root.Show(true);

                var rect = new Rect(_width * 0.5f - 160f, 300f + i * 40f, 320f, 32f);
                row.Root.Place(rect);
                var selected = i == _defeatCursor;
                SetRowSelected(row, selected);
                if (selected) _defeatPointer.Place(new Rect(rect.x - 24f, rect.y, 20f, rect.height));
            }
        }
        #endregion

        #region Timing prompts
        private void RefreshTimingPrompts()
        {
            var defense = Manager.ActiveDefense;
            var qte = Manager.ActiveQte;
            _promptLayer.Show(defense != null || qte != null);
            if (defense != null) RefreshDefensePrompt(defense);
            else if (qte != null) RefreshQtePrompt(qte);
        }

        /// <summary>
        /// Shown on the character being attacked, where the command menu would be: a fixed ring with a second ring
        /// closing in on it that meets it at the moment of impact, and the Parry and Dodge keys beside it. The
        /// closing ring turns white inside the parry window and blue inside the wider dodge window.
        /// </summary>
        private void RefreshDefensePrompt(DefensePrompt prompt)
        {
            var center = ClampPrompt(DefenseAnchor(), _width);
            var lead = prompt.ImpactTime - prompt.ShownAt;
            var remaining = prompt.ImpactTime - Time.time;
            var t = Mathf.Clamp01(remaining / lead);

            var tuning = Manager.Tuning;
            var inParry = Mathf.Abs(remaining) <= tuning.ParryWindow;
            var inDodge = Mathf.Abs(remaining) <= tuning.DodgeWindow;
            var closing = inParry ? ParryColor : inDodge ? DodgeColor : prompt.IsGuardCounter ? Muted : RedZone;
            var glyph = prompt.Input == DefenseInput.None ? "!" : prompt.Input == DefenseInput.Parry ? Input.Glyph(CombatButton.Parry, true) : Input.Glyph(CombatButton.Dodge, true);

            SetTimingRings(center, t, closing, inParry || inDodge, glyph);

            var hits = prompt.HitCount > 1 ? $"  {prompt.HitNumber}/{prompt.HitCount}" : string.Empty;
            var rule = prompt.Rule == DefenseRule.DodgeOnly ? " — DODGE ONLY" : prompt.Rule == DefenseRule.ParryOnly ? " — PARRY ONLY" : string.Empty;
            _promptTitle.text = prompt.IsGuardCounter ? $"{prompt.Attacker.Name} counters{rule}" : $"{prompt.Attacker.Name} strikes{hits}{rule}";
            _promptTitle.style.color = rule.Length > 0 ? RedZone : TextColor;
            _promptTitle.Place(new Rect(center.x - 40f, center.y - 92f, 440f, 26f));
            _promptResult.Show(false);

            var parried = prompt.Input == DefenseInput.Parry;
            var dodged = prompt.Input == DefenseInput.Dodge;
            // Only the inputs the hit's defense rule allows are offered.
            if (prompt.Rule == DefenseRule.DodgeOnly) SetPromptEntries(center, ("Dodge", Input.Glyph(CombatButton.Dodge), dodged));
            else if (prompt.Rule == DefenseRule.ParryOnly) SetPromptEntries(center, ("Parry", Input.Glyph(CombatButton.Parry), parried));
            else SetPromptEntries(center, ("Parry", Input.Glyph(CombatButton.Parry), parried), ("Dodge", Input.Glyph(CombatButton.Dodge), dodged));
        }

        /// <summary>The ability QTE, shown on the acting character: press as the closing ring meets the fixed one.</summary>
        private void RefreshQtePrompt(AbilityQtePrompt prompt)
        {
            var center = ClampPrompt(CharacterChest(Manager.CurrentExecution?.Actor), _width);
            var remaining = prompt.TargetTime - Time.time;
            var t = Mathf.Clamp(remaining / (prompt.Duration * 0.5f), -1f, 1f);
            var inZone = Mathf.Abs(remaining) <= prompt.HalfWindow;

            if (prompt.Result == QteResult.None) SetTimingRings(center, Mathf.Max(-0.7f, t), inZone ? QteZone : Muted, inZone, "!");
            else SetTimingRings(center, 0f, prompt.Result == QteResult.Fail ? Muted : QteZone, false, "•");

            _promptTitle.text = prompt.ActionName;
            _promptTitle.style.color = TextColor;
            _promptTitle.Place(new Rect(center.x - 40f, center.y - 92f, 440f, 26f));

            _promptResult.Show(prompt.Result != QteResult.None);
            if (prompt.Result != QteResult.None)
            {
                _promptResult.text = prompt.Result.ToString();
                _promptResult.style.color = prompt.Result == QteResult.Fail ? Muted : QteZone;
                _promptResult.Place(new Rect(center.x - 220f, center.y + 52f, 440f, 26f));
            }

            SetPromptEntries(center, ("Strike", Input.Glyph(CombatButton.Qte), prompt.Result != QteResult.None && prompt.Result != QteResult.Fail));
        }

        /// <summary>The shared timing graphic: a fixed ring, and a ring <paramref name="t"/> of the way out from it.</summary>
        private void SetTimingRings(Vector2 center, float t, Color closing, bool emphasise, string glyph)
        {
            const float baseSize = 64f;
            SizeRing(_ringDisc, center, baseSize);
            SizeRing(_ringBase, center, baseSize);

            var size = Mathf.Max(1f, baseSize * (1f + 2.2f * t));
            SizeRing(_ringClosing, center, size);
            Hud.SetBorder(_ringClosing, 3f, closing);
            _ringEmphasis.Show(emphasise);
            if (emphasise)
            {
                SizeRing(_ringEmphasis, center, Mathf.Max(1f, size - 4f));
                Hud.SetBorder(_ringEmphasis, 3f, closing);
            }

            // The button's icon when it has one; otherwise its name, or a mark such as "!".
            var icons = InputPrompts.Icons(glyph, Input.UsingGamepad);
            var useIcon = icons != null && icons.Length == 1;
            _ringKey.Show(useIcon);
            _ringGlyph.Show(!useIcon);
            if (useIcon)
            {
                _ringKey.Set(glyph, Input.UsingGamepad);
                _ringKey.Move(new Vector2(center.x - _ringKey.IconWidth * 0.5f, center.y - 17f));
            }
            else
            {
                _ringGlyph.text = glyph;
                _ringGlyph.Place(new Rect(center.x - 30f, center.y - 20f, 60f, 40f));
            }
        }

        /// <summary>Key prompts laid out like the command menu, stepping down beside a timing ring, tilted a little.</summary>
        private void SetPromptEntries(Vector2 center, params (string label, string key, bool highlighted)[] entries)
        {
            var origin = new Vector2(center.x + 60f, center.y - 30f);
            for (var i = 0; i < _promptEntries.Count; i++)
            {
                var entry = _promptEntries[i];
                entry.Root.Show(i < entries.Length);
                if (i >= entries.Length) continue;

                var (label, key, highlighted) = entries[i];
                // Wide enough for a mouse-and-key pair such as "LMB / E" beside the label.
                entry.Root.Place(new Rect(origin.x + i * 16f, origin.y + i * 40f, 200f, 34f));
                entry.Root.style.backgroundColor = highlighted ? PanelColor : PanelSoft;
                Hud.SetBorder(entry.Root, 2f, highlighted ? Accent : Color.clear);
                entry.Sweep.Show(highlighted);
                entry.Bar.Show(highlighted);
                entry.Key.Show(!string.IsNullOrEmpty(key));
                if (!string.IsNullOrEmpty(key)) entry.Key.Set(key, Input.UsingGamepad);
                entry.Title.text = label;
                entry.Title.style.color = highlighted ? Accent : TextColor;
            }
        }

        /// <summary>Where the defense prompt goes: on the attacked party member, or the middle of the party for a sweep.</summary>
        private Vector2 DefenseAnchor()
        {
            var target = Manager.CurrentExecution?.ChosenTarget;
            if (target != null && target.Side == BattleSide.Party && TryPanelPoint(Manager.GetView(target), 1f, out var point))
                return point;

            var points = new List<Vector2>();
            foreach (var member in Model.Party.Where(m => m.IsAlive))
            {
                if (TryPanelPoint(Manager.GetView(member), 1f, out var memberPoint)) points.Add(memberPoint);
            }

            return points.Count > 0
                ? new Vector2(points.Average(p => p.x), points.Average(p => p.y))
                : new Vector2(_width * 0.5f, ReferenceHeight * 0.48f);
        }

        private Vector2 CharacterChest(Combatant unit) =>
            TryPanelPoint(Manager.GetView(unit), 1f, out var point) ? point : new Vector2(_width * 0.5f, ReferenceHeight * 0.48f);
        #endregion

        #region Hints
        /// <summary>Key prompts in the bottom left, as keycaps with labels. Ones with an action can be clicked.</summary>
        private void RefreshHints()
        {
            var gamepad = Input.UsingGamepad;
            switch (Model.State)
            {
                case BattleState.AwaitingCommand when Model.CurrentActor != null:
                    if (_menu == Menu.Target)
                        _hints.Set(gamepad, Hint("←→", "Target"), Hint(ConfirmKey, "Confirm", ConfirmTarget), Hint(CancelKey, "Back", Back));
                    // On a controller every root row shows its own button, so the cursor hints would only mislead.
                    else if (_menu == Menu.Root)
                        _hints.Set(gamepad, gamepad ? Array.Empty<HintBar.Hint>() : new[] { Hint("↑↓", "Choose"), Hint(ConfirmKey, "Confirm", () => Activate(_cursor)) });
                    else
                        _hints.Set(gamepad, Hint("↑↓", "Choose"), Hint(ConfirmKey, "Confirm", () => Activate(_cursor)), Hint(CancelKey, "Back", Back));
                    break;
                case BattleState.Defeat:
                    _hints.Set(gamepad, Hint("↑↓", "Choose"), Hint(ConfirmKey, "Confirm", () => ChooseDefeatOption(_defeatCursor)));
                    break;
                default:
                    _hints.Set(gamepad);
                    break;
            }
        }

        private static HintBar.Hint Hint(string key, string label, Action click = null) => new HintBar.Hint(key, label, click);
        #endregion
    }
}
