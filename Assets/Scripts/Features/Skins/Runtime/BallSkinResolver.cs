using UnityEngine;

public static class BallSkinResolver
{
    public static bool TryResolve(
        BallSkinDatabase database,
        BallSkinData data,
        out Color baseColor,
        out Texture2D patternTexture,
        out Color patternColor,
        out float patternIntensity,
        out float patternScale)
    {
        baseColor = Color.white;
        patternTexture = null;
        patternColor = Color.white;
        patternIntensity = 1f;
        patternScale = 1f;

        if (database == null)
        {
            Debug.LogError("[BallSkinResolver] Database nullo.");
            return false;
        }

        if (data == null)
        {
            Debug.LogError("[BallSkinResolver] BallSkinData nulla.");
            return false;
        }

        if (database.baseColorLibrary == null)
        {
            Debug.LogError("[BallSkinResolver] BaseColorLibrary nulla.");
            return false;
        }

        if (database.patternLibrary == null)
        {
            Debug.LogError("[BallSkinResolver] PatternLibrary nulla.");
            return false;
        }

        if (database.patternColorLibrary == null)
        {
            Debug.LogError("[BallSkinResolver] PatternColorLibrary nulla.");
            return false;
        }

        BallColorLibrary.ColorEntry baseEntry = database.baseColorLibrary.GetById(data.baseColorId);
        if (baseEntry == null)
        {
            if (database.baseColorLibrary.Colors != null && database.baseColorLibrary.Colors.Count > 0)
            {
                baseEntry = database.baseColorLibrary.Colors[0];
                Debug.LogWarning("[BallSkinResolver] Base color ID non trovato: " + data.baseColorId + " -> fallback al primo colore disponibile.");
            }
            else
            {
                Debug.LogError("[BallSkinResolver] Nessun colore base disponibile.");
                return false;
            }
        }

        BallPatternLibrary.PatternEntry patternEntry = database.patternLibrary.GetById(data.patternId);
        if (patternEntry == null)
        {
            if (database.patternLibrary.Patterns != null && database.patternLibrary.Patterns.Count > 0)
            {
                patternEntry = database.patternLibrary.Patterns[0];
                Debug.LogWarning("[BallSkinResolver] Pattern ID non trovato: " + data.patternId + " -> fallback al primo pattern disponibile.");
            }
            else
            {
                Debug.LogError("[BallSkinResolver] Nessun pattern disponibile.");
                return false;
            }
        }

        BallColorLibrary.ColorEntry patternColorEntry = database.patternColorLibrary.GetById(data.patternColorId);
        if (patternColorEntry == null)
        {
            if (database.patternColorLibrary.Colors != null && database.patternColorLibrary.Colors.Count > 0)
            {
                patternColorEntry = database.patternColorLibrary.Colors[0];
                Debug.LogWarning("[BallSkinResolver] Pattern color ID non trovato: " + data.patternColorId + " -> fallback al primo colore pattern disponibile.");
            }
            else
            {
                Debug.LogError("[BallSkinResolver] Nessun colore pattern disponibile.");
                return false;
            }
        }

        baseColor = baseEntry.color;
        patternTexture = patternEntry.texture;
        patternColor = patternColorEntry.color;
        patternIntensity = Mathf.Max(0f, data.patternIntensity);
        patternScale = Mathf.Max(0.01f, data.patternScale);

        return true;
    }
}