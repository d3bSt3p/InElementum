using InElementum.Combat.Interfaces;
using InElementum.Combat.Model;
using InElementum.Data.Containers;
using UnityEngine;

namespace InElementum.Combat.Characters
{
    public class EnemyBase : CharacterBase, IEnemy
    {
        [Header("Enemy Base References")]
        [Tooltip("Optional world-space health, status, and intent display.")]
        [SerializeField] protected EnemyCanvas enemyCanvas;
        [SerializeField] protected SoundProfileData deathSoundProfileData;

        public EnemyCanvas EnemyCanvas => enemyCanvas;
        public SoundProfileData DeathSoundProfileData => deathSoundProfileData;

        public override void Bind(Combatant combatant)
        {
            base.Bind(combatant);
            if (enemyCanvas == null) return;

            enemyCanvas.InitCanvas();
            combatant.Stats.BindCanvas(enemyCanvas);
            enemyCanvas.UpdateHealthText(combatant.Stats.CurrentHealth, combatant.Stats.MaxHealth);
            ClearIntent();
        }

        /// <summary>Shows what this enemy is casting. The placeholder HUD draws intents too, so the canvas is optional.</summary>
        public void ShowIntent(EnemyIntent intent)
        {
            if (enemyCanvas == null || intent == null) return;

            var intention = intent.Ability.Intention;
            if (enemyCanvas.IntentImage != null && intention != null)
            {
                enemyCanvas.IntentImage.sprite = intention.IntentionSprite;
                enemyCanvas.IntentImage.gameObject.SetActive(true);
            }

            if (enemyCanvas.IntentionTooltipTrigger != null)
                enemyCanvas.IntentionTooltipTrigger.SetIntentionData(intention);

            if (enemyCanvas.NextActionValueText != null)
            {
                var showValue = !intent.Ability.HideActionValue && intent.Ability.ActionList.Count > 0;
                enemyCanvas.NextActionValueText.gameObject.SetActive(showValue);
                if (showValue) enemyCanvas.NextActionValueText.text = intent.Ability.ActionList[0].ActionValue.ToString();
            }
        }

        public void ClearIntent()
        {
            if (enemyCanvas == null) return;
            if (enemyCanvas.IntentImage != null) enemyCanvas.IntentImage.gameObject.SetActive(false);
            if (enemyCanvas.NextActionValueText != null) enemyCanvas.NextActionValueText.gameObject.SetActive(false);
        }

        protected override void OnIncapacitated()
        {
            base.OnIncapacitated();
            ClearIntent();

            var clip = deathSoundProfileData != null ? deathSoundProfileData.GetRandomClip() : null;
            if (clip != null && AudioManager != null) AudioManager.PlayOneShot(clip);
        }
    }
}
