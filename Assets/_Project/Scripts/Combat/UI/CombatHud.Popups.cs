using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Managers;
using InElementum.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Combat.UI
{
    /// <summary>Damage numbers and impact bursts, drawn over the battle HUD and the victory screen alike.</summary>
    public partial class CombatHud
    {
        /// <summary>Where one popup draws this frame, after it has been lifted clear of the others.</summary>
        private struct PopupLayout
        {
            public FeedbackPopup Popup;
            public float Elapsed;
            public float Scale;
            public int FontSize;
            /// <summary>Center of the popup's text, in panel units.</summary>
            public Vector2 Center;
            /// <summary>Where its shock ring bursts: the natural spot, before any lift.</summary>
            public Vector2 RingCenter;
            /// <summary>The space the popup takes up, crit label included, plus a small gap.</summary>
            public Rect Box;
        }

        private const float PopupGap = 4f;
        private const float CritLabelSize = 15f;

        private VisualElement _popupLayer;
        private readonly List<VisualElement> _rings = new List<VisualElement>();
        private readonly List<Label> _popupLabels = new List<Label>();
        private readonly List<Label> _critLabels = new List<Label>();
        private readonly List<PopupLayout> _popupLayouts = new List<PopupLayout>();
        private readonly List<Rect> _placedPopups = new List<Rect>();
        private readonly Dictionary<int, float> _popupLift = new Dictionary<int, float>();
        private readonly HashSet<int> _livePopups = new HashSet<int>();

        private void BuildPopups(VisualElement root)
        {
            _popupLayer = Layer(root);
        }

        /// <summary>
        /// Lays out every popup, then shows them. Oldest popups keep their path; each newer one is lifted above
        /// anything it would touch, across all targets, so no two ever overlap. A popup's lift only grows, so it
        /// never drops back down into another. Rings sit under every number.
        /// </summary>
        private void RefreshPopups()
        {
            LayoutPopups();

            var ring = 0;
            foreach (var impact in Manager.ImpactRings)
            {
                if (HudPanel.WorldToPanel(_root, impact.Origin, out var center))
                    SetBurst(ref ring, center, Time.time - impact.Time, 100f, impact.Color);
            }

            foreach (var layout in _popupLayouts)
            {
                if (layout.Popup.Style == PopupStyle.Info) continue;
                SetBurst(ref ring, layout.RingCenter, layout.Elapsed, layout.Popup.Style == PopupStyle.Crit ? 150f : 100f, layout.Popup.Color);
            }

            for (var i = ring; i < _rings.Count; i++) _rings[i].Show(false);

            var crits = 0;
            for (var i = 0; i < _popupLayouts.Count; i++) SetPopup(i, _popupLayouts[i], ref crits);
            for (var i = _popupLayouts.Count; i < _popupLabels.Count; i++) _popupLabels[i].Show(false);
            for (var i = crits; i < _critLabels.Count; i++) _critLabels[i].Show(false);
        }

        private void LayoutPopups()
        {
            _popupLayouts.Clear();
            foreach (var popup in Manager.Popups)
            {
                if (!HudPanel.WorldToPanel(_root, popup.Origin, out var origin)) continue;
                _popupLayouts.Add(LayoutPopup(popup, origin, Time.time - popup.Time, Manager.PopupLifetime));
            }

            _popupLayouts.Sort((a, b) => a.Popup.Id.CompareTo(b.Popup.Id));
            _placedPopups.Clear();
            _livePopups.Clear();
            for (var i = 0; i < _popupLayouts.Count; i++)
            {
                var layout = _popupLayouts[i];
                _popupLift.TryGetValue(layout.Popup.Id, out var lift);
                lift = LiftToClear(layout.Box, lift);

                layout.Center.y -= lift;
                layout.Box.y -= lift;
                _popupLayouts[i] = layout;

                _placedPopups.Add(layout.Box);
                _popupLift[layout.Popup.Id] = lift;
                _livePopups.Add(layout.Popup.Id);
            }

            foreach (var id in _popupLift.Keys.Where(id => !_livePopups.Contains(id)).ToList())
                _popupLift.Remove(id);
        }

        /// <summary>How far up, from at least <paramref name="lift"/>, <paramref name="box"/> must move to clear every placed popup.</summary>
        private float LiftToClear(Rect box, float lift)
        {
            for (var pass = 0; pass < 64; pass++)
            {
                var shifted = new Rect(box.x, box.y - lift, box.width, box.height);
                var blocked = false;
                foreach (var other in _placedPopups)
                {
                    if (!shifted.Overlaps(other)) continue;
                    // The small extra stops float rounding from leaving the box a hair inside and retrying forever.
                    lift += shifted.yMax - other.yMin + 0.01f;
                    blocked = true;
                    break;
                }

                if (!blocked) break;
            }

            return lift;
        }

        private static float PopupFontSize(PopupStyle style)
        {
            switch (style)
            {
                case PopupStyle.Crit: return 46f;
                case PopupStyle.Weak: return 38f;
                case PopupStyle.Damage: return 32f;
                default: return 22f;
            }
        }

        /// <summary>A popup's natural motion: it slams in oversized, settles, arcs up and to the side, then shrinks and fades.</summary>
        private static PopupLayout LayoutPopup(FeedbackPopup popup, Vector2 origin, float elapsed, float lifetime)
        {
            var age = Mathf.Clamp01(elapsed / lifetime);
            var isDamage = popup.Style != PopupStyle.Info;
            var isCrit = popup.Style == PopupStyle.Crit;

            // Punch: grow past full size fast, then ease back down. Timed in seconds so it stays snappy at any lifetime.
            var peak = isCrit ? 1.8f : isDamage ? 1.5f : 1.2f;
            var punch = elapsed < 0.07f
                ? Mathf.Lerp(0.4f, peak, elapsed / 0.07f)
                : Mathf.Lerp(peak, 1f, EaseOutCubic((elapsed - 0.07f) / 0.16f));
            var scale = punch * Mathf.Lerp(1f, 0.75f, Mathf.InverseLerp(0.65f, 1f, age));

            var rise = EaseOutCubic(age) * (isDamage ? 75f : 55f);
            var drift = EaseOutCubic(age) * popup.Drift * (isDamage ? 30f : 12f);
            var center = new Vector2(origin.x + drift, origin.y - rise);

            var fontSize = Mathf.Max(1, Mathf.RoundToInt(PopupFontSize(popup.Style) * scale));
            // Bold numerals run about 0.6 em wide.
            var width = (popup.Text?.Length ?? 0) * fontSize * 0.6f;
            var textHeight = fontSize * 1.15f;
            var top = center.y - textHeight * 0.5f - (isCrit ? CritLabelSize * scale * 1.2f : 0f);
            var bottom = center.y + textHeight * 0.5f;

            // Room for the outline, crit shake and tilt.
            var pad = PopupGap + (isCrit ? 6f : 3f);
            var box = Rect.MinMaxRect(center.x - width * 0.5f - pad, top - pad, center.x + width * 0.5f + pad, bottom + pad);

            return new PopupLayout
            {
                Popup = popup,
                Elapsed = elapsed,
                Scale = scale,
                FontSize = fontSize,
                Center = center,
                RingCenter = center,
                Box = box
            };
        }

        /// <summary>A shock ring <paramref name="elapsed"/> seconds into bursting out to <paramref name="maxDiameter"/> and fading.</summary>
        private void SetBurst(ref int index, Vector2 center, float elapsed, float maxDiameter, Color color)
        {
            if (elapsed >= CombatManager.ImpactRingLifetime) return;

            if (index >= _rings.Count)
            {
                var created = Ring(20f, 3f, color);
                _popupLayer.Insert(0, created);
                _rings.Add(created);
            }

            var ring = _rings[index++];
            var t = elapsed / CombatManager.ImpactRingLifetime;
            color.a = (1f - t) * 0.7f;
            SizeRing(ring, center, Mathf.Lerp(20f, maxDiameter, EaseOutCubic(t)));
            Hud.SetBorder(ring, 3f, color);
            ring.Show(true);
        }

        /// <summary>Shows a laid-out popup with a white impact flash. Crits also shake, tilt, and carry a CRITICAL tag.</summary>
        private void SetPopup(int index, PopupLayout layout, ref int crits)
        {
            while (_popupLabels.Count <= index)
            {
                var created = Hud.Text(string.Empty, "cb-popup").Absolute();
                _popupLayer.Add(created);
                _popupLabels.Add(created);
            }

            var popup = layout.Popup;
            var elapsed = layout.Elapsed;
            var isDamage = popup.Style != PopupStyle.Info;
            var isCrit = popup.Style == PopupStyle.Crit;
            var age = Mathf.Clamp01(elapsed / Manager.PopupLifetime);

            var center = layout.Center;
            if (isCrit && elapsed < 0.3f)
            {
                var shake = 4f * (1f - elapsed / 0.3f);
                center += new Vector2(Mathf.Sin(elapsed * 95f) * shake, Mathf.Cos(elapsed * 80f) * shake);
            }

            // Flash white on impact, then settle into the popup's color.
            var color = Color.Lerp(Color.white, popup.Color, Mathf.Clamp01(elapsed / 0.15f));
            color.a = 1f - Mathf.InverseLerp(0.65f, 1f, age);
            var outline = new Color(0.05f, 0.03f, 0.08f, color.a * 0.9f);
            var thickness = isDamage ? 2.5f : 1.5f;

            var label = _popupLabels[index];
            label.Show(true);
            StylePopup(label, popup.Text, layout.FontSize, color, outline, thickness);
            label.Move(center);
            label.style.rotate = new Rotate(new Angle(isCrit ? popup.Drift * -6f : 0f, AngleUnit.Degree));
            label.BringToFront();

            if (!isCrit) return;

            if (crits >= _critLabels.Count)
            {
                var created = Hud.Text("CRITICAL", "cb-popup").Absolute();
                _popupLayer.Add(created);
                _critLabels.Add(created);
            }

            var crit = _critLabels[crits++];
            var labelSize = CritLabelSize * layout.Scale;
            crit.Show(true);
            StylePopup(crit, "CRITICAL", Mathf.Max(1, Mathf.RoundToInt(labelSize)), color, outline, 1.5f);
            crit.Move(new Vector2(center.x, center.y - layout.FontSize * 0.575f - labelSize * 0.6f));
            crit.style.rotate = label.style.rotate;
            crit.BringToFront();
        }

        /// <summary>Solid outlined text with a little drop depth, so it punches out of any background.</summary>
        private static void StylePopup(Label label, string text, int fontSize, Color color, Color outline, float thickness)
        {
            label.text = text;
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityTextOutlineColor = outline;
            label.style.unityTextOutlineWidth = thickness;
            label.style.textShadow = new TextShadow { offset = new Vector2(0f, thickness * 1.6f), blurRadius = 0f, color = outline };
        }
    }
}
