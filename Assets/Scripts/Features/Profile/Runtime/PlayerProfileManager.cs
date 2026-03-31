using System;
using UnityEngine;

public class PlayerProfileManager : MonoBehaviour
{
    public static PlayerProfileManager Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private string saveKeyPrefix = "PLAYER_PROFILE_CACHE";

    [Header("Default Profile")]
    [SerializeField] private string defaultProfileId = "local_player_1";
    [SerializeField] private string defaultDisplayName = "Player 1";

    [Header("Runtime")]
    [SerializeField] private PlayerProfileRuntimeData activeProfile = new PlayerProfileRuntimeData();

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public event Action<PlayerProfileRuntimeData> OnActiveProfileChanged;
    public event Action<PlayerProfileRuntimeData> OnActiveProfileDataChanged;

    public PlayerProfileRuntimeData ActiveProfile => activeProfile;
    public string ActiveProfileId => activeProfile != null ? activeProfile.profileId : string.Empty;
    public string ActiveDisplayName => activeProfile != null ? activeProfile.displayName : string.Empty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyDuplicateRuntimeRoot();
            return;
        }

        Instance = this;
        MarkRuntimeRootPersistentIfNeeded();

        LoadOrCreateProfile(defaultProfileId, defaultDisplayName);
        ApplyActiveProfileToSystems();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileManager] Initialized. " +
                "ProfileId=" + ActiveProfileId +
                " | DisplayName=" + ActiveDisplayName +
                " | XP=" + activeProfile.xp +
                " | Level=" + activeProfile.level +
                " | Matches=" + activeProfile.totalMatchesPlayed +
                " | Wins=" + activeProfile.totalWins,
                this
            );
        }
    }

    private void Start()
    {
        ApplyActiveProfileToSystems();
    }

    public void SetActiveProfile(string profileId, bool createIfMissing = true, string fallbackDisplayName = null)
    {
        string sanitizedProfileId = SanitizeProfileId(profileId);

        string saveKey = GetResolvedSaveKey(sanitizedProfileId);
        bool hasSave = PlayerPrefs.HasKey(saveKey);

        if (!hasSave && !createIfMissing)
        {
            Debug.LogWarning(
                "[PlayerProfileManager] SetActiveProfile aborted, save not found and createIfMissing=false. " +
                "ProfileId=" + sanitizedProfileId,
                this
            );
            return;
        }

        if (hasSave)
        {
            LoadOrCreateProfile(sanitizedProfileId, fallbackDisplayName);
        }
        else
        {
            CreateDefaultProfile(
                sanitizedProfileId,
                string.IsNullOrWhiteSpace(fallbackDisplayName) ? defaultDisplayName : fallbackDisplayName
            );
            SaveActiveProfile();
        }

        ApplyActiveProfileToSystems();
        NotifyActiveProfileChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileManager] Active profile changed. " +
                "ProfileId=" + ActiveProfileId +
                " | DisplayName=" + ActiveDisplayName,
                this
            );
        }
    }

    public void UpdateDisplayName(string newDisplayName)
    {
        EnsureRuntimeStructure();

        string sanitized = string.IsNullOrWhiteSpace(newDisplayName) ? defaultDisplayName : newDisplayName.Trim();

        if (activeProfile.displayName == sanitized)
            return;

        activeProfile.displayName = sanitized;
        SaveActiveProfile();
        NotifyActiveProfileDataChanged();

        if (logDebug)
            Debug.Log("[PlayerProfileManager] DisplayName updated -> " + activeProfile.displayName, this);
    }

    public void AddXp(int amount)
    {
        EnsureRuntimeStructure();

        if (amount <= 0)
            return;

        activeProfile.xp += amount;
        SaveActiveProfile();
        NotifyActiveProfileDataChanged();

        if (logDebug)
            Debug.Log("[PlayerProfileManager] XP added -> +" + amount + " | Total=" + activeProfile.xp, this);
    }

    public void SetLevel(int level)
    {
        EnsureRuntimeStructure();

        int sanitized = Mathf.Max(1, level);

        if (activeProfile.level == sanitized)
            return;

        activeProfile.level = sanitized;
        SaveActiveProfile();
        NotifyActiveProfileDataChanged();

        if (logDebug)
            Debug.Log("[PlayerProfileManager] Level set -> " + activeProfile.level, this);
    }

    public void AddSoftCurrency(int amount)
    {
        EnsureRuntimeStructure();

        if (amount == 0)
            return;

        activeProfile.softCurrency = Mathf.Max(0, activeProfile.softCurrency + amount);
        SaveActiveProfile();
        NotifyActiveProfileDataChanged();

        if (logDebug)
            Debug.Log("[PlayerProfileManager] SoftCurrency delta -> " + amount + " | Total=" + activeProfile.softCurrency, this);
    }

    public void AddPremiumCurrency(int amount)
    {
        EnsureRuntimeStructure();

        if (amount == 0)
            return;

        activeProfile.premiumCurrency = Mathf.Max(0, activeProfile.premiumCurrency + amount);
        SaveActiveProfile();
        NotifyActiveProfileDataChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileManager] PremiumCurrency delta -> " +
                amount +
                " | Total=" + activeProfile.premiumCurrency,
                this
            );
        }
    }

    public void ApplyProgressionState(
        int? totalXp = null,
        int? totalLevel = null,
        int? totalMatchesPlayed = null,
        int? totalWins = null,
        int? versusMatchesPlayed = null,
        int? versusWins = null,
        int? versusTimeMatchesPlayed = null,
        int? versusScoreMatchesPlayed = null,
        int? botMatchesPlayed = null,
        int? botWins = null,
        int? multiplayerMatchesPlayed = null,
        int? multiplayerWins = null,
        int? rankedMatchesPlayed = null,
        int? rankedWins = null,
        string lastDailyLoginClaimDateUtc = null,
        int? consecutiveLoginDays = null)
    {
        EnsureRuntimeStructure();

        if (totalXp.HasValue)
            activeProfile.xp = Mathf.Max(0, totalXp.Value);

        if (totalLevel.HasValue)
            activeProfile.level = Mathf.Max(1, totalLevel.Value);

        if (totalMatchesPlayed.HasValue)
            activeProfile.totalMatchesPlayed = Mathf.Max(0, totalMatchesPlayed.Value);

        if (totalWins.HasValue)
            activeProfile.totalWins = Mathf.Max(0, totalWins.Value);

        if (versusMatchesPlayed.HasValue)
            activeProfile.versusMatchesPlayed = Mathf.Max(0, versusMatchesPlayed.Value);

        if (versusWins.HasValue)
            activeProfile.versusWins = Mathf.Max(0, versusWins.Value);

        if (versusTimeMatchesPlayed.HasValue)
            activeProfile.versusTimeMatchesPlayed = Mathf.Max(0, versusTimeMatchesPlayed.Value);

        if (versusScoreMatchesPlayed.HasValue)
            activeProfile.versusScoreMatchesPlayed = Mathf.Max(0, versusScoreMatchesPlayed.Value);

        if (botMatchesPlayed.HasValue)
            activeProfile.botMatchesPlayed = Mathf.Max(0, botMatchesPlayed.Value);

        if (botWins.HasValue)
            activeProfile.botWins = Mathf.Max(0, botWins.Value);

        if (multiplayerMatchesPlayed.HasValue)
            activeProfile.multiplayerMatchesPlayed = Mathf.Max(0, multiplayerMatchesPlayed.Value);

        if (multiplayerWins.HasValue)
            activeProfile.multiplayerWins = Mathf.Max(0, multiplayerWins.Value);

        if (rankedMatchesPlayed.HasValue)
            activeProfile.rankedMatchesPlayed = Mathf.Max(0, rankedMatchesPlayed.Value);

        if (rankedWins.HasValue)
            activeProfile.rankedWins = Mathf.Max(0, rankedWins.Value);

        if (lastDailyLoginClaimDateUtc != null)
            activeProfile.lastDailyLoginClaimDateUtc = lastDailyLoginClaimDateUtc;

        if (consecutiveLoginDays.HasValue)
            activeProfile.consecutiveLoginDays = Mathf.Max(0, consecutiveLoginDays.Value);

        SaveActiveProfile();
        NotifyActiveProfileDataChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileManager] ApplyProgressionState -> " +
                "XP=" + activeProfile.xp +
                " | Level=" + activeProfile.level +
                " | TotalMatches=" + activeProfile.totalMatchesPlayed +
                " | TotalWins=" + activeProfile.totalWins +
                " | Versus=" + activeProfile.versusMatchesPlayed +
                " | Bot=" + activeProfile.botMatchesPlayed +
                " | Multiplayer=" + activeProfile.multiplayerMatchesPlayed +
                " | Ranked=" + activeProfile.rankedMatchesPlayed,
                this
            );
        }
    }

    public void RegisterMatchResult(
        MatchRuntimeConfig.GameMode gameMode,
        MatchMode matchMode,
        bool localPlayerWon,
        bool isRanked)
    {
        EnsureRuntimeStructure();

        int totalMatchesPlayed = activeProfile.totalMatchesPlayed + 1;
        int totalWins = activeProfile.totalWins + (localPlayerWon ? 1 : 0);

        int versusMatchesPlayed = activeProfile.versusMatchesPlayed;
        int versusWins = activeProfile.versusWins;
        int versusTimeMatchesPlayed = activeProfile.versusTimeMatchesPlayed;
        int versusScoreMatchesPlayed = activeProfile.versusScoreMatchesPlayed;

        int botMatchesPlayed = activeProfile.botMatchesPlayed;
        int botWins = activeProfile.botWins;

        int multiplayerMatchesPlayed = activeProfile.multiplayerMatchesPlayed;
        int multiplayerWins = activeProfile.multiplayerWins;

        int rankedMatchesPlayed = activeProfile.rankedMatchesPlayed;
        int rankedWins = activeProfile.rankedWins;

        switch (gameMode)
        {
            case MatchRuntimeConfig.GameMode.Versus:
                versusMatchesPlayed++;
                if (localPlayerWon)
                    versusWins++;

                if (matchMode == MatchMode.TimeLimit)
                    versusTimeMatchesPlayed++;
                else
                    versusScoreMatchesPlayed++;
                break;

            case MatchRuntimeConfig.GameMode.Bot:
                botMatchesPlayed++;
                if (localPlayerWon)
                    botWins++;
                break;

            case MatchRuntimeConfig.GameMode.Multiplayer:
                multiplayerMatchesPlayed++;
                if (localPlayerWon)
                    multiplayerWins++;

                if (isRanked)
                {
                    rankedMatchesPlayed++;
                    if (localPlayerWon)
                        rankedWins++;
                }
                break;
        }

        ApplyProgressionState(
            totalMatchesPlayed: totalMatchesPlayed,
            totalWins: totalWins,
            versusMatchesPlayed: versusMatchesPlayed,
            versusWins: versusWins,
            versusTimeMatchesPlayed: versusTimeMatchesPlayed,
            versusScoreMatchesPlayed: versusScoreMatchesPlayed,
            botMatchesPlayed: botMatchesPlayed,
            botWins: botWins,
            multiplayerMatchesPlayed: multiplayerMatchesPlayed,
            multiplayerWins: multiplayerWins,
            rankedMatchesPlayed: rankedMatchesPlayed,
            rankedWins: rankedWins
        );

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileManager] RegisterMatchResult -> " +
                "GameMode=" + gameMode +
                " | MatchMode=" + matchMode +
                " | LocalWin=" + localPlayerWon +
                " | Ranked=" + isRanked,
                this
            );
        }
    }

    public void ApplyAuthoritativeSnapshot(PlayerProfileRuntimeData snapshot)
    {
        if (snapshot == null)
        {
            Debug.LogWarning("[PlayerProfileManager] ApplyAuthoritativeSnapshot called with null snapshot.", this);
            return;
        }

        activeProfile = CloneRuntimeData(snapshot);
        EnsureRuntimeStructure();
        SaveActiveProfile();
        ApplyActiveProfileToSystems();
        NotifyActiveProfileChanged();

        if (logDebug)
        {
            Debug.Log(
                "[PlayerProfileManager] Applied authoritative snapshot. " +
                "ProfileId=" + ActiveProfileId +
                " | DisplayName=" + ActiveDisplayName,
                this
            );
        }
    }

    public void SaveActiveProfile()
    {
        EnsureRuntimeStructure();

        activeProfile.lastLocalSaveUnixTimeSeconds = GetNowUnixSeconds();

        PlayerProfileSaveData saveData = new PlayerProfileSaveData
        {
            saveVersion = activeProfile.saveVersion,
            profileId = activeProfile.profileId,
            displayName = activeProfile.displayName,
            xp = activeProfile.xp,
            level = activeProfile.level,
            softCurrency = activeProfile.softCurrency,
            premiumCurrency = activeProfile.premiumCurrency,
            totalMatchesPlayed = activeProfile.totalMatchesPlayed,
            totalWins = activeProfile.totalWins,
            versusMatchesPlayed = activeProfile.versusMatchesPlayed,
            versusWins = activeProfile.versusWins,
            versusTimeMatchesPlayed = activeProfile.versusTimeMatchesPlayed,
            versusScoreMatchesPlayed = activeProfile.versusScoreMatchesPlayed,
            botMatchesPlayed = activeProfile.botMatchesPlayed,
            botWins = activeProfile.botWins,
            multiplayerMatchesPlayed = activeProfile.multiplayerMatchesPlayed,
            multiplayerWins = activeProfile.multiplayerWins,
            rankedMatchesPlayed = activeProfile.rankedMatchesPlayed,
            rankedWins = activeProfile.rankedWins,
            lastDailyLoginClaimDateUtc = activeProfile.lastDailyLoginClaimDateUtc,
            consecutiveLoginDays = activeProfile.consecutiveLoginDays,
            createdFromServer = activeProfile.createdFromServer,
            pendingServerSync = activeProfile.pendingServerSync,
            lastServerSyncUnixTimeSeconds = activeProfile.lastServerSyncUnixTimeSeconds,
            lastLocalSaveUnixTimeSeconds = activeProfile.lastLocalSaveUnixTimeSeconds
        };

        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(GetResolvedSaveKey(activeProfile.profileId), json);
        PlayerPrefs.Save();
    }

    public void ReloadActiveProfile()
    {
        string currentProfileId = ActiveProfileId;
        string currentDisplayName = ActiveDisplayName;

        LoadOrCreateProfile(currentProfileId, currentDisplayName);
        ApplyActiveProfileToSystems();
        NotifyActiveProfileChanged();
    }

    public void ClearActiveProfileForDebug()
    {
        EnsureRuntimeStructure();

        string saveKey = GetResolvedSaveKey(activeProfile.profileId);
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();

        CreateDefaultProfile(activeProfile.profileId, defaultDisplayName);
        SaveActiveProfile();
        ApplyActiveProfileToSystems();
        NotifyActiveProfileChanged();
    }

    private void LoadOrCreateProfile(string profileId, string fallbackDisplayName)
    {
        string sanitizedProfileId = SanitizeProfileId(profileId);
        string saveKey = GetResolvedSaveKey(sanitizedProfileId);

        if (!PlayerPrefs.HasKey(saveKey))
        {
            CreateDefaultProfile(
                sanitizedProfileId,
                string.IsNullOrWhiteSpace(fallbackDisplayName) ? defaultDisplayName : fallbackDisplayName
            );
            SaveActiveProfile();
            return;
        }

        string json = PlayerPrefs.GetString(saveKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            CreateDefaultProfile(
                sanitizedProfileId,
                string.IsNullOrWhiteSpace(fallbackDisplayName) ? defaultDisplayName : fallbackDisplayName
            );
            SaveActiveProfile();
            return;
        }

        PlayerProfileSaveData saveData = JsonUtility.FromJson<PlayerProfileSaveData>(json);

        if (saveData == null)
        {
            CreateDefaultProfile(
                sanitizedProfileId,
                string.IsNullOrWhiteSpace(fallbackDisplayName) ? defaultDisplayName : fallbackDisplayName
            );
            SaveActiveProfile();
            return;
        }

        activeProfile = new PlayerProfileRuntimeData
        {
            saveVersion = Mathf.Max(1, saveData.saveVersion),
            profileId = SanitizeProfileId(saveData.profileId),
            displayName = string.IsNullOrWhiteSpace(saveData.displayName)
                ? (string.IsNullOrWhiteSpace(fallbackDisplayName) ? defaultDisplayName : fallbackDisplayName)
                : saveData.displayName.Trim(),
            xp = Mathf.Max(0, saveData.xp),
            level = Mathf.Max(1, saveData.level),
            softCurrency = Mathf.Max(0, saveData.softCurrency),
            premiumCurrency = Mathf.Max(0, saveData.premiumCurrency),
            totalMatchesPlayed = Mathf.Max(0, saveData.totalMatchesPlayed),
            totalWins = Mathf.Max(0, saveData.totalWins),
            versusMatchesPlayed = Mathf.Max(0, saveData.versusMatchesPlayed),
            versusWins = Mathf.Max(0, saveData.versusWins),
            versusTimeMatchesPlayed = Mathf.Max(0, saveData.versusTimeMatchesPlayed),
            versusScoreMatchesPlayed = Mathf.Max(0, saveData.versusScoreMatchesPlayed),
            botMatchesPlayed = Mathf.Max(0, saveData.botMatchesPlayed),
            botWins = Mathf.Max(0, saveData.botWins),
            multiplayerMatchesPlayed = Mathf.Max(0, saveData.multiplayerMatchesPlayed),
            multiplayerWins = Mathf.Max(0, saveData.multiplayerWins),
            rankedMatchesPlayed = Mathf.Max(0, saveData.rankedMatchesPlayed),
            rankedWins = Mathf.Max(0, saveData.rankedWins),
            lastDailyLoginClaimDateUtc = string.IsNullOrWhiteSpace(saveData.lastDailyLoginClaimDateUtc)
                ? string.Empty
                : saveData.lastDailyLoginClaimDateUtc,
            consecutiveLoginDays = Mathf.Max(0, saveData.consecutiveLoginDays),
            createdFromServer = saveData.createdFromServer,
            pendingServerSync = saveData.pendingServerSync,
            lastServerSyncUnixTimeSeconds = Math.Max(0L, saveData.lastServerSyncUnixTimeSeconds),
            lastLocalSaveUnixTimeSeconds = Math.Max(0L, saveData.lastLocalSaveUnixTimeSeconds)
        };

        EnsureRuntimeStructure();
    }

    private void CreateDefaultProfile(string profileId, string displayName)
    {
        activeProfile = new PlayerProfileRuntimeData
        {
            saveVersion = 3,
            profileId = SanitizeProfileId(profileId),
            displayName = string.IsNullOrWhiteSpace(displayName) ? defaultDisplayName : displayName.Trim(),
            xp = 0,
            level = 1,
            softCurrency = 0,
            premiumCurrency = 0,
            totalMatchesPlayed = 0,
            totalWins = 0,
            versusMatchesPlayed = 0,
            versusWins = 0,
            versusTimeMatchesPlayed = 0,
            versusScoreMatchesPlayed = 0,
            botMatchesPlayed = 0,
            botWins = 0,
            multiplayerMatchesPlayed = 0,
            multiplayerWins = 0,
            rankedMatchesPlayed = 0,
            rankedWins = 0,
            lastDailyLoginClaimDateUtc = string.Empty,
            consecutiveLoginDays = 0,
            createdFromServer = false,
            pendingServerSync = false,
            lastServerSyncUnixTimeSeconds = 0,
            lastLocalSaveUnixTimeSeconds = GetNowUnixSeconds()
        };
    }

    private void ApplyActiveProfileToSystems()
    {
        EnsureRuntimeStructure();

        PlayerSkinInventory skinInventory = PlayerSkinInventory.Instance;
        if (skinInventory != null)
            skinInventory.SetActiveProfileId(activeProfile.profileId, true);

        PlayerChestSlotInventory chestInventory = PlayerChestSlotInventory.Instance;
        if (chestInventory != null)
            chestInventory.SetActiveProfileId(activeProfile.profileId, true);

        PlayerSkinLoadout skinLoadout = PlayerSkinLoadout.Instance;
        if (skinLoadout != null)
            skinLoadout.SetPlayer1ProfileId(activeProfile.profileId);
    }

    private void NotifyActiveProfileChanged()
    {
        OnActiveProfileChanged?.Invoke(CloneRuntimeData(activeProfile));
    }

    private void NotifyActiveProfileDataChanged()
    {
        OnActiveProfileDataChanged?.Invoke(CloneRuntimeData(activeProfile));
    }

    private void EnsureRuntimeStructure()
    {
        if (activeProfile == null)
            activeProfile = new PlayerProfileRuntimeData();

        if (string.IsNullOrWhiteSpace(activeProfile.profileId))
            activeProfile.profileId = SanitizeProfileId(defaultProfileId);

        if (string.IsNullOrWhiteSpace(activeProfile.displayName))
            activeProfile.displayName = defaultDisplayName;

        activeProfile.level = Mathf.Max(1, activeProfile.level);
        activeProfile.xp = Mathf.Max(0, activeProfile.xp);
        activeProfile.softCurrency = Mathf.Max(0, activeProfile.softCurrency);
        activeProfile.premiumCurrency = Mathf.Max(0, activeProfile.premiumCurrency);

        activeProfile.totalMatchesPlayed = Mathf.Max(0, activeProfile.totalMatchesPlayed);
        activeProfile.totalWins = Mathf.Max(0, activeProfile.totalWins);

        activeProfile.versusMatchesPlayed = Mathf.Max(0, activeProfile.versusMatchesPlayed);
        activeProfile.versusWins = Mathf.Max(0, activeProfile.versusWins);
        activeProfile.versusTimeMatchesPlayed = Mathf.Max(0, activeProfile.versusTimeMatchesPlayed);
        activeProfile.versusScoreMatchesPlayed = Mathf.Max(0, activeProfile.versusScoreMatchesPlayed);

        activeProfile.botMatchesPlayed = Mathf.Max(0, activeProfile.botMatchesPlayed);
        activeProfile.botWins = Mathf.Max(0, activeProfile.botWins);

        activeProfile.multiplayerMatchesPlayed = Mathf.Max(0, activeProfile.multiplayerMatchesPlayed);
        activeProfile.multiplayerWins = Mathf.Max(0, activeProfile.multiplayerWins);

        activeProfile.rankedMatchesPlayed = Mathf.Max(0, activeProfile.rankedMatchesPlayed);
        activeProfile.rankedWins = Mathf.Max(0, activeProfile.rankedWins);

        activeProfile.consecutiveLoginDays = Mathf.Max(0, activeProfile.consecutiveLoginDays);
        activeProfile.saveVersion = Mathf.Max(1, activeProfile.saveVersion);
        activeProfile.lastServerSyncUnixTimeSeconds = Math.Max(0L, activeProfile.lastServerSyncUnixTimeSeconds);
        activeProfile.lastLocalSaveUnixTimeSeconds = Math.Max(0L, activeProfile.lastLocalSaveUnixTimeSeconds);
    }

    private PlayerProfileRuntimeData CloneRuntimeData(PlayerProfileRuntimeData source)
    {
        if (source == null)
            return null;

        return new PlayerProfileRuntimeData
        {
            saveVersion = source.saveVersion,
            profileId = source.profileId,
            displayName = source.displayName,
            xp = source.xp,
            level = source.level,
            softCurrency = source.softCurrency,
            premiumCurrency = source.premiumCurrency,
            totalMatchesPlayed = source.totalMatchesPlayed,
            totalWins = source.totalWins,
            versusMatchesPlayed = source.versusMatchesPlayed,
            versusWins = source.versusWins,
            versusTimeMatchesPlayed = source.versusTimeMatchesPlayed,
            versusScoreMatchesPlayed = source.versusScoreMatchesPlayed,
            botMatchesPlayed = source.botMatchesPlayed,
            botWins = source.botWins,
            multiplayerMatchesPlayed = source.multiplayerMatchesPlayed,
            multiplayerWins = source.multiplayerWins,
            rankedMatchesPlayed = source.rankedMatchesPlayed,
            rankedWins = source.rankedWins,
            lastDailyLoginClaimDateUtc = source.lastDailyLoginClaimDateUtc,
            consecutiveLoginDays = source.consecutiveLoginDays,
            createdFromServer = source.createdFromServer,
            pendingServerSync = source.pendingServerSync,
            lastServerSyncUnixTimeSeconds = source.lastServerSyncUnixTimeSeconds,
            lastLocalSaveUnixTimeSeconds = source.lastLocalSaveUnixTimeSeconds
        };
    }

    private string GetResolvedSaveKey(string profileId)
    {
        return saveKeyPrefix + "_" + SanitizeProfileId(profileId);
    }

    private string SanitizeProfileId(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return defaultProfileId;

        return profileId.Trim();
    }

    private long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private void MarkRuntimeRootPersistentIfNeeded()
    {
        if (!dontDestroyOnLoad)
            return;

        GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
        DontDestroyOnLoad(runtimeRoot);
    }

    private void DestroyDuplicateRuntimeRoot()
    {
        GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
        Destroy(duplicateRoot);
    }
}