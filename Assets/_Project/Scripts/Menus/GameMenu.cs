using System.Collections.Generic;
using System.Linq;
using InElementum.Core.Managers;
using InElementum.Core.UI;
using InElementum.Core.Utils;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Encounters;
using InElementum.Exploration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace InElementum.Menus
{
    /// <summary>
    /// The pause menu, in UI Toolkit and in the same look as the combat HUD: a category list of bevelled bars on the
    /// left (Party, Inventory, Close), the party's element diamonds beside it, the chosen screen on a framed ink plate,
    /// and key prompts along the bottom. Choosing a member on the Party screen opens their character pages (Weapon,
    /// Pictos, Skills): Previous/Next switch member and PreviousTab/NextTab switch page. Opens with the Menu map's Toggle while
    /// exploring, pauses the game, and edits the room's <see cref="PartyService"/> directly. The layout lives in
    /// Resources/UI/GameMenu.uxml and the look in GameMenu.uss, on the shared HUD panel (720 units tall).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class GameMenu : MonoBehaviour
    {
        public const string LayoutName = "GameMenu";
        private const float StripDiamond = 30f;
        private const float StripSpriteScale = 0.9f;

        [Tooltip("Optional. Defaults to Resources/UI/GameMenu.")]
        [SerializeField] private VisualTreeAsset layout;
        [Tooltip("Optional. Defaults to the room's EncounterDirector.")]
        [SerializeField] private EncounterDirector director;
        [Tooltip("Optional. Defaults to the project-wide input actions.")]
        [SerializeField] private InputActionAsset inputActions;

        private readonly List<MenuScreen> _screens = new List<MenuScreen>();
        private readonly List<MenuScreen> _pages = new List<MenuScreen>();
        private readonly List<VisualElement> _navItems = new List<VisualElement>();
        private readonly List<VisualElement> _tabItems = new List<VisualElement>();
        private readonly List<PartyMemberData> _members = new List<PartyMemberData>();
        private readonly List<(VisualElement tile, CharacterFigure figure)> _tiles = new List<(VisualElement, CharacterFigure)>();

        private UIDocument _document;
        private MenuInput _input;
        private PlayerController2D _player;
        private VisualElement _root;
        private VisualElement _navList;
        private VisualElement _strip;
        private VisualElement _content;
        private VisualElement _tabs;
        private KeyPrompt _tabPrevious;
        private KeyPrompt _tabNext;
        private KeyPrompt _memberPrevious;
        private KeyPrompt _memberNext;
        private Emblem _tabEmblem;
        private Label _tabMember;
        private PartyScreen _partyScreen;
        private HintBar _hints;
        private Label _clock;
        private Label _currency;

        private int _category;
        /// <summary>Whether the category cursor is on Close, below the screens.</summary>
        private bool _closeSelected;
        /// <summary>The open character page, or -1 when no character is open.</summary>
        private int _page = -1;
        private int _lastPage;
        private int _memberIndex;
        private bool _inScreen;
        private float _savedTimeScale = 1f;
        private static float _playStart = -1f;

        public static GameMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.Open;

        public bool Open { get; private set; }
        public PartyService Party => director != null ? director.Party : null;
        public PartyMemberData Member => _memberIndex >= 0 && _memberIndex < _members.Count ? _members[_memberIndex] : null;
        public IReadOnlyList<PartyMemberData> Members => _members;
        public MenuInput Input => _input;
        /// <summary>The screen on the plate: the open character page, else the chosen category's screen.</summary>
        public MenuScreen Current => _page >= 0 && _page < _pages.Count ? _pages[_page]
            : _category >= 0 && _category < _screens.Count ? _screens[_category] : null;
        public bool InScreen => _inScreen;
        /// <summary>Whether a member's character pages (Weapon, Pictos, Skills) are open.</summary>
        public bool InCharacter => _page >= 0;
        /// <summary>The open character page's index into <see cref="Pages"/>, or -1.</summary>
        public int Page => _page;
        public IReadOnlyList<MenuScreen> Pages => _pages;
        /// <summary>Whether the category cursor is on Close.</summary>
        public bool CloseSelected => _closeSelected;

        /// <summary>Seconds played since the game started. Stands in for a saved play time.</summary>
        public static double PlayTime => _playStart < 0f ? 0d : Time.unscaledTime - _playStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            _playStart = -1f;
        }

        /// <summary>
        /// Adds the menu to any scene with an <see cref="EncounterDirector"/>, so rooms need no menu object wired up.
        /// A room can still place its own GameMenu to override the defaults.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToRoom();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToRoom();

        private static void AttachToRoom()
        {
            if (Instance != null) return;
            var room = FindAnyObjectByType<EncounterDirector>();
            if (room != null && room.GetComponent<GameMenu>() == null) room.gameObject.AddComponent<GameMenu>();
        }

        private void Awake()
        {
            Instance = this;
            if (_playStart < 0f) _playStart = Time.unscaledTime;
            if (director == null) director = GetComponent<EncounterDirector>();
            if (director == null) director = FindAnyObjectByType<EncounterDirector>();
            _player = FindAnyObjectByType<PlayerController2D>();
            if (inputActions == null) inputActions = InputSystem.actions;
            _input = new MenuInput(inputActions);

            BuildDocument();
        }

        private void OnDestroy()
        {
            if (Open) Close();
            _input?.Dispose();
            if (Instance == this) Instance = null;
        }

        #region Building
        private void BuildDocument()
        {
            if (layout == null) layout = HudPanel.Layout(LayoutName);
            // The layout links GameMenu.uss itself: Resources cannot load a style sheet that shares the layout's name.
            _document = HudPanel.CreateDocument(transform, "Game Menu", HudPanel.Layer.Menu);
            var tree = _document.rootVisualElement;
            layout?.CloneTree(tree);

            _root = tree.Q("menu-root");
            if (_root == null)
            {
                Debug.LogError($"[GameMenu] Resources/{HudPanel.ResourceFolder}{LayoutName}.uxml is missing or has no 'menu-root'. The menu is disabled.", this);
                enabled = false;
                return;
            }

            _navList = tree.Q("nav-list");
            _strip = tree.Q("party-strip");
            _content = tree.Q("content");
            _clock = tree.Q<Label>("clock");
            _currency = tree.Q<Label>("currency");

            var glow = tree.Q("glow");
            glow.style.backgroundImage = new StyleBackground(UiTextures.Radial);
            tree.Q("nav-rule").style.backgroundImage = new StyleBackground(UiTextures.FadeRight);

            // The screens sit on one framed ink plate, like the combat HUD's panels.
            MenuUi.Plate(_content);

            _partyScreen = new PartyScreen();
            _screens.Add(_partyScreen);
            _screens.Add(new InventoryScreen());
            _pages.Add(new WeaponScreen());
            _pages.Add(new PictosScreen());
            _pages.Add(new SkillsScreen());

            BuildTabs();

            for (var i = 0; i < _screens.Count; i++)
            {
                var screen = _screens[i];
                screen.Attach(this, _content);

                var index = i;
                var item = NavItem(screen.Title);
                item.RegisterCallback<PointerDownEvent>(_ => OnNavClicked(index));
                _navItems.Add(item);
            }

            foreach (var page in _pages) page.Attach(this, _content);

            var close = NavItem("Close");
            close.AddToClassList("nav-item--close");
            close.RegisterCallback<PointerDownEvent>(_ => Close());
            _navItems.Add(close);

            _hints = new HintBar();
            _root.Add(_hints);

            _root.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// The character pages' tab bar along the top of the plate: the page names between the PreviousTab and NextTab
        /// keys on the left, and on the right whose pages they are, between the Previous and Next keys.
        /// </summary>
        private void BuildTabs()
        {
            _tabs = MenuUi.Box(_content, "page-tabs");
            _tabPrevious = new KeyPrompt(22f);
            _tabPrevious.AddToClassList("page-tabs__key");
            _tabs.Add(_tabPrevious);
            for (var i = 0; i < _pages.Count; i++)
            {
                var tab = MenuUi.Box(_tabs, "page-tab");
                tab.pickingMode = PickingMode.Position;
                MenuUi.Text(tab, _pages[i].Title.ToUpperInvariant(), "page-tab__label");
                var index = i;
                tab.RegisterCallback<PointerDownEvent>(_ => ShowPage(index));
                _tabItems.Add(tab);
            }

            _tabNext = new KeyPrompt(22f);
            _tabNext.AddToClassList("page-tabs__key");
            _tabs.Add(_tabNext);

            MenuUi.Box(_tabs, "page-tabs__spacer");
            var member = MenuUi.Box(_tabs, "page-tabs__member");
            _memberPrevious = new KeyPrompt(22f);
            _memberPrevious.AddToClassList("page-tabs__key");
            member.Add(_memberPrevious);
            _tabEmblem = new Emblem(26f);
            _tabEmblem.AddToClassList("page-tabs__emblem");
            member.Add(_tabEmblem);
            _tabMember = MenuUi.Text(member, "", "page-tabs__name");
            _memberNext = new KeyPrompt(22f);
            _memberNext.AddToClassList("page-tabs__key");
            member.Add(_memberNext);
            _tabs.style.display = DisplayStyle.None;
        }

        private VisualElement NavItem(string title)
        {
            var item = MenuUi.Row(_navList, "nav-item");
            item.pickingMode = PickingMode.Position;
            MenuUi.Text(item, "▶", "nav-item__pointer");
            MenuUi.Text(item, title, "nav-item__label");
            return item;
        }
        #endregion

        #region Opening
        private void Update()
        {
            if (_root == null) return;

            if (!Open)
            {
                if (_input.TogglePressed && CanOpen()) Show();
                return;
            }

            if (director != null && director.Busy)
            {
                Close();
                return;
            }

            HandleInput();
            if (_clock != null) _clock.text = MenuUi.Clock(PlayTime);
        }

        private bool CanOpen() => Party != null && (director == null || !director.Busy);

        /// <summary>Opens the menu on <paramref name="category"/>, pausing the game.</summary>
        public void Show(int category = -1)
        {
            if (Open || !CanOpen()) return;

            Open = true;
            // End a switch's hit stop first, so closing the menu restores the real time scale.
            HitStop.Release();
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (_player != null) _player.SetControlsEnabled(false);

            RebuildMembers();
            if (category >= 0) _category = Mathf.Clamp(category, 0, _screens.Count - 1);
            _closeSelected = false;
            _page = -1;
            _inScreen = false;
            _root.style.display = DisplayStyle.Flex;
            RefreshAll();
            AudioManager.PlayUi(AudioActionType.MenuOpen);
        }

        public void Close()
        {
            if (!Open) return;

            LeaveScreen();
            Open = false;
            Time.timeScale = _savedTimeScale <= 0f ? 1f : _savedTimeScale;
            if (_player != null && (director == null || !director.Busy)) _player.SetControlsEnabled(true);
            _root.style.display = DisplayStyle.None;
            AudioManager.PlayUi(AudioActionType.MenuClose);
        }
        #endregion

        #region Input
        private void HandleInput()
        {
            var screen = Current;

            // Escape is both Toggle and Cancel on a keyboard; Cancel wins so it backs out one step at a time.
            if (_input.CancelPressed)
            {
                if (InCharacter || _inScreen) AudioManager.PlayUi(AudioActionType.MenuBack);
                Back();
                return;
            }

            if (_input.TogglePressed)
            {
                Close();
                return;
            }

            var move = _input.ReadMove();
            PlayInputSound(move);
            if (_inScreen)
            {
                if (move != Vector2Int.zero) screen.Move(move);
                if (_input.ConfirmPressed) screen.Confirm();
                if (_input.SecondaryPressed) screen.Secondary();
                if (_input.SwitchPressed) screen.Switch();
                if (_input.PreviousPressed && !screen.Cycle(-1)) SwitchMember(-1);
                if (_input.NextPressed && !screen.Cycle(1)) SwitchMember(1);
                if (InCharacter && _input.PreviousTabPressed) SwitchPage(-1);
                if (InCharacter && _input.NextTabPressed) SwitchPage(1);
            }
            else
            {
                if (move.y != 0) MoveCategory(move.y);
                if (_closeSelected)
                {
                    if (_input.ConfirmPressed)
                    {
                        Close();
                        return;
                    }
                }
                else if ((_input.ConfirmPressed || move.x > 0) && screen.Interactive)
                {
                    EnterScreen();
                }
            }

            RefreshChrome();
        }

        /// <summary>
        /// One sound per frame for the menu input pressed: tabs and members, confirming, or moving the cursor. Backing
        /// out and closing play their own.
        /// </summary>
        private void PlayInputSound(Vector2Int move)
        {
            if (_inScreen && (_input.PreviousPressed || _input.NextPressed || _input.PreviousTabPressed || _input.NextTabPressed))
                AudioManager.PlayUi(AudioActionType.MenuTab);
            else if (_input.ConfirmPressed || (_inScreen && (_input.SecondaryPressed || _input.SwitchPressed)))
                AudioManager.PlayUi(AudioActionType.MenuConfirm);
            else if (move != Vector2Int.zero)
                AudioManager.PlayUi(AudioActionType.MenuMove);
        }

        /// <summary>
        /// Backs out one step: out of the screen's own state, out of a member's character pages, to the category
        /// list, then closes.
        /// </summary>
        private void Back()
        {
            if (!Open) return;
            if (InCharacter)
            {
                if (!Current.Cancel()) CloseCharacter();
            }
            else if (_inScreen)
            {
                if (!Current.Cancel()) LeaveScreen();
            }
            else
            {
                Close();
                return;
            }

            RefreshChrome();
        }

        private void OnNavClicked(int index)
        {
            AudioManager.PlayUi(AudioActionType.MenuConfirm);
            _closeSelected = false;
            if (index == _category && !InCharacter && Current.Interactive && !_inScreen)
            {
                EnterScreen();
            }
            else
            {
                if (_inScreen) LeaveScreen();
                SelectCategory(index);
                RefreshAll();
            }

            RefreshChrome();
        }

        /// <summary>Moves the category cursor through the screens and Close, wrapping around.</summary>
        public void MoveCategory(int step)
        {
            var position = _closeSelected ? _screens.Count : _category;
            position = MenuNavigation.Wrap(position, step, _screens.Count + 1);
            _closeSelected = position == _screens.Count;
            // The plate keeps the last screen while Close is highlighted.
            if (!_closeSelected) SelectCategory(position);
            RefreshChrome();
        }

        private void SelectCategory(int index)
        {
            if (index == _category) return;
            if (_inScreen) LeaveScreen();
            _category = index;
            RefreshAll();
        }

        public void EnterScreen()
        {
            if (_inScreen || Current == null || !Current.Interactive) return;
            _closeSelected = false;
            _inScreen = true;
            Current.Enter();
            RefreshChrome();
        }

        private void LeaveScreen()
        {
            if (!_inScreen) return;
            if (InCharacter) CloseCharacter();
            _inScreen = false;
            Current?.Exit();
        }

        /// <summary>
        /// Opens <paramref name="member"/>'s character pages on <paramref name="page"/> (an index into
        /// <see cref="Pages"/>), or on the page open last. The Party screen keeps its cursor underneath.
        /// </summary>
        public void OpenCharacter(PartyMemberData member, int page = -1)
        {
            if (!Open || _pages.Count == 0) return;
            var index = _members.IndexOf(member);
            if (index < 0) return;

            _closeSelected = false;
            _category = _screens.IndexOf(_partyScreen);
            _inScreen = true;
            _memberIndex = index;
            _page = Mathf.Clamp(page >= 0 ? page : _lastPage, 0, _pages.Count - 1);
            _lastPage = _page;
            RefreshAll();
            Current.Enter();
            RefreshChrome();
        }

        /// <summary>Closes the character pages, back to the Party screen with its cursor on the member last shown.</summary>
        public void CloseCharacter()
        {
            if (!InCharacter) return;
            Current.Exit();
            _page = -1;
            _partyScreen.Focus(Member);
            RefreshAll();
        }

        /// <summary>Shows character page <paramref name="index"/>, from the tab bar or PreviousTab/NextTab.</summary>
        public void ShowPage(int index)
        {
            if (!InCharacter || index < 0 || index >= _pages.Count || index == _page) return;
            Current.Exit();
            _page = index;
            _lastPage = index;
            RefreshAll();
            Current.Enter();
            RefreshChrome();
        }

        private void SwitchPage(int step)
        {
            if (_pages.Count < 2) return;
            ShowPage(MenuNavigation.Wrap(_page, step, _pages.Count));
        }

        /// <summary>Shows the member at <paramref name="index"/> on every screen.</summary>
        public void SelectMember(int index)
        {
            if (_members.Count == 0) return;
            _memberIndex = Mathf.Clamp(index, 0, _members.Count - 1);
            RefreshAll();
        }

        private void SwitchMember(int step)
        {
            if (Current != null && !Current.PerMember) return;
            SelectMember(MenuNavigation.Wrap(_memberIndex, step, _members.Count));
        }
        #endregion

        #region Drawing
        /// <summary>Call after the formation changes: reorders the party strip, then redraws everything.</summary>
        public void PartyChanged()
        {
            if (!Open) return;
            RebuildMembers();
            RefreshAll();
        }

        /// <summary>Redraws everything: call after the party changes.</summary>
        public void RefreshAll()
        {
            if (!Open) return;

            var current = Current;
            foreach (var screen in _screens.Concat(_pages))
            {
                var shown = screen == current;
                screen.Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (shown) screen.Refresh();
            }

            RefreshTabs();
            RefreshStrip();
            RefreshChrome();
        }

        private void RebuildMembers()
        {
            var previous = Member;
            _members.Clear();
            var party = Party;
            if (party != null)
            {
                _members.AddRange(party.Active.Where(member => member != null));
                _members.AddRange(party.Members.Where(member => member != null && !_members.Contains(member)));
            }

            var index = previous != null ? _members.IndexOf(previous) : -1;
            _memberIndex = index >= 0 ? index : 0;

            _strip.Clear();
            _tiles.Clear();
            for (var i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                var tile = MenuUi.Box(_strip, "strip-tile");
                tile.pickingMode = PickingMode.Position;
                var figure = new CharacterFigure(StripDiamond, StripSpriteScale);
                figure.AddToClassList("strip-tile__figure");
                figure.SetCharacter(member);
                tile.Add(figure);
                var index2 = i;
                tile.RegisterCallback<PointerDownEvent>(_ => SelectMember(index2));
                _tiles.Add((tile, figure));
            }
        }

        private void RefreshTabs()
        {
            _tabs.style.display = InCharacter ? DisplayStyle.Flex : DisplayStyle.None;
            if (!InCharacter) return;

            for (var i = 0; i < _tabItems.Count; i++) MenuUi.SetClass(_tabItems[i], "page-tab--selected", i == _page);
            var member = Member;
            _tabMember.text = member != null ? member.CharacterName.ToUpperInvariant() : "";
            if (member != null)
            {
                _tabEmblem.SetCharacter(member.CharacterName, member.Portrait, MenuUi.ElementColor(member.Element));
                _tabEmblem.SetRim(HudColors.Accent, 2f);
            }

            RefreshTabKeys();
        }

        /// <summary>The tab bar's key prompts, redrawn with the chrome so they follow the last device used.</summary>
        private void RefreshTabKeys()
        {
            if (!InCharacter) return;
            var gamepad = MenuInput.UsingGamepad;
            _tabPrevious.Set(_input.Glyph(MenuButton.PreviousTab), gamepad);
            _tabNext.Set(_input.Glyph(MenuButton.NextTab), gamepad);
            var switchable = _members.Count > 1;
            _memberPrevious.Show(switchable);
            _memberNext.Show(switchable);
            _memberPrevious.Set(_input.Glyph(MenuButton.Previous), gamepad);
            _memberNext.Set(_input.Glyph(MenuButton.Next), gamepad);
        }

        private void RefreshStrip()
        {
            var perMember = Current != null && Current.PerMember;
            _strip.style.visibility = perMember ? Visibility.Visible : Visibility.Hidden;

            for (var i = 0; i < _tiles.Count && i < _members.Count; i++)
            {
                var (tile, figure) = _tiles[i];
                var selected = i == _memberIndex;
                MenuUi.SetClass(tile, "strip-tile--selected", selected);
                figure.SetRim(selected ? HudColors.Accent : HudColors.Rail, selected ? 2.5f : 2f);
                var role = Party.GetRole(_members[i]);
                MenuUi.SetClass(tile, "strip-tile--benched", role == PartyRole.Benched);
            }
        }

        /// <summary>Redraws the category list and key prompts.</summary>
        public void RefreshChrome()
        {
            for (var i = 0; i < _navItems.Count; i++)
            {
                var selected = _closeSelected ? i == _screens.Count : i == _category;
                MenuUi.SetClass(_navItems[i], "mrow--selected", selected);
                MenuUi.SetClass(_navItems[i], "nav-item--selected", selected);
                MenuUi.SetClass(_navItems[i], "nav-item--entered", selected && _inScreen);
            }

            RefreshTabKeys();
            if (_currency != null && Party != null) _currency.text = Party.Currency.ToString("N0");
            if (_clock != null) _clock.text = MenuUi.Clock(PlayTime);

            var screen = Current;
            var hints = new List<HintBar.Hint>();
            if (_inScreen)
            {
                foreach (var (button, label) in screen.Prompts())
                    hints.Add(new HintBar.Hint(_input.Glyph(button), label, button == MenuButton.Cancel ? Back : (System.Action)null));
                if (screen.PerMember && _members.Count > 1)
                    hints.Add(new HintBar.Hint($"{_input.Glyph(MenuButton.Previous)} / {_input.Glyph(MenuButton.Next)}", "Member"));
                if (InCharacter && _pages.Count > 1)
                    hints.Add(new HintBar.Hint($"{_input.Glyph(MenuButton.PreviousTab)} / {_input.Glyph(MenuButton.NextTab)}", "Page"));
            }
            else if (_closeSelected)
            {
                hints.Add(new HintBar.Hint(_input.Glyph(MenuButton.Confirm), "Close", Close));
            }
            else
            {
                if (screen != null && screen.Interactive) hints.Add(new HintBar.Hint(_input.Glyph(MenuButton.Confirm), "Select", EnterScreen));
                hints.Add(new HintBar.Hint(_input.Glyph(MenuButton.Cancel), "Close", Close));
            }

            _hints.Set(MenuInput.UsingGamepad, hints.ToArray());
        }
        #endregion
    }
}
