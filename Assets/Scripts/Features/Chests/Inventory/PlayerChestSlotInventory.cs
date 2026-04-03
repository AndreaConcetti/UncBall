using System;
using System.Collections.Generic;
using UnityEngine;
using UncballArena.Core.Runtime;

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
    [SerializeField] private string saveKey = "PLAYER_CHEST_SLOT_INVENTORY_V3";

    [Header("Profile")]
    [SerializeField] private string activeProfileId = "local_player_1";

    [Header("Slots")]
    [SerializeField] private int slotCount = 4;

    [Header("Dependencies")]
    [SerializeField] private ChestTimeProviderBase timeProvider;
    [SerializeField] private PlayerProfileManager profileManager;

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

    [SerializeField] private PlayerChestSlotInventorySaveData runtimeData = new PlayerChestSlotInventorySaveData();

    public event Action OnChestInventoryChanged;

    public int SlotCount => Mathf.Max(1, slotCount);
    public string ActiveProfileId => activeProfileId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyDuplicateRuntimeRoot();
            return;
        }

        Instance = this;
        MarkRuntimeRootPersistentIfNeeded();

        ResolveDependencies();
        activeProfileId = ResolveCurrentProfileId();

        LoadInventory();
        EnsureRuntimeStructure();
        MigrateLoadedDataIfNeeded();
        SaveInventory();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] Initialized. " +
                "ProfileId=" + activeProfileId +
                " | SlotCount=" + SlotCount +
                " | OccupiedSlots=" + GetOccupiedSlotCount() +
                " | Queued=" + GetQueuedChestCount() +
                " | TimeProvider=" + GetTimeProviderDebugName(),
                this
            );
        }
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (profileManager != null)
        {
            profileManager.OnActiveProfileChanged -= HandleActiveProfileChanged;
            profileManager.OnActiveProfileChanged += HandleActiveProfileChanged;
        }
    }

    private void OnDisable()
    {
        if (profileManager != null)
            profileManager.OnActiveProfileChanged -= HandleActiveProfileChanged;
    }

    private void Start()
    {
        ResolveDependencies();
        SetActiveProfileId(ResolveCurrentProfileId(), true);
    }

    public bool AwardChest(ChestType chestType)
    {
        long now = GetNowUnixSeconds();
        ChestRuntimeData newChest = new ChestRuntimeData(chestType, now, activeProfileId);

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
                    " | ChestId=" + newChest.chestInstanceId +
                    " | UnlockEnd=" + newChest.unlockEndUnixTimeSeconds,
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

        return runtimeData.slots[slotIndex] != null &&
               runtimeData.slots[slotIndex].hasChest &&
               runtimeData.slots[slotIndex].chest != null;
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
        return Math.Max(0L, remaining);
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

        runtimeData.profileId = activeProfileId;
        runtimeData.lastLocalSaveUnixTimeSeconds = GetNowUnixSeconds();

        string json = JsonUtility.ToJson(runtimeData);
        PlayerPrefs.SetString(GetResolvedSaveKey(), json);
        PlayerPrefs.Save();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] SaveInventory completed. " +
                "ProfileId=" + runtimeData.profileId +
                " | SaveKey=" + GetResolvedSaveKey(),
                this
            );
        }
    }

    public void LoadInventory()
    {
        string resolvedSaveKey = GetResolvedSaveKey();

        if (!PlayerPrefs.HasKey(resolvedSaveKey))
        {
            TryMigrateLegacyLocalProfileSave();
        }

        if (!PlayerPrefs.HasKey(resolvedSaveKey))
        {
            runtimeData = new PlayerChestSlotInventorySaveData();
            return;
        }

        string json = PlayerPrefs.GetString(resolvedSaveKey, string.Empty);

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

        PlayerPrefs.DeleteKey(GetResolvedSaveKey());
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

    public void SetActiveProfileId(string newProfileId, bool reloadInventory = true)
    {
        string sanitized = SanitizeProfileId(newProfileId);

        if (activeProfileId == sanitized)
            return;

        activeProfileId = sanitized;

        if (reloadInventory)
        {
            LoadInventory();
            EnsureRuntimeStructure();
            MigrateLoadedDataIfNeeded();
            SaveInventory();
            NotifyInventoryChanged();
        }

        if (logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] Active profile changed. ProfileId=" + activeProfileId +
                " | Reload=" + reloadInventory,
                this
            );
        }
    }

    public void ApplyAuthoritativeServerSnapshot(PlayerChestSlotInventorySaveData snapshot, string profileId)
    {
        if (snapshot == null)
        {
            Debug.LogWarning("[PlayerChestSlotInventory] ApplyAuthoritativeServerSnapshot called with null snapshot.", this);
            return;
        }

        activeProfileId = SanitizeProfileId(profileId);
        runtimeData = snapshot;

        EnsureRuntimeStructure();
        MigrateLoadedDataIfNeeded();
        SaveInventory();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] Applied authoritative snapshot. " +
                "ProfileId=" + activeProfileId +
                " | Occupied=" + GetOccupiedSlotCount() +
                " | Queued=" + GetQueuedChestCount(),
                this
            );
        }
    }

    public long GetCurrentReferenceUnixTime()
    {
        return GetNowUnixSeconds();
    }

    public bool IsUsingAuthoritativeServerTime()
    {
        ResolveDependencies();
        return timeProvider != null && timeProvider.IsUsingAuthoritativeServerTime();
    }

    private void HandleActiveProfileChanged(PlayerProfileRuntimeData profileData)
    {
        if (profileData == null)
            return;

        SetActiveProfileId(profileData.profileId, true);
    }

    private void AssignChestToSlot(int slotIndex, ChestRuntimeData chest)
    {
        if (!IsValidSlotIndex(slotIndex) || chest == null)
            return;

        EnsureRuntimeStructure();

        if (runtimeData.slots[slotIndex] == null)
            runtimeData.slots[slotIndex] = new ChestSlotSaveData();

        long now = GetNowUnixSeconds();
        int unlockDurationSeconds = GetUnlockDurationSeconds(chest.chestType);

        chest.ownerProfileId = activeProfileId;
        chest.slotAssignedUnixTimeSeconds = now;
        chest.unlockEndUnixTimeSeconds = now + unlockDurationSeconds;
        chest.lastServerSyncUnixTimeSeconds = timeProvider != null && timeProvider.IsUsingAuthoritativeServerTime() ? now : 0;

        runtimeData.slots[slotIndex].hasChest = true;
        runtimeData.slots[slotIndex].chest = chest;

        if (logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] AssignChestToSlot -> Slot=" + slotIndex +
                " | Type=" + chest.chestType +
                " | Duration=" + unlockDurationSeconds +
                " | Start=" + chest.slotAssignedUnixTimeSeconds +
                " | End=" + chest.unlockEndUnixTimeSeconds,
                this
            );
        }
    }

    private void TryFillFreeSlotsFromQueue()
    {
        EnsureRuntimeStructure();

        if (runtimeData.queuedChests == null || runtimeData.queuedChests.Count == 0)
            return;

        bool changed = false;

        while (runtimeData.queuedChests.Count > 0)
        {
            int freeSlotIndex = GetFirstFreeSlotIndex();
            if (freeSlotIndex < 0)
                break;

            ChestRuntimeData queuedChest = runtimeData.queuedChests[0];
            runtimeData.queuedChests.RemoveAt(0);

            AssignChestToSlot(freeSlotIndex, queuedChest);
            changed = true;
        }

        if (changed && logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] Filled free slots from queue. " +
                "Occupied=" + GetOccupiedSlotCount() +
                " | RemainingQueue=" + GetQueuedChestCount(),
                this
            );
        }
    }

    private void EnsureRuntimeStructure()
    {
        if (runtimeData == null)
            runtimeData = new PlayerChestSlotInventorySaveData();

        if (runtimeData.queuedChests == null)
            runtimeData.queuedChests = new List<ChestRuntimeData>();

        if (runtimeData.slots == null || runtimeData.slots.Length != SlotCount)
        {
            ChestSlotSaveData[] previous = runtimeData.slots;
            ChestSlotSaveData[] rebuilt = new ChestSlotSaveData[SlotCount];

            for (int i = 0; i < rebuilt.Length; i++)
            {
                if (previous != null && i < previous.Length && previous[i] != null)
                    rebuilt[i] = previous[i];
                else
                    rebuilt[i] = new ChestSlotSaveData();
            }

            runtimeData.slots = rebuilt;
        }

        for (int i = 0; i < runtimeData.slots.Length; i++)
        {
            if (runtimeData.slots[i] == null)
                runtimeData.slots[i] = new ChestSlotSaveData();
        }
    }

    private void MigrateLoadedDataIfNeeded()
    {
        EnsureRuntimeStructure();

        if (runtimeData.saveVersion <= 0)
            runtimeData.saveVersion = 2;

        if (string.IsNullOrWhiteSpace(runtimeData.profileId))
            runtimeData.profileId = activeProfileId;

        for (int i = 0; i < runtimeData.slots.Length; i++)
        {
            ChestSlotSaveData slot = runtimeData.slots[i];
            if (slot == null || !slot.hasChest || slot.chest == null)
                continue;

            if (string.IsNullOrWhiteSpace(slot.chest.chestInstanceId))
                slot.chest.chestInstanceId = Guid.NewGuid().ToString("N");

            if (string.IsNullOrWhiteSpace(slot.chest.ownerProfileId))
                slot.chest.ownerProfileId = runtimeData.profileId;
        }

        if (runtimeData.queuedChests != null)
        {
            for (int i = 0; i < runtimeData.queuedChests.Count; i++)
            {
                ChestRuntimeData chest = runtimeData.queuedChests[i];
                if (chest == null)
                    continue;

                if (string.IsNullOrWhiteSpace(chest.chestInstanceId))
                    chest.chestInstanceId = Guid.NewGuid().ToString("N");

                if (string.IsNullOrWhiteSpace(chest.ownerProfileId))
                    chest.ownerProfileId = runtimeData.profileId;
            }
        }
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SlotCount;
    }

    private void NotifyInventoryChanged()
    {
        OnChestInventoryChanged?.Invoke();
    }

    private void ResolveDependencies()
    {
        if (timeProvider == null)
            timeProvider = GetComponent<ChestTimeProviderBase>();

        if (timeProvider == null)
            timeProvider = GetComponentInChildren<ChestTimeProviderBase>();

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (timeProvider == null)
            timeProvider = FindFirstObjectByType<ChestTimeProviderBase>();
#else
        if (timeProvider == null)
            timeProvider = FindObjectOfType<ChestTimeProviderBase>();
#endif
    }

    private long GetNowUnixSeconds()
    {
        ResolveDependencies();

        if (timeProvider != null)
            return timeProvider.GetUnixTimeSeconds();

        long fallback = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (logDebug)
            Debug.LogWarning("[PlayerChestSlotInventory] TimeProvider missing. Falling back to device UTC time: " + fallback, this);

        return fallback;
    }

    private string GetResolvedSaveKey()
    {
        return saveKey + "_" + SanitizeProfileId(activeProfileId);
    }

    private string GetLegacySaveKey()
    {
        return saveKey + "_local_player_1";
    }

    private void TryMigrateLegacyLocalProfileSave()
    {
        string targetKey = GetResolvedSaveKey();
        string legacyKey = GetLegacySaveKey();

        if (string.Equals(targetKey, legacyKey, StringComparison.Ordinal))
            return;

        if (PlayerPrefs.HasKey(targetKey))
            return;

        if (!PlayerPrefs.HasKey(legacyKey))
            return;

        string legacyJson = PlayerPrefs.GetString(legacyKey, string.Empty);
        if (string.IsNullOrWhiteSpace(legacyJson))
            return;

        PlayerPrefs.SetString(targetKey, legacyJson);
        PlayerPrefs.Save();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerChestSlotInventory] Migrated legacy save -> " +
                "From=" + legacyKey +
                " | To=" + targetKey,
                this
            );
        }
    }

    private string ResolveCurrentProfileId()
    {
        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
            return profileManager.ActiveProfileId.Trim();

        if (OnlineLocalPlayerContext.IsAvailable && !string.IsNullOrWhiteSpace(OnlineLocalPlayerContext.PlayerId))
            return OnlineLocalPlayerContext.PlayerId.Trim();

        if (!string.IsNullOrWhiteSpace(activeProfileId))
            return activeProfileId.Trim();

        return "guest_fallback";
    }

    private string SanitizeProfileId(string profileId)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
            return profileId.Trim();

        return ResolveCurrentProfileId();
    }

    private string GetTimeProviderDebugName()
    {
        if (timeProvider == null)
            return "Fallback_DeviceUtcNow";

        return timeProvider.GetProviderDebugName();
    }

    private string FormatDuration(long totalSeconds)
    {
        if (totalSeconds <= 0)
            return "00:00";

        TimeSpan span = TimeSpan.FromSeconds(totalSeconds);

        if (span.TotalHours >= 1d)
            return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)span.TotalHours, span.Minutes, span.Seconds);

        return string.Format("{0:D2}:{1:D2}", span.Minutes, span.Seconds);
    }

    private void MarkRuntimeRootPersistentIfNeeded()
    {
        if (!dontDestroyOnLoad)
            return;

        GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
        if (runtimeRoot.transform.parent != null)
            runtimeRoot.transform.SetParent(null);

        DontDestroyOnLoad(runtimeRoot);
    }

    private void DestroyDuplicateRuntimeRoot()
    {
        GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
        Destroy(duplicateRoot);
    }
}