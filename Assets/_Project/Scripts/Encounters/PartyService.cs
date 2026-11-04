using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Actions;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Items;
using InElementum.Data.Enums;
using InElementum.Data.Progression;
using InElementum.Data.Settings;

namespace InElementum.Encounters
{
    /// <summary>Where a party member stands in the formation.</summary>
    public enum PartyRole
    {
        /// <summary>In the party but not taken into battle.</summary>
        Benched,
        /// <summary>Fights in battle, in their slot's order.</summary>
        Active
    }

    /// <summary>
    /// The party between battles: who fights (the active members, in slot order), their progression, the shared item bag, currency,
    /// materials, and HP carried from one battle to the next. Who is explored with in the field does not matter
    /// here. Plain C#. Only a victory changes it, so a restarted battle starts from the same state.
    /// </summary>
    public sealed class PartyService
    {
        private readonly CombatTuning _tuning;
        private readonly List<PartyMemberData> _active = new List<PartyMemberData>();
        private readonly Dictionary<MaterialData, int> _materials = new Dictionary<MaterialData, int>();
        /// <summary>Current HP of members hurt in an earlier battle. A member missing here is at full health.</summary>
        private readonly Dictionary<PartyMemberData, int> _health = new Dictionary<PartyMemberData, int>();

        public PartyService(CombatTuning tuning, ProgressionTuning progressionTuning)
        {
            _tuning = tuning != null ? tuning : throw new ArgumentNullException(nameof(tuning));
            State = new PartyState(tuning, progressionTuning);
        }

        public CombatTuning Tuning => _tuning;

        /// <summary>Levels, pictos, Luminas, skill trees, relationships, and discovered weaknesses.</summary>
        public PartyState State { get; }
        /// <summary>The shared item bag. A battle spends from a copy, written back on victory.</summary>
        public Inventory Items { get; private set; } = new Inventory();
        public int Currency { get; private set; }
        public IReadOnlyDictionary<MaterialData, int> Materials => _materials;

        public IReadOnlyList<PartyMemberData> Active => _active;
        public IEnumerable<PartyMemberData> Members => State.Members;
        /// <summary>Members in the party but not active, in the order they joined.</summary>
        public IEnumerable<PartyMemberData> Benched => State.Members.Where(member => !_active.Contains(member));
        public int MaxActive => _tuning.MaxActivePartySize;

        #region Formation
        /// <summary>
        /// Adds a member at level 1 and puts them in <paramref name="role"/>, or on the bench if that role is full.
        /// Does nothing to a member already in the party.
        /// </summary>
        public void AddMember(PartyMemberData member, PartyRole role = PartyRole.Benched)
        {
            if (member == null || State.GetProgress(member) != null) return;

            State.AddMember(member);
            TrySetRole(member, role);
        }

        public PartyRole GetRole(PartyMemberData member) => _active.Contains(member) ? PartyRole.Active : PartyRole.Benched;

        /// <summary>
        /// Moves a member to <paramref name="role"/>, joining the active party in its last slot. Refused for a member
        /// not in the party, a full active party, or the last active member leaving it.
        /// </summary>
        public bool TrySetRole(PartyMemberData member, PartyRole role)
        {
            if (!InParty(member)) return false;

            var current = GetRole(member);
            if (current == role) return true;
            if (role == PartyRole.Active)
            {
                if (_active.Count >= _tuning.MaxActivePartySize) return false;
                _active.Add(member);
                return true;
            }

            if (_active.Count == 1) return false;
            _active.Remove(member);
            return true;
        }

        /// <summary>
        /// Swaps two members' places: two active members trade slots, and an active and a benched member trade
        /// roles, the newcomer taking the leaver's slot. Refused for a member not in the party or two benched members.
        /// </summary>
        public bool TrySwap(PartyMemberData first, PartyMemberData second)
        {
            if (!InParty(first) || !InParty(second)) return false;
            if (first == second) return true;

            var firstSlot = _active.IndexOf(first);
            var secondSlot = _active.IndexOf(second);
            if (firstSlot < 0 && secondSlot < 0) return false;

            if (firstSlot >= 0) _active[firstSlot] = second;
            if (secondSlot >= 0) _active[secondSlot] = first;
            return true;
        }

        /// <summary>
        /// Moves an active member to <paramref name="slot"/>, shifting the members between. A benched member joins
        /// there if the active party has room. Refused for a member not in the party or a full active party.
        /// </summary>
        public bool TryMoveToSlot(PartyMemberData member, int slot)
        {
            if (!InParty(member)) return false;

            var active = _active.Contains(member);
            if (!active && _active.Count >= _tuning.MaxActivePartySize) return false;

            _active.Remove(member);
            _active.Insert(Math.Max(0, Math.Min(slot, _active.Count)), member);
            return true;
        }

        private bool InParty(PartyMemberData member) => member != null && State.GetProgress(member) != null;
        #endregion

        #region Health
        /// <summary>The member's current HP, or null when they are at full health.</summary>
        public int? GetHealth(PartyMemberData member) =>
            member != null && _health.TryGetValue(member, out var health) ? health : (int?)null;

        /// <summary>The member's max HP with their pictos and Luminas, or 0 for someone not in the party.</summary>
        public int GetMaxHealth(PartyMemberData member) =>
            member != null && State.GetProgress(member) != null ? CreateCombatant(member, 0).Stats.MaxHealth : 0;

        /// <summary>The member's HP now, which is their max when they are not hurt.</summary>
        public int GetCurrentHealth(PartyMemberData member) => GetHealth(member) ?? GetMaxHealth(member);

        /// <summary>Back to full HP, as at a checkpoint.</summary>
        public void RestoreAll() => _health.Clear();
        #endregion

        #region Items
        /// <summary>
        /// Whether <paramref name="item"/> can be used on <paramref name="member"/> outside combat: the bag holds one,
        /// it is marked usable outside combat, and it would restore health the member is missing. Only HP carries
        /// between battles, so items that restore AP or revive do nothing here.
        /// </summary>
        public bool CanUseItem(ItemData item, PartyMemberData member)
        {
            if (item == null || !item.UsableOutsideCombat || Items.GetCount(item) <= 0) return false;
            if (member == null || State.GetProgress(member) == null || !HealsHealth(item)) return false;
            return _health.ContainsKey(member);
        }

        /// <summary>Uses one <paramref name="item"/> on <paramref name="member"/> outside combat. False if it cannot be used.</summary>
        public bool UseItem(ItemData item, PartyMemberData member)
        {
            if (!CanUseItem(item, member) || !Items.TryConsume(item)) return false;

            if (!EffectProcessor.IsInitialized) EffectProcessor.Initialize();

            var unit = CreateCombatant(member, 0);
            foreach (var effect in item.Effects.Where(effect => effect != null && effect.EffectType == EffectType.Heal))
                EffectProcessor.GetEffect(effect.EffectType)?.Apply(EffectContext.From(effect, unit, unit, item));

            if (unit.Stats.CurrentHealth >= unit.Stats.MaxHealth) _health.Remove(member);
            else _health[member] = unit.Stats.CurrentHealth;
            return true;
        }

        private static bool HealsHealth(ItemData item) =>
            item.Effects != null && item.Effects.Any(effect => effect != null && effect.EffectType == EffectType.Heal && effect.Value > 0f);
        #endregion

        #region Battle
        /// <summary>
        /// A battle-ready unit for <paramref name="member"/>: their pictos, Luminas, skill tree bonuses, and weapon,
        /// and the HP they carry.
        /// </summary>
        public Combatant CreateCombatant(PartyMemberData member, int slot)
        {
            var unit = Combatant.ForPartyMember(member, _tuning, slot, State.GetLoadout(member));
            var weapon = State.Weapons.GetWeapon(member);
            unit.SetWeapon(weapon, State.Weapons.GetLevel(weapon));
            var tree = State.GetSkillTree(member);
            if (tree != null) unit.Stats.SetPermanentBonuses(tree.Modifiers, tree.Passives);
            // Skill tree bonuses can raise max HP after the unit was made, so an unhurt member is filled back up.
            unit.Stats.SetCurrentHealth(_health.TryGetValue(member, out var health) ? health : unit.Stats.MaxHealth);
            return unit;
        }

        /// <summary>
        /// Writes a battle's outcome into the party. On victory: XP, picto mastery, and picto drops (see
        /// <see cref="PartyState.ApplyVictory"/>), currency and materials, the items left in
        /// <paramref name="battleInventory"/>, and each participant's HP, with downed members back up at 1 HP.
        /// A defeat changes nothing and returns null.
        /// </summary>
        public VictoryReport ApplyBattle(BattleResult result, Inventory battleInventory)
        {
            if (result == null || result.Outcome != BattleOutcome.Victory) return null;

            var participants = result.Participants.Where(unit => unit.Member != null).ToList();
            var rewards = result.Rewards ?? new BattleRewards();
            var report = State.ApplyVictory(participants.Select(unit => unit.Member), rewards.Experience, rewards.Pictos);

            Currency += rewards.Currency;
            foreach (var (material, amount) in rewards.Materials)
            {
                if (material == null || amount <= 0) continue;
                _materials[material] = (_materials.TryGetValue(material, out var owned) ? owned : 0) + amount;
            }

            if (battleInventory != null) Items = battleInventory.Clone();

            foreach (var unit in participants)
            {
                var health = unit.IsAlive ? unit.Stats.CurrentHealth : 1;
                if (health >= unit.Stats.MaxHealth) _health.Remove(unit.Member);
                else _health[unit.Member] = health;
            }

            return report;
        }
        #endregion

        /// <summary>The Debug Battle's party and items, at level 1 and full health.</summary>
        public static PartyService FromDebugBattle(DebugBattleData debugBattle, CombatTuning tuning, ProgressionTuning progressionTuning)
        {
            var service = new PartyService(tuning, progressionTuning);
            foreach (var member in debugBattle.ActiveParty) service.AddMember(member, PartyRole.Active);
            foreach (var member in debugBattle.BenchedParty) service.AddMember(member);
            foreach (var stack in debugBattle.StartingItems.Where(stack => stack != null && stack.Item != null))
                service.Items.Add(stack.Item, stack.Count);
            return service;
        }
    }
}
