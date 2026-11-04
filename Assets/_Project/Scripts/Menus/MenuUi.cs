using System;
using System.Text.RegularExpressions;
using InElementum.Combat.Characters;
using InElementum.Core.UI;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Menus
{
    /// <summary>
    /// Element builders for the menu screens, in the look the combat and exploration HUDs share: framed ink plates,
    /// bevelled bars that take a blue sweep when selected, gilt section headings, and element diamonds. Every element
    /// gets USS classes from GameMenu.uss, so sizes and colors can be changed in the stylesheet without code.
    /// </summary>
    public static class MenuUi
    {
        /// <summary>Corner size of the large framed plates, as in combat.</summary>
        public const float PanelCorner = 16f;
        /// <summary>Corner size of small framed boxes, as in combat.</summary>
        public const float SmallCorner = 9f;

        public static VisualElement Box(VisualElement parent, params string[] classes)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var name in classes) element.AddToClassList(name);
            parent?.Add(element);
            return element;
        }

        public static Label Text(VisualElement parent, string text, params string[] classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var name in classes) label.AddToClassList(name);
            parent?.Add(label);
            return label;
        }

        /// <summary>
        /// A section heading: a small gilt diamond, the caption in capitals, and a fading gilt rule to the right.
        /// Returns the caption.
        /// </summary>
        public static Label Heading(VisualElement parent, string text)
        {
            var row = Box(parent, "mhead");
            var jewel = new Diamond(8f);
            jewel.Fill = HudColors.Accent;
            jewel.AddToClassList("mhead__jewel");
            row.Add(jewel);
            var label = Text(row, text.ToUpperInvariant(), "mhead__label");
            var rule = Box(row, "mhead__rule");
            rule.style.backgroundImage = new StyleBackground(UiTextures.FadeRight);
            return label;
        }

        /// <summary>
        /// A menu bar like the combat command menu's: a dark fill with a light bevel along its top and left edges and
        /// a corner tick at the bottom right. Add <c>mrow--selected</c> for the blue sweep and gilt bevel.
        /// Content added afterwards lays out in a row over the decoration.
        /// </summary>
        public static VisualElement Row(VisualElement parent, params string[] classes)
        {
            var row = Box(parent, "mrow");
            foreach (var name in classes) row.AddToClassList(name);
            var sweep = Box(row, "mrow__sweep");
            sweep.style.backgroundImage = new StyleBackground(UiTextures.FadeRight);
            var top = Box(row, "mrow__bevel-top");
            top.style.backgroundImage = new StyleBackground(UiTextures.FadeRight);
            Box(row, "mrow__bevel-left");
            Box(row, "mrow__tick-h");
            Box(row, "mrow__tick-v");
            return row;
        }

        /// <summary>Frames <paramref name="element"/> as an ink plate with the Kenney gilt border, as combat panels are.</summary>
        public static T Plate<T>(T element, float corner = PanelCorner, bool small = false) where T : VisualElement
        {
            Hud.Frame(element, corner, HudColors.MenuFill, HudColors.Gilt, small);
            return element;
        }

        /// <summary>A character's diamond badge in their element's color, with their portrait cut into it.</summary>
        public static Emblem Badge(VisualElement parent, float size, string name, Sprite portrait, Element element)
        {
            var emblem = new Emblem(size);
            emblem.SetCharacter(name, portrait, ElementColor(element));
            emblem.SetRim(HudColors.Rail);
            parent?.Add(emblem);
            return emblem;
        }

        public static Color ElementColor(Element element) => PlaceholderCharacter.ElementColor(element);

        /// <summary>A small diamond in <paramref name="color"/>, such as an element mark.</summary>
        public static Diamond Mark(VisualElement parent, Color color, float size = 12f)
        {
            var mark = new Diamond(size);
            mark.Fill = color;
            mark.AddToClassList("mmark");
            parent?.Add(mark);
            return mark;
        }

        public static void SetSprite(VisualElement element, Sprite sprite)
        {
            element.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
        }

        /// <summary>A thin gauge: a track with a fill sized by <see cref="SetBar"/>.</summary>
        public static VisualElement Bar(VisualElement parent, params string[] classes)
        {
            var track = Box(parent, "bar");
            foreach (var name in classes) track.AddToClassList(name);
            Box(track, "bar__fill");
            return track;
        }

        public static void SetBar(VisualElement bar, float fraction)
        {
            var fill = bar.Q(className: "bar__fill");
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
        }

        /// <summary>A gauge readout like the combat party cards: a colored tag, the value, and "/ max" in small type.</summary>
        public static (Label value, Label max) Gauge(VisualElement parent, string tag, string tagClass)
        {
            var row = Box(parent, "mgauge");
            Text(row, tag, "mgauge__tag", tagClass);
            var value = Text(row, "", "mgauge__value");
            var max = Text(row, "", "mgauge__max");
            return (value, max);
        }

        public static void SetClass(VisualElement element, string className, bool on)
        {
            if (on) element.AddToClassList(className);
            else element.RemoveFromClassList(className);
        }

        /// <summary>"LightOrb" becomes "Light Orb".</summary>
        public static string Pretty(string identifier) =>
            string.IsNullOrEmpty(identifier) ? "" : Regex.Replace(identifier, "(?<=[a-z])(?=[A-Z])", " ");

        public static string Pretty(Enum value) => Pretty(value.ToString());

        /// <summary>Initials for a member without a portrait.</summary>
        public static string Initials(string name) =>
            string.IsNullOrEmpty(name) ? "?" : name.Substring(0, Math.Min(2, name.Length));

        /// <summary>Formats seconds as the play clock, "000:12:34".</summary>
        public static string Clock(double seconds)
        {
            var total = (long)Math.Max(0, seconds);
            return $"{total / 3600:000}:{total / 60 % 60:00}:{total % 60:00}";
        }
    }

    /// <summary>
    /// A character's figure for a card: their sprite standing on a deep diamond of their element, holding the first
    /// idle frame or running while <see cref="Running"/>. Every frame is drawn whole at one scale with its pivot (the
    /// feet) pinned to the same spot, so poses never jitter. Shows their diamond badge when they have no sprite yet.
    /// </summary>
    public class CharacterFigure : VisualElement
    {
        private readonly Diamond _diamond;
        private readonly VisualElement _sprite;
        private readonly Emblem _badge;
        private readonly Vector2 _feet;
        private readonly float _scale;
        private readonly IVisualElementScheduledItem _ticker;

        private Sprite _idle;
        private SpriteClip _run;
        private Sprite _shown;
        private bool _running;
        private float _runStart;

        /// <param name="diamond">The diamond's size, point to point.</param>
        /// <param name="scale">Panel units per sprite pixel.</param>
        public CharacterFigure(float diamond = 34f, float scale = 1.2f)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("character-figure");
            _scale = scale;
            var width = diamond + 6f;
            var height = diamond + 16f;
            style.width = width;
            style.height = height;
            var ground = new Rect(3f, height - diamond - 2f, diamond, diamond);
            // The feet rest a little below the diamond's middle, so the figure stands on it.
            _feet = new Vector2(width * 0.5f, ground.center.y + diamond * 0.18f);

            _diamond = new Diamond(diamond).Absolute();
            _diamond.AddToClassList("character-figure__diamond");
            _diamond.Place(ground);
            Add(_diamond);
            _sprite = MenuUi.Box(this, "character-figure__sprite").Absolute();
            _badge = new Emblem(diamond - 8f).Absolute();
            _badge.AddToClassList("character-figure__badge");
            _badge.Place(ground);
            Add(_badge);

            // The menu pauses the game, so the run plays on the panel's own clock, not scaled time.
            _ticker = schedule.Execute(Tick).Every(16);
            _ticker.Pause();
        }

        /// <summary>Whether the figure plays its run, else holds its first idle frame.</summary>
        public bool Running
        {
            get => _running;
            set
            {
                if (_running == value) return;
                _running = value;
                _runStart = Time.realtimeSinceStartup;
                if (_running && _run != null) _ticker.Resume();
                else _ticker.Pause();
                Tick();
            }
        }

        public void SetCharacter(CharacterDataBase character)
        {
            var color = MenuUi.ElementColor(character.Element);
            var set = character.SpriteSet;
            var idle = set != null ? set.Find(CharacterAnimation.Idle) : null;
            _idle = idle != null ? idle.Frames[0] : character.BattleSprite;
            _run = set != null ? set.Find(CharacterAnimation.Run) : null;

            var hasSprite = _idle != null;
            // A deep shade of the element, so a sprite drawn in the same color still stands out against it.
            _diamond.Fill = Color.Lerp(HudColors.Panel, color, 0.4f);
            _diamond.Show(hasSprite);
            _sprite.Show(hasSprite);
            _badge.Show(!hasSprite);
            if (!hasSprite) _badge.SetCharacter(character.CharacterName, character.Portrait, color);

            if (_running && _run != null) _ticker.Resume();
            else _ticker.Pause();
            Tick();
        }

        public void SetRim(Color rim, float width = 2f)
        {
            _diamond.SetRim(width, rim);
            _badge.SetRim(rim, width);
        }

        private void Tick()
        {
            var frame = _idle;
            if (_running && _run != null)
            {
                var index = (int)((Time.realtimeSinceStartup - _runStart) * _run.FramesPerSecond) % _run.Frames.Count;
                frame = _run.Frames[index];
            }

            Show(frame);
        }

        private void Show(Sprite frame)
        {
            if (frame == _shown) return;
            _shown = frame;
            if (frame == null)
            {
                _sprite.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                return;
            }

            var size = frame.rect.size * _scale;
            var pivot = frame.pivot * _scale;
            _sprite.Place(new Rect(_feet.x - pivot.x, _feet.y - (size.y - pivot.y), size.x, size.y));
            _sprite.style.backgroundImage = new StyleBackground(frame);
        }
    }
}
