using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChestCardUI : MonoBehaviour
{
    [Serializable]
    public class ChestVisualEntry
    {
        public ChestType chestType;
        public string displayLabel = "RANDOM";
        public Sprite backgroundSprite;
    }

    [Header("Dependencies")]
    [SerializeField] private ObtainNewSkin obtainNewSkin;

    [Header("UI References")]
    [SerializeField] private TMP_Text chestTypeText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Image chestTypeBackgroundImage;
    [SerializeField] private Button openButton;
    [SerializeField] private GameObject availableBadgeObject;
    [SerializeField] private GameObject unavailableOverlayObject;

    [Header("Visual Mapping")]
    [SerializeField] private List<ChestVisualEntry> chestVisualEntries = new List<ChestVisualEntry>();

    [Header("Ready State")]
    [SerializeField] private string readyText = "OPEN";

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private readonly Dictionary<ChestType, ChestVisualEntry> visualMap = new Dictionary<ChestType, ChestVisualEntry>();

    private PlayerChestSlotInventory boundInventory;
    private int boundSlotIndex = -1;

    private void Awake()
    {
        RebuildVisualMap();

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OnOpenButtonClicked);
            openButton.onClick.AddListener(OnOpenButtonClicked);
        }
    }

    private void OnValidate()
    {
        RebuildVisualMap();
    }

    public void Bind(PlayerChestSlotInventory inventory, int slotIndex, ObtainNewSkin obtainNewSkinReference)
    {
        boundInventory = inventory;
        boundSlotIndex = slotIndex;
        obtainNewSkin = obtainNewSkinReference;

        RefreshUI();
    }

    public void RefreshUI()
    {
        if (boundInventory == null || boundSlotIndex < 0)
            return;

        ChestRuntimeData chest = boundInventory.GetChestInSlot(boundSlotIndex);
        if (chest == null)
            return;

        ChestVisualEntry entry = GetVisualEntry(chest.chestType);

        string label = entry != null ? entry.displayLabel : GetDefaultLabel(chest.chestType);
        Sprite backgroundSprite = entry != null ? entry.backgroundSprite : null;

        bool canOpen = boundInventory.IsChestReadyToOpen(boundSlotIndex);
        string remainingTime = boundInventory.GetRemainingUnlockTimeFormatted(boundSlotIndex);

        if (chestTypeText != null)
            chestTypeText.text = label;

        if (chestTypeBackgroundImage != null)
            chestTypeBackgroundImage.sprite = backgroundSprite;

        if (timerText != null)
            timerText.text = canOpen ? readyText : remainingTime;

        if (openButton != null)
            openButton.interactable = canOpen;

        if (availableBadgeObject != null)
            availableBadgeObject.SetActive(canOpen);

        if (unavailableOverlayObject != null)
            unavailableOverlayObject.SetActive(!canOpen);

        if (logDebug)
        {
            Debug.Log(
                "[ChestCardUI] RefreshUI -> Slot=" + boundSlotIndex +
                " | Type=" + chest.chestType +
                " | CanOpen=" + canOpen +
                " | Remaining=" + remainingTime,
                this
            );
        }
    }

    private void OnOpenButtonClicked()
    {
        if (obtainNewSkin == null)
        {
            Debug.LogError("[ChestCardUI] ObtainNewSkin reference missing.", this);
            return;
        }

        if (boundSlotIndex < 0)
        {
            Debug.LogError("[ChestCardUI] Invalid bound slot index.", this);
            return;
        }

        obtainNewSkin.OpenChestFromSlot(boundSlotIndex);
    }

    private ChestVisualEntry GetVisualEntry(ChestType chestType)
    {
        if (visualMap.TryGetValue(chestType, out ChestVisualEntry entry))
            return entry;

        return null;
    }

    private void RebuildVisualMap()
    {
        visualMap.Clear();

        if (chestVisualEntries == null)
            return;

        for (int i = 0; i < chestVisualEntries.Count; i++)
        {
            ChestVisualEntry entry = chestVisualEntries[i];
            if (entry == null)
                continue;

            if (visualMap.ContainsKey(entry.chestType))
                visualMap[entry.chestType] = entry;
            else
                visualMap.Add(entry.chestType, entry);
        }
    }

    private string GetDefaultLabel(ChestType chestType)
    {
        switch (chestType)
        {
            case ChestType.Random:
                return "RANDOM";

            case ChestType.GuaranteedCommon:
                return "COMMON";

            case ChestType.GuaranteedRare:
                return "RARE";

            case ChestType.GuaranteedEpic:
                return "EPIC";

            case ChestType.GuaranteedLegendary:
                return "LEGENDARY";

            default:
                return "UNKNOWN";
        }
    }
}