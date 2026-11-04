using System.IO;
using System.Linq;
using InElementum.Core.UI;
using UnityEditor;
using UnityEngine;

namespace InElementum.Editor
{
    /// <summary>
    /// Fills the <see cref="UiArt"/> asset in Resources from the Kenney packs under <see cref="UiFolder"/>: every
    /// Fantasy UI Borders panel, border, and fading divider at both sizes, and the solid (not outlined) Input Prompts
    /// icons for keyboard and mouse, Xbox, PlayStation, and Switch. Safe to run again; style indices set by hand stay.
    /// </summary>
    public static class UiArtBuilder
    {
        public const string UiFolder = "Assets/_Project/Art/Placeholder/UI";
        private const string BordersFolder = UiFolder + "/kenney_fantasy-ui-borders/PNG";
        private const string PromptsFolder = UiFolder + "/kenney_input-prompts_1.5";
        private const string ResourcesFolder = "Assets/_Project/Resources";
        private const string AssetPath = ResourcesFolder + "/" + UiArt.ResourcePath + ".asset";

        private static readonly string[] PromptDevices = { "Keyboard & Mouse", "Xbox Series", "PlayStation Series", "Nintendo Switch" };

        [MenuItem("Tools/In Elementum/Build UI Art")]
        public static void Build()
        {
            DataAssetOps.EnsureFolder(ResourcesFolder);
            var art = AssetDatabase.LoadAssetAtPath<UiArt>(AssetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<UiArt>();
                AssetDatabase.CreateAsset(art, AssetPath);
            }

            art.panels = Textures($"{BordersFolder}/Default/Panel");
            art.panelsDouble = Textures($"{BordersFolder}/Double/Panel");
            art.borders = Textures($"{BordersFolder}/Default/Border");
            art.bordersDouble = Textures($"{BordersFolder}/Double/Border");
            art.dividerFades = Textures($"{BordersFolder}/Default/Divider Fade");
            art.dividerFadesDouble = Textures($"{BordersFolder}/Double/Divider Fade");
            art.prompts = PromptDevices
                .SelectMany(device => Textures($"{PromptsFolder}/{device}/Default"))
                .Where(texture => !texture.name.EndsWith("_outline") && !texture.name.StartsWith("controller_"))
                .ToArray();

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log($"UI art built: {art.panels.Length} frames, {art.dividerFades.Length} dividers, {art.prompts.Length} input prompts.");
        }

        /// <summary>The PNGs directly in <paramref name="folder"/>, sorted by file name.</summary>
        private static Texture2D[] Textures(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"UI art folder missing: {folder}");
                return new Texture2D[0];
            }

            return Directory.GetFiles(folder, "*.png")
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => Path.GetFileName(path), System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .Where(texture => texture != null)
                .ToArray();
        }
    }
}
