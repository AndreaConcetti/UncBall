using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BallColorLibrary", menuName = "Uncball/Skins/Ball Color Library")]
public class BallColorLibrary : ScriptableObject
{
    [System.Serializable]
    public class ColorEntry
    {
        public string id;
        public Color color = Color.white;
        public SkinRarity rarity = SkinRarity.Common;
    }

    [SerializeField] private List<ColorEntry> colors = new List<ColorEntry>();

    public IReadOnlyList<ColorEntry> Colors => colors;

    public ColorEntry GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || colors == null)
            return null;

        string lookup = id.Trim().ToLowerInvariant();

        for (int i = 0; i < colors.Count; i++)
        {
            if (colors[i] == null || string.IsNullOrWhiteSpace(colors[i].id))
                continue;

            if (colors[i].id.Trim().ToLowerInvariant() == lookup)
                return colors[i];
        }

        return null;
    }

    public List<ColorEntry> GetEntriesByRarity(SkinRarity rarity)
    {
        List<ColorEntry> result = new List<ColorEntry>();

        if (colors == null)
            return result;

        for (int i = 0; i < colors.Count; i++)
        {
            if (colors[i] != null && colors[i].rarity == rarity)
                result.Add(colors[i]);
        }

        return result;
    }

    public ColorEntry GetRandomByRarity(SkinRarity rarity)
    {
        List<ColorEntry> list = GetEntriesByRarity(rarity);

        if (list.Count == 0)
            return null;

        return list[Random.Range(0, list.Count)];
    }
}