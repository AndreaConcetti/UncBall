using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkinRarityTable", menuName = "Uncball/Skins/Skin Rarity Table")]
public class SkinRarityTable : ScriptableObject
{
    [System.Serializable]
    public class RarityWeight
    {
        public SkinRarity rarity;
        [Min(0f)] public float weight = 1f;
    }

    [SerializeField]
    private List<RarityWeight> weights = new List<RarityWeight>
    {
        new RarityWeight { rarity = SkinRarity.Common, weight = 70f },
        new RarityWeight { rarity = SkinRarity.Rare, weight = 20f },
        new RarityWeight { rarity = SkinRarity.Epic, weight = 8f },
        new RarityWeight { rarity = SkinRarity.Legendary, weight = 2f }
    };

    public SkinRarity RollRarity(SkinRarity? minimumGuaranteedRarity = null)
    {
        List<RarityWeight> valid = new List<RarityWeight>();

        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] == null || weights[i].weight <= 0f)
                continue;

            if (minimumGuaranteedRarity.HasValue && weights[i].rarity < minimumGuaranteedRarity.Value)
                continue;

            valid.Add(weights[i]);
        }

        if (valid.Count == 0)
        {
            if (minimumGuaranteedRarity.HasValue)
                return minimumGuaranteedRarity.Value;

            return SkinRarity.Common;
        }

        float totalWeight = 0f;
        for (int i = 0; i < valid.Count; i++)
            totalWeight += valid[i].weight;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < valid.Count; i++)
        {
            cumulative += valid[i].weight;
            if (roll <= cumulative)
                return valid[i].rarity;
        }

        return valid[valid.Count - 1].rarity;
    }
}