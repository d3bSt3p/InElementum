using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Containers
{
    [CreateAssetMenu(fileName = "Enemy Intention", menuName = "In Elementum/Containers/EnemyIntention", order = 0)]
    public class EnemyIntentionData : ScriptableObject
    {
        [SerializeField] private EnemyIntentionType enemyIntentionType;
        [SerializeField] private Sprite intentionSprite;

        [Header("Tooltip")]
        [SerializeField] private string tooltipHeader;
        [TextArea] [SerializeField] private string tooltipContent;

        public EnemyIntentionType EnemyIntentionType { get => enemyIntentionType; internal set => enemyIntentionType = value; }
        public Sprite IntentionSprite { get => intentionSprite; internal set => intentionSprite = value; }
        public string TooltipHeader { get => tooltipHeader; internal set => tooltipHeader = value; }
        public string TooltipContent { get => tooltipContent; internal set => tooltipContent = value; }
    }
}