using System.Collections.Generic;
using UnityEngine;

namespace InElementum.Core.UI
{
    /// <summary>
    /// The placeholder UI art the IMGUI screens draw with: Kenney's Fantasy UI Borders for panels and dividers, and
    /// Kenney's Input Prompts for key and button icons. Lives in a Resources folder so code-built screens can find it
    /// without a scene reference. Filled in by Tools/In Elementum/Build UI Art; the style indices can be changed by
    /// hand in the inspector.
    /// </summary>
    public class UiArt : ScriptableObject
    {
        public const string ResourcePath = "UiArt";

        [Header("Fantasy UI Borders, indexed as in the pack (Default 48 px, Double 96 px)")]
        public Texture2D[] panels;
        public Texture2D[] panelsDouble;
        public Texture2D[] borders;
        public Texture2D[] bordersDouble;
        public Texture2D[] dividerFades;
        public Texture2D[] dividerFadesDouble;

        [Header("Styles")]
        [Tooltip("Frame for large panels: the context panel, banners, and center dialogs.")]
        public int panelStyle = 2;
        [Tooltip("Frame for small boxes: the command menu and world-space notes.")]
        public int smallStyle = 1;
        public int dividerStyle;

        [Header("Input Prompts, by file name")]
        public Texture2D[] prompts;

        private Dictionary<string, Texture2D> _promptsByName;

        private static UiArt _instance;
        private static bool _loaded;

        /// <summary>The art in Resources, or null when it has not been built.</summary>
        public static UiArt Instance
        {
            get
            {
                if (_loaded) return _instance;
                _instance = Resources.Load<UiArt>(ResourcePath);
                _loaded = true;
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _loaded = false;
        }

        /// <summary>The input prompt icon with this file name, such as "keyboard_e" or "xbox_button_color_a".</summary>
        public Texture2D Prompt(string name)
        {
            if (_promptsByName == null)
            {
                _promptsByName = new Dictionary<string, Texture2D>();
                if (prompts != null)
                {
                    foreach (var prompt in prompts)
                    {
                        if (prompt != null) _promptsByName[prompt.name] = prompt;
                    }
                }
            }

            return name != null && _promptsByName.TryGetValue(name, out var texture) ? texture : null;
        }

        /// <summary>The style's texture from <paramref name="small"/> (48 px art) or <paramref name="large"/> (96 px).</summary>
        public static Texture2D Pick(Texture2D[] small, Texture2D[] large, int style, bool useLarge)
        {
            var set = useLarge && large != null && large.Length > 0 ? large : small;
            if (set == null || set.Length == 0) return null;
            return set[Mathf.Clamp(style, 0, set.Length - 1)];
        }

        private void OnValidate() => _promptsByName = null;
    }
}
