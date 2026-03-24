using UnityEngine;

public class PlayerSkinLoadout : MonoBehaviour
{
    public static PlayerSkinLoadout Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private BallSkinDatabase database;

    [Header("Use Inspector Test Defaults")]
    [SerializeField] private bool loadTestDefaultsOnAwake = true;

    [Header("Test Default P1")]
    [SerializeField] private string testP1BaseColorId = "blue";
    [SerializeField] private string testP1PatternId = "stripes";
    [SerializeField] private string testP1PatternColorId = "yellow";
    [SerializeField] private float testP1PatternIntensity = 1f;
    [SerializeField] private float testP1PatternScale = 1f;
    [SerializeField] private SkinRarity testP1Rarity = SkinRarity.Common;

    [Header("Test Default P2")]
    [SerializeField] private string testP2BaseColorId = "red";
    [SerializeField] private string testP2PatternId = "stripes";
    [SerializeField] private string testP2PatternColorId = "white";
    [SerializeField] private float testP2PatternIntensity = 1f;
    [SerializeField] private float testP2PatternScale = 1f;
    [SerializeField] private SkinRarity testP2Rarity = SkinRarity.Common;

    [Header("Runtime Equipped Skins")]
    [SerializeField] private BallSkinData equippedSkinPlayer1;
    [SerializeField] private BallSkinData equippedSkinPlayer2;

    public BallSkinDatabase Database => database;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        if (loadTestDefaultsOnAwake)
        {
            equippedSkinPlayer1 = BallSkinFactory.CreateSkin(
                testP1BaseColorId,
                testP1PatternId,
                testP1PatternColorId,
                testP1PatternIntensity,
                testP1PatternScale,
                testP1Rarity
            );

            equippedSkinPlayer2 = BallSkinFactory.CreateSkin(
                testP2BaseColorId,
                testP2PatternId,
                testP2PatternColorId,
                testP2PatternIntensity,
                testP2PatternScale,
                testP2Rarity
            );
        }
    }

    public void EquipSkinForPlayer1(BallSkinData skin)
    {
        equippedSkinPlayer1 = CloneSkin(skin);
    }

    public void EquipSkinForPlayer2(BallSkinData skin)
    {
        equippedSkinPlayer2 = CloneSkin(skin);
    }

    public BallSkinData GetEquippedSkinForPlayer1()
    {
        return equippedSkinPlayer1;
    }

    public BallSkinData GetEquippedSkinForPlayer2()
    {
        return equippedSkinPlayer2;
    }

    private BallSkinData CloneSkin(BallSkinData source)
    {
        if (source == null)
            return null;

        return new BallSkinData
        {
            skinUniqueId = source.skinUniqueId,
            baseColorId = source.baseColorId,
            patternId = source.patternId,
            patternColorId = source.patternColorId,
            patternIntensity = source.patternIntensity,
            patternScale = source.patternScale,
            rarity = source.rarity
        };
    }
}