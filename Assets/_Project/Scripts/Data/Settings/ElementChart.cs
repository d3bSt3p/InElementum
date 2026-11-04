using System;
using System.Collections.Generic;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Settings
{
    /// <summary>
    /// Which elements counter which. A landed hit whose element counters the element of the action its target is
    /// casting fizzles that action. <see cref="Element.None"/> counters nothing and is countered by nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "Element Chart", menuName = "In Elementum/Settings/Element Chart", order = 3)]
    public class ElementChart : ScriptableObject
    {
        [Tooltip("Each entry: hits of Element fizzle casts of Counters. Add the reverse entry too for a mutual pair.")]
        [SerializeField] private List<ElementCounter> pairs = new List<ElementCounter>();

        public List<ElementCounter> Pairs { get => pairs; internal set => pairs = value; }

        /// <summary>True if a hit of <paramref name="hit"/> fizzles a cast of <paramref name="cast"/>.</summary>
        public bool IsCounter(Element hit, Element cast)
        {
            if (hit == Element.None || cast == Element.None) return false;

            foreach (var pair in pairs)
            {
                if (pair != null && pair.Element == hit && pair.Counters == cast) return true;
            }

            return false;
        }

        /// <summary>
        /// Placeholder pairs until the real chart is set: Water > Fire > Wind > Earth > Lightning > Water, and
        /// Light and Dark counter each other.
        /// </summary>
        public static List<ElementCounter> PlaceholderPairs() => new List<ElementCounter>
        {
            new ElementCounter(Element.Water, Element.Fire),
            new ElementCounter(Element.Fire, Element.Wind),
            new ElementCounter(Element.Wind, Element.Earth),
            new ElementCounter(Element.Earth, Element.Lightning),
            new ElementCounter(Element.Lightning, Element.Water),
            new ElementCounter(Element.Light, Element.Dark),
            new ElementCounter(Element.Dark, Element.Light)
        };
    }

    [Serializable]
    public class ElementCounter
    {
        [SerializeField] private Element element;
        [SerializeField] private Element counters;

        public ElementCounter() { }

        public ElementCounter(Element element, Element counters)
        {
            this.element = element;
            this.counters = counters;
        }

        public Element Element => element;
        public Element Counters => counters;
    }
}
