using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSkinInventory : MonoBehaviour
{
    [Serializable]
    public class StartupUnlockedSkinDefinition
    {
        [Header("IDs")]
        public string baseColorId;
        public string patternId;
        public string patternColorId;

        [Header("Material Params")]
        public float patternIntensity = 1f;
        public float patternScale = 1f;

        [Header("Meta")]
        public SkinRarity rarity = SkinRarity.Common;
        public bool equipOnInitialization = false;
    }

    public static PlayerSkinInventory Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private BallSkinDatabase database;
    [SerializeField] private PlayerSkinLoadout playerSkinLoadout;
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private string saveKeyPrefix = "PLAYER_SKIN_INVENTORY";

    [Header("Profile")]
    [SerializeField] private string activeProfileId = "local_player_1";

    [Header("Startup Unlocked Skins")]
    [SerializeField] private bool createStartupUnlockedSkinsIfSaveEmpty = true;
    [SerializeField] private List<StartupUnlockedSkinDefinition> startupUnlockedSkins = new List<StartupUnlockedSkinDefinition>();

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    [SerializeField] private PlayerSkinInventorySaveData runtimeData = new PlayerSkinInventorySaveData();

    public event Action OnInventoryChanged;

    public BallSkinDatabase Database => database;
    public IReadOnlyList<BallSkinData> UnlockedSkins => runtimeData.unlockedSkins;
    public string ActiveProfileId => activeProfileId;
    public string EquippedSkinId => runtimeData != null ? runtimeData.equippedSkinId : string.Empty;

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

        ResolveDependencies();

        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
            activeProfileId = profileManager.ActiveProfileId;

        LoadInventory();
        EnsureRuntimeStructure();
        MigrateLoadedDataIfNeeded();
        RemoveDuplicateSkinsInPlace();

        if (createStartupUnlockedSkinsIfSaveEmpty && IsInventoryEmpty())
            CreateStartupUnlockedSkins();

        EnsureEquippedSkinAppliedToLoadout();
        SaveInventory();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinInventory] Initialized. " +
                "ProfileId=" + activeProfileId +
                " | UnlockedSkins=" + runtimeData.unlockedSkins.Count +
                " | EquippedSkinId=" + runtimeData.equippedSkinId,
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

        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
            SetActiveProfileId(profileManager.ActiveProfileId, true);
    }

    public bool AddUnlockedSkin(BallSkinData skin)
    {
        if (skin == null)
        {
            Debug.LogWarning("[PlayerSkinInventory] Tried to add null skin.", this);
            return false;
        }

        EnsureRuntimeStructure();

        if (string.IsNullOrWhiteSpace(skin.skinUniqueId))
            skin.skinUniqueId = BuildFallbackSkinId(skin);

        if (ContainsSkin(skin.skinUniqueId))
        {
            if (logDebug)
                Debug.Log("[PlayerSkinInventory] Skin already unlocked: " + skin.skinUniqueId, this);

            return false;
        }

        runtimeData.unlockedSkins.Add(CloneSkin(skin));
        RemoveDuplicateSkinsInPlace();

        if (string.IsNullOrWhiteSpace(runtimeData.equippedSkinId))
            runtimeData.equippedSkinId = skin.skinUniqueId;

        SaveInventory();
        EnsureEquippedSkinAppliedToLoadout();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinInventory] Added unlocked skin: " +
                skin.skinUniqueId +
                " | ProfileId=" + activeProfileId,
                this
            );
        }

        return true;
    }

    public bool ContainsSkin(string skinUniqueId)
    {
        if (string.IsNullOrWhiteSpace(skinUniqueId))
            return false;

        EnsureRuntimeStructure();

        for (int i = 0; i < runtimeData.unlockedSkins.Count; i++)
        {
            BallSkinData skin = runtimeData.unlockedSkins[i];
            if (skin != null && skin.skinUniqueId == skinUniqueId)
                return true;
        }

        return false;
    }

    public BallSkinData GetUnlockedSkinById(string skinUniqueId)
    {
        if (string.IsNullOrWhiteSpace(skinUniqueId))
            return null;

        EnsureRuntimeStructure();

        for (int i = 0; i < runtimeData.unlockedSkins.Count; i++)
        {
            BallSkinData skin = runtimeData.unlockedSkins[i];
            if (skin != null && skin.skinUniqueId == skinUniqueId)
                return skin;
        }

        return null;
    }

    public bool EquipSkin(string skinUniqueId)
    {
        BallSkinData skin = GetUnlockedSkinById(skinUniqueId);
        if (skin == null)
        {
            Debug.LogWarning("[PlayerSkinInventory] Cannot equip missing skin: " + skinUniqueId, this);
            return false;
        }

        runtimeData.equippedSkinId = skinUniqueId;

        ResolveDependencies();

        if (playerSkinLoadout != null)
            playerSkinLoadout.EquipSkinForPlayer1(skin);

        SaveInventory();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinInventory] Equipped skin: " +
                skinUniqueId +
                " | ProfileId=" + activeProfileId,
                this
            );
        }

        return true;
    }

    public BallSkinData GetEquippedSkin()
    {
        if (string.IsNullOrWhiteSpace(runtimeData.equippedSkinId))
            return null;

        return GetUnlockedSkinById(runtimeData.equippedSkinId);
    }

    public void SaveInventory()
    {
        EnsureRuntimeStructure();
        RemoveDuplicateSkinsInPlace();

        runtimeData.profileId = activeProfileId;
        runtimeData.lastLocalSaveUnixTimeSeconds = GetNowUnixSeconds();

        string json = JsonUtility.ToJson(runtimeData);
        PlayerPrefs.SetString(GetResolvedSaveKey(), json);
        PlayerPrefs.Save();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinInventory] SaveInventory completed. " +
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
            runtimeData = new PlayerSkinInventorySaveData();
            return;
        }

        string json = PlayerPrefs.GetString(resolvedSaveKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            runtimeData = new PlayerSkinInventorySaveData();
            return;
        }

        runtimeData = JsonUtility.FromJson<PlayerSkinInventorySaveData>(json);

        if (runtimeData == null)
            runtimeData = new PlayerSkinInventorySaveData();
    }

    public void ClearInventoryForDebug()
    {
        runtimeData = new PlayerSkinInventorySaveData();
        EnsureRuntimeStructure();

        PlayerPrefs.DeleteKey(GetResolvedSaveKey());
        PlayerPrefs.Save();

        SaveInventory();
        EnsureEquippedSkinAppliedToLoadout();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinInventory] Inventory cleared for debug. ProfileId=" + activeProfileId,
                this
            );
        }
    }

    public void RebuildStartupInventoryForDebug()
    {
        ClearInventoryForDebug();
        LoadInventory();
        EnsureRuntimeStructure();
        MigrateLoadedDataIfNeeded();
        RemoveDuplicateSkinsInPlace();

        if (createStartupUnlockedSkinsIfSaveEmpty && IsInventoryEmpty())
            CreateStartupUnlockedSkins();

        EnsureEquippedSkinAppliedToLoadout();
        SaveInventory();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinInventory] Rebuilt startup inventory for debug. " +
                "UnlockedSkins=" + runtimeData.unlockedSkins.Count +
                " | ProfileId=" + activeProfileId,
                this
            );
        }
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
            RemoveDuplicateSkinsInPlace();

            if (createStartupUnlockedSkinsIfSaveEmpty && IsInventoryEmpty())
                CreateStartupUnlockedSkins();

            EnsureEquippedSkinAppliedToLoadout();
            SaveInventory();
            NotifyInventoryChanged();
        }

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinInventory] Active profile changed. " +
                "ProfileId=" + activeProfileId +
                " | Reload=" + reloadInventory,
                this
            );
        }
    }

    public void ApplyAuthoritativeSnapshot(PlayerSkinInventorySaveData snapshot, string profileId)
    {
        if (snapshot == null)
        {
            Debug.LogWarning("[PlayerSkinInventory] ApplyAuthoritativeSnapshot called with null snapshot.", this);
            return;
        }

        activeProfileId = SanitizeProfileId(profileId);
        runtimeData = snapshot;

        EnsureRuntimeStructure();
        MigrateLoadedDataIfNeeded();
        RemoveDuplicateSkinsInPlace();
        SaveInventory();
        EnsureEquippedSkinAppliedToLoadout();
        NotifyInventoryChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerSkinInventory] Applied authoritative snapshot. " +
                "ProfileId=" + activeProfileId +
                " | UnlockedSkins=" + runtimeData.unlockedSkins.Count +
                " | Equipped=" + runtimeData.equippedSkinId,
                this
            );
        }
    }

    private void HandleActiveProfileChanged(PlayerProfileRuntimeData profileData)
    {
        if (profileData == null)
            return;

        SetActiveProfileId(profileData.profileId, true);
    }

    private void ResolveDependencies()
    {
        if (playerSkinLoadout == null)
            playerSkinLoadout = PlayerSkinLoadout.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (database == null)
            database = FindFirstObjectByType<BallSkinDatabase>();
#else
        if (database == null)
            database = FindObjectOfType<BallSkinDatabase>();
#endif
    }

    private void EnsureRuntimeStructure()
    {
        if (runtimeData == null)
            runtimeData = new PlayerSkinInventorySaveData();

        if (runtimeData.unlockedSkins == null)
            runtimeData.unlockedSkins = new List<BallSkinData>();
    }

    private void MigrateLoadedDataIfNeeded()
    {
        EnsureRuntimeStructure();

        if (runtimeData.saveVersion <= 0)
            runtimeData.saveVersion = 2;

        if (string.IsNullOrWhiteSpace(runtimeData.profileId))
            runtimeData.profileId = activeProfileId;

        for (int i = 0; i < runtimeData.unlockedSkins.Count; i++)
        {
            BallSkinData skin = runtimeData.unlockedSkins[i];
            if (skin == null)
                continue;

            if (string.IsNullOrWhiteSpace(skin.skinUniqueId))
                skin.skinUniqueId = BuildFallbackSkinId(skin);
        }

        if (!string.IsNullOrWhiteSpace(runtimeData.equippedSkinId) && !ContainsSkin(runtimeData.equippedSkinId))
        {
            runtimeData.equippedSkinId =
                runtimeData.unlockedSkins.Count > 0 && runtimeData.unlockedSkins[0] != null
                    ? runtimeData.unlockedSkins[0].skinUniqueId
                    : string.Empty;
        }
    }

    private void RemoveDuplicateSkinsInPlace()
    {
        EnsureRuntimeStructure();

        HashSet<string> seenIds = new HashSet<string>();
        List<BallSkinData> deduped = new List<BallSkinData>();

        for (int i = 0; i < runtimeData.unlockedSkins.Count; i++)
        {
            BallSkinData skin = runtimeData.unlockedSkins[i];
            if (skin == null)
                continue;

            if (string.IsNullOrWhiteSpace(skin.skinUniqueId))
                skin.skinUniqueId = BuildFallbackSkinId(skin);

            if (seenIds.Contains(skin.skinUniqueId))
            {
                if (logDebug)
                    Debug.Log("[PlayerSkinInventory] Removed duplicate skin from inventory: " + skin.skinUniqueId, this);

                continue;
            }

            seenIds.Add(skin.skinUniqueId);
            deduped.Add(skin);
        }

        runtimeData.unlockedSkins = deduped;

        if (!string.IsNullOrWhiteSpace(runtimeData.equippedSkinId) && !seenIds.Contains(runtimeData.equippedSkinId))
            runtimeData.equippedSkinId = deduped.Count > 0 ? deduped[0].skinUniqueId : string.Empty;
    }

    private bool IsInventoryEmpty()
    {
        return runtimeData == null || runtimeData.unlockedSkins == null || runtimeData.unlockedSkins.Count == 0;
    }

    private void CreateStartupUnlockedSkins()
    {
        ResolveDependencies();

        if (database == null)
        {
            Debug.LogError("[PlayerSkinInventory] Database missing, cannot create startup unlocked skins.", this);
            return;
        }

        if (startupUnlockedSkins == null || startupUnlockedSkins.Count == 0)
        {
            Debug.LogWarning("[PlayerSkinInventory] No startup unlocked skins configured.", this);
            return;
        }

        string skinToEquip = null;

        for (int i = 0; i < startupUnlockedSkins.Count; i++)
        {
            StartupUnlockedSkinDefinition definition = startupUnlockedSkins[i];
            if (definition == null)
                continue;

            if (!TryBuildSkinFromDefinition(definition, out BallSkinData builtSkin))
                continue;

            bool added = AddUnlockedSkin(builtSkin);

            if (logDebug)
            {
                Debug.Log(
                    "[PlayerSkinInventory] Startup skin built -> " +
                    builtSkin.skinUniqueId +
                    " | Base: " + builtSkin.baseColorId +
                    " | Pattern: " + builtSkin.patternId +
                    " | PatternColor: " + builtSkin.patternColorId +
                    " | Rarity: " + builtSkin.rarity +
                    " | Added: " + added,
                    this
                );
            }

            if (definition.equipOnInitialization)
                skinToEquip = builtSkin.skinUniqueId;
        }

        if (!string.IsNullOrWhiteSpace(skinToEquip))
            EquipSkin(skinToEquip);
        else
            EnsureEquippedSkinAppliedToLoadout();

        SaveInventory();

        if (logDebug)
            Debug.Log("[PlayerSkinInventory] Final startup unlocked skins count: " + runtimeData.unlockedSkins.Count, this);
    }

    private bool TryBuildSkinFromDefinition(StartupUnlockedSkinDefinition definition, out BallSkinData builtSkin)
    {
        builtSkin = null;

        if (definition == null)
            return false;

        if (string.IsNullOrWhiteSpace(definition.baseColorId))
        {
            Debug.LogError("[PlayerSkinInventory] Startup skin missing baseColorId.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.patternId))
        {
            Debug.LogError("[PlayerSkinInventory] Startup skin missing patternId.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.patternColorId))
        {
            Debug.LogError("[PlayerSkinInventory] Startup skin missing patternColorId.", this);
            return false;
        }

        if (database == null ||
            database.baseColorLibrary == null ||
            database.patternLibrary == null ||
            database.patternColorLibrary == null)
        {
            Debug.LogError("[PlayerSkinInventory] Database or libraries missing.", this);
            return false;
        }

        if (database.baseColorLibrary.GetById(definition.baseColorId) == null)
        {
            Debug.LogError("[PlayerSkinInventory] BaseColor ID not found in library: " + definition.baseColorId, this);
            return false;
        }

        if (database.patternLibrary.GetById(definition.patternId) == null)
        {
            Debug.LogError("[PlayerSkinInventory] Pattern ID not found in library: " + definition.patternId, this);
            return false;
        }

        if (database.patternColorLibrary.GetById(definition.patternColorId) == null)
        {
            Debug.LogError("[PlayerSkinInventory] PatternColor ID not found in library: " + definition.patternColorId, this);
            return false;
        }

        builtSkin = BallSkinFactory.CreateSkin(
            definition.baseColorId,
            definition.patternId,
            definition.patternColorId,
            definition.patternIntensity,
            definition.patternScale,
            definition.rarity
        );

        return builtSkin != null;
    }

    private void EnsureEquippedSkinAppliedToLoadout()
    {
        ResolveDependencies();

        BallSkinData equippedSkin = GetEquippedSkin();

        if (equippedSkin == null && runtimeData.unlockedSkins.Count > 0)
        {
            BallSkinData firstValid = runtimeData.unlockedSkins[0];
            if (firstValid != null)
            {
                runtimeData.equippedSkinId = firstValid.skinUniqueId;
                equippedSkin = firstValid;
            }
        }

        if (equippedSkin != null && playerSkinLoadout != null)
            playerSkinLoadout.EquipSkinForPlayer1(equippedSkin);
    }

    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private string GetResolvedSaveKey()
    {
        return saveKeyPrefix + "_" + SanitizeProfileId(activeProfileId);
    }

    private string SanitizeProfileId(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return "local_player_1";

        return profileId.Trim();
    }

    private long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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

    private string BuildFallbackSkinId(BallSkinData skin)
    {
        if (skin == null)
            return Guid.NewGuid().ToString("N");

        return BallSkinFactory.BuildSkinId(
            skin.baseColorId,
            skin.patternId,
            skin.patternColorId,
            skin.patternIntensity,
            skin.patternScale,
            skin.rarity
        );
    }
}