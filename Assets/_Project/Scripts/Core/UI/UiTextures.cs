using System;
using System.Collections.Generic;
using UnityEngine;

namespace InElementum.Core.UI
{
    /// <summary>
    /// Textures the screens make in code: white fades that a tint colors (for sweeps, rules, and soft clouds), and
    /// diamond-cut portrait badges baked from a character's portrait sprite. Made once and shared.
    /// </summary>
    public static class UiTextures
    {
        private static Texture2D _fadeRight;
        private static Texture2D _fadeLeft;
        private static Texture2D _fadeBoth;
        private static Texture2D _softBox;
        private static Texture2D _radial;
        private static readonly Dictionary<Sprite, Texture2D> PortraitBadges = new Dictionary<Sprite, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _fadeRight = _fadeLeft = _fadeBoth = _softBox = _radial = null;
            PortraitBadges.Clear();
        }

        /// <summary>Opaque on the left, fading out to the right.</summary>
        public static Texture2D FadeRight => _fadeRight != null ? _fadeRight : _fadeRight = CreateFade(1, (u, v) => 1f - Mathf.SmoothStep(0f, 1f, u));
        /// <summary>Fading in from the left, opaque on the right.</summary>
        public static Texture2D FadeLeft => _fadeLeft != null ? _fadeLeft : _fadeLeft = CreateFade(1, (u, v) => Mathf.SmoothStep(0f, 1f, u));
        /// <summary>Opaque in the middle, fading out at both ends.</summary>
        public static Texture2D FadeBoth => _fadeBoth != null ? _fadeBoth : _fadeBoth = CreateFade(1, (u, v) => EdgeFade(u, 0.15f));
        /// <summary>A soft cloud: opaque in the middle, fading out on every side.</summary>
        public static Texture2D SoftBox => _softBox != null ? _softBox : _softBox = CreateFade(64, (u, v) => EdgeFade(u, 0.12f) * EdgeFade(v, 0.35f));
        /// <summary>A round glow: opaque in the middle, fading to nothing at the rim.</summary>
        public static Texture2D Radial => _radial != null ? _radial : _radial = CreateFade(128, (u, v) =>
        {
            var distance = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            return 1f - Mathf.SmoothStep(0f, 1f, distance);
        }, 128);

        /// <summary>1 in the middle of 0 to 1, easing down to 0 over the outer <paramref name="edge"/> at each end.</summary>
        private static float EdgeFade(float t, float edge) => Mathf.SmoothStep(0f, 1f, Mathf.Min(t, 1f - t) / edge);

        /// <summary>A white texture whose alpha at each point is <paramref name="alpha"/> of its position across and down.</summary>
        private static Texture2D CreateFade(int height, Func<float, float, float> alpha, int width = 256)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x / (width - 1f), height > 1 ? y / (height - 1f) : 0.5f)));
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Paper behind the portrait sketches, so line art in an element's own color still shows on its badge.</summary>
        private static readonly Color PortraitPaper = new Color(0.96f, 0.94f, 0.9f);
        private const int PortraitBadgeSize = 128;

        /// <summary>
        /// <paramref name="portrait"/> baked into a diamond of white paper with the sketch laid on like ink, or null
        /// when there is no portrait or this device cannot bake (batchmode without graphics).
        /// </summary>
        public static Texture2D PortraitBadge(Sprite portrait)
        {
            if (portrait == null) return null;
            if (PortraitBadges.TryGetValue(portrait, out var badge) && (badge != null || !CanBake)) return badge;

            badge = portrait.texture != null && CanBake ? CreatePortraitBadge(portrait, PortraitBadgeSize) : null;
            PortraitBadges[portrait] = badge;
            return badge;
        }

        private static bool CanBake => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        /// <summary>
        /// Bakes the sprite rect into a white-paper diamond: its color multiplied in and deepened, so pale lines read
        /// at badge size while white shading stays white. The sprite rect is the crop, so reframing a face is a Sprite
        /// Editor job. Goes through the GPU because imported textures are not CPU-readable.
        /// </summary>
        private static Texture2D CreatePortraitBadge(Sprite portrait, int size)
        {
            var source = portrait.texture;
            var rect = portrait.textureRect;
            var scale = new Vector2(rect.width / source.width, rect.height / source.height);
            var offset = new Vector2(rect.x / source.width, rect.y / source.height);

            var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            GL.PushMatrix();
            Graphics.Blit(source, target, scale, offset);
            RenderTexture.active = target;
            var badge = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            badge.ReadPixels(new Rect(0f, 0f, size, size), 0, 0, false);
            RenderTexture.active = previous;
            GL.PopMatrix();
            RenderTexture.ReleaseTemporary(target);

            const float toEdge = 0.70710678f;
            var radius = (size * 0.5f - 1f) * toEdge;
            var middle = (size - 1) * 0.5f;
            var pixels = badge.GetPixels();
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var i = y * size + x;
                    var ink = pixels[i];
                    var a = ink.a;
                    var distance = (Mathf.Abs(x - middle) + Mathf.Abs(y - middle)) * toEdge;
                    pixels[i] = new Color(
                        PortraitPaper.r * (1f - a + a * Mathf.Pow(ink.r, 2.2f)),
                        PortraitPaper.g * (1f - a + a * Mathf.Pow(ink.g, 2.2f)),
                        PortraitPaper.b * (1f - a + a * Mathf.Pow(ink.b, 2.2f)),
                        Mathf.Clamp01(radius - distance + 0.5f));
                }
            }

            badge.SetPixels(pixels);
            badge.Apply();
            return badge;
        }
    }
}
