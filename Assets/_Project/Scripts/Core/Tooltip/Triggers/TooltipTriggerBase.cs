using InElementum.Core.Tooltip;
using InElementum.Core.Tooltip.CursorSystem;
using InElementum.Core.Tooltip.Interfaces;
using UnityEngine;

namespace InElementum.Core.Tooltip.Triggers
{
    public abstract class TooltipTriggerBase : MonoBehaviour, ITooltipTargetBase
    {
        
        [SerializeField] protected string headerText = "";
        [TextArea] [SerializeField] protected string contentText;
        [SerializeField] private Transform tooltipStaticTargetTransform;
        [SerializeField] private CursorType cursorType = CursorType.Default;
        [SerializeField] private float delayShowDuration =0;
        
        protected virtual void ShowTooltipInfo()
        {
            ShowTooltipInfo(TooltipManager.Instance,contentText,headerText,tooltipStaticTargetTransform,cursorType,delayShow : delayShowDuration);
        }

        public void ShowTooltipInfo(TooltipManager tooltipManager, string content, string header = "", Transform tooltipStaticTransform = null,CursorType targetCursor = CursorType.Default,Camera cam = null, float delayShow =0)
        {
            tooltipManager.ShowTooltip(content,header,tooltipStaticTransform,targetCursor,cam,delayShow);
        }

        public virtual void HideTooltipInfo(TooltipManager tooltipManager)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}