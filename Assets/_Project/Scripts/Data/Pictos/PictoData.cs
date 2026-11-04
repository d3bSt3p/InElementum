using System.Collections.Generic;
using InElementum.Data.Stats;
using UnityEngine;

namespace InElementum.Data.Pictos
{
    /// <summary>
    /// A picto: worn in one of a character's picto slots, it grants its stats and its passive. Winning
    /// <see cref="BattlesToMaster"/> battles while wearing it teaches its Lumina to the whole party. A Lumina gives the
    /// passive alone, for <see cref="LuminaCost"/> Lumina points.
    /// </summary>
    [CreateAssetMenu(fileName = "Picto", menuName = "In Elementum/Pictos/Picto", order = 0)]
    public class PictoData : ScriptableObject
    {
        [Header("Profile")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] [TextArea] private string description;

        [Header("While worn")]
        [Tooltip("Apply only while this is worn as a picto, never through its Lumina.")]
        [SerializeField] private List<StatModifier> modifiers = new List<StatModifier>();
        [Tooltip("The picto's effect. Its Lumina grants this alone.")]
        [SerializeField] private PassiveData passive;

        [Header("Lumina")]
        [Tooltip("Victories needed while wearing this picto to learn its Lumina.")]
        [SerializeField] [Min(1)] private int battlesToMaster = 4;
        [Tooltip("Lumina points a character spends to equip the learned Lumina.")]
        [SerializeField] [Min(0)] private int luminaCost = 1;

        public string Id { get => id; internal set => id = value; }
        public string DisplayName { get => displayName; internal set => displayName = value; }
        public Sprite Icon { get => icon; internal set => icon = value; }
        public string Description { get => description; internal set => description = value; }
        public List<StatModifier> Modifiers { get => modifiers; internal set => modifiers = value; }
        public PassiveData Passive { get => passive; internal set => passive = value; }
        public int BattlesToMaster { get => battlesToMaster; internal set => battlesToMaster = value; }
        public int LuminaCost { get => luminaCost; internal set => luminaCost = value; }
    }
}
