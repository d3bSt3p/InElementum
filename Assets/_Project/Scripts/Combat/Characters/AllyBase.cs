using System;
using InElementum.Combat.Interfaces;
using InElementum.Combat.Model;
using UnityEngine;

namespace InElementum.Combat.Characters
{
    public abstract class AllyBase : CharacterBase, IAlly
    {
        [Header("Ally Base Settings")]
        [Tooltip("Optional world-space health and status display.")]
        [SerializeField] private AllyCanvas allyCanvas;

        public AllyCanvas AllyCanvas => allyCanvas;

        public override void Bind(Combatant combatant)
        {
            base.Bind(combatant);
            if (allyCanvas == null) return;

            allyCanvas.InitCanvas();
            combatant.Stats.BindCanvas(allyCanvas);
            allyCanvas.UpdateHealthText(combatant.Stats.CurrentHealth, combatant.Stats.MaxHealth);
        }
    }

    [Serializable]
    public class AllyHealthData
    {
        [SerializeField] private string characterId;
        [SerializeField] private int maxHealth;
        [SerializeField] private int currentHealth;

        public int MaxHealth
        {
            get => maxHealth;
            set => maxHealth = value;
        }

        public int CurrentHealth
        {
            get => currentHealth;
            set => currentHealth = value;
        }

        public string CharacterId
        {
            get => characterId;
            set => characterId = value;
        }
    }
}
