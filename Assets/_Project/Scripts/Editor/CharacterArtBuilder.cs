using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using InElementum.Core.Utils;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Settings;
using InElementum.Exploration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace InElementum.Editor
{
    /// <summary>
    /// Turns the placeholder character sheets (one folder per color under <see cref="SheetFolder"/>) into game art:
    /// slices every sheet into frames standing on a shared foot line, builds one <see cref="CharacterSpriteSet"/> per
    /// color, gives each party member the color of its element, and dresses the Test Room player in the leader's set.
    /// Safe to run again; frame ids are kept, so references survive.
    /// </summary>
    public static class CharacterArtBuilder
    {
        public const string SheetFolder = "Assets/_Project/Art/Placeholder/Characters";
        public const string SetFolder = "Assets/_Project/Data/Characters/Sprite Sets";
        private const string PartyFolder = "Assets/_Project/Data/Characters/Party";
        private const string TestRoomScenePath = "Assets/_Project/Scenes/TestRoom.unity";
        private const string DebugBattlePath = "Assets/_Project/Data/Settings/Debug Battle.asset";

        /// <summary>The art is drawn about 31 pixels tall; at 20 per unit it matches the 1.5 unit player collider.</summary>
        public const float PixelsPerUnit = 20f;
        /// <summary>Pixels from the bottom of a 48 pixel tall frame to the feet. Taller frames add their extra half.</summary>
        private const int FootLine = 8;

        /// <summary>Sheets whose name does not give the frame size.</summary>
        private static readonly Dictionary<string, int> FrameWidthOverrides = new Dictionary<string, int>
        {
            { "player katana running attack (upper body)", 80 }
        };

        private readonly struct ClipSpec
        {
            public readonly CharacterAnimation Animation;
            public readonly string Sheet;
            public readonly float Fps;
            public readonly bool Loop;
            public readonly int Impact;
            public readonly int First;
            public readonly int Count;

            public ClipSpec(CharacterAnimation animation, string sheet, float fps, bool loop, int impact = 0, int first = 0, int count = -1)
            {
                Animation = animation;
                Sheet = sheet;
                Fps = fps;
                Loop = loop;
                Impact = impact;
                First = first;
                Count = count;
            }
        }

        private static readonly ClipSpec[] Clips =
        {
            new ClipSpec(CharacterAnimation.Idle, "Player Idle 48x48", 10f, true),
            new ClipSpec(CharacterAnimation.BattleIdle, "Player Sword Idle 48x48", 10f, true),
            new ClipSpec(CharacterAnimation.Run, "player run 48x48", 14f, true),
            new ClipSpec(CharacterAnimation.Jump, "player new jump 48x48", 14f, false, first: 0, count: 3),
            new ClipSpec(CharacterAnimation.Fall, "player new jump 48x48", 10f, false, first: 3, count: 3),
            new ClipSpec(CharacterAnimation.Land, "player land 48x48", 24f, false, first: 0, count: 5),
            new ClipSpec(CharacterAnimation.Attack, "player sword atk 64x64", 18f, false, impact: 2),
            new ClipSpec(CharacterAnimation.Ability, "player katana attack-sheathe 80x64", 16f, false, impact: 2),
            new ClipSpec(CharacterAnimation.Surge, "player katana continuous attack 80x64", 16f, false, impact: 1),
            new ClipSpec(CharacterAnimation.Counter, "Player Sword Stab 96x48", 20f, false, impact: 4),
            new ClipSpec(CharacterAnimation.Parry, "Player Jab 48x48", 24f, false, impact: 1),
            new ClipSpec(CharacterAnimation.Dodge, "player new jump 48x48", 12f, false),
            new ClipSpec(CharacterAnimation.Hurt, "Player Hurt 48x48", 12f, false),
            new ClipSpec(CharacterAnimation.Death, "Player Death 64x64", 12f, false)
        };

        /// <summary>Which sheet color each element's characters wear.</summary>
        public static string ColorFor(Element element)
        {
            switch (element)
            {
                case Element.Fire: return "red";
                case Element.Water: return "blue";
                case Element.Wind: return "green";
                case Element.Lightning: return "yellow";
                case Element.Earth: return "orange";
                case Element.Dark: return "purple";
                default: return "grey";
            }
        }

        [MenuItem("Tools/In Elementum/Build Character Art")]
        public static void BuildCharacterArt()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>Builds without asking to save open scenes, for batchmode.</summary>
        public static void Build()
        {
            SliceSheets();
            var sets = BuildSets();
            AssignPartyMembers(sets);
            AssetDatabase.SaveAssets();
            DressTestRoomPlayer();
        }

        #region Slicing
        /// <summary>Grid-slices every sheet: point filtered, uncompressed, pivot on the foot line.</summary>
        public static void SliceSheets()
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SheetFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

                var size = ReadSize(path);
                if (size.x <= 0 || size.y <= 0) continue;
                var frameWidth = FrameWidth(Path.GetFileNameWithoutExtension(path), size.x, size.y);

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteExtrude = 0;
                importer.SetTextureSettings(settings);

                var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var existing = provider.GetSpriteRects().ToDictionary(rect => rect.name, rect => rect.spriteID);

                var stem = Path.GetFileNameWithoutExtension(path);
                var pivot = new Vector2(0.5f, (FootLine + (size.y - 48) * 0.5f) / size.y);
                var rects = new List<SpriteRect>();
                for (var i = 0; i < size.x / frameWidth; i++)
                {
                    var name = $"{stem}_{i}";
                    rects.Add(new SpriteRect
                    {
                        name = name,
                        rect = new Rect(i * frameWidth, 0, frameWidth, size.y),
                        alignment = SpriteAlignment.Custom,
                        pivot = pivot,
                        spriteID = existing.TryGetValue(name, out var id) ? id : GUID.Generate()
                    });
                }

                provider.SetSpriteRects(rects.ToArray());
                var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                names?.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
                provider.Apply();
                importer.SaveAndReimport();
            }
        }

        /// <summary>The frame size in the name when it tiles the sheet, otherwise square frames as tall as the sheet.</summary>
        private static int FrameWidth(string stem, int width, int height)
        {
            if (FrameWidthOverrides.TryGetValue(stem, out var overridden)) return overridden;

            var match = Regex.Match(stem, @"(\d+)x(\d+)");
            if (match.Success)
            {
                int named = int.Parse(match.Groups[1].Value), namedHeight = int.Parse(match.Groups[2].Value);
                if (named > 0 && namedHeight == height && width % named == 0) return named;
            }

            return height;
        }

        /// <summary>The PNG's own size, read from its header, so the importer's max size cannot hide it.</summary>
        private static Vector2Int ReadSize(string path)
        {
            var header = new byte[24];
            using (var stream = File.OpenRead(path))
            {
                if (stream.Read(header, 0, header.Length) < header.Length) return Vector2Int.zero;
            }

            int Read(int offset) => (header[offset] << 24) | (header[offset + 1] << 16) | (header[offset + 2] << 8) | header[offset + 3];
            return new Vector2Int(Read(16), Read(20));
        }
        #endregion

        #region Sets
        private static Dictionary<string, CharacterSpriteSet> BuildSets()
        {
            if (!AssetDatabase.IsValidFolder(SetFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(SetFolder).Replace('\\', '/'), Path.GetFileName(SetFolder));

            var sets = new Dictionary<string, CharacterSpriteSet>();
            foreach (var folder in AssetDatabase.GetSubFolders(SheetFolder))
            {
                var color = Path.GetFileName(folder);
                var clips = new List<SpriteClip>();
                foreach (var spec in Clips)
                {
                    var frames = LoadFrames($"{folder}/{spec.Sheet}.png");
                    if (frames.Length == 0)
                    {
                        Debug.LogWarning($"[CharacterArtBuilder] {color}: no frames in '{spec.Sheet}' for {spec.Animation}.");
                        continue;
                    }

                    var count = spec.Count < 0 ? frames.Length - spec.First : Mathf.Min(spec.Count, frames.Length - spec.First);
                    clips.Add(new SpriteClip(spec.Animation, frames.Skip(spec.First).Take(count).ToArray(), spec.Fps, spec.Loop, spec.Impact));
                }

                var path = $"{SetFolder}/{Capitalize(color)}.asset";
                var set = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(path);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<CharacterSpriteSet>();
                    AssetDatabase.CreateAsset(set, path);
                }

                set.SetClips(clips);
                EditorUtility.SetDirty(set);
                sets[color] = set;
            }

            return sets;
        }

        /// <summary>A sheet's frames in order, left to right.</summary>
        private static Sprite[] LoadFrames(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => sprite.rect.x)
                .ToArray();

        private static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        private static void AssignPartyMembers(IReadOnlyDictionary<string, CharacterSpriteSet> sets)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:PartyMemberData", new[] { PartyFolder }))
            {
                var member = AssetDatabase.LoadAssetAtPath<PartyMemberData>(AssetDatabase.GUIDToAssetPath(guid));
                if (member == null || !sets.TryGetValue(ColorFor(member.Element), out var set) || member.SpriteSet == set) continue;

                member.SpriteSet = set;
                EditorUtility.SetDirty(member);
            }
        }
        #endregion

        #region Test Room
        /// <summary>
        /// Swaps the Test Room player's block body for an animated sprite wearing the starting leader's colors. Does
        /// nothing when the scene is missing or already dressed.
        /// </summary>
        public static void DressTestRoomPlayer()
        {
            if (!File.Exists(TestRoomScenePath)) return;

            var scene = EditorSceneManager.OpenScene(TestRoomScenePath, OpenSceneMode.Single);
            var player = Object.FindAnyObjectByType<PlayerController2D>();
            if (player == null || player.GetComponentInChildren<SpriteAnimator>(true) != null) return;

            var serialized = new SerializedObject(player);
            var visual = serialized.FindProperty("visual").objectReferenceValue as Transform;
            if (visual == null) visual = player.transform;

            foreach (var child in new[] { "Body", "Eye", "Slash" })
            {
                var block = visual.Find(child);
                if (block != null) Object.DestroyImmediate(block.gameObject);
            }

            serialized.FindProperty("slashVisual").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var collider = player.GetComponent<BoxCollider2D>();
            var feet = collider != null ? collider.offset.y - collider.size.y * 0.5f : 0f;
            AddPlayerSprite(visual, feet, StartingLeaderSet());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>The animated sprite for an explored character, its pivot on <paramref name="feetY"/>.</summary>
        public static SpriteAnimator AddPlayerSprite(Transform visual, float feetY, CharacterSpriteSet set)
        {
            var spriteObject = new GameObject("Sprite") { layer = visual.gameObject.layer };
            spriteObject.transform.SetParent(visual, false);
            spriteObject.transform.localPosition = new Vector3(0f, feetY, 0f);
            spriteObject.AddComponent<SpriteRenderer>().sortingOrder = 10;
            var animator = spriteObject.AddComponent<SpriteAnimator>();
            var serialized = new SerializedObject(animator);
            serialized.FindProperty("spriteSet").objectReferenceValue = set;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            spriteObject.AddComponent<PlayerSpriteAnimator>();

            // Show the first idle frame in the scene view before play.
            var idle = set != null ? set.Find(CharacterAnimation.Idle) : null;
            if (idle != null && !idle.IsEmpty) spriteObject.GetComponent<SpriteRenderer>().sprite = idle.Frames[0];
            return animator;
        }

        /// <summary>The sprite set of the first active member of the Debug Battle party the Test Room starts with.</summary>
        public static CharacterSpriteSet StartingLeaderSet()
        {
            var battle = AssetDatabase.LoadAssetAtPath<DebugBattleData>(DebugBattlePath);
            var leader = battle != null ? battle.ActiveParty.FirstOrDefault(member => member != null) : null;
            if (leader != null && leader.SpriteSet != null) return leader.SpriteSet;
            return AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>($"{SetFolder}/{Capitalize(ColorFor(Element.None))}.asset");
        }
        #endregion
    }
}
