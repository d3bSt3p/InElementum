using System;
using System.Collections.Generic;
using InElementum.Data.Actions;

namespace InElementum.Combat.Model
{
    /// <summary>Items shared by the whole party. Stub: persistence comes with the save system.</summary>
    public class Inventory
    {
        private readonly Dictionary<ItemData, int> _items = new Dictionary<ItemData, int>();

        public IReadOnlyDictionary<ItemData, int> Items => _items;

        public int GetCount(ItemData item) => _items.TryGetValue(item, out var count) ? count : 0;

        /// <summary>A separate inventory with the same items, so a battle can spend from it without touching this one.</summary>
        public Inventory Clone()
        {
            var copy = new Inventory();
            foreach (var pair in _items)
                copy._items.Add(pair.Key, pair.Value);
            return copy;
        }

        /// <summary>Adds up to the item's stack limit and returns how many were actually added.</summary>
        public int Add(ItemData item, int count = 1)
        {
            var current = GetCount(item);
            var added = Math.Max(0, Math.Min(count, item.StackMax - current));
            if (added > 0) _items[item] = current + added;
            return added;
        }

        public bool TryConsume(ItemData item)
        {
            var current = GetCount(item);
            if (current <= 0) return false;

            if (current == 1)
                _items.Remove(item);
            else
                _items[item] = current - 1;

            return true;
        }
    }
}
