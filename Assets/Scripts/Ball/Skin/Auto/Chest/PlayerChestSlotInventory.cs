using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerChestSlotInventory : MonoBehaviour
{
    [Serializable]
    public class ChestUnlockDurationEntry
    {
        public ChestType chestType = ChestType.Random;
        [Min(0)] public int unlockDurationSeconds = 60;
    }

    public static PlayerChestSlotInventory Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Slots")]
    [SerializeField] private int slotCount = 4;

    [Header("Unlock Durations By Chest Type")]
    [SerializeField]
    private List<ChestUnlockDurationEntry> unlockDurationEntries = new List<ChestUnlockDurationEntry>
    {
        new ChestUnlockDurationEntry { chestType = ChestType.Random, unlockDurationSeconds = 60 },
        new ChestUnlockDurationEntry { chestType = ChestType.GuaranteedCommon, unlockDurationSeconds = 300 },
        new ChestUnlockDurationEntry { chestType = ChestType.GuaranteedRare, unlockDurationSeconds = 1800 },
        new ChestUnlockDurationEntry { chestType = ChestType.GuaranteedEpic, unlockDurationSeconds = 7200 },
        new ChestUnlockDurationEntry { chestType = ChestType.GuaranteedLegendary, unlockDurationSeconds = 14400 }
    };

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private const string SaveKey = "PLAYER1_CHEST_SLOT_INVENTORY_V2";

    [SerializeField] private PlayerChestSlotInventorySaveData runtimeData = new PlayerChestSlotInventorySaveData();

    public event Action OnChestInventoryChanged;

    public int SlotCount => Mathf.Max(1, slotCount);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        LoadInventory();
        EnsureRuntimeStructure();
        SaveInventory();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] Initialized. SlotCount=" + SlotCount +
                " | OccupiedSlots=" + GetOccupiedSlotCount() +
                " | Queued=" + GetQueuedChestCount(),
                this
            );
        }
    }

    public bool AwardChest(ChestType chestType)
    {
        ChestRuntimeData newChest = new ChestRuntimeData(chestType, GetNowUnixSeconds());

        int freeSlotIndex = GetFirstFreeSlotIndex();
        if (freeSlotIndex >= 0)
        {
            AssignChestToSlot(freeSlotIndex, newChest);
            SaveInventory();
            NotifyInventoryChanged();

            if (logDebug)
            {
                Debug.Log(
                    "[PlayerChestSlotInventory] Awarded chest directly to slot " + freeSlotIndex +
                    " | Type=" + chestType +
                    " | ChestId=" + newChest.chestInstanceId,
                    this
                );
            }

            return true;
        }

        runtimeData.queuedChests.Add(newChest);
        SaveInventory();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] All slots occupied. Chest queued." +
                " | Type=" + chestType +
                " | ChestId=" + newChest.chestInstanceId +
                " | QueueCount=" + runtimeData.queuedChests.Count,
                this
            );
        }

        return true;
    }

    public bool HasChestInSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        EnsureRuntimeStructure();
        return runtimeData.slots[slotIndex] != null && runtimeData.slots[slotIndex].hasChest && runtimeData.slots[slotIndex].chest != null;
    }

    public ChestRuntimeData GetChestInSlot(int slotIndex)
    {
        if (!HasChestInSlot(slotIndex))
            return null;

        return runtimeData.slots[slotIndex].chest;
    }

    public ChestType GetChestTypeInSlot(int slotIndex)
    {
        ChestRuntimeData chest = GetChestInSlot(slotIndex);
        return chest != null ? chest.chestType : ChestType.Random;
    }

    public bool IsChestReadyToOpen(int slotIndex)
    {
        ChestRuntimeData chest = GetChestInSlot(slotIndex);
        if (chest == null)
            return false;

        return GetNowUnixSeconds() >= chest.unlockEndUnixTimeSeconds;
    }

    public long GetRemainingUnlockSeconds(int slotIndex)
    {
        ChestRuntimeData chest = GetChestInSlot(slotIndex);
        if (chest == null)
            return 0;

        long remaining = chest.unlockEndUnixTimeSeconds - GetNowUnixSeconds();
        return Mathf.Max(0, (int)remaining);
    }

    public string GetRemainingUnlockTimeFormatted(int slotIndex)
    {
        long remaining = GetRemainingUnlockSeconds(slotIndex);
        return FormatDuration(remaining);
    }

    public bool ConsumeOpenedChestInSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        if (!HasChestInSlot(slotIndex))
            return false;

        if (!IsChestReadyToOpen(slotIndex))
        {
            if (logDebug)
                Debug.LogWarning("[PlayerChestSlotInventory] Cannot consume chest. Unlock not completed yet. Slot=" + slotIndex, this);

            return false;
        }

        ChestRuntimeData consumedChest = runtimeData.slots[slotIndex].chest;

        runtimeData.slots[slotIndex].hasChest = false;
        runtimeData.slots[slotIndex].chest = null;

        TryFillFreeSlotsFromQueue();

        SaveInventory();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] Consumed chest from slot " + slotIndex +
                " | Type=" + consumedChest.chestType +
                " | ChestId=" + consumedChest.chestInstanceId,
                this
            );
        }

        return true;
    }

    public int GetFirstFreeSlotIndex()
    {
        EnsureRuntimeStructure();

        for (int i = 0; i < runtimeData.slots.Length; i++)
        {
            if (runtimeData.slots[i] == null || !runtimeData.slots[i].hasChest || runtimeData.slots[i].chest == null)
                return i;
        }

        return -1;
    }

    public int GetOccupiedSlotCount()
    {
        EnsureRuntimeStructure();

        int count = 0;
        for (int i = 0; i < runtimeData.slots.Length; i++)
        {
            if (HasChestInSlot(i))
                count++;
        }

        return count;
    }

    public int GetQueuedChestCount()
    {
        EnsureRuntimeStructure();
        return runtimeData.queuedChests != null ? runtimeData.queuedChests.Count : 0;
    }

    public int GetUnlockDurationSeconds(ChestType chestType)
    {
        for (int i = 0; i < unlockDurationEntries.Count; i++)
        {
            if (unlockDurationEntries[i] != null && unlockDurationEntries[i].chestType == chestType)
                return Mathf.Max(0, unlockDurationEntries[i].unlockDurationSeconds);
        }

        return 0;
    }

    public void SaveInventory()
    {
        EnsureRuntimeStructure();
        string json = JsonUtility.ToJson(runtimeData);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public void LoadInventory()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            runtimeData = new PlayerChestSlotInventorySaveData();
            return;
        }

        string json = PlayerPrefs.GetString(SaveKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            runtimeData = new PlayerChestSlotInventorySaveData();
            return;
        }

        runtimeData = JsonUtility.FromJson<PlayerChestSlotInventorySaveData>(json);

        if (runtimeData == null)
            runtimeData = new PlayerChestSlotInventorySaveData();
    }

    public void ClearAllChestDataForDebug()
    {
        runtimeData = new PlayerChestSlotInventorySaveData();
        EnsureRuntimeStructure();

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();

        SaveInventory();
        NotifyInventoryChanged();

        if (logDebug)
            Debug.Log("[PlayerChestSlotInventory] Cleared all chest data.", this);
    }

    public void DebugAddRandomChest()
    {
        AwardChest(ChestType.Random);
    }

    public void DebugAddCommonChest()
    {
        AwardChest(ChestType.GuaranteedCommon);
    }

    public void DebugAddRareChest()
    {
        AwardChest(ChestType.GuaranteedRare);
    }

    public void DebugAddEpicChest()
    {
        AwardChest(ChestType.GuaranteedEpic);
    }

    public void DebugAddLegendaryChest()
    {
        AwardChest(ChestType.GuaranteedLegendary);
    }

    private void AssignChestToSlot(int slotIndex, ChestRuntimeData chest)
    {
        if (!IsValidSlotIndex(slotIndex) || chest == null)
            return;

        long now = GetNowUnixSeconds();
        chest.slotAssignedUnixTimeSeconds = now;
        chest.unlockEndUnixTimeSeconds = now + GetUnlockDurationSeconds(chest.chestType);

        runtimeData.slots[slotIndex].hasChest = true;
        runtimeData.slots[slotIndex].chest = chest;
    }

    private void TryFillFreeSlotsFromQueue()
    {
        EnsureRuntimeStructure();

        if (runtimeData.queuedChests == null)
            runtimeData.queuedChests = new List<ChestRuntimeData>();

        bool assignedAtLeastOne = true;

        while (assignedAtLeastOne)
        {
            assignedAtLeastOne = false;

            int freeSlotIndex = GetFirstFreeSlotIndex();
            if (freeSlotIndex < 0)
                break;

            if (runtimeData.queuedChests.Count <= 0)
                break;

            ChestRuntimeData nextChest = runtimeData.queuedChests[0];
            runtimeData.queuedChests.RemoveAt(0);

            AssignChestToSlot(freeSlotIndex, nextChest);
            assignedAtLeastOne = true;

            if (logDebug)
            {
                Debug.Log(
                    "[PlayerChestSlotInventory] Moved queued chest into free slot " + freeSlotIndex +
                    " | Type=" + nextChest.chestType +
                    " | ChestId=" + nextChest.chestInstanceId,
                    this
                );
            }
        }
    }

    private void EnsureRuntimeStructure()
    {
        if (runtimeData == null)
            runtimeData = new PlayerChestSlotInventorySaveData();

        if (runtimeData.queuedChests == null)
            runtimeData.queuedChests = new List<ChestRuntimeData>();

        int targetSlotCount = SlotCount;

        if (runtimeData.slots == null || runtimeData.slots.Length != targetSlotCount)
        {
            ChestSlotSaveData[] oldSlots = runtimeData.slots;
            runtimeData.slots = new ChestSlotSaveData[targetSlotCount];

            for (int i = 0; i < targetSlotCount; i++)
            {
                runtimeData.slots[i] = new ChestSlotSaveData();
            }

            if (oldSlots != null)
            {
                int copyCount = Mathf.Min(oldSlots.Length, runtimeData.slots.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    runtimeData.slots[i] = oldSlots[i] ?? new ChestSlotSaveData();
                }

                if (oldSlots.Length > runtimeData.slots.Length)
                {
                    for (int i = runtimeData.slots.Length; i < oldSlots.Length; i++)
                    {
                        if (oldSlots[i] != null && oldSlots[i].hasChest && oldSlots[i].chest != null)
                            runtimeData.queuedChests.Add(oldSlots[i].chest);
                    }
                }
            }
        }

        for (int i = 0; i < runtimeData.slots.Length; i++)
        {
            if (runtimeData.slots[i] == null)
                runtimeData.slots[i] = new ChestSlotSaveData();
        }
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        EnsureRuntimeStructure();
        return slotIndex >= 0 && slotIndex < runtimeData.slots.Length;
    }

    private long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void NotifyInventoryChanged()
    {
        OnChestInventoryChanged?.Invoke();
    }

    private string FormatDuration(long totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);

        TimeSpan timeSpan = TimeSpan.FromSeconds(totalSeconds);

        if (timeSpan.TotalHours >= 1d)
            return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds);

        return string.Format("{0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);
    }
}