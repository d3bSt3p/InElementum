using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Core.UI
{
    /// <summary>
    /// UI pinned to points in the world: interaction prompts, signs, markers over enemies. Owners create an element
    /// once, set its <see cref="WorldElement.Anchor"/> and <see cref="WorldElement.Shown"/> as they go, and
    /// <see cref="WorldElement.Release"/> it when destroyed. The layer moves every element to its anchor after the
    /// cameras have moved. One layer for the whole game, under every other screen.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class WorldUi : MonoBehaviour
    {
        private static WorldUi _instance;
        private readonly List<WorldElement> _elements = new List<WorldElement>();
        private UIDocument _document;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private static WorldUi Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("World UI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<WorldUi>();
                return _instance;
            }
        }

        private void Awake()
        {
            _document = HudPanel.CreateDocument(transform, "World", HudPanel.Layer.World);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Adds <paramref name="element"/> to the layer, hidden until it is shown.</summary>
        public static T Add<T>(T element) where T : WorldElement
        {
            if (!Application.isPlaying) return element;
            var layer = Instance;
            element.Shown = false;
            layer._document.rootVisualElement.Add(element);
            layer._elements.Add(element);
            return element;
        }

        internal static void Remove(WorldElement element)
        {
            if (_instance == null) return;
            _instance._elements.Remove(element);
            element.RemoveFromHierarchy();
        }

        private void LateUpdate()
        {
            var root = _document.rootVisualElement;
            foreach (var element in _elements)
            {
                var point = Vector2.zero;
                var visible = element.Shown && HudPanel.WorldToPanel(root, element.Anchor, out point);
                element.Show(visible);
                if (visible) element.Move(point + element.Offset);
            }
        }
    }

    /// <summary>An element of the <see cref="WorldUi"/> layer, centered on a world point.</summary>
    public abstract class WorldElement : VisualElement
    {
        protected WorldElement()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("ie-world");
            style.position = Position.Absolute;
        }

        public Vector3 Anchor { get; set; }
        /// <summary>Panel units added after projecting the anchor.</summary>
        public Vector2 Offset { get; set; }
        public bool Shown { get; set; }

        public void Release() => WorldUi.Remove(this);
    }

    /// <summary>A line of text over a world point, bare or in a small framed box.</summary>
    public class WorldText : WorldElement
    {
        private readonly Label _label;

        public WorldText(bool boxed, int fontSize = 15)
        {
            AddToClassList("ie-world-text");
            if (boxed)
            {
                AddToClassList("ie-world-text--boxed");
                Hud.Frame(this, 9f, HudColors.Panel, HudColors.Gilt, small: true);
            }

            _label = Hud.Text(string.Empty, "ie-world-text__label");
            _label.style.fontSize = fontSize;
            Add(_label);
        }

        public string Text
        {
            get => _label.text;
            set => _label.text = value;
        }

        public Color Color
        {
            set => _label.style.color = value;
        }
    }

    /// <summary>An input prompt over a world point: the button's icon and a verb, such as "[E] Read".</summary>
    public class WorldPrompt : WorldElement
    {
        private readonly Label _prefix;
        private readonly KeyPrompt _key;
        private readonly Label _verb;

        public WorldPrompt(int fontSize = 15)
        {
            AddToClassList("ie-world-prompt");
            _prefix = Hud.Text(string.Empty, "ie-world-prompt__text");
            _key = new KeyPrompt(fontSize * 1.6f);
            _verb = Hud.Text(string.Empty, "ie-world-prompt__text");
            _prefix.style.fontSize = fontSize;
            _verb.style.fontSize = fontSize;
            Add(_prefix);
            Add(_key);
            Add(_verb);
        }

        /// <summary>
        /// Shows <paramref name="glyph"/> and <paramref name="verb"/>. Words before the key, as in "Hold E", stay as
        /// text in front of the icon.
        /// </summary>
        public void Set(string glyph, bool gamepad, string verb, Color color)
        {
            string prefix = null;
            var key = glyph;
            var space = glyph != null ? glyph.LastIndexOf(' ') : -1;
            if (space > 0 && InputPrompts.Icons(glyph, gamepad) == null)
            {
                prefix = glyph.Substring(0, space);
                key = glyph.Substring(space + 1);
            }

            _prefix.text = prefix ?? string.Empty;
            _prefix.Show(prefix != null);
            _key.Set(key, gamepad);
            _verb.text = verb;
            _prefix.style.color = color;
            _verb.style.color = color;
        }
    }
}
