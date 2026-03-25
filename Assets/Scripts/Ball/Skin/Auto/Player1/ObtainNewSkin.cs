using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObtainNewSkin : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private BallSkinRandomGenerator skinRandomGenerator;
    [SerializeField] private Player1SkinInventory player1SkinInventory;

    [Header("New Skin Preview Section")]
    [SerializeField] private GameObject newSkinPreviewSection;
    [SerializeField] private BallSkinApplier previewSkinApplier;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button closeButton;

    [Header("Optional Info UI")]
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text skinIdText;

    [Header("Hide When Preview Open")]
    [SerializeField] private List<GameObject> objectsToHideWhenPreviewOpen = new List<GameObject>();

    [Header("Optional Refresh Targets")]
    [SerializeField] private SkinCollectionPagedUI collectionPagedUI;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BallSkinData currentRolledSkin;

    private void Awake()
    {
        if (equipButton != null)
        {
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(EquipCurrentRolledSkin);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseNewSkinPreviewSection);
        }

        if (newSkinPreviewSection != null)
            newSkinPreviewSection.SetActive(false);

        SetHiddenObjectsVisible(true);
    }

    public void RollRandom()
    {
        if (!ValidateDependencies())
            return;

        currentRolledSkin = skinRandomGenerator.GenerateRandomSkin();
        OnSkinRolled("Random");
    }

    public void RollCommonGuaranteed()
    {
        RollWithMinimumRarity(SkinRarity.Common, "Common Guaranteed");
    }

    public void RollRareGuaranteed()
    {
        RollWithMinimumRarity(SkinRarity.Rare, "Rare Guaranteed");
    }

    public void RollEpicGuaranteed()
    {
        RollWithMinimumRarity(SkinRarity.Epic, "Epic Guaranteed");
    }

    public void RollLegendaryGuaranteed()
    {
        RollWithMinimumRarity(SkinRarity.Legendary, "Legendary Guaranteed");
    }

    public void EquipCurrentRolledSkin()
    {
        if (!ValidateDependencies())
            return;

        if (currentRolledSkin == null)
        {
            Debug.LogWarning("[ObtainNewSkin] No rolled skin available to equip.", this);
            return;
        }

        bool equipped = player1SkinInventory.EquipSkin(currentRolledSkin.skinUniqueId);

        if (logDebug)
        {
            Debug.Log(
                "[ObtainNewSkin] Equip current rolled skin -> " +
                currentRolledSkin.skinUniqueId +
                " | equipped: " + equipped,
                this
            );
        }

        RefreshCollectionIfVisible();
        CloseNewSkinPreviewSection();
    }

    public void OpenNewSkinPreviewSection()
    {
        if (newSkinPreviewSection != null)
            newSkinPreviewSection.SetActive(true);

        SetHiddenObjectsVisible(false);
    }

    public void CloseNewSkinPreviewSection()
    {
        if (newSkinPreviewSection != null)
            newSkinPreviewSection.SetActive(false);

        SetHiddenObjectsVisible(true);
    }

    private void RollWithMinimumRarity(SkinRarity rarity, string label)
    {
        if (!ValidateDependencies())
            return;

        currentRolledSkin = skinRandomGenerator.GenerateRandomSkinWithMinimumRarity(rarity);
        OnSkinRolled(label);
    }

    private void OnSkinRolled(string sourceLabel)
    {
        if (currentRolledSkin == null)
        {
            Debug.LogError("[ObtainNewSkin] Skin generation failed from source: " + sourceLabel, this);
            return;
        }

        bool added = player1SkinInventory.AddUnlockedSkin(currentRolledSkin);

        if (logDebug)
        {
            Debug.Log(
                "[ObtainNewSkin] Rolled skin from " + sourceLabel +
                " -> ID: " + currentRolledSkin.skinUniqueId +
                " | rarity: " + currentRolledSkin.rarity +
                " | added to inventory: " + added,
                this
            );
        }

        OpenNewSkinPreviewSection();
        RefreshNewSkinPreviewSection();
        RefreshCollectionIfVisible();
    }

    private void RefreshNewSkinPreviewSection()
    {
        if (currentRolledSkin == null)
        {
            if (rarityText != null)
                rarityText.text = string.Empty;

            if (skinIdText != null)
                skinIdText.text = string.Empty;

            return;
        }

        if (rarityText != null)
            rarityText.text = currentRolledSkin.rarity.ToString().ToUpperInvariant();

        if (skinIdText != null)
            skinIdText.text = currentRolledSkin.skinUniqueId;

        if (previewSkinApplier != null && player1SkinInventory != null)
        {
            bool applied = previewSkinApplier.ApplySkinData(player1SkinInventory.Database, currentRolledSkin);

            if (!applied)
                Debug.LogError("[ObtainNewSkin] Failed to apply popup preview skin: " + currentRolledSkin.skinUniqueId, this);
        }
        else
        {
            Debug.LogWarning("[ObtainNewSkin] Preview references missing.", this);
        }
    }

    private void RefreshCollectionIfVisible()
    {
        if (collectionPagedUI == null)
            return;

        if (!collectionPagedUI.gameObject.activeInHierarchy)
        {
            if (logDebug)
                Debug.Log("[ObtainNewSkin] CollectionPanel inactive, skip rebuild. It will rebuild on enable.", this);

            return;
        }

        collectionPagedUI.Rebuild();
    }

    private void SetHiddenObjectsVisible(bool visible)
    {
        if (objectsToHideWhenPreviewOpen == null)
            return;

        for (int i = 0; i < objectsToHideWhenPreviewOpen.Count; i++)
        {
            if (objectsToHideWhenPreviewOpen[i] != null)
                objectsToHideWhenPreviewOpen[i].SetActive(visible);
        }
    }

    private bool ValidateDependencies()
    {
        if (skinRandomGenerator == null)
        {
            Debug.LogError("[ObtainNewSkin] BallSkinRandomGenerator missing.", this);
            return false;
        }

        if (player1SkinInventory == null)
            player1SkinInventory = Player1SkinInventory.Instance;

        if (player1SkinInventory == null)
        {
            Debug.LogError("[ObtainNewSkin] Player1SkinInventory missing.", this);
            return false;
        }

        if (player1SkinInventory.Database == null)
        {
            Debug.LogError("[ObtainNewSkin] Player1SkinInventory database missing.", this);
            return false;
        }

        return true;
    }
}