using InElementum.Core.Tooltip;
using InElementum.Core.Tooltip.CursorSystem;
using UnityEngine;

namespace InElementum.Core.Tooltip.Interfaces
{
    public interface ITooltipTargetBase
    {
        void ShowTooltipInfo(TooltipManager tooltipManager,string content,string header ="",Transform tooltipStaticTransform = null,CursorType targetCursor = CursorType.Default,Camera cam = null, float delayShow =0);

        void HideTooltipInfo(TooltipManager tooltipManager);
    }
}