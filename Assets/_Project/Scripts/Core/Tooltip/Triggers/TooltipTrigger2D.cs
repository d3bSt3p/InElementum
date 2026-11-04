using InElementum.Core.Tooltip;
using InElementum.Core.Tooltip.Interfaces;
using UnityEngine.EventSystems;

namespace InElementum.Core.Tooltip.Triggers
{
    public class TooltipTrigger2D : TooltipTriggerBase,I2DTooltipTarget
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
           ShowTooltipInfo();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltipInfo(TooltipManager.Instance);
        }
    }
}