using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BallPatternLibrary", menuName = "Uncball/Skins/Ball Pattern Library")]
public class BallPatternLibrary : ScriptableObject
{
    [System.Serializable]
    public class PatternEntry
    {
        public string id;
        public Texture2D texture;
        public SkinRarity rarity = SkinRarity.Common;
    }

    [SerializeField] private List<PatternEntry> patterns = new List<PatternEntry>();

    public IReadOnlyList<PatternEntry> Patterns => patterns;

    public PatternEntry GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || patterns == null)
            return null;

        string lookup = id.Trim().ToLowerInvariant();

        for (int i = 0; i < patterns.Count; i++)
        {
            if (patterns[i] == null || string.IsNullOrWhiteSpace(patterns[i].id))
                continue;

            if (patterns[i].id.Trim().ToLowerInvariant() == lookup)
                return patterns[i];
        }

        return null;
    }

    public List<PatternEntry> GetEntriesByRarity(SkinRarity rarity)
    {
        List<PatternEntry> result = new List<PatternEntry>();

        if (patterns == null)
            return result;

        for (int i = 0; i < patterns.Count; i++)
        {
            if (patterns[i] != null && patterns[i].rarity == rarity)
                result.Add(patterns[i]);
        }

        return result;
    }

    public PatternEntry GetRandomByRarity(SkinRarity rarity)
    {
        List<PatternEntry> list = GetEntriesByRarity(rarity);

        if (list.Count == 0)
            return null;

        return list[Random.Range(0, list.Count)];
    }
}