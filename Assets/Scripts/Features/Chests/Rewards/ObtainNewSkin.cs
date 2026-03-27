using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class ChestOpenRewardResult
{
    public bool success;
    public int slotIndex = -1;
    public ChestType chestType = ChestType.Random;
    public BallSkinData grantedSkin;
    public string source = "";
    public string reason = "";
}

public class ObtainNewSkin : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private BallSkinRandomGenerator skinRandomGenerator;
    [SerializeField] private PlayerSkinInventory playerSkinInventory;
    [SerializeField] private PlayerChestSlotInventory playerChestSlotInventory;

    [Header("Popup UI")]
    [SerializeField] private GameObject newSkinPreviewSection;
    [SerializeField] private BallSkinApplier previewSkinApplier;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button closeButton;

    [Header("Texts")]
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text skinIdText;

    [Header("Hide When Preview Open")]
    [SerializeField] private List<GameObject> objectsToHideWhenPreviewOpen = new List<GameObject>();

    [Header("Optional Refresh Targets")]
    [SerializeField] private SkinCollectionPagedUI collectionPagedUI;

    [Header("Generation Limits")]
    [SerializeField] private int maxUniqueRollAttempts = 50;
    [SerializeField] private int maxExactRarityRollAttempts = 200;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BallSkinData currentRolledSkin;

    public event Action<ChestOpenRewardResult> OnChestOpenedAndRewardGranted;

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
        ChestOpenRewardResult result = TryOpenChestFromSlot(slotIndex);

        if (!result.success)
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[ObtainNewSkin] OpenChestFromSlot failed. " +
                    "Slot=" + slotIndex +
                    " | Reason=" + result.reason,
                    this
                );
            }

            return;
        }

        currentRolledSkin = result.grantedSkin;
        RefreshNewSkinPreviewSection();
        OpenNewSkinPreviewSection();
        RefreshCollectionIfVisible();

        OnChestOpenedAndRewardGranted?.Invoke(result);

        if (logDebug)
        {
            Debug.Log(
                "[ObtainNewSkin] Chest opened successfully. " +
                "Slot=" + result.slotIndex +
                " | Type=" + result.chestType +
                " | SkinId=" + (result.grantedSkin != null ? result.grantedSkin.skinUniqueId : "null"),
                this
            );
        }
    }

    public ChestOpenRewardResult TryOpenChestFromSlot(int slotIndex)
    {
        ChestOpenRewardResult result = new ChestOpenRewardResult
        {
            success = false,
            slotIndex = slotIndex,
            source = "chest_slot_open",
            reason = ""
        };

        if (!ValidateDependencies())
        {
            result.reason = "missing_dependencies";
            return result;
        }

        if (!playerChestSlotInventory.HasChestInSlot(slotIndex))
        {
            result.reason = "slot_has_no_chest";
            return result;
        }

        if (!playerChestSlotInventory.IsChestReadyToOpen(slotIndex))
        {
            result.reason = "chest_not_ready";
            return result;
        }

        ChestType chestType = playerChestSlotInventory.GetChestTypeInSlot(slotIndex);
        result.chestType = chestType;

        BallSkinData grantedSkin = TryGenerateAndGrantChestReward(chestType, "Chest Slot " + slotIndex);
        if (grantedSkin == null)
        {
            result.reason = "failed_to_generate_unique_skin";
            return result;
        }

        bool consumed = playerChestSlotInventory.ConsumeOpenedChestInSlot(slotIndex);
        if (!consumed)
        {
            result.reason = "failed_to_consume_chest_after_reward";
            return result;
        }

        result.success = true;
        result.grantedSkin = grantedSkin;
        result.reason = "granted";

        return result;
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

        bool equipped = playerSkinInventory.EquipSkin(currentRolledSkin.skinUniqueId);

        if (logDebug)
        {
            Debug.Log(
                "[ObtainNewSkin] Equip current rolled skin -> " +
                currentRolledSkin.skinUniqueId +
                " | Equipped=" + equipped,
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

    private BallSkinData TryGenerateAndGrantChestReward(ChestType chestType, string sourceLabel)
    {
        BallSkinData grantedSkin = null;

        for (int attempt = 0; attempt < maxUniqueRollAttempts; attempt++)
        {
            BallSkinData candidate = GenerateCandidateForChestType(chestType);

            if (candidate == null)
                continue;

            bool wasAdded = playerSkinInventory.AddUnlockedSkin(candidate);

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
            break;
        }

        if (grantedSkin == null)
        {
            Debug.LogWarning(
                "[ObtainNewSkin] Failed to grant a unique skin after " +
                maxUniqueRollAttempts +
                " attempts from source: " + sourceLabel,
                this
            );
        }

        return grantedSkin;
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

        if (previewSkinApplier != null && playerSkinInventory != null)
        {
            bool applied = previewSkinApplier.ApplySkinData(playerSkinInventory.Database, currentRolledSkin);

            if (!applied)
            {
                Debug.LogError(
                    "[ObtainNewSkin] Failed to apply popup preview skin: " + currentRolledSkin.skinUniqueId,
                    this
                );
            }
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

        if (playerSkinInventory == null)
            playerSkinInventory = PlayerSkinInventory.Instance;

        if (playerSkinInventory == null)
        {
            Debug.LogError("[ObtainNewSkin] PlayerSkinInventory missing.", this);
            return false;
        }

        if (playerSkinInventory.Database == null)
        {
            Debug.LogError("[ObtainNewSkin] PlayerSkinInventory database missing.", this);
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