using System;
using System.Collections.Generic;
using InElementum.Data.Items;
using InElementum.Data.Pictos;
using UnityEngine;

namespace InElementum.Data.Characters
{
    [Serializable]
    public class RewardTable
    {
        [SerializeField] [Min(0)] private int experience;
        [SerializeField] [Min(0)] private int currencyMin;
        [SerializeField] [Min(0)] private int currencyMax;
        [SerializeField] private List<MaterialDrop> materialDrops = new List<MaterialDrop>();
        [Tooltip("Meant for bosses. Always drops; ignored if the party already owns it.")]
        [SerializeField] private PictoData pictoDrop;

        public int Experience { get => experience; internal set => experience = value; }
        public int CurrencyMin { get => currencyMin; internal set => currencyMin = value; }
        public int CurrencyMax { get => currencyMax; internal set => currencyMax = value; }
        public List<MaterialDrop> MaterialDrops { get => materialDrops; internal set => materialDrops = value; }
        public PictoData PictoDrop { get => pictoDrop; internal set => pictoDrop = value; }
    }

    [Serializable]
    public class MaterialDrop
    {
        [SerializeField] private MaterialData material;
        [SerializeField] [Min(1)] private int amount = 1;
        [SerializeField] [Range(0f, 1f)] private float chance = 1f;

        public MaterialDrop() { }

        public MaterialDrop(MaterialData material, int amount, float chance)
        {
            this.material = material;
            this.amount = amount;
            this.chance = chance;
        }

        public MaterialData Material => material;
        public int Amount => amount;
        public float Chance => chance;
    }
}
