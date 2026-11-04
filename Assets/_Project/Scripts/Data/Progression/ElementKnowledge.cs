using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Characters;
using InElementum.Data.Enums;

namespace InElementum.Data.Progression
{
    /// <summary>
    /// Which elements the party has tried on each kind of enemy. An element is discovered for an enemy type the first
    /// time a party hit of that element lands on one, and its reaction (weak, resist, immune, or normal) shows from then
    /// on, in every later battle too. Plain C#; persistence comes later.
    /// </summary>
    public class ElementKnowledge
    {
        private readonly Dictionary<EnemyCharacterData, HashSet<Element>> _known = new Dictionary<EnemyCharacterData, HashSet<Element>>();

        /// <summary>An element was discovered for an enemy type for the first time.</summary>
        public event Action<EnemyCharacterData, Element> Discovered;

        /// <summary>Records that <paramref name="element"/> hit <paramref name="enemy"/>. False if it was already known or is None.</summary>
        public bool Discover(EnemyCharacterData enemy, Element element)
        {
            if (enemy == null || element == Element.None) return false;

            if (!_known.TryGetValue(enemy, out var elements))
            {
                elements = new HashSet<Element>();
                _known.Add(enemy, elements);
            }

            if (!elements.Add(element)) return false;

            Discovered?.Invoke(enemy, element);
            return true;
        }

        public bool IsDiscovered(EnemyCharacterData enemy, Element element) =>
            enemy != null && _known.TryGetValue(enemy, out var elements) && elements.Contains(element);

        /// <summary>The discovered elements for an enemy type, in enum order.</summary>
        public IEnumerable<Element> GetDiscovered(EnemyCharacterData enemy) =>
            enemy != null && _known.TryGetValue(enemy, out var elements)
                ? elements.OrderBy(element => element)
                : Enumerable.Empty<Element>();

        /// <summary>
        /// The enemy type's authored damage multiplier for an element: 2 weak, 0.5 resist, 0 immune, 1 when it has no
        /// affinity. Unlike the live combatant, this ignores a Break's resistance drop.
        /// </summary>
        public static float GetAffinity(EnemyCharacterData enemy, Element element)
        {
            if (enemy == null || element == Element.None || enemy.Affinities == null) return 1f;

            foreach (var affinity in enemy.Affinities)
            {
                if (affinity != null && affinity.Element == element) return affinity.DamageMultiplier;
            }

            return 1f;
        }
    }
}
