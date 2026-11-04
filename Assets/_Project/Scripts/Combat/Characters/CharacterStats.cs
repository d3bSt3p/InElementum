using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Pictos;
using InElementum.Data.Settings;
using InElementum.Data.Stats;

namespace InElementum.Combat.Characters
{
    public class StatusStats
    {
        public StatusStats(StatusKey key)
        {
            Key = key;
        }

        public StatusKey Key { get; }
        public int StatusValue { get; set; }
        public bool DecreaseOverTurn { get; set; }
        public bool IncreaseOverTurn { get; set; }
        public bool IsActive { get; set; }
        public bool IsNegative { get; set; }

        public Action OnTriggerAction;
    }

    /// <summary>
    /// Runtime state of one combatant: stats, HP, AP, and statuses. Plain C#, so views subscribe to its events
    /// (see <see cref="BindCanvas"/>) and tests can run it without a scene.
    /// </summary>
    public class CharacterStats
    {
        private static readonly StatType[] AllStats = (StatType[])Enum.GetValues(typeof(StatType));

        private readonly CombatTuning _tuning;
        private readonly Dictionary<StatusKey, StatusStats> _statuses = new Dictionary<StatusKey, StatusStats>();
        private Loadout _loadout;
        private IEnumerable<StatModifier> _mealBuff;
        private IReadOnlyList<StatModifier> _permanentModifiers = Array.Empty<StatModifier>();
        private IReadOnlyList<PassiveData> _permanentPassives = Array.Empty<PassiveData>();
        private IReadOnlyList<ElementAffinity> _affinities;
        private int _bonusMaxHealth;

        public CombatTuning Tuning => _tuning;

        public StatBlock BaseStats { get; }
        public StatBlock FinalStats { get; private set; }
        public Loadout Loadout => _loadout;

        /// <summary>Every passive in effect: from worn pictos, equipped Luminas, and the skill tree.</summary>
        public IEnumerable<PassiveData> Passives =>
            (_loadout != null ? _loadout.Passives : Enumerable.Empty<PassiveData>()).Concat(_permanentPassives);

        public int MaxHealth { get; private set; }
        public int CurrentHealth { get; private set; }
        public int MaxAp { get; }
        public int CurrentAp { get; private set; }
        public bool IsStunned { get; private set; }
        public bool IsIncapacitated { get; private set; }

        public IReadOnlyDictionary<StatusKey, StatusStats> Statuses => _statuses;

        public Action OnIncapacitated;
        public Action OnRevived;
        public Action<int, int> OnHealthChanged;
        public Action<int, int> OnApChanged;
        public Action<StatusKey, int> OnStatusChanged;
        public Action<StatusKey, int> OnStatusApplied;
        public Action<StatusKey> OnStatusCleared;
        public Action OnHealAction;
        public Action OnTakeDamageAction;

        #region Setup
        public CharacterStats(StatBlock baseStats, CombatTuning tuning, int maxAp = 0)
        {
            _tuning = tuning != null ? tuning : throw new ArgumentNullException(nameof(tuning));
            BaseStats = baseStats;
            MaxAp = Math.Max(0, maxAp);

            SetAllStatus();
            RecalculateStats();
            CurrentHealth = MaxHealth;
        }

        public void BindCanvas(CharacterCanvas canvas)
        {
            OnHealthChanged += canvas.UpdateHealthText;
            OnStatusChanged += canvas.UpdateStatusText;
            OnStatusApplied += canvas.ApplyStatus;
            OnStatusCleared += canvas.ClearStatus;
        }

        private void SetAllStatus()
        {
            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
            {
                if (type == StatusEffectType.None) continue;

                if (StatusKey.IsKeyedByStat(type))
                {
                    foreach (var stat in AllStats)
                        AddStatus(new StatusKey(type, stat));
                }
                else
                {
                    AddStatus(new StatusKey(type));
                }
            }

            var poison = GetStatus(StatusEffectType.Poison);
            poison.DecreaseOverTurn = true;
            poison.IsNegative = true;
            poison.OnTriggerAction += DamagePoison;

            var stun = GetStatus(StatusEffectType.Stun);
            stun.DecreaseOverTurn = true;
            stun.IsNegative = true;
            stun.OnTriggerAction += CheckStunStatus;

            var burn = GetStatus(StatusEffectType.Burn);
            burn.IncreaseOverTurn = true;
            burn.IsNegative = true;
            burn.OnTriggerAction += DamageBurn;

            var weakness = GetStatus(StatusEffectType.Weakness);
            weakness.DecreaseOverTurn = true;
            weakness.IsNegative = true;

            var soft = GetStatus(StatusEffectType.Soft);
            soft.DecreaseOverTurn = true;
            soft.IsNegative = true;

            foreach (var stat in AllStats)
                GetStatus(StatusEffectType.StatDebuff, stat).IsNegative = true;
        }

