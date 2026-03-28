using System.Collections.Generic;
using UnityEngine;

public static class BallColorHarmonyPicker
{
    private struct Candidate
    {
        public BallColorLibrary.ColorEntry entry;
        public float weight;

        public Candidate(BallColorLibrary.ColorEntry entry, float weight)
        {
            this.entry = entry;
            this.weight = weight;
        }
    }

    public static BallColorLibrary.ColorEntry PickPatternColor(
        BallColorLibrary baseColorLibrary,
        BallColorLibrary patternColorLibrary,
        BallColorHarmonyProfile harmonyProfile,
        string baseColorId,
        SkinRarity baseColorRarity,
        string excludedPatternColorId = null)
    {
        if (baseColorLibrary == null)
        {
            Debug.LogError("[BallColorHarmonyPicker] baseColorLibrary è null.");
            return null;
        }

        if (patternColorLibrary == null)
        {
            Debug.LogError("[BallColorHarmonyPicker] patternColorLibrary è null.");
            return null;
        }

        if (harmonyProfile == null)
        {
            Debug.LogError("[BallColorHarmonyPicker] harmonyProfile è null.");
            return null;
        }

        BallColorLibrary.ColorEntry baseEntry = baseColorLibrary.GetById(baseColorId);
        if (baseEntry == null)
        {
            Debug.LogError("[BallColorHarmonyPicker] Base color ID non trovato: " + baseColorId);
            return null;
        }

        List<Candidate> candidates = BuildCandidates(
            baseEntry,
            baseColorRarity,
            patternColorLibrary,
            harmonyProfile,
            excludedPatternColorId);

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[BallColorHarmonyPicker] Nessun candidato valido trovato.");
            return null;
        }

        return RollWeighted(candidates);
    }

    public static string PickPatternColorId(
        BallColorLibrary baseColorLibrary,
        BallColorLibrary patternColorLibrary,
        BallColorHarmonyProfile harmonyProfile,
        string baseColorId,
        SkinRarity baseColorRarity,
        string excludedPatternColorId = null)
    {
        BallColorLibrary.ColorEntry entry = PickPatternColor(
            baseColorLibrary,
            patternColorLibrary,
            harmonyProfile,
            baseColorId,
            baseColorRarity,
            excludedPatternColorId);

        return entry != null ? entry.id : null;
    }

    private static List<Candidate> BuildCandidates(
        BallColorLibrary.ColorEntry baseEntry,
        SkinRarity baseColorRarity,
        BallColorLibrary patternColorLibrary,
        BallColorHarmonyProfile harmonyProfile,
        string excludedPatternColorId)
    {
        List<Candidate> result = new List<Candidate>();
        IReadOnlyList<BallColorLibrary.ColorEntry> entries = patternColorLibrary.Colors;

        if (entries == null || entries.Count == 0)
            return result;

        Color.RGBToHSV(baseEntry.color, out float baseH, out float baseS, out float baseV);

        string excludedNormalized = NormalizeId(excludedPatternColorId);

        for (int i = 0; i < entries.Count; i++)
        {
            BallColorLibrary.ColorEntry candidate = entries[i];

            if (candidate == null || string.IsNullOrWhiteSpace(candidate.id))
                continue;

            if (!string.IsNullOrEmpty(excludedNormalized) && NormalizeId(candidate.id) == excludedNormalized)
                continue;

            float weight = ComputeCandidateWeight(
                baseEntry,
                baseH,
                baseS,
                baseV,
                baseColorRarity,
                candidate,
                harmonyProfile);

            if (weight >= harmonyProfile.minimumCandidateWeight)
                result.Add(new Candidate(candidate, weight));
        }

        return result;
    }

    private static float ComputeCandidateWeight(
        BallColorLibrary.ColorEntry baseEntry,
        float baseH,
        float baseS,
        float baseV,
        SkinRarity baseColorRarity,
        BallColorLibrary.ColorEntry candidate,
        BallColorHarmonyProfile harmonyProfile)
    {
        Color.RGBToHSV(candidate.color, out float candH, out float candS, out float candV);

        float hueDelta = CircularHueDistance(baseH, candH);

        float complementaryScore = PeakScore(hueDelta, 0.500f, 0.090f);
        float splitCompA = PeakScore(hueDelta, 0.417f, 0.070f);
        float splitCompB = PeakScore(hueDelta, 0.583f, 0.070f);
        float splitComplementaryScore = Mathf.Max(splitCompA, splitCompB);

        float analogousA = PeakScore(hueDelta, 0.083f, 0.060f);
        float analogousB = PeakScore(hueDelta, 0.917f, 0.060f);
        float analogousScore = Mathf.Max(analogousA, analogousB);

        float triadicA = PeakScore(hueDelta, 0.333f, 0.070f);
        float triadicB = PeakScore(hueDelta, 0.667f, 0.070f);
        float triadicScore = Mathf.Max(triadicA, triadicB);

        bool candidateIsNeutral = candS <= 0.10f || candV <= 0.08f || candV >= 0.97f;
        float neutralScore = candidateIsNeutral ? 1f : 0f;

        float valueContrast = Mathf.Abs(baseV - candV);
        float saturationContrast = Mathf.Abs(baseS - candS);

        float weight = 0f;

        weight += complementaryScore * harmonyProfile.complementaryWeight;
        weight += splitComplementaryScore * harmonyProfile.splitComplementaryWeight;
        weight += analogousScore * harmonyProfile.analogousWeight;
        weight += triadicScore * harmonyProfile.triadicWeight;
        weight += neutralScore * harmonyProfile.neutralWeight;
        weight += valueContrast * harmonyProfile.valueContrastWeight;
        weight += saturationContrast * harmonyProfile.saturationContrastWeight;

        if (hueDelta <= 0.035f)
            weight *= harmonyProfile.sameHuePenalty;

        float rgbDistance = Vector3.Distance(
            new Vector3(baseEntry.color.r, baseEntry.color.g, baseEntry.color.b),
            new Vector3(candidate.color.r, candidate.color.g, candidate.color.b));

        if (rgbDistance <= 0.18f)
            weight *= harmonyProfile.tooSimilarPenalty;

        weight *= harmonyProfile.GetRarityMultiplier(baseColorRarity, candidate.rarity);

        return Mathf.Max(0f, weight);
    }

    private static BallColorLibrary.ColorEntry RollWeighted(List<Candidate> candidates)
    {
        float totalWeight = 0f;

        for (int i = 0; i < candidates.Count; i++)
            totalWeight += candidates[i].weight;

        if (totalWeight <= 0f)
            return candidates[Random.Range(0, candidates.Count)].entry;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += candidates[i].weight;
            if (roll <= cumulative)
                return candidates[i].entry;
        }

        return candidates[candidates.Count - 1].entry;
    }

    private static float CircularHueDistance(float a, float b)
    {
        float d = Mathf.Abs(a - b);
        return Mathf.Min(d, 1f - d);
    }

    private static float PeakScore(float value, float center, float radius)
    {
        float distance = Mathf.Abs(value - center);
        if (distance > 0.5f)
            distance = 1f - distance;

        if (distance >= radius)
            return 0f;

        float t = 1f - (distance / radius);
        return t * t;
    }

    private static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? null
            : id.Trim().ToLowerInvariant();
    }
}