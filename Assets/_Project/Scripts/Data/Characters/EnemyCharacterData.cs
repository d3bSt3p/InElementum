using System;
using System.Collections.Generic;
using InElementum.Combat.Characters;
using InElementum.Core.Extensions;
using InElementum.Data.Containers;
using InElementum.Data.Enums;
using InElementum.Data.Stats;
using UnityEngine;
using Random = UnityEngine.Random;

namespace InElementum.Data.Characters
{
    [CreateAssetMenu(fileName = "Enemy Character Data", menuName = "In Elementum/Characters/Enemy", order = 1)]
    public class EnemyCharacterData : CharacterDataBase
    {
        [Header("Enemy Defaults")]
        [SerializeField] private bool followAbilityPattern;
        [SerializeField] private List<EnemyAbilityData> enemyAbilityList = new List<EnemyAbilityData>();

        [Header("Elements")]
        [Tooltip("Damage multiplier per element: 2 is weak, 0.5 is resistant, 0 is immune. Unlisted elements are 1.")]
        [SerializeField] private List<ElementAffinity> affinities = new List<ElementAffinity>();

        [Header("Break")]
        [Tooltip("Elites and bosses only. Damage drains it; at 0 the enemy is Broken. 0 means it cannot be broken.")]
        [SerializeField] [Min(0)] private int breakMeter;

        [Header("Rewards")]
        [SerializeField] private RewardTable rewards = new RewardTable();

        [Header("Audio")]
        [Tooltip("Optional. Cries as the enemy begins an ability and when it goes down.")]
        [SerializeField] private SoundProfileData voice;

        public bool FollowAbilityPattern { get => followAbilityPattern; internal set => followAbilityPattern = value; }
        public List<EnemyAbilityData> EnemyAbilityList { get => enemyAbilityList; internal set => enemyAbilityList = value; }
        public List<ElementAffinity> Affinities { get => affinities; internal set => affinities = value; }
        public int BreakMeter { get => breakMeter; internal set => breakMeter = value; }
        public RewardTable Rewards { get => rewards; internal set => rewards = value; }
        public SoundProfileData Voice { get => voice; internal set => voice = value; }

        public EnemyBase EnemyPrefab => prefab as EnemyBase;

        public float GetDamageMultiplier(Element attackElement)
        {
            foreach (var affinity in affinities)
            {
                if (affinity.Element == attackElement)
                    return affinity.DamageMultiplier;
            }

            return 1f;
        }

        public EnemyAbilityData GetAbility()
        {
            return EnemyAbilityList.RandomItem();
        }

        public EnemyAbilityData GetAbility(int usedAbilityCount)
        {
            if (followAbilityPattern)
            {
                var index = usedAbilityCount % EnemyAbilityList.Count;
                return EnemyAbilityList[index];
            }

            return GetAbility();
        }
    }

    [Serializable]
    public class ElementAffinity
    {
        [SerializeField] private Element element;
        [SerializeField] private float damageMultiplier = 1f;

        public ElementAffinity() { }

        public ElementAffinity(Element element, float damageMultiplier)
        {
            this.element = element;
            this.damageMultiplier = damageMultiplier;
        }

        public Element Element => element;
        public float DamageMultiplier => damageMultiplier;
    }

    [Serializable]
    public class EnemyAbilityData
    {
        [Header("Settings")]
        [SerializeField] private string name;
        [SerializeField] private EnemyIntentionData intention;
        [SerializeField] private bool hideActionValue;
        [SerializeField] private List<EnemyActionData> actionList = new List<EnemyActionData>();

        [Header("Combat")]
        [SerializeField] [Min(1)] private int hitCount = 1;
        [Tooltip("A hit whose element counters this one fizzles the cast. None cannot be fizzled.")]
        [SerializeField] private Element element;
        [SerializeField] [Min(0f)] private float castTime = 1f;
        [Tooltip("Cannot be fizzled by counter-element hits.")]
        [SerializeField] private bool isSteadfast;
        [Tooltip("Which defense inputs work against its hits. The forbidden input counts as a miss.")]
        [SerializeField] private DefenseRule defenseRule;
        [Tooltip("Extra seconds of wind-up before each hit lands, in hit order, for an uneven rhythm. Missing entries are 0.")]
        [SerializeField] private List<float> hitDelays = new List<float>();

        [Header("Guard")]
        [Tooltip("While the enemy casts this, it takes reduced damage and answers each basic-attack hit with a counter-hit.")]
        [SerializeField] private bool isGuard;
        [Tooltip("Damage of each guard counter-hit, before the enemy's Strength.")]
        [SerializeField] [Min(0)] private int guardCounterPower;

        public string Name { get => name; internal set => name = value; }
        public EnemyIntentionData Intention { get => intention; internal set => intention = value; }
        public bool HideActionValue { get => hideActionValue; internal set => hideActionValue = value; }
        public List<EnemyActionData> ActionList { get => actionList; internal set => actionList = value; }
        public int HitCount { get => Mathf.Max(1, hitCount); internal set => hitCount = value; }
        public Element Element { get => element; internal set => element = value; }
        public float CastTime { get => castTime; internal set => castTime = value; }
        public bool IsSteadfast { get => isSteadfast; internal set => isSteadfast = value; }
        public DefenseRule DefenseRule { get => defenseRule; internal set => defenseRule = value; }
        public List<float> HitDelays { get => hitDelays; internal set => hitDelays = value; }
        public bool IsGuard { get => isGuard; internal set => isGuard = value; }
        public int GuardCounterPower { get => guardCounterPower; internal set => guardCounterPower = value; }

        /// <summary>Extra wind-up before hit <paramref name="hitIndex"/> (from 0) lands.</summary>
        public float GetHitDelay(int hitIndex) =>
            hitDelays != null && hitIndex >= 0 && hitIndex < hitDelays.Count ? Mathf.Max(0f, hitDelays[hitIndex]) : 0f;

        /// <summary>Has a damaging effect aimed at the other side.</summary>
        public bool DealsDamage => HasHit(target =>
            target == ActionTargetType.Enemy || target == ActionTargetType.AllEnemies || target == ActionTargetType.RandomEnemy);

        /// <summary>Has a damaging effect aimed at a single opponent. Taunt redirects these.</summary>
        public bool IsSingleTargetAttack => HasHit(target =>
            target == ActionTargetType.Enemy || target == ActionTargetType.RandomEnemy);

        private bool HasHit(Func<ActionTargetType, bool> aim)
        {
            if (actionList == null) return false;

            foreach (var action in actionList)
            {
                if (action != null && (action.EffectType == EffectType.Damage || action.EffectType == EffectType.LifeSteal)
                                   && aim(action.TargetType))
                    return true;
            }

            return false;
        }
    }

    /// <summary>One effect of an enemy ability, with a value rolled from a range each time it is shown.</summary>
    [Serializable]
    public class EnemyActionData
    {
        [SerializeField] private EffectType effectType;
        [SerializeField] private ActionTargetType targetType = ActionTargetType.Enemy;
        [SerializeField] private int minActionValue;
        [SerializeField] private int maxActionValue;
        [Tooltip("ApplyStatus only.")]
        [SerializeField] private StatusEffectType status;
        [Tooltip("ModifyStat, and ApplyStatus with StatBuff or StatDebuff.")]
        [SerializeField] private StatType stat;

        public EnemyActionData() { }

        public EnemyActionData(EffectType effectType, ActionTargetType targetType, int minActionValue, int maxActionValue,
            StatusEffectType status = StatusEffectType.None, StatType stat = default)
        {
            this.effectType = effectType;
            this.targetType = targetType;
            this.minActionValue = minActionValue;
            this.maxActionValue = maxActionValue;
            this.status = status;
            this.stat = stat;
        }

        public EffectType EffectType => effectType;
        public ActionTargetType TargetType => targetType;
        public StatusEffectType Status => status;
        public StatType Stat => stat;

        private int _cachedActionValue;
        private bool _hasRolled;

        public int ActionValue
        {
            get
            {
                if (!_hasRolled)
                    RollValue();
                return _cachedActionValue;
            }
        }

        public void RollValue()
        {
            _cachedActionValue = Random.Range(minActionValue, maxActionValue);
            _hasRolled = true;
        }

        public void ResetRoll() => _hasRolled = false;
    }
}