        private void AddStatus(StatusKey key) => _statuses.Add(key, new StatusStats(key));
        private StatusStats GetStatus(StatusEffectType type, StatType stat = default) => _statuses[new StatusKey(type, stat)];
        #endregion

        #region Stats
        public void SetLoadout(Loadout loadout)
        {
            _loadout = loadout;
            RecalculateStats();
        }

        /// <summary>
        /// Bonuses that last for good, such as unlocked skill tree nodes: stat modifiers, and passives that work like
        /// equipped Luminas.
        /// </summary>
        public void SetPermanentBonuses(IEnumerable<StatModifier> modifiers, IEnumerable<PassiveData> passives)
        {
            _permanentModifiers = modifiers != null ? modifiers.ToList() : (IReadOnlyList<StatModifier>)Array.Empty<StatModifier>();
            _permanentPassives = passives != null ? passives.Where(passive => passive != null).ToList() : (IReadOnlyList<PassiveData>)Array.Empty<PassiveData>();
            RecalculateStats();
        }

        public void SetMealBuff(IEnumerable<StatModifier> mealBuff)
        {
            _mealBuff = mealBuff;
            RecalculateStats();
        }

        /// <summary>Call after changing a <see cref="Loadout"/> already assigned to this character.</summary>
        public void RecalculateStats()
        {
            FinalStats = StatCalculator.GetFinalStats(BaseStats, _loadout, GetStatusModifiers(), _mealBuff, _permanentModifiers);
            MaxHealth = StatCalculator.GetMaxHealth(FinalStats.Vitality, _tuning) + _bonusMaxHealth;

            if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        private List<StatModifier> GetStatusModifiers()
        {
            var modifiers = new List<StatModifier>();

            foreach (var stat in AllStats)
            {
                var net = GetStatusValue(StatusEffectType.StatBuff, stat) - GetStatusValue(StatusEffectType.StatDebuff, stat);
                if (net != 0) modifiers.Add(new StatModifier(stat, ModifierKind.Flat, net));
            }

            return modifiers;
        }

        /// <summary>Damage multipliers per element. Elements not listed take normal damage.</summary>
        public void SetElementAffinities(IReadOnlyList<ElementAffinity> affinities)
        {
            _affinities = affinities;
        }

        /// <summary>While set, as for a Broken enemy, resistances and immunities count as normal damage.</summary>
        public bool IgnoreResistances { get; set; }

        /// <summary>2 is weak, 0.5 is resistant, 0 is immune, 1 is normal.</summary>
        public float GetElementMultiplier(Element element)
        {
            if (_affinities == null || element == Element.None) return 1f;

            foreach (var affinity in _affinities)
            {
                if (affinity.Element == element)
                    return IgnoreResistances ? Math.Max(1f, affinity.DamageMultiplier) : affinity.DamageMultiplier;
            }

            return 1f;
        }

        public void IncreaseMaxHealth(int value)
        {
            _bonusMaxHealth += value;
            RecalculateStats();
        }
        #endregion

        #region Health
        /// <summary>Sets health on a standing character, for example when restoring it between battles. Minimum 1.</summary>
        public void SetCurrentHealth(int targetCurrentHealth)
        {
            CurrentHealth = Math.Min(Math.Max(1, targetCurrentHealth), MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void Heal(int value)
        {
            if (IsIncapacitated) return;

            CurrentHealth = Math.Min(CurrentHealth + value, MaxHealth);
            OnHealAction?.Invoke();
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>
        /// Deals damage to this character and returns the amount dealt. Soft increases it by its stack count.
        /// If this character has Thorns active, the <paramref name="attacker"/> receives Thorns damage in return.
        /// </summary>
        public int Damage(int value, CharacterStats attacker = null)
        {
            if (IsIncapacitated) return 0;
            OnTakeDamageAction?.Invoke();

            var dealt = Math.Max(0, value + GetStatusValue(StatusEffectType.Soft));
            CurrentHealth = Math.Max(0, CurrentHealth - dealt);
            var wasIncapacitated = CurrentHealth == 0;
            if (wasIncapacitated) IsIncapacitated = true;

            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (wasIncapacitated) OnIncapacitated?.Invoke();

            var thorns = GetStatusValue(StatusEffectType.Thorns);
            if (attacker != null && thorns > 0)
                attacker.Damage(thorns);

            return dealt;
        }

        /// <summary>Returns an incapacitated character to the fight and clears its negative statuses.</summary>
        public bool Revive(int health)
        {
            if (!IsIncapacitated) return false;

            IsIncapacitated = false;
            ClearNegativeStatus();
            IsStunned = false;
            CurrentHealth = Math.Min(Math.Max(1, health), MaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            OnRevived?.Invoke();
            return true;
        }

        /// <summary>Flat damage this character's attacks lose to its Weakness status.</summary>
        public int AttackReduction => GetStatusValue(StatusEffectType.Weakness);
        #endregion

        #region Ability Points
        public void GainAp(int value)
        {
            CurrentAp = Math.Min(Math.Max(0, CurrentAp + value), MaxAp);
            OnApChanged?.Invoke(CurrentAp, MaxAp);
        }

        public bool TrySpendAp(int cost)
        {
            if (cost > CurrentAp) return false;

            CurrentAp -= cost;
            OnApChanged?.Invoke(CurrentAp, MaxAp);
            return true;
        }
        #endregion

        #region Statuses
        public bool IsTaunting => GetStatusValue(StatusEffectType.Taunt) > 0;

        public int GetStatusValue(StatusEffectType type, StatType stat = default)
        {
            if (type == StatusEffectType.None) return 0;

            var status = GetStatus(type, stat);
            return status.IsActive ? status.StatusValue : 0;
        }

        /// <param name="stat">Only used by StatBuff and StatDebuff.</param>
        public void ApplyStatus(StatusEffectType type, int value, StatType stat = default)
        {
            if (type == StatusEffectType.None || value <= 0) return;

            var status = GetStatus(type, stat);
            if (status.IsActive)
            {
                status.StatusValue += value;
                OnStatusChanged?.Invoke(status.Key, status.StatusValue);
            }
            else
            {
                status.StatusValue = value;
                status.IsActive = true;
                OnStatusApplied?.Invoke(status.Key, status.StatusValue);
            }

            if (status.Key.IsStatKeyed) RecalculateStats();
        }

        public void TriggerAllStatus()
        {
            foreach (var status in _statuses.Values)
            {
                if (IsIncapacitated) return;
                TriggerStatus(status);
            }
        }

        public void ClearAllStatus()
        {
            foreach (var status in _statuses.Values)
                ClearStatus(status.Key);
        }

        /// <summary>Clears all active statuses marked as negative (Poison, Burn, Stun, Weakness, Soft, StatDebuff).</summary>
        public void ClearNegativeStatus()
        {
            foreach (var status in _statuses.Values)
            {
                if (status.IsNegative && status.IsActive)
                    ClearStatus(status.Key);
            }
        }

        public void ClearStatus(StatusKey key)
        {
            var status = _statuses[key];
            var wasActive = status.IsActive;

            status.IsActive = false;
            status.StatusValue = 0;
            OnStatusCleared?.Invoke(key);

            if (wasActive && key.IsStatKeyed) RecalculateStats();
        }

        private void TriggerStatus(StatusStats status)
        {
            status.OnTriggerAction?.Invoke();

            if (!status.IsActive) return;

            if (status.IncreaseOverTurn)
                status.StatusValue++;
            else if (status.DecreaseOverTurn)
                status.StatusValue--;

            if (status.StatusValue <= 0)
                ClearStatus(status.Key);
            else
                OnStatusChanged?.Invoke(status.Key, status.StatusValue);
        }

        private void DamagePoison()
        {
            var value = GetStatusValue(StatusEffectType.Poison);
            if (value > 0) Damage(value);
        }

        private void DamageBurn()
        {
            var value = GetStatusValue(StatusEffectType.Burn);
            if (value > 0) Damage(value);
        }

        private void CheckStunStatus()
        {
            IsStunned = GetStatusValue(StatusEffectType.Stun) > 0;
        }
        #endregion
    }
}
