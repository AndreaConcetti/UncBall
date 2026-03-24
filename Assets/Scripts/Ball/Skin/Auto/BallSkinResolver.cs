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
            Debug.LogError("[BallSkinResolver] Base color ID non trovato: " + data.baseColorId);
            return false;
        }

        BallPatternLibrary.PatternEntry patternEntry = database.patternLibrary.GetById(data.patternId);
        if (patternEntry == null)
        {
            Debug.LogError("[BallSkinResolver] Pattern ID non trovato: " + data.patternId);
            return false;
        }

        BallColorLibrary.ColorEntry patternColorEntry = database.patternColorLibrary.GetById(data.patternColorId);
        if (patternColorEntry == null)
        {
            Debug.LogError("[BallSkinResolver] Pattern color ID non trovato: " + data.patternColorId);
            return false;
        }

        baseColor = baseEntry.color;
        patternTexture = patternEntry.texture;
        patternColor = patternColorEntry.color;
        patternIntensity = data.patternIntensity;
        patternScale = data.patternScale;

        return true;
    }
}