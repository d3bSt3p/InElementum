using System;
using System.Collections.Generic;
using InElementum.Combat.UI;
using InElementum.Data.Enums;
using InElementum.Data.Stats;
using UnityEngine;

namespace InElementum.Data.Containers
{
    [CreateAssetMenu(fileName = "Status Icons", menuName = "In Elementum/Containers/StatusIcons", order = 2)]
    public class StatusIconsData : ScriptableObject
    {
        [SerializeField] private StatusIconBase statusIconBasePrefab;
        [SerializeField] private List<StatusIconData> statusIconList;

        public StatusIconBase StatusIconBasePrefab => statusIconBasePrefab;
        public List<StatusIconData> StatusIconList => statusIconList;
    }

    [Serializable]
    public class StatusIconData
    {
        [SerializeField] private StatusEffectType iconStatus;
        [Tooltip("StatBuff and StatDebuff only: which stat this icon is for.")]
        [SerializeField] private StatType iconStat;
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private List<SpecialKeywords> specialKeywords;

        public StatusKey Key => new StatusKey(iconStatus, iconStat);
        public Sprite IconSprite => iconSprite;
        public List<SpecialKeywords> SpecialKeywords => specialKeywords;
    }
}
