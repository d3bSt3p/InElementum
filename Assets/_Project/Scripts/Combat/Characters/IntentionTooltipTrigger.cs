using InElementum.Data.Containers;
using InElementum.Core.Tooltip;
using InElementum.Core.Tooltip.CursorSystem;
using InElementum.Core.Tooltip.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InElementum.Combat.Characters
{
    public class IntentionTooltipTrigger : MonoBehaviour, I2DTooltipTarget
    {
        [SerializeField] private Transform tooltipStaticTargetTransform;
        [SerializeField] private float delayShowDuration = 0f;

        private EnemyIntentionData _intentionData;

        public void SetIntentionData(EnemyIntentionData intentionData)
        {
            _intentionData = intentionData;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ShowTooltipInfo();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltipInfo(TooltipManager.Instance);
        }

        private void ShowTooltipInfo()
        {
            if (_intentionData == null) return;
            if (string.IsNullOrEmpty(_intentionData.TooltipContent) &&
                string.IsNullOrEmpty(_intentionData.TooltipHeader)) return;

            ShowTooltipInfo(
                TooltipManager.Instance,
                _intentionData.TooltipContent,
                _intentionData.TooltipHeader,
                tooltipStaticTargetTransform,
                CursorType.Default,
                delayShow: delayShowDuration);
        }

        public void ShowTooltipInfo(TooltipManager tooltipManager, string content, string header = "",
            Transform tooltipStaticTransform = null, CursorType targetCursor = CursorType.Default,
            Camera cam = null, float delayShow = 0)
        {
            tooltipManager.ShowTooltip(content, header, tooltipStaticTransform, targetCursor, cam, delayShow);
        }

        public void HideTooltipInfo(TooltipManager tooltipManager)
        {
            tooltipManager.HideTooltip();
        }
    }
}