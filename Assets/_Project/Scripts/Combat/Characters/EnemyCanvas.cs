using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InElementum.Combat.Characters
{
    public class EnemyCanvas : CharacterCanvas
    {
        [Header("Enemy Canvas Settings")]
        [SerializeField] private Image intentImage;
        [SerializeField] private TextMeshProUGUI nextActionValueText;
        [SerializeField] private IntentionTooltipTrigger intentionTooltipTrigger;

        public Image IntentImage => intentImage;
        public TextMeshProUGUI NextActionValueText => nextActionValueText;
        public IntentionTooltipTrigger IntentionTooltipTrigger => intentionTooltipTrigger;
    }
}