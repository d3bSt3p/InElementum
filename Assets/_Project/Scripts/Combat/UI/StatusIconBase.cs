using InElementum.Core.Tooltip;
using InElementum.Core.Tooltip.CursorSystem;
using InElementum.Core.Tooltip.Interfaces;
using InElementum.Data.Containers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InElementum.Combat.UI
{
    public class StatusIconBase : MonoBehaviour, I2DTooltipTarget
    {
        [SerializeField] private Image statusImage;
        [SerializeField] private TextMeshProUGUI statusValueText;

        public StatusIconData MyStatusIconData { get; private set; } = null;
        public Image StatusImage => statusImage;
        public TextMeshProUGUI StatusValueText => statusValueText;

        public void SetStatus(StatusIconData statusIconData)
        {
            MyStatusIconData = statusIconData;
            StatusImage.sprite = statusIconData.IconSprite;
        }

        public void SetStatusValue(int statusValue)
        {
            StatusValueText.text = statusValue.ToString();
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
            if (MyStatusIconData == null) return;
            if (MyStatusIconData.SpecialKeywords == null || MyStatusIconData.SpecialKeywords.Count == 0) return;

            var tooltipManager = TooltipManager.Instance;
            foreach (var keyword in MyStatusIconData.SpecialKeywords)
            {
                var keywordData = tooltipManager.SpecialKeywordData.SpecialKeywordBaseList
                    .Find(x => x.SpecialKeyword == keyword);
                if (keywordData == null) continue;

                ShowTooltipInfo(tooltipManager, keywordData.GetContent(), keywordData.GetHeader());
            }
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
