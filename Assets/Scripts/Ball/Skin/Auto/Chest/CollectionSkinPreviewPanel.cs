using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CollectionSkinPreviewPanel : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Player1SkinInventory player1SkinInventory;
    [SerializeField] private BallSkinApplier previewSkinApplier;

    [Header("Panel")]
    [SerializeField] private GameObject previewPanelRoot;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button closeButton;

    [Header("Texts")]
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text skinIdText;
    [SerializeField] private string noSelectionText = "NO SELECTION";

    [Header("Hide When Preview Open")]
    [SerializeField] private List<GameObject> objectsToHideWhenPreviewOpen = new List<GameObject>();

    [Header("Optional Refresh Targets")]
    [SerializeField] private SkinCollectionPagedUI collectionPagedUI;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private BallSkinData currentSelectedSkin;

    private void Awake()
    {
        if (equipButton != null)
        {
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(EquipCurrentSelectedSkin);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ClosePreview);
        }

        SetPreviewVisible(false);
        ClearPreviewTexts();
    }

    public void ShowPreview(BallSkinData skinData)
    {
        if (!ValidateDependencies())
            return;

        if (skinData == null)
        {
            if (logDebug)
                Debug.LogWarning("[CollectionSkinPreviewPanel] ShowPreview called with null skin.", this);

            ShowNoSelection();
            return;
        }

        currentSelectedSkin = skinData;

        bool applied = previewSkinApplier.ApplySkinData(player1SkinInventory.Database, skinData);
        if (!applied)
        {
            Debug.LogError(
                "[CollectionSkinPreviewPanel] Failed to apply preview skin: " + skinData.skinUniqueId,
                this
            );
            return;
        }

        if (rarityText != null)
            rarityText.text = skinData.rarity.ToString().ToUpperInvariant();

        if (skinIdText != null)
            skinIdText.text = skinData.skinUniqueId;

        SetPreviewVisible(true);

        if (logDebug)
        {
            Debug.Log(
                "[CollectionSkinPreviewPanel] Preview opened for skin: " +
                skinData.skinUniqueId + " | rarity: " + skinData.rarity,
                this
            );
        }
    }

    public void ShowPreviewBySkinId(string skinUniqueId)
    {
        if (!ValidateDependencies())
            return;

        if (string.IsNullOrWhiteSpace(skinUniqueId))
        {
            ShowNoSelection();
            return;
        }

        BallSkinData skinData = player1SkinInventory.GetUnlockedSkinById(skinUniqueId);
        if (skinData == null)
        {
            Debug.LogWarning(
                "[CollectionSkinPreviewPanel] No unlocked skin found for id: " + skinUniqueId,
                this
            );
            ShowNoSelection();
            return;
        }

        ShowPreview(skinData);
    }

    public void ShowNoSelection()
    {
        currentSelectedSkin = null;

        if (rarityText != null)
            rarityText.text = noSelectionText;

        if (skinIdText != null)
            skinIdText.text = string.Empty;

        SetPreviewVisible(true);

        if (logDebug)
            Debug.Log("[CollectionSkinPreviewPanel] Preview opened with no selection.", this);
    }

    public void ClosePreview()
    {
        SetPreviewVisible(false);
        currentSelectedSkin = null;
        ClearPreviewTexts();

        if (logDebug)
            Debug.Log("[CollectionSkinPreviewPanel] Preview closed.", this);
    }

    public void EquipCurrentSelectedSkin()
    {
        if (!ValidateDependencies())
            return;

        if (currentSelectedSkin == null)
        {
            Debug.LogWarning("[CollectionSkinPreviewPanel] No selected skin to equip.", this);
            return;
        }

        bool equipped = player1SkinInventory.EquipSkin(currentSelectedSkin.skinUniqueId);

        if (logDebug)
        {
            Debug.Log(
                "[CollectionSkinPreviewPanel] Equip selected skin -> " +
                currentSelectedSkin.skinUniqueId +
                " | equipped: " + equipped,
                this
            );
        }

        RefreshCollectionIfVisible();
        ClosePreview();
    }

    private void SetPreviewVisible(bool visible)
    {
        if (previewPanelRoot != null)
            previewPanelRoot.SetActive(visible);

        SetHiddenObjectsVisible(!visible);
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

    private void ClearPreviewTexts()
    {
        if (rarityText != null)
            rarityText.text = noSelectionText;

        if (skinIdText != null)
            skinIdText.text = string.Empty;
    }

    private void RefreshCollectionIfVisible()
    {
        if (collectionPagedUI == null)
            return;

        if (!collectionPagedUI.gameObject.activeInHierarchy)
            return;

        collectionPagedUI.Rebuild();
    }

    private bool ValidateDependencies()
    {
        if (player1SkinInventory == null)
            player1SkinInventory = Player1SkinInventory.Instance;

        if (player1SkinInventory == null)
        {
            Debug.LogError("[CollectionSkinPreviewPanel] Player1SkinInventory missing.", this);
            return false;
        }

        if (player1SkinInventory.Database == null)
        {
            Debug.LogError("[CollectionSkinPreviewPanel] Player1SkinInventory.Database missing.", this);
            return false;
        }

        if (previewSkinApplier == null)
        {
            Debug.LogError("[CollectionSkinPreviewPanel] PreviewSkinApplier missing.", this);
            return false;
        }

        return true;
    }
}