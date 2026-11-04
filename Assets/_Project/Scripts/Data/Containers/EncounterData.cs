using System.Collections.Generic;
using InElementum.Data.Characters;
using UnityEngine;

namespace InElementum.Data.Containers
{
    /// <summary>
    /// One fight an overworld enemy leads to: the enemies faced and the battle's backdrop. Who strikes first is
    /// decided in the field, not here.
    /// </summary>
    [CreateAssetMenu(fileName = "Encounter", menuName = "In Elementum/Containers/Encounter", order = 3)]
    public class EncounterData : ScriptableObject
    {
        [SerializeField] private string displayName;
        [Tooltip("Enemies in slot order. Up to as many as the combat scene lays out comfortably (3 to 4).")]
        [SerializeField] private List<EnemyCharacterData> enemies = new List<EnemyCharacterData>();

        [Header("Backdrop")]
        [Tooltip("The battle camera's clear color.")]
        [SerializeField] private Color backdropColor = new Color(0.12f, 0.13f, 0.18f);

        public string DisplayName { get => displayName; internal set => displayName = value; }
        public List<EnemyCharacterData> Enemies { get => enemies; internal set => enemies = value; }
        public Color BackdropColor { get => backdropColor; internal set => backdropColor = value; }
    }
}
