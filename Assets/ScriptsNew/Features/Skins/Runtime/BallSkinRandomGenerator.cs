using UnityEngine;

public class BallSkinRandomGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BallSkinDatabase database;
    [SerializeField] private SkinRarityTable rarityTable;

    [Header("Ranges")]
    [SerializeField] private Vector2 patternIntensityRange = new Vector2(0.8f, 1.2f);
    [SerializeField] private Vector2 patternScaleRange = new Vector2(0.75f, 2f);

    public BallSkinData GenerateRandomSkin()
    {
        return GenerateRandomSkinWithMinimumRarity(null);
    }

    public BallSkinData GenerateRandomSkinWithMinimumRarity(SkinRarity? minimumGuaranteedRarity)
    {
        if (database == null ||
            database.baseColorLibrary == null ||
            database.patternLibrary == null ||
            database.patternColorLibrary == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] Database o librerie mancanti.", this);
            return null;
        }

        if (rarityTable == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] SkinRarityTable mancante.", this);
            return null;
        }

        SkinRarity targetRarity = rarityTable.RollRarity(minimumGuaranteedRarity);

        BallColorLibrary.ColorEntry baseColor = database.baseColorLibrary.GetRandomByRarity(targetRarity);
        BallPatternLibrary.PatternEntry pattern = database.patternLibrary.GetRandomByRarity(targetRarity);
        BallColorLibrary.ColorEntry patternColor = database.patternColorLibrary.GetRandomByRarity(targetRarity);

        if (baseColor == null || pattern == null || patternColor == null)
        {
            Debug.LogError("[BallSkinRandomGenerator] Nessun elemento disponibile per la rarità: " + targetRarity, this);
            return null;
        }

        float patternIntensity = Random.Range(patternIntensityRange.x, patternIntensityRange.y);
        float patternScale = Random.Range(patternScaleRange.x, patternScaleRange.y);

        BallSkinData newSkin = BallSkinFactory.CreateSkin(
            baseColor.id,
            pattern.id,
            patternColor.id,
            patternIntensity,
            patternScale,
            targetRarity
        );

        return newSkin;
    }

    public BallSkinData GenerateGuaranteedCommonSkin()
    {
        return GenerateRandomSkinWithMinimumRarity(SkinRarity.Common);
    }

    public BallSkinData GenerateGuaranteedRareSkin()
    {
        return GenerateRandomSkinWithMinimumRarity(SkinRarity.Rare);
    }

    public BallSkinData GenerateGuaranteedEpicSkin()
    {
        return GenerateRandomSkinWithMinimumRarity(SkinRarity.Epic);
    }

    public BallSkinData GenerateGuaranteedLegendarySkin()
    {
        return GenerateRandomSkinWithMinimumRarity(SkinRarity.Legendary);
    }
}