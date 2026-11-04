using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Core.UI
{
    /// <summary>
    /// The shared UI Toolkit panel every game screen draws on. Screens build their own <see cref="UIDocument"/> at
    /// runtime through <see cref="CreateDocument"/>, so scenes need no UI objects wired up. The panel scales like the
    /// old IMGUI screens did: 720 units tall whatever the resolution, so layout numbers are in those units. Style
    /// sheets and layouts live in <c>Resources/UI</c> and can be edited in the UI Builder.
    /// </summary>
    public static class HudPanel
    {
        /// <summary>Layout height every screen is authored at; the width follows the screen's aspect.</summary>
        public const float ReferenceHeight = 720f;
        public const string ResourceFolder = "UI/";
        public const string ThemePath = ResourceFolder + "Hud Theme";
        public const string SharedSheet = "Hud";

        /// <summary>Draw order of the game's documents: higher draws on top.</summary>
        public static class Layer
        {
            public const int World = 0;
            public const int Exploration = 10;
            public const int Combat = 20;
            public const int Debug = 30;
            public const int Menu = 40;
            public const int Fade = 100;
        }

        private static PanelSettings _settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _settings = null;

        /// <summary>The panel settings shared by every document, made on first use.</summary>
        public static PanelSettings Settings
        {
            get
            {
                if (_settings != null) return _settings;

                _settings = ScriptableObject.CreateInstance<PanelSettings>();
                _settings.name = "Hud Panel Settings";
                _settings.hideFlags = HideFlags.HideAndDontSave;
                _settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                _settings.referenceResolution = new Vector2Int(1280, (int)ReferenceHeight);
                _settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                _settings.match = 1f;
                _settings.clearColor = false;
                _settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemePath);
                if (_settings.themeStyleSheet == null) Debug.LogWarning($"[HudPanel] No theme at Resources/{ThemePath}.");
                return _settings;
            }
        }

        /// <summary>A style sheet from <c>Resources/UI</c>, by file name without extension, or null.</summary>
        public static StyleSheet Sheet(string name) => Resources.Load<StyleSheet>(ResourceFolder + name);

        /// <summary>A UXML layout from <c>Resources/UI</c>, by file name without extension, or null.</summary>
        public static VisualTreeAsset Layout(string name) => Resources.Load<VisualTreeAsset>(ResourceFolder + name);

        /// <summary>
        /// A full-screen document on a new child of <paramref name="host"/>, drawn at <paramref name="sortingOrder"/>
        /// (see <see cref="Layer"/>), with the shared sheet and <paramref name="sheets"/> applied. Its root ignores the
        /// pointer, so only the elements that ask for clicks take them.
        /// </summary>
        public static UIDocument CreateDocument(Transform host, string name, int sortingOrder, params string[] sheets)
        {
            var go = new GameObject($"UI: {name}");
            go.SetActive(false);
            if (host != null) go.transform.SetParent(host, false);

            var document = go.AddComponent<UIDocument>();
            document.panelSettings = Settings;
            document.sortingOrder = sortingOrder;
            go.SetActive(true);

            var root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.AddToClassList("ie-root");
            AddSheet(root, SharedSheet);
            foreach (var sheet in sheets) AddSheet(root, sheet);
            return document;
        }

        private static void AddSheet(VisualElement root, string name)
        {
            var sheet = Sheet(name);
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogWarning($"[HudPanel] Missing style sheet Resources/{ResourceFolder}{name}.uss");
        }

        /// <summary>The panel's width in layout units, before or after its first layout.</summary>
        public static float Width(VisualElement root)
        {
            var width = root?.panel?.visualTree.layout.width ?? float.NaN;
            if (!float.IsNaN(width) && width > 0f) return width;
            return Screen.height > 0 ? Screen.width * ReferenceHeight / Screen.height : 1280f;
        }

        /// <summary>
        /// A world point in the panel's layout units, through <paramref name="camera"/> (the main camera when null).
        /// False when there is no camera or the point is behind it.
        /// </summary>
        public static bool WorldToPanel(VisualElement root, Vector3 world, out Vector2 point, Camera camera = null)
        {
            point = default;
            camera = camera != null ? camera : Camera.main;
            if (camera == null || root?.panel == null) return false;
            if (camera.WorldToScreenPoint(world).z < 0f) return false;

            point = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, camera);
            return true;
        }

        /// <summary>A screen pixel (origin bottom left, as the Input System reports it) in panel layout units.</summary>
        public static Vector2 ScreenToPanel(VisualElement root, Vector2 screen)
        {
            if (root?.panel == null) return screen;
            return RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
        }
    }
}
