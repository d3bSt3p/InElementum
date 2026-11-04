using System.Collections.Generic;
using InElementum.Data.Actions;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Pictos
{
    /// <summary>
    /// A picto's effect. It applies while the picto is worn or its Lumina is equipped: <see cref="ExtraAttackHits"/>
    /// always, and <see cref="Effects"/> each time <see cref="Trigger"/> fires, from the wearer. Single-enemy effects
    /// aim at the enemy the trigger concerns, and Ally effects at the wearer.
    /// </summary>
    [CreateAssetMenu(fileName = "Passive", menuName = "In Elementum/Pictos/Passive", order = 1)]
    public class PassiveData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] [TextArea] private string description;

        [Header("Always on")]
        [Tooltip("Added to the number of hits per basic Attack.")]
        [SerializeField] [Min(0)] private int extraAttackHits;

        [Header("Triggered")]
        [SerializeField] private PassiveTrigger trigger;
        [SerializeField] private List<ActionEffectData> effects = new List<ActionEffectData>();

        public string Id { get => id; internal set => id = value; }
        public string DisplayName { get => displayName; internal set => displayName = value; }
        public string Description { get => description; internal set => description = value; }
        public int ExtraAttackHits { get => extraAttackHits; internal set => extraAttackHits = value; }
        public PassiveTrigger Trigger { get => trigger; internal set => trigger = value; }
        public List<ActionEffectData> Effects { get => effects; internal set => effects = value; }
    }
}
