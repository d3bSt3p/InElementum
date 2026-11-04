using System.Collections.Generic;
using InElementum.Combat.Characters;
using InElementum.Combat.Model;
using InElementum.Core.UI;
using InElementum.Data.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Combat.UI
{
    /// <summary>A thin bar over a dark track; the fill's width is the ratio.</summary>
    internal class HudBar : VisualElement
    {
        private readonly VisualElement _fill;

        public HudBar()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("ie-bar");
            style.position = Position.Absolute;
            _fill = Hud.Box("ie-bar__fill");
            Add(_fill);
        }

        public void Set(float current, float max, Color color)
        {
            _fill.style.width = Length.Percent(max > 0f ? 100f * Mathf.Clamp01(current / max) : 0f);
            _fill.style.backgroundColor = color;
        }
    }

    /// <summary>Four corner brackets framing a rect.</summary>
    internal class Brackets : VisualElement
    {
        private readonly VisualElement[] _lines = new VisualElement[8];

        public Brackets()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            for (var i = 0; i < _lines.Length; i++)
            {
                _lines[i] = Hud.Fill(Color.white);
                Add(_lines[i]);
            }
        }

        /// <summary>Frames <paramref name="rect"/> (in the parent's units) with arms <paramref name="length"/> long.</summary>
        public void Set(Rect rect, Color color, float length, float thickness)
        {
            this.Place(rect);
            float w = rect.width, h = rect.height;
            var rects = new[]
            {
                new Rect(0f, 0f, length, thickness), new Rect(0f, 0f, thickness, length),
                new Rect(w - length, 0f, length, thickness), new Rect(w - thickness, 0f, thickness, length),
                new Rect(0f, h - thickness, length, thickness), new Rect(0f, h - length, thickness, length),
                new Rect(w - length, h - thickness, length, thickness), new Rect(w - thickness, h - length, thickness, length)
            };

            for (var i = 0; i < _lines.Length; i++)
            {
                _lines[i].Place(rects[i]);
                _lines[i].style.backgroundColor = color;
            }
        }
    }

    /// <summary>
    /// A row of small diamonds, one per discovered element in its color, marked ▲ weak, ▼ resist, or × immune.
    /// Rebuilt only when the list changes.
    /// </summary>
    internal class AffinityRow : VisualElement
    {
        public const float ChipWidth = 26f;
        private string _signature;

        public AffinityRow()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.flexDirection = FlexDirection.Row;
            style.height = 16f;
        }

        public void Set(List<(Element element, ElementReaction reaction)> known, Color strong, Color weak, Color immune)
        {
            var signature = string.Join(",", known);
            if (signature == _signature) return;
            _signature = signature;
            Clear();
            style.width = known.Count * ChipWidth;

            foreach (var (element, reaction) in known)
            {
                var (mark, color) = reaction == ElementReaction.Weak ? ("▲", strong)
                    : reaction == ElementReaction.Resist ? ("▼", weak)
                    : ("×", immune);

                var chip = Hud.Box();
                chip.style.width = ChipWidth;
                chip.style.height = 16f;
                var back = new Diamond(14f).Absolute();
                back.Fill = HudColors.Panel;
                back.CenterOn(new Vector2(6f, 8f));
                var fill = new Diamond(10f).Absolute();
                fill.Fill = PlaceholderCharacter.ElementColor(element);
                fill.CenterOn(new Vector2(6f, 8f));
                var label = Hud.Text(mark, "cb-tag").Absolute();
                label.Place(new Rect(13f, 0f, 12f, 16f));
                label.style.color = color;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                chip.Add(back);
                chip.Add(fill);
                chip.Add(label);
                Add(chip);
            }
        }
    }

    /// <summary>A small outlined label: STRONG, WEAK, or NO EFFECT, for an action against the enemy's element.</summary>
    internal class AffinityChip : Label
    {
        public AffinityChip()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("cb-chip");
        }

        public void Set(ElementReaction reaction, Color strong, Color weak, Color immune)
        {
            // The enemy's reaction, read from the attack's side: an enemy weak to it makes the attack strong.
            var (label, color) = reaction == ElementReaction.Weak ? ("STRONG", strong)
                : reaction == ElementReaction.Resist ? ("WEAK", weak)
                : ("NO EFFECT", immune);
            text = label;
            style.color = color;
            Hud.SetBorder(this, 1f, color);
        }
    }

    /// <summary>A row of diamond pips for AP: lit ones larger and colored. Rebuilt only when the count changes.</summary>
    internal class PipRow : VisualElement
    {
        private readonly List<Diamond> _pips = new List<Diamond>();

        public PipRow()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
        }

        /// <summary>Lays <paramref name="max"/> pips <paramref name="step"/> apart, centered on y = 0 of this element.</summary>
        public void Set(int max, int lit, float step, Color litColor, Color emptyColor)
        {
            while (_pips.Count < max)
            {
                var pip = new Diamond().Absolute();
                _pips.Add(pip);
                Add(pip);
            }

            for (var i = 0; i < _pips.Count; i++)
            {
                var pip = _pips[i];
                pip.Show(i < max);
                if (i >= max) continue;
                var on = i < lit;
                pip.Size = on ? 11f : 8f;
                pip.Fill = on ? litColor : emptyColor;
                pip.CenterOn(new Vector2(i * step, 0f));
            }
        }
    }
}
