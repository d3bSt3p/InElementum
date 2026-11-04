using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Core.UI
{
    /// <summary>Small helpers for building screens in code: absolute placement, tinted textures, fills.</summary>
    public static class Hud
    {
        public static T Absolute<T>(this T element) where T : VisualElement
        {
            element.style.position = Position.Absolute;
            return element;
        }

        public static T Ignore<T>(this T element) where T : VisualElement
        {
            element.pickingMode = PickingMode.Ignore;
            return element;
        }

        public static T Classes<T>(this T element, params string[] classes) where T : VisualElement
        {
            foreach (var name in classes) element.AddToClassList(name);
            return element;
        }

        /// <summary>Places an absolute element at <paramref name="rect"/>, in panel units.</summary>
        public static void Place(this VisualElement element, Rect rect)
        {
            element.style.left = rect.x;
            element.style.top = rect.y;
            element.style.width = rect.width;
            element.style.height = rect.height;
        }

        public static void Move(this VisualElement element, Vector2 position)
        {
            element.style.left = position.x;
            element.style.top = position.y;
        }

        public static void Show(this VisualElement element, bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != display) element.style.display = display;
        }

        /// <summary>A plain element that ignores the pointer.</summary>
        public static VisualElement Box(params string[] classes) => new VisualElement().Ignore().Classes(classes);

        public static Label Text(string text, params string[] classes) => new Label(text).Ignore().Classes(classes);

        /// <summary>An absolute, pointer-ignoring element showing a white texture tinted <paramref name="tint"/>.</summary>
        public static VisualElement Tinted(Texture2D texture, Color tint)
        {
            var element = Box().Absolute();
            element.style.backgroundImage = new StyleBackground(texture);
            element.style.unityBackgroundImageTintColor = tint;
            return element;
        }

        /// <summary>An absolute, pointer-ignoring solid fill.</summary>
        public static VisualElement Fill(Color color)
        {
            var element = Box().Absolute();
            element.style.backgroundColor = color;
            return element;
        }

        public static Color Alpha(this Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);

        public static float EaseOutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        /// <summary>A unit's initials, for badges without a portrait.</summary>
        public static string Initials(string name) => string.IsNullOrEmpty(name) ? "?" : name.Substring(0, Math.Min(2, name.Length));

        /// <summary>
        /// Frames <paramref name="element"/> with the Kenney art: the panel shape tinted <paramref name="fill"/>
        /// behind it, and the border tinted <paramref name="border"/> on a child laid under its other children.
        /// <paramref name="corner"/> is how big each ornamented corner draws. Without the art, a flat fill with a
        /// thin border. Returns the border child, or null.
        /// </summary>
        public static VisualElement Frame(VisualElement element, float corner, Color fill, Color border, bool small = false)
        {
            const int texels = 16;
            var art = UiArt.Instance;
            if (art == null)
            {
                element.style.backgroundColor = fill;
                SetBorder(element, 1f, border);
                return null;
            }

            // The 96 px art once the 48 px art would be blown up on screen.
            var large = corner * Screen.height / HudPanel.ReferenceHeight > texels * 1.25f;
            var slice = large ? texels * 2 : texels;
            var style = small ? art.smallStyle : art.panelStyle;

            ApplySliced(element, UiArt.Pick(art.panels, art.panelsDouble, style, large), slice, corner / slice, fill);

            var edge = Box("ie-frame__border").Absolute();
            edge.style.left = edge.style.top = edge.style.right = edge.style.bottom = 0f;
            ApplySliced(edge, UiArt.Pick(art.borders, art.bordersDouble, style, large), slice, corner / slice, border);
            element.Insert(0, edge);
            return edge;
        }

        private static void ApplySliced(VisualElement element, Texture2D texture, int slice, float scale, Color tint)
        {
            if (texture == null) return;
            element.style.backgroundImage = new StyleBackground(texture);
            element.style.unityBackgroundImageTintColor = tint;
            element.style.unitySliceLeft = slice;
            element.style.unitySliceRight = slice;
            element.style.unitySliceTop = slice;
            element.style.unitySliceBottom = slice;
            element.style.unitySliceScale = scale;
        }

        public static void SetBorder(VisualElement element, float width, Color color)
        {
            element.style.borderLeftWidth = element.style.borderRightWidth = element.style.borderTopWidth = element.style.borderBottomWidth = width;
            element.style.borderLeftColor = element.style.borderRightColor = element.style.borderTopColor = element.style.borderBottomColor = color;
        }

        public static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius =
                element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
        }
    }

    /// <summary>A diamond (a square on its point) <see cref="Size"/> across, with a fill and an optional rim.</summary>
    public class Diamond : VisualElement
    {
        private readonly VisualElement _shape;
        private float _size = -1f;

        public Diamond(float size = 16f)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("ie-diamond");
            _shape = Hud.Box("ie-diamond__shape").Absolute();
            Add(_shape);
            Size = size;
        }

        /// <summary>Point to point, in panel units.</summary>
        public float Size
        {
            get => _size;
            set
            {
                if (Mathf.Approximately(_size, value)) return;
                _size = value;
                style.width = value;
                style.height = value;
                var side = value * 0.70710678f;
                _shape.style.width = side;
                _shape.style.height = side;
                _shape.style.left = (value - side) * 0.5f;
                _shape.style.top = (value - side) * 0.5f;
            }
        }

        public Color Fill
        {
            set => _shape.style.backgroundColor = value;
        }

        public void SetRim(float width, Color color) => Hud.SetBorder(_shape, width, color);

        /// <summary>Centers the diamond on <paramref name="center"/>, for absolute diamonds.</summary>
        public void CenterOn(Vector2 center)
        {
            style.left = center.x - _size * 0.5f;
            style.top = center.y - _size * 0.5f;
        }
    }

    /// <summary>
    /// A character's diamond badge: a rimmed diamond in their element's color with their portrait cut into it, or
    /// their initials when they have no portrait yet.
    /// </summary>
    public class Emblem : VisualElement
    {
        private readonly Diamond _back;
        private readonly Diamond _fill;
        private readonly VisualElement _portrait;
        private readonly Label _initials;
        private float _size = -1f;
        private Sprite _sprite;

        public Emblem(float size = 32f)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("ie-emblem");
            _back = new Diamond().Absolute();
            _fill = new Diamond().Absolute();
            _portrait = Hud.Box("ie-emblem__portrait").Absolute();
            _initials = Hud.Text(string.Empty, "ie-emblem__initials").Absolute();
            Add(_back);
            Add(_fill);
            Add(_portrait);
            Add(_initials);
            Size = size;
        }

        /// <summary>The colored diamond's size; the rim stands 4 units proud of it on every side.</summary>
        public float Size
        {
            get => _size;
            set
            {
                if (Mathf.Approximately(_size, value)) return;
                _size = value;
                var outer = value + 8f;
                style.width = outer;
                style.height = outer;
                _back.Size = outer;
                _back.Move(Vector2.zero);
                _fill.Size = value;
                _fill.Move(new Vector2(4f, 4f));
                var badge = value - 2f * Mathf.Max(3f, value * 0.1f);
                _portrait.Place(new Rect((outer - badge) * 0.5f, (outer - badge) * 0.5f, badge, badge));
                _initials.Place(new Rect(0f, 0f, outer, outer));
                _initials.style.fontSize = Mathf.Max(9f, value * 0.36f);
            }
        }

        /// <summary>Who the badge shows: a portrait when there is one, else the initials of <paramref name="name"/>.</summary>
        public void SetCharacter(string name, Sprite portrait, Color elementColor)
        {
            _fill.Fill = elementColor;
            if (_sprite != portrait || portrait == null)
            {
                _sprite = portrait;
                var badge = UiTextures.PortraitBadge(portrait);
                if (badge != null)
                {
                    _portrait.style.backgroundImage = new StyleBackground(badge);
                    _portrait.Show(true);
                    _initials.Show(false);
                }
                else
                {
                    _portrait.Show(false);
                    _initials.Show(true);
                    _initials.text = Hud.Initials(name);
                }
            }
        }

        public void SetRim(Color rim, float width = 2f)
        {
            _back.Fill = HudColors.Panel;
            _back.SetRim(width, rim);
        }

        /// <summary>Greys the badge out, for a downed character.</summary>
        public void SetTint(Color tint)
        {
            _portrait.style.unityBackgroundImageTintColor = tint;
            _fill.style.opacity = tint.grayscale < 0.99f ? 0.6f : 1f;
        }

        public void CenterOn(Vector2 center)
        {
            style.left = center.x - (_size + 8f) * 0.5f;
            style.top = center.y - (_size + 8f) * 0.5f;
        }
    }

    /// <summary>
    /// A key or button: its Kenney prompt icon, one per input with a slash between ("LMB / E"), or its name in a
    /// cap when there is no icon: a square box for keyboard and mouse, a pill for controller buttons.
    /// </summary>
    public class KeyPrompt : VisualElement
    {
        /// <summary>Kenney's prompt icons leave 8 of their 64 pixels empty on every side.</summary>
        private const float PromptVisible = 48f / 64f;
        private const float Slash = 12f;

        private string _glyph;
        private bool _gamepad;
        private float _height = -1f;

        public KeyPrompt(float height = 26f)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("ie-key");
            _height = height;
        }

        /// <summary>Whether the last glyph had icons; false when it fell back to a text cap.</summary>
        public bool HasIcons { get; private set; }

        /// <summary>The icons' laid-out width, for centering by hand; 0 for a text cap, which sizes to its text.</summary>
        public float IconWidth { get; private set; }

        public void Set(string glyph, bool gamepad, float? height = null)
        {
            var h = height ?? _height;
            if (glyph == _glyph && gamepad == _gamepad && Mathf.Approximately(h, _height) && childCount > 0) return;
            _glyph = glyph;
            _gamepad = gamepad;
            _height = h;
            Clear();
            style.height = h;

            var icons = InputPrompts.Icons(glyph, gamepad);
            HasIcons = icons != null;
            IconWidth = 0f;
            if (icons != null)
            {
                for (var i = 0; i < icons.Length; i++)
                {
                    if (i > 0)
                    {
                        var slash = Hud.Text("/", "ie-key__slash");
                        slash.style.width = Slash;
                        slash.style.height = h;
                        Add(slash);
                        IconWidth += Slash;
                    }

                    // The slot is as wide as the icon's art, so narrow icons (the mice) sit as close to their label
                    // as keys do.
                    var width = h * InputPrompts.VisibleWidth(icons[i]);
                    var slot = Hud.Box("ie-key__slot");
                    slot.style.width = width;
                    slot.style.height = h;
                    IconWidth += width;
                    var size = h / PromptVisible;
                    var image = Hud.Box("ie-key__icon").Absolute();
                    image.Place(new Rect((width - size) * 0.5f, (h - size) * 0.5f, size, size));
                    image.style.backgroundImage = new StyleBackground(icons[i]);
                    slot.Add(image);
                    Add(slot);
                }

                return;
            }

            var cap = Hud.Text(string.IsNullOrEmpty(glyph) ? "?" : glyph, "ie-key__cap", gamepad ? "ie-key__cap--pad" : "ie-key__cap--key");
            cap.style.height = h;
            cap.style.minWidth = h;
            if (gamepad) Hud.SetRadius(cap, h * 0.5f);
            Add(cap);
        }
    }

    /// <summary>
    /// The controls strip in the bottom left: a key and a label per hint. Hints with an action can be clicked.
    /// Rebuilt only when the hints change, so it can be set every frame.
    /// </summary>
    public class HintBar : VisualElement
    {
        public readonly struct Hint
        {
            public Hint(string key, string label, Action click = null)
            {
                Key = key;
                Label = label;
                Click = click;
            }

            public string Key { get; }
            public string Label { get; }
            public Action Click { get; }
        }

        private const float KeyHeight = 26f;
        private readonly List<Action> _clicks = new List<Action>();
        private string _signature;

        public HintBar()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("ie-hints");
        }

        public void Set(bool gamepad, params Hint[] hints)
        {
            var signature = gamepad ? "pad" : "key";
            foreach (var hint in hints) signature += $"|{hint.Key}:{hint.Label}:{hint.Click != null}";

            if (signature == _signature)
            {
                for (var i = 0; i < hints.Length; i++) _clicks[i] = hints[i].Click;
                return;
            }

            _signature = signature;
            Clear();
            _clicks.Clear();
            for (var i = 0; i < hints.Length; i++)
            {
                var hint = hints[i];
                var entry = new VisualElement();
                entry.AddToClassList("ie-hint");
                entry.pickingMode = hint.Click != null ? PickingMode.Position : PickingMode.Ignore;
                if (hint.Click != null) entry.AddToClassList("ie-hint--clickable");

                var key = new KeyPrompt(KeyHeight);
                key.Set(hint.Key, gamepad);
                entry.Add(key);
                entry.Add(Hud.Text(hint.Label, "ie-hint__label"));

                var index = i;
                _clicks.Add(hint.Click);
                entry.RegisterCallback<ClickEvent>(_ => _clicks[index]?.Invoke());
                Add(entry);
            }
        }
    }

    /// <summary>The palette shared by every screen, for colors set from code. Static colors live in Hud.uss.</summary>
    public static class HudColors
    {
        public static readonly Color Panel = new Color(0.03f, 0.04f, 0.07f, 0.9f);
        public static readonly Color PanelSoft = new Color(0.03f, 0.04f, 0.07f, 0.65f);
        public static readonly Color MenuFill = new Color(0.03f, 0.04f, 0.07f, 0.88f);
        public static readonly Color Gilt = new Color(0.86f, 0.8f, 0.62f, 0.7f);
        public static readonly Color Accent = new Color(0.97f, 0.85f, 0.56f);
        public static readonly Color Text = new Color(0.97f, 0.96f, 0.92f);
        public static readonly Color Muted = new Color(0.6f, 0.61f, 0.64f);
        public static readonly Color Rail = new Color(0.8f, 0.82f, 0.86f, 0.75f);
        public static readonly Color PartyHealth = new Color(0.36f, 0.8f, 0.56f);
        public static readonly Color EnemyHealth = new Color(0.86f, 0.22f, 0.24f);
        public static readonly Color Ap = new Color(0.35f, 0.55f, 1f);
        public static readonly Color Surge = new Color(0.75f, 0.45f, 1f);
        public static readonly Color ActiveSweep = new Color(0.1f, 0.36f, 0.8f, 0.85f);
        public static readonly Color Track = new Color(0.12f, 0.12f, 0.14f, 0.9f);
    }
}
