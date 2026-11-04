using InElementum.Data.Enums;
using InElementum.Data.Weapons;
using UnityEngine;
using UnityEngine.Serialization;

namespace InElementum.Data.Settings
{
    /// <summary>Global combat constants. Every number the combat rules use lives here, never as a literal in logic.</summary>
    [CreateAssetMenu(fileName = "Combat Tuning", menuName = "In Elementum/Settings/CombatTuning", order = 1)]
    public class CombatTuning : ScriptableObject
    {
        [Header("Party")]
        [SerializeField] [Min(1)] private int maxActivePartySize = 4;

        [Header("Health")]
        [SerializeField] [Min(0)] private int baseHealth;
        [SerializeField] [Min(1)] private int healthPerVitality = 10;

        [Header("Ability Points")]
        [SerializeField] [Min(0)] private int apPerHit = 1;
        [Tooltip("AP a party member gains for each enemy hit they dodge.")]
        [SerializeField] [Min(0)] private int apPerDodge = 1;

        [Header("Surge")]
        [SerializeField] [Min(1)] private int surgeGaugeMax = 100;
        [Tooltip("Surge gauge charge a character gains for each AP they spend.")]
        [SerializeField] [Min(0)] private int surgeChargePerAp = 10;

        [Header("Pictos")]
        [Tooltip("Pictos each party member can wear at once.")]
        [SerializeField] [Min(0)] private int pictoSlots = 3;

        [Header("Weapon Scaling")]
        [Tooltip("Weapon power gained per attribute point at each scaling grade.")]
        [SerializeField] [Min(0f)] private float scalingE = 0.1f;
        [SerializeField] [Min(0f)] private float scalingD = 0.2f;
        [SerializeField] [Min(0f)] private float scalingC = 0.35f;
        [SerializeField] [Min(0f)] private float scalingB = 0.5f;
        [SerializeField] [Min(0f)] private float scalingA = 0.7f;
        [SerializeField] [Min(0f)] private float scalingS = 1f;

        [Header("Timeline")]
        [Tooltip("Length of the Wait section, in timeline units.")]
        [SerializeField] [Min(1f)] private float waitLength = 100f;
        [Tooltip("Length of the Cast section, in timeline units.")]
        [SerializeField] [Min(1f)] private float castLength = 25f;
        [Tooltip("Units per second every combatant moves through Wait, before Speed.")]
        [SerializeField] [Min(0f)] private float baseWaitRate = 5f;
        [Tooltip("Extra units per second through Wait for each point of Speed.")]
        [SerializeField] [Min(0f)] private float waitRatePerSpeed = 1f;
        [Tooltip("Where the initiating side starts, as a fraction of Wait. Its fastest unit starts at the Cast boundary.")]
        [SerializeField] [Range(0f, 1f)] private float initiatorStartPosition = 0.5f;
        [Tooltip("Head start for the side with the higher average Speed, as a fraction of Wait.")]
        [SerializeField] [Range(0f, 1f)] private float averageSpeedStartBonus = 0.1f;
        [Tooltip("How far back from the Cast boundary an interrupted unit lands, as a fraction of Wait. 1 is the bar start.")]
        [SerializeField] [Range(0f, 1f)] private float interruptKnockback = 0.5f;

        [Header("Interrupts and Counters")]
        [Tooltip("Which elements fizzle which. With none assigned, nothing fizzles.")]
        [SerializeField] private ElementChart elementChart;
        [Tooltip("Damage multiplier for each hit of a counter attack: the basic attack a party member strikes back with after parrying a full chain.")]
        [SerializeField] [Min(1f)] [FormerlySerializedAs("riposteMultiplier")] private float counterMultiplier = 1.5f;
        [Tooltip("Damage multiplier for the queued action of a party member who parried a full chain while in Cast.")]
        [SerializeField] [Min(1f)] private float superRiposteMultiplier = 2f;
        [Tooltip("Extra AP a taunting party member gains for each dodge.")]
        [SerializeField] [Min(0)] private int tauntDodgeApBonus = 1;
        [Tooltip("Added to the counter attack (or Super Riposte) multiplier earned by a taunting party member's parry.")]
        [SerializeField] [Min(0f)] [FormerlySerializedAs("tauntRiposteBonus")] private float tauntCounterBonus = 0.5f;

        [Header("Break")]
        [Tooltip("Break drain multiplier for damage to an enemy in Cast.")]
        [SerializeField] [Min(0f)] private float castBreakMultiplier = 1.5f;
        [Tooltip("Break drain multiplier for counter attack and Super Riposte damage to the parried enemy.")]
        [SerializeField] [Min(0f)] [FormerlySerializedAs("riposteBreakMultiplier")] private float counterBreakMultiplier = 2f;
        [Tooltip("Timeline seconds a Broken enemy stays frozen on the bar before its meter refills.")]
        [SerializeField] [Min(0f)] private float breakStunDuration = 4f;

        [Header("Guard")]
        [Tooltip("Damage multiplier for an enemy while it casts a Guard ability.")]
        [SerializeField] [Range(0f, 1f)] private float guardDamageMultiplier = 0.5f;

