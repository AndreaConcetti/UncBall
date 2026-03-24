using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinRewardTestMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BallSkinRandomGenerator skinRandomGenerator;
    [SerializeField] private PlayerSkinLoadout playerSkinLoadout;

    [Header("Optional Preview")]
    [SerializeField] private BallSkinApplier previewSkinApplier;

    [Header("Optional UI")]
    [SerializeField] private TMP_Text currentSkinText;
    [SerializeField] private TMP_Text player1EquippedText;
    [SerializeField] private TMP_Text player2EquippedText;
    [SerializeField] private Image rarityColorPreview;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BallSkinData currentGeneratedSkin;

    private void Awake()
    {
        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance;
    }

    private void Start()
    {
        RefreshEquippedTexts();
        RefreshCurrentSkinUI();
    }

    public void RollRandom()
    {
        if (!ValidateGenerator())
            return;

        currentGeneratedSkin = skinRandomGenerator.GenerateRandomSkin();
        OnSkinGenerated("Random");
    }

    public void RollCommonGuaranteed()
    {
        GenerateGuaranteed(SkinRarity.Common, "Common Guaranteed");
    }

    public void RollRareGuaranteed()
    {
        GenerateGuaranteed(SkinRarity.Rare, "Rare Guaranteed");
    }

    public void RollEpicGuaranteed()
    {
        GenerateGuaranteed(SkinRarity.Epic, "Epic Guaranteed");
    }

    public void RollLegendaryGuaranteed()
    {
        GenerateGuaranteed(SkinRarity.Legendary, "Legendary Guaranteed");
    }

    public void EquipCurrentToPlayer1()
    {
        if (!ValidateGeneratedSkin())
            return;

        if (!ValidateLoadout())
            return;

        playerSkinLoadout.EquipSkinForPlayer1(currentGeneratedSkin);

        if (logDebug)
            Debug.Log("[SkinRewardTestMenu] Equipped current skin to Player 1: " + currentGeneratedSkin.skinUniqueId, this);

        RefreshEquippedTexts();
    }

    public void EquipCurrentToPlayer2()
    {
        if (!ValidateGeneratedSkin())
            return;

        if (!ValidateLoadout())
            return;

        playerSkinLoadout.EquipSkinForPlayer2(currentGeneratedSkin);

        if (logDebug)
            Debug.Log("[SkinRewardTestMenu] Equipped current skin to Player 2: " + currentGeneratedSkin.skinUniqueId, this);

        RefreshEquippedTexts();
    }

    public void ApplyPlayer1EquippedToPreview()
    {
        if (!ValidateLoadout())
            return;

        BallSkinData skin = playerSkinLoadout.GetEquippedSkinForPlayer1();
        ApplySkinToPreview(skin);
    }

    public void ApplyPlayer2EquippedToPreview()
    {
        if (!ValidateLoadout())
            return;

        BallSkinData skin = playerSkinLoadout.GetEquippedSkinForPlayer2();
        ApplySkinToPreview(skin);
    }

    public void ApplyCurrentGeneratedToPreview()
    {
        if (!ValidateGeneratedSkin())
            return;

        ApplySkinToPreview(currentGeneratedSkin);
    }

    private void GenerateGuaranteed(SkinRarity rarity, string debugLabel)
    {
        if (!ValidateGenerator())
            return;

        currentGeneratedSkin = skinRandomGenerator.GenerateRandomSkinWithMinimumRarity(rarity);
        OnSkinGenerated(debugLabel);
    }

    private void OnSkinGenerated(string sourceLabel)
    {
        if (currentGeneratedSkin == null)
        {
            Debug.LogError("[SkinRewardTestMenu] Skin generation failed from source: " + sourceLabel, this);
            RefreshCurrentSkinUI();
            return;
        }

        if (logDebug)
        {
            Debug.Log(
                "[SkinRewardTestMenu] Generated skin from " + sourceLabel +
                " -> ID: " + currentGeneratedSkin.skinUniqueId +
                " | Rarity: " + currentGeneratedSkin.rarity +
                " | Base: " + currentGeneratedSkin.baseColorId +
                " | Pattern: " + currentGeneratedSkin.patternId +
                " | PatternColor: " + currentGeneratedSkin.patternColorId +
                " | Intensity: " + currentGeneratedSkin.patternIntensity.ToString("F2") +
                " | Scale: " + currentGeneratedSkin.patternScale.ToString("F2"),
                this
            );
        }

        RefreshCurrentSkinUI();
        ApplyCurrentGeneratedToPreview();
    }

    private void ApplySkinToPreview(BallSkinData skin)
    {
        if (previewSkinApplier == null)
            return;

        if (!ValidateLoadout())
            return;

        if (skin == null)
        {
            Debug.LogWarning("[SkinRewardTestMenu] Preview requested with null skin.", this);
            return;
        }

        bool applied = previewSkinApplier.ApplySkinData(playerSkinLoadout.Database, skin);

        if (!applied)
            Debug.LogError("[SkinRewardTestMenu] Failed to apply skin to preview: " + skin.skinUniqueId, this);
    }

    private void RefreshCurrentSkinUI()
    {
        if (currentSkinText != null)
        {
            if (currentGeneratedSkin == null)
            {
                currentSkinText.text = "No skin generated";
            }
            else
            {
                currentSkinText.text =
                    "Generated Skin\n" +
                    "ID: " + currentGeneratedSkin.skinUniqueId + "\n" +
                    "Rarity: " + currentGeneratedSkin.rarity + "\n" +
                    "Base: " + currentGeneratedSkin.baseColorId + "\n" +
                    "Pattern: " + currentGeneratedSkin.patternId + "\n" +
                    "Pattern Color: " + currentGeneratedSkin.patternColorId + "\n" +
                    "Intensity: " + currentGeneratedSkin.patternIntensity.ToString("F2") + "\n" +
                    "Scale: " + currentGeneratedSkin.patternScale.ToString("F2");
            }
        }

        if (rarityColorPreview != null)
        {
            if (currentGeneratedSkin == null)
            {
                rarityColorPreview.color = Color.gray;
            }
            else
            {
                rarityColorPreview.color = GetRarityDisplayColor(currentGeneratedSkin.rarity);
            }
        }
    }

    private void RefreshEquippedTexts()
    {
        if (!ValidateLoadoutSilently())
        {
            if (player1EquippedText != null)
                player1EquippedText.text = "P1 Equipped: Missing Loadout";

            if (player2EquippedText != null)
                player2EquippedText.text = "P2 Equipped: Missing Loadout";

            return;
        }

        BallSkinData p1 = playerSkinLoadout.GetEquippedSkinForPlayer1();
        BallSkinData p2 = playerSkinLoadout.GetEquippedSkinForPlayer2();

        if (player1EquippedText != null)
            player1EquippedText.text = "P1 Equipped: " + GetCompactSkinLabel(p1);

        if (player2EquippedText != null)
            player2EquippedText.text = "P2 Equipped: " + GetCompactSkinLabel(p2);
    }

    private string GetCompactSkinLabel(BallSkinData skin)
    {
        if (skin == null)
            return "None";

        return skin.rarity + " | " + skin.skinUniqueId;
    }

    private Color GetRarityDisplayColor(SkinRarity rarity)
    {
        switch (rarity)
        {
            case SkinRarity.Common:
                return new Color(0.75f, 0.75f, 0.75f);
            case SkinRarity.Rare:
                return new Color(0.25f, 0.55f, 1f);
            case SkinRarity.Epic:
                return new Color(0.7f, 0.3f, 1f);
            case SkinRarity.Legendary:
                return new Color(1f, 0.65f, 0.1f);
            default:
                return Color.white;
        }
    }

    private bool ValidateGenerator()
    {
        if (skinRandomGenerator != null)
            return true;

        Debug.LogError("[SkinRewardTestMenu] BallSkinRandomGenerator not assigned.", this);
        return false;
    }

    private bool ValidateLoadout()
    {
        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance;

        if (playerSkinLoadout != null && playerSkinLoadout.Database != null)
            return true;

        Debug.LogError("[SkinRewardTestMenu] PlayerSkinLoadout or Database missing.", this);
        return false;
    }

    private bool ValidateLoadoutSilently()
    {
        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance;

        return playerSkinLoadout != null && playerSkinLoadout.Database != null;
    }

    private bool ValidateGeneratedSkin()
    {
        if (currentGeneratedSkin != null)
            return true;

        Debug.LogWarning("[SkinRewardTestMenu] No generated skin available.", this);
        return false;
    }
}