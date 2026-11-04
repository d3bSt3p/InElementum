using System.Collections.Generic;
using InElementum.Combat.Characters;
using InElementum.Combat.Interfaces;
using InElementum.Combat.Model;
using InElementum.Data.Actions;
using InElementum.Data.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Progression;
using InElementum.Data.Settings;
using InElementum.Data.Stats;
using UnityEngine;

namespace InElementum.Tests.EditMode
{
    internal sealed class TestCombatant : ICombatant
    {
        public TestCombatant(CharacterStats stats)
        {
            Stats = stats;
        }

        public CharacterStats Stats { get; }
        public Transform ViewRoot => null;
        public Transform TextRoot => null;
    }

    /// <summary>Always rolls the same value, and the bottom of every range.</summary>
    internal sealed class FixedRandom : IRandomSource
    {
        public FixedRandom(float value = 0.99f)
        {
            NextValue = value;
        }

        public float NextValue { get; set; }
        public float Value() => NextValue;
        public int Range(int minInclusive, int maxExclusive) => minInclusive;
    }

    /// <summary>Creates data objects for a test and destroys them afterwards.</summary>
    internal sealed class TestAssets
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        public T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        /// <summary>
        /// Round numbers for tests. 10 HP per Vitality, no flat base. Damage is the action's power: Strength, Defense, and
        /// crits are off. Wait is 100 units crossed at 1 unit per second per Speed; Cast is 25 units.
        /// </summary>
        public CombatTuning Tuning()
        {
            var tuning = Create<CombatTuning>();
            tuning.MaxActivePartySize = 4;
            tuning.BaseHealth = 0;
            tuning.HealthPerVitality = 10;
            tuning.ApPerHit = 1;
            tuning.ApPerDodge = 1;
            tuning.SurgeGaugeMax = 100;
            tuning.SurgeChargePerAp = 10;
            tuning.PictoSlots = 3;

            tuning.WaitLength = 100f;
            tuning.CastLength = 25f;
            tuning.BaseWaitRate = 0f;
            tuning.WaitRatePerSpeed = 1f;
            tuning.InitiatorStartPosition = 0.5f;
            tuning.AverageSpeedStartBonus = 0f;
            tuning.InterruptKnockback = 1f;

            tuning.StrengthCoefficient = 0f;
            tuning.DefenseCoefficient = 0f;
            tuning.MinimumDamage = 1;
            tuning.BaseCritChance = 0f;
            tuning.CritChancePerLuck = 0f;
            tuning.CritMultiplier = 1.5f;

            tuning.DodgeWindow = 0.2f;
            tuning.ParryWindow = 0.1f;
            tuning.PerfectWindowFraction = 0.25f;

            var chart = Create<ElementChart>();
            chart.Pairs = ElementChart.PlaceholderPairs();
            tuning.ElementChart = chart;
            tuning.CounterMultiplier = 1.5f;
            tuning.SuperRiposteMultiplier = 2f;
            tuning.TauntDodgeApBonus = 1;
            tuning.TauntCounterBonus = 0.5f;
            tuning.CastBreakMultiplier = 1.5f;
            tuning.CounterBreakMultiplier = 2f;
            tuning.BreakStunDuration = 5f;
            tuning.GuardDamageMultiplier = 0.5f;
            return tuning;
        }

        /// <summary>Level 2 needs 100 XP and each level after needs the same. 5 Lumina points at level 1, plus 1 per level.</summary>
        public ProgressionTuning Progression()
        {
            var tuning = Create<ProgressionTuning>();
            tuning.MaxLevel = 99;
            tuning.XpToLevelTwo = 100;
            tuning.XpGrowthPerLevel = 1f;
            tuning.StartingLuminaPoints = 5;
            tuning.LuminaPointsPerLevel = 1;
            return tuning;
        }

        /// <summary>A character with 100 max HP, all other stats at 10, and 10 max AP.</summary>
        public TestCombatant Combatant()
        {
            return new TestCombatant(new CharacterStats(new StatBlock(10, 10, 10, 10, 10), Tuning(), 10));
        }

        public AttackData Attack(float power, int hits = 1, float castTime = 0f)
        {
            var attack = Create<AttackData>();
            attack.DisplayName = "Attack";
            attack.TargetType = ActionTargetType.Enemy;
            attack.PowerPerHit = power;
            attack.BaseHitCount = hits;
            attack.CastTime = castTime;
            return attack;
        }

        public T Action<T>(ActionTargetType target, float castTime, params ActionEffectData[] effects) where T : CombatActionData
        {
            var action = Create<T>();
            action.DisplayName = typeof(T).Name;
            action.TargetType = target;
            action.CastTime = castTime;
            action.Effects = new List<ActionEffectData>(effects);
            return action;
        }

        /// <param name="speed">All other stats are 10, so max HP is 100.</param>
        public PartyMemberData Member(string name, int speed, AttackData attack = null)
        {
            var member = Create<PartyMemberData>();
            member.CharacterName = name;
            member.BaseStats = new StatBlock(10, 10, 10, speed, 10);
            member.MaxAp = 10;
            member.BasicAttack = attack ?? Attack(10);
            return member;
        }

        /// <summary>An enemy with 100 max HP whose abilities are used in order.</summary>
        public EnemyCharacterData Enemy(string name, int speed, params EnemyAbilityData[] abilities)
        {
            var enemy = Create<EnemyCharacterData>();
            enemy.CharacterName = name;
            enemy.BaseStats = new StatBlock(10, 10, 10, speed, 10);
            enemy.FollowAbilityPattern = true;
            enemy.EnemyAbilityList = new List<EnemyAbilityData>(abilities);
            return enemy;
        }

        public static EnemyAbilityData EnemyHit(int damage, float castTime = 0f, int hits = 1, Element element = Element.None)
        {
            return new EnemyAbilityData
            {
                Name = "Hit",
                CastTime = castTime,
                HitCount = hits,
                Element = element,
                ActionList = new List<EnemyActionData>
                {
                    new EnemyActionData(EffectType.Damage, ActionTargetType.Enemy, damage, damage)
                }
            };
        }

        public void DestroyAll()
        {
            foreach (var asset in _created)
                Object.DestroyImmediate(asset);
            _created.Clear();
        }
    }
}
