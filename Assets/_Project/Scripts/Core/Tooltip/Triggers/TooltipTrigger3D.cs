using InElementum.Core.Tooltip;
using InElementum.Core.Tooltip.Interfaces;

namespace InElementum.Core.Tooltip.Triggers
{
    public class TooltipTrigger3D : TooltipTriggerBase, I3DTooltipTarget
    {
        public void OnMouseEnter()
        {
           ShowTooltipInfo();
        }

        public void OnMouseExit()
        {
            HideTooltipInfo(TooltipManager.Instance);
        }
    }
}