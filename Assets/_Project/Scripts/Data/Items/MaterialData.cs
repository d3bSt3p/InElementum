using UnityEngine;

namespace InElementum.Data.Items
{
    /// <summary>A crafting material dropped by enemies. Spent on upgrades once Bouldy's upgrade system exists.</summary>
    [CreateAssetMenu(fileName = "Material", menuName = "In Elementum/Items/Material", order = 0)]
    public class MaterialData : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;

        public string Id { get => id; internal set => id = value; }
        public string DisplayName { get => displayName; internal set => displayName = value; }
        public Sprite Icon { get => icon; internal set => icon = value; }
    }
}
