using InElementum.Combat.Characters;
using InElementum.Data.Enums;
using InElementum.Data.Stats;
using UnityEngine;

namespace InElementum.Data.Characters
{
    public abstract class CharacterDataBase : ScriptableObject
    {
        [Header("Base")]
        [SerializeField] protected string characterID;
        [SerializeField] protected string characterName;
        [SerializeField] [TextArea] protected string characterDescription;
        [SerializeField] protected Element element;
        [Tooltip("Max HP is derived from Vitality using CombatTuning.")]
        [SerializeField] protected StatBlock baseStats;

        [Header("Presentation")]
        [SerializeField] protected Sprite portrait;
        [SerializeField] protected Sprite battleSprite;
        [Tooltip("Animated look for combat and exploration. Without one, combat shows a colored block.")]
        [SerializeField] protected CharacterSpriteSet spriteSet;
        [SerializeField] protected CharacterBase prefab;

        public string CharacterID { get => characterID; internal set => characterID = value; }
        public string CharacterName { get => characterName; internal set => characterName = value; }
        public string CharacterDescription { get => characterDescription; internal set => characterDescription = value; }
        public Element Element { get => element; internal set => element = value; }
        public StatBlock BaseStats { get => baseStats; internal set => baseStats = value; }
        public Sprite Portrait { get => portrait; internal set => portrait = value; }
        public Sprite BattleSprite { get => battleSprite; internal set => battleSprite = value; }
        public CharacterSpriteSet SpriteSet { get => spriteSet; internal set => spriteSet = value; }
        public CharacterBase Prefab { get => prefab; internal set => prefab = value; }
    }
}
