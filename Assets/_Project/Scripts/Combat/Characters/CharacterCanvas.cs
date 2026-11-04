using System.Collections.Generic;
using System.Linq;
using InElementum.Data.Containers;
using InElementum.Data.Stats;
using InElementum.Combat.Managers;
using InElementum.Core.Managers;
using InElementum.Combat.UI;
using InElementum.Core.Tooltip;
using InElementum.Core.Tooltip.CursorSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InElementum.Combat.Characters
{
    [RequireComponent(typeof(Canvas))]
    public abstract class CharacterCanvas : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] protected Transform statusIconRoot;
        [SerializeField] protected Transform highlightRoot;
        [SerializeField] protected Transform descriptionRoot;
        [SerializeField] protected StatusIconsData statusIconsData;
        [SerializeField] protected TextMeshProUGUI currentHealthText;

        #region Cache

        protected Dictionary<StatusKey, StatusIconBase> StatusDict = new Dictionary<StatusKey, StatusIconBase>();
        protected Canvas TargetCanvas;

        protected FxManager FxManager => FxManager.Instance;
        protected AudioManager AudioManager => AudioManager.Instance;
        protected GameManager GameManager => GameManager.Instance;
        protected CombatManager CombatManager => CombatManager.Instance;
        protected UIManager UIManager => UIManager.Instance;

        #endregion

        #region Setup

        public void InitCanvas()
        {
            highlightRoot.gameObject.SetActive(false);

            TargetCanvas = GetComponent<Canvas>();

            if (TargetCanvas)
                TargetCanvas.worldCamera = Camera.main;
        }

        #endregion

        #region Public Methods

        public void ApplyStatus(StatusKey targetStatus, int value)
        {
            if (!StatusDict.TryGetValue(targetStatus, out var icon) || icon == null)
            {
                var targetData = statusIconsData.StatusIconList.FirstOrDefault(x => x.Key.Equals(targetStatus));

                if (targetData == null) return;

                icon = Instantiate(statusIconsData.StatusIconBasePrefab, statusIconRoot);
                icon.SetStatus(targetData);
                StatusDict[targetStatus] = icon;
            }

            icon.SetStatusValue(value);
        }

        public void ClearStatus(StatusKey targetStatus)
        {
            if (StatusDict.TryGetValue(targetStatus, out var icon) && icon)
                Destroy(icon.gameObject);

            StatusDict.Remove(targetStatus);
        }

        public void UpdateStatusText(StatusKey targetStatus, int value)
        {
            if (StatusDict.TryGetValue(targetStatus, out var icon) && icon)
                icon.StatusValueText.text = $"{value}";
        }

        public void UpdateHealthText(int currentHealth, int maxHealth) =>
            currentHealthText.text = $"{currentHealth}/{maxHealth}";

        public void SetHighlight(bool open) => highlightRoot.gameObject.SetActive(open);

        #endregion
    }
}