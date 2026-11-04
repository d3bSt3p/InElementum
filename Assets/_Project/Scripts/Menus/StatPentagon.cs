using InElementum.Core.UI;
using InElementum.Data.Enums;
using InElementum.Data.Stats;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Menus
{
    /// <summary>
    /// A radar chart of the five attributes: Vitality at the top, then Strength, Speed, Luck, and Defense clockwise.
    /// The gilt shape is the member's final stats; the faint outline under it is their base stats, so what skills and
    /// pictos add shows as the gap between the two. Every axis shares one scale, rounded up to the next 5 above the
    /// highest stat (at least 20), so the shape keeps its proportions.
    /// </summary>
    public sealed class StatPentagon : VisualElement
    {
        public static readonly StatType[] Axes = { StatType.Vitality, StatType.Strength, StatType.Speed, StatType.Luck, StatType.Defense };

        private const int Rings = 4;
        private const float MinScale = 20f;
        /// <summary>Room left around the chart for the axis labels, in panel units.</summary>
        private const float LabelRoom = 26f;

        private readonly Label[] _labels = new Label[Axes.Length];
        private readonly float[] _final = new float[Axes.Length];
        private readonly float[] _base = new float[Axes.Length];
        private float _scale = MinScale;
        private bool _hasBase;

        public StatPentagon()
        {
            AddToClassList("stat-pentagon");
            pickingMode = PickingMode.Ignore;
            for (var i = 0; i < Axes.Length; i++)
            {
                var label = new Label();
                label.AddToClassList("stat-pentagon__label");
                label.pickingMode = PickingMode.Ignore;
                label.style.position = Position.Absolute;
                label.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
                _labels[i] = label;
                Add(label);
            }

            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        /// <summary>
        /// Shows <paramref name="final"/>, with <paramref name="baseStats"/> as the faint outline beneath.
        /// <paramref name="mark"/> can add a gilt tag after an axis's value, such as a weapon's scaling grade.
        /// </summary>
        public void SetStats(StatBlock final, StatBlock? baseStats = null, System.Func<StatType, string> mark = null)
        {
            var highest = 0f;
            var accent = ColorUtility.ToHtmlStringRGB(HudColors.Accent);
            for (var i = 0; i < Axes.Length; i++)
            {
                _final[i] = final[Axes[i]];
                _base[i] = baseStats.HasValue ? baseStats.Value[Axes[i]] : _final[i];
                highest = Mathf.Max(highest, _final[i], _base[i]);
                var tag = mark?.Invoke(Axes[i]);
                var suffix = string.IsNullOrEmpty(tag) ? "" : $" <color=#{accent}>{tag}</color>";
                _labels[i].text = $"{Short(Axes[i])} <color=#F7F5EB>{final[Axes[i]]}</color>{suffix}";
            }

            _hasBase = baseStats.HasValue;
            _scale = Mathf.Max(MinScale, Mathf.Ceil(highest / 5f) * 5f);
            MarkDirtyRepaint();
        }

        public static string Short(StatType stat)
        {
            switch (stat)
            {
                case StatType.Vitality: return "VIT";
                case StatType.Strength: return "STR";
                case StatType.Speed: return "SPD";
                case StatType.Luck: return "LCK";
                default: return "DEF";
            }
        }

        private Vector2 Center => contentRect.center;
        private float Radius => Mathf.Max(0f, Mathf.Min(contentRect.width, contentRect.height) * 0.5f - LabelRoom);

        /// <summary>Point on axis <paramref name="axis"/> at <paramref name="fraction"/> of the radius.</summary>
        private Vector2 Point(int axis, float fraction, float radius)
        {
            var angle = Mathf.Deg2Rad * (-90f + 360f / Axes.Length * axis);
            return Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * fraction;
        }

        private void PlaceLabels()
        {
            var radius = Radius;
            for (var i = 0; i < Axes.Length; i++)
            {
                var point = Point(i, 1f, radius + 14f);
                _labels[i].style.left = point.x;
                _labels[i].style.top = point.y;
            }

            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var radius = Radius;
            if (radius <= 1f) return;
            var painter = context.painter2D;
            painter.lineJoin = LineJoin.Round;

            // Grid rings and spokes.
            painter.lineWidth = 1f;
            painter.strokeColor = new Color(0.8f, 0.82f, 0.86f, 0.16f);
            for (var ring = 1; ring <= Rings; ring++)
                Polygon(painter, i => (float)ring / Rings, radius, fill: false);
            painter.strokeColor = new Color(0.8f, 0.82f, 0.86f, 0.22f);
            for (var i = 0; i < Axes.Length; i++)
            {
                painter.BeginPath();
                painter.MoveTo(Center);
                painter.LineTo(Point(i, 1f, radius));
                painter.Stroke();
            }

            // Final stats: filled gilt shape.
            var accent = HudColors.Accent;
            painter.fillColor = new Color(accent.r, accent.g, accent.b, 0.28f);
            painter.strokeColor = accent;
            painter.lineWidth = 2f;
            Polygon(painter, i => Fraction(_final[i]), radius, fill: true);

            // Base stats: faint outline; it sits under the gilt edge wherever nothing adds to a stat.
            if (_hasBase)
            {
                painter.strokeColor = new Color(0.97f, 0.96f, 0.92f, 0.45f);
                painter.lineWidth = 1f;
                Polygon(painter, i => Fraction(_base[i]), radius, fill: false);
            }

            // Vertex dots.
            painter.fillColor = accent;
            for (var i = 0; i < Axes.Length; i++)
            {
                painter.BeginPath();
                painter.Arc(Point(i, Fraction(_final[i]), radius), 2.5f, 0f, 360f);
                painter.Fill();
            }
        }

        private float Fraction(float value) => Mathf.Clamp(value / _scale, 0.04f, 1f);

        private void Polygon(Painter2D painter, System.Func<int, float> fraction, float radius, bool fill)
        {
            painter.BeginPath();
            painter.MoveTo(Point(0, fraction(0), radius));
            for (var i = 1; i < Axes.Length; i++) painter.LineTo(Point(i, fraction(i), radius));
            painter.ClosePath();
            if (fill) painter.Fill();
            painter.Stroke();
        }
    }
}