        [Header("Damage")]
        [Tooltip("Damage added per point of the attacker's Strength.")]
        [SerializeField] [Min(0f)] private float strengthCoefficient = 1f;
        [Tooltip("Damage removed per point of the defender's Defense.")]
        [SerializeField] [Min(0f)] private float defenseCoefficient = 0.5f;
        [SerializeField] [Min(0)] private int minimumDamage = 1;
        [SerializeField] [Range(0f, 1f)] private float baseCritChance = 0.05f;
        [SerializeField] [Range(0f, 1f)] private float critChancePerLuck = 0.01f;
        [SerializeField] [Min(1f)] private float critMultiplier = 1.5f;

        [Header("Defense QTE")]
        [Tooltip("Seconds either side of a hit's impact in which Dodge succeeds.")]
        [SerializeField] [Min(0f)] private float dodgeWindow = 0.2f;
        [Tooltip("Seconds either side of a hit's impact in which Parry succeeds. Tighter than dodge.")]
        [SerializeField] [Min(0f)] private float parryWindow = 0.1f;
        [Tooltip("Fraction of a QTE window, around its centre, that counts as Perfect.")]
        [SerializeField] [Range(0f, 1f)] private float perfectWindowFraction = 0.3f;

        public int MaxActivePartySize { get => maxActivePartySize; internal set => maxActivePartySize = value; }
        public int BaseHealth { get => baseHealth; internal set => baseHealth = value; }
        public int HealthPerVitality { get => healthPerVitality; internal set => healthPerVitality = value; }
        public int ApPerHit { get => apPerHit; internal set => apPerHit = value; }
        public int ApPerDodge { get => apPerDodge; internal set => apPerDodge = value; }
        public int SurgeGaugeMax { get => surgeGaugeMax; internal set => surgeGaugeMax = value; }
        public int SurgeChargePerAp { get => surgeChargePerAp; internal set => surgeChargePerAp = value; }
        public int PictoSlots { get => pictoSlots; internal set => pictoSlots = value; }

        /// <summary>Weapon power per attribute point at <paramref name="grade"/>.</summary>
        public float GetScaling(ScalingGrade grade)
        {
            switch (grade)
            {
                case ScalingGrade.S: return scalingS;
                case ScalingGrade.A: return scalingA;
                case ScalingGrade.B: return scalingB;
                case ScalingGrade.C: return scalingC;
                case ScalingGrade.D: return scalingD;
                default: return scalingE;
            }
        }

        internal void SetScaling(ScalingGrade grade, float value)
        {
            switch (grade)
            {
                case ScalingGrade.S: scalingS = value; break;
                case ScalingGrade.A: scalingA = value; break;
                case ScalingGrade.B: scalingB = value; break;
                case ScalingGrade.C: scalingC = value; break;
                case ScalingGrade.D: scalingD = value; break;
                default: scalingE = value; break;
            }
        }

        public float WaitLength { get => waitLength; internal set => waitLength = value; }
        public float CastLength { get => castLength; internal set => castLength = value; }
        public float BaseWaitRate { get => baseWaitRate; internal set => baseWaitRate = value; }
        public float WaitRatePerSpeed { get => waitRatePerSpeed; internal set => waitRatePerSpeed = value; }
        public float InitiatorStartPosition { get => initiatorStartPosition; internal set => initiatorStartPosition = value; }
        public float AverageSpeedStartBonus { get => averageSpeedStartBonus; internal set => averageSpeedStartBonus = value; }
        public float InterruptKnockback { get => interruptKnockback; internal set => interruptKnockback = value; }

        public ElementChart ElementChart { get => elementChart; internal set => elementChart = value; }
        public float CounterMultiplier { get => counterMultiplier; internal set => counterMultiplier = value; }
        public float SuperRiposteMultiplier { get => superRiposteMultiplier; internal set => superRiposteMultiplier = value; }
        public int TauntDodgeApBonus { get => tauntDodgeApBonus; internal set => tauntDodgeApBonus = value; }
        public float TauntCounterBonus { get => tauntCounterBonus; internal set => tauntCounterBonus = value; }
        public float CastBreakMultiplier { get => castBreakMultiplier; internal set => castBreakMultiplier = value; }
        public float CounterBreakMultiplier { get => counterBreakMultiplier; internal set => counterBreakMultiplier = value; }
        public float BreakStunDuration { get => breakStunDuration; internal set => breakStunDuration = value; }
        public float GuardDamageMultiplier { get => guardDamageMultiplier; internal set => guardDamageMultiplier = value; }

        /// <summary>True if a hit of <paramref name="hit"/> fizzles a cast of <paramref name="cast"/>.</summary>
        public bool Counters(Element hit, Element cast) => elementChart != null && elementChart.IsCounter(hit, cast);

        public float StrengthCoefficient { get => strengthCoefficient; internal set => strengthCoefficient = value; }
        public float DefenseCoefficient { get => defenseCoefficient; internal set => defenseCoefficient = value; }
        public int MinimumDamage { get => minimumDamage; internal set => minimumDamage = value; }
        public float BaseCritChance { get => baseCritChance; internal set => baseCritChance = value; }
        public float CritChancePerLuck { get => critChancePerLuck; internal set => critChancePerLuck = value; }
        public float CritMultiplier { get => critMultiplier; internal set => critMultiplier = value; }

        public float DodgeWindow { get => dodgeWindow; internal set => dodgeWindow = value; }
        public float ParryWindow { get => parryWindow; internal set => parryWindow = value; }
        public float PerfectWindowFraction { get => perfectWindowFraction; internal set => perfectWindowFraction = value; }
    }
}
