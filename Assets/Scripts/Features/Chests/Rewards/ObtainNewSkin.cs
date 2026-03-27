using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObtainNewSkin : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private BallSkinRandomGenerator skinRandomGenerator;
    [SerializeField] private Player1SkinInventory player1SkinInventory;
    [SerializeField] private PlayerChestSlotInventory playerChestSlotInventory;

    [Header("Generation")]
    [SerializeField] private int maxUniqueRollAttempts = 50;
    [SerializeField] private int maxExactRarityRollAttempts = 100;

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

    public void OpenChestFromSlot(int slotIndex)
    {
        if (!ValidateDependencies())
            return;

        if (!playerChestSlotInventory.HasChestInSlot(slotIndex))
        {
            if (logDebug)
                Debug.LogWarning("[ObtainNewSkin] Slot has no chest. Slot=" + slotIndex, this);

            return;
        }

        if (!playerChestSlotInventory.IsChestReadyToOpen(slotIndex))
        {
            if (logDebug)
                Debug.LogWarning("[ObtainNewSkin] Chest not ready to open yet. Slot=" + slotIndex, this);

            return;
        }

        ChestType chestType = playerChestSlotInventory.GetChestTypeInSlot(slotIndex);

        bool granted = TryGrantUniqueSkinForChestType(chestType, "Chest Slot " + slotIndex);

        if (!granted)
        {
            Debug.LogWarning(
                "[ObtainNewSkin] Failed to grant skin from chest slot " + slotIndex +
                ". Chest remains in slot.",
                this
            );
            return;
        }

        bool consumed = playerChestSlotInventory.ConsumeOpenedChestInSlot(slotIndex);

        if (logDebug)
        {
            Debug.Log(
                "[ObtainNewSkin] OpenChestFromSlot completed. Slot=" + slotIndex +
                " | Type=" + chestType +
                " | Consumed=" + consumed,
                this
            );
        }
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

    private bool TryGrantUniqueSkinForChestType(ChestType chestType, string sourceLabel)
    {
        BallSkinData grantedSkin = null;
        bool added = false;

        for (int attempt = 0; attempt < maxUniqueRollAttempts; attempt++)
        {
            BallSkinData candidate = GenerateCandidateForChestType(chestType);

            if (candidate == null)
                continue;

            bool wasAdded = player1SkinInventory.AddUnlockedSkin(candidate);

            if (!wasAdded)
            {
                if (logDebug)
                {
                    Debug.Log(
                        "[ObtainNewSkin] Duplicate skin rolled, retrying... Attempt " +
                        (attempt + 1) + "/" + maxUniqueRollAttempts +
                        " | Skin ID: " + candidate.skinUniqueId,
                        this
                    );
                }

                continue;
            }

            grantedSkin = candidate;
            added = true;
            break;
        }

        if (!added || grantedSkin == null)
        {
            Debug.LogWarning(
                "[ObtainNewSkin] Failed to grant a unique skin after " +
                maxUniqueRollAttempts +
                " attempts from source: " + sourceLabel,
                this
            );

            return false;
        }

        currentRolledSkin = grantedSkin;
        OnSkinRolled(sourceLabel, true);
        return true;
    }

    private BallSkinData GenerateCandidateForChestType(ChestType chestType)
    {
        switch (chestType)
        {
            case ChestType.Random:
                return skinRandomGenerator.GenerateRandomSkin();

            case ChestType.GuaranteedCommon:
                return GenerateRandomSkinOfExactRarity(SkinRarity.Common);

            case ChestType.GuaranteedRare:
                return GenerateRandomSkinOfExactRarity(SkinRarity.Rare);

            case ChestType.GuaranteedEpic:
                return GenerateRandomSkinOfExactRarity(SkinRarity.Epic);

            case ChestType.GuaranteedLegendary:
                return GenerateRandomSkinOfExactRarity(SkinRarity.Legendary);

            default:
                return skinRandomGenerator.GenerateRandomSkin();
        }
    }

    private BallSkinData GenerateRandomSkinOfExactRarity(SkinRarity targetRarity)
    {
        for (int i = 0; i < maxExactRarityRollAttempts; i++)
        {
            BallSkinData candidate = skinRandomGenerator.GenerateRandomSkin();
            if (candidate != null && candidate.rarity == targetRarity)
                return candidate;
        }

        Debug.LogWarning(
            "[ObtainNewSkin] Failed to generate exact rarity skin after " +
            maxExactRarityRollAttempts +
            " attempts. TargetRarity=" + targetRarity,
            this
        );

        return null;
    }

    private void OnSkinRolled(string sourceLabel, bool alreadyAddedToInventory)
    {
        if (currentRolledSkin == null)
        {
            Debug.LogError("[ObtainNewSkin] Skin generation failed from source: " + sourceLabel, this);
            return;
        }

        if (logDebug)
        {
            Debug.Log(
                "[ObtainNewSkin] Rolled skin from " + sourceLabel +
                " -> ID: " + currentRolledSkin.skinUniqueId +
                " | rarity: " + currentRolledSkin.rarity +
                " | already added to inventory: " + alreadyAddedToInventory,
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

        if (playerChestSlotInventory == null)
            playerChestSlotInventory = PlayerChestSlotInventory.Instance;

        if (playerChestSlotInventory == null)
        {
            Debug.LogError("[ObtainNewSkin] PlayerChestSlotInventory missing.", this);
            return false;
        }

        return true;
    }
}