using System;
using UnityEngine;
using UncballArena.Core.Bootstrap;
using UncballArena.Core.Profile.Models;
using UncballArena.Core.Runtime;

public class PlayerProfileManager : MonoBehaviour
{
    public static PlayerProfileManager Instance { get; private set; }

    [Header("Legacy Fallback Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private string legacySaveKeyPrefix = "PLAYER_PROFILE_CACHE";

    [Header("Legacy Fallback Defaults")]
    [SerializeField] private string defaultProfileId = "local_player_1";
    [SerializeField] private string defaultDisplayName = "Player 1";

    [Header("Runtime")]
    [SerializeField] private PlayerProfileRuntimeData activeProfile = new PlayerProfileRuntimeData();

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool subscribedToCompositionRoot;

    public event Action<PlayerProfileRuntimeData> OnActiveProfileChanged;
    public event Action<PlayerProfileRuntimeData> OnActiveProfileDataChanged;

    public PlayerProfileRuntimeData ActiveProfile => activeProfile;
    public string ActiveProfileId => activeProfile != null ? activeProfile.profileId : string.Empty;
    public string ActiveDisplayName => activeProfile != null ? activeProfile.displayName : string.Empty;
    public int ActiveRankedLp => activeProfile != null ? Mathf.Max(0, activeProfile.rankedLp) : 1000;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyDuplicateRuntimeRoot();
            return;
        }

        Instance = this;
        MarkRuntimeRootPersistentIfNeeded();

        BootstrapLocalRuntimeMirror();
        SubscribeCompositionRootIfNeeded();
        TryPullFromCompositionRoot(forceNotify: false);
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
        SubscribeCompositionRootIfNeeded();
        TryPullFromCompositionRoot(forceNotify: true);
        ApplyActiveProfileToSystems();
    }

    private void OnEnable()
    {
        SubscribeCompositionRootIfNeeded();
        TryPullFromCompositionRoot(forceNotify: false);
    }

    private void OnDisable()
    {
        UnsubscribeCompositionRootIfNeeded();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnsubscribeCompositionRootIfNeeded();
    }

    public void SetActiveProfile(string profileId, bool createIfMissing = true, string fallbackDisplayName = null)
    {
        if (IsUsingCoreProfile())
        {
            TryPullFromCompositionRoot(forceNotify: true);
            return;
        }

        string sanitizedProfileId = SanitizeProfileId(profileId);
        string saveKey = GetLegacyResolvedSaveKey(sanitizedProfileId);
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
            LoadOrCreateLegacyProfile(sanitizedProfileId, fallbackDisplayName);
        }
        else
        {
            CreateDefaultRuntimeProfile(
                sanitizedProfileId,
                string.IsNullOrWhiteSpace(fallbackDisplayName) ? defaultDisplayName : fallbackDisplayName.Trim()
            );
            SaveLegacyActiveProfile();
        }

        ApplyActiveProfileToSystems();
        NotifyActiveProfileChanged();
    }

    public void UpdateDisplayName(string newDisplayName)
    {
        EnsureRuntimeStructure();

        string sanitized = string.IsNullOrWhiteSpace(newDisplayName)
            ? ResolvePreferredDisplayName()
            : newDisplayName.Trim();

        if (string.Equals(activeProfile.displayName, sanitized, StringComparison.Ordinal))
            return;

        activeProfile.displayName = sanitized;

        if (IsUsingCoreProfile())
            PushDisplayNameToCore(sanitized);
        else
            SaveLegacyActiveProfile();

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

        if (IsUsingCoreProfile())
            PushProfileStatsToCompositionRoot();
        else
            SaveLegacyActiveProfile();

        NotifyActiveProfileDataChanged();
    }

    public void SetLevel(int level)
    {
        EnsureRuntimeStructure();

        int sanitized = Mathf.Max(1, level);

        if (activeProfile.level == sanitized)
            return;

        activeProfile.level = sanitized;

        if (IsUsingCoreProfile())
            PushProfileStatsToCompositionRoot();
        else
            SaveLegacyActiveProfile();

        NotifyActiveProfileDataChanged();
    }

    public void AddSoftCurrency(int amount)
    {
        EnsureRuntimeStructure();

        if (amount == 0)
            return;

        activeProfile.softCurrency = Mathf.Max(0, activeProfile.softCurrency + amount);

        if (IsUsingCoreProfile())
            PushFullProfileToCompositionRoot();
        else
            SaveLegacyActiveProfile();

        NotifyActiveProfileDataChanged();
    }

    public void AddPremiumCurrency(int amount)
    {
        EnsureRuntimeStructure();

        if (amount == 0)
            return;

        activeProfile.premiumCurrency = Mathf.Max(0, activeProfile.premiumCurrency + amount);

        if (IsUsingCoreProfile())
            PushFullProfileToCompositionRoot();
        else
            SaveLegacyActiveProfile();

        NotifyActiveProfileDataChanged();
    }

    public void AddRankedLp(int amount)
    {
        EnsureRuntimeStructure();

        if (amount == 0)
            return;

        activeProfile.rankedLp = Mathf.Max(0, activeProfile.rankedLp + amount);

        if (!IsUsingCoreProfile())
            SaveLegacyActiveProfile();

        NotifyActiveProfileDataChanged();
    }

    public void SetRankedLp(int value)
    {
        EnsureRuntimeStructure();

        int sanitized = Mathf.Max(0, value);

        if (activeProfile.rankedLp == sanitized)
            return;

        activeProfile.rankedLp = sanitized;

        if (!IsUsingCoreProfile())
            SaveLegacyActiveProfile();

        NotifyActiveProfileDataChanged();
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
        int? rankedLp = null,
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

        if (rankedLp.HasValue)
            activeProfile.rankedLp = Mathf.Max(0, rankedLp.Value);

        if (lastDailyLoginClaimDateUtc != null)
            activeProfile.lastDailyLoginClaimDateUtc = lastDailyLoginClaimDateUtc;

        if (consecutiveLoginDays.HasValue)
            activeProfile.consecutiveLoginDays = Mathf.Max(0, consecutiveLoginDays.Value);

        if (IsUsingCoreProfile())
            PushFullProfileToCompositionRoot();
        else
            SaveLegacyActiveProfile();

        NotifyActiveProfileDataChanged();
    }

    public void RegisterMatchResult(
        PlayerMatchCategory matchCategory,
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

        switch (matchCategory)
        {
            case PlayerMatchCategory.LocalVersus:
                versusMatchesPlayed++;
                if (localPlayerWon)
                    versusWins++;

                if (matchMode == MatchMode.TimeLimit)
                    versusTimeMatchesPlayed++;
                else
                    versusScoreMatchesPlayed++;
                break;

            case PlayerMatchCategory.Bot:
                botMatchesPlayed++;
                if (localPlayerWon)
                    botWins++;
                break;

            case PlayerMatchCategory.OnlineMultiplayer:
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

        if (IsUsingCoreProfile())
            PushFullProfileToCompositionRoot();
        else
            SaveLegacyActiveProfile();

        ApplyActiveProfileToSystems();
        NotifyActiveProfileChanged();
    }

    public void SaveActiveProfile()
    {
        if (IsUsingCoreProfile())
        {
            PushFullProfileToCompositionRoot();
            return;
        }

        SaveLegacyActiveProfile();
    }

    public void ReloadActiveProfile()
    {
        if (IsUsingCoreProfile())
        {
            TryPullFromCompositionRoot(forceNotify: true);
            return;
        }

        LoadOrCreateLegacyProfile(ActiveProfileId, ActiveDisplayName);
        ApplyActiveProfileToSystems();
        NotifyActiveProfileChanged();
    }

    public void ClearActiveProfileForDebug()
    {
        if (IsUsingCoreProfile())
        {
            CreateDefaultRuntimeProfile(ResolvePreferredProfileId(), ResolvePreferredDisplayName());
            PushFullProfileToCompositionRoot();
            ApplyActiveProfileToSystems();
            NotifyActiveProfileChanged();
            return;
        }

        string saveKey = GetLegacyResolvedSaveKey(activeProfile.profileId);
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();

        CreateDefaultRuntimeProfile(activeProfile.profileId, defaultDisplayName);
        SaveLegacyActiveProfile();
        ApplyActiveProfileToSystems();
        NotifyActiveProfileChanged();
    }

    private void BootstrapLocalRuntimeMirror()
    {
        if (IsUsingCoreProfile())
        {
            TryPullFromCompositionRoot(forceNotify: false);
            return;
        }

        LoadOrCreateLegacyProfile(ResolvePreferredProfileId(), ResolvePreferredDisplayName());
    }

    private bool IsUsingCoreProfile()
    {
        return GameCompositionRoot.Instance != null &&
               GameCompositionRoot.Instance.IsReady &&
               GameCompositionRoot.Instance.ProfileService != null &&
               GameCompositionRoot.Instance.AuthService != null;
    }

    private void SubscribeCompositionRootIfNeeded()
    {
        if (subscribedToCompositionRoot)
            return;

        if (GameCompositionRoot.Instance == null || GameCompositionRoot.Instance.ProfileRuntimeState == null)
            return;

        GameCompositionRoot.Instance.ProfileRuntimeState.Changed -= HandleCompositionProfileChanged;
        GameCompositionRoot.Instance.ProfileRuntimeState.Changed += HandleCompositionProfileChanged;
        subscribedToCompositionRoot = true;
    }

    private void UnsubscribeCompositionRootIfNeeded()
    {
        if (!subscribedToCompositionRoot)
            return;

        if (GameCompositionRoot.Instance != null && GameCompositionRoot.Instance.ProfileRuntimeState != null)
            GameCompositionRoot.Instance.ProfileRuntimeState.Changed -= HandleCompositionProfileChanged;

        subscribedToCompositionRoot = false;
    }

    private void HandleCompositionProfileChanged(ProfileSnapshot snapshot)
    {
        ApplySnapshotFromCore(snapshot, true);
    }

    private void TryPullFromCompositionRoot(bool forceNotify)
    {
        if (!IsUsingCoreProfile())
            return;

        SubscribeCompositionRootIfNeeded();

        ProfileSnapshot snapshot = GameCompositionRoot.Instance.ProfileService.CurrentProfile;
        ApplySnapshotFromCore(snapshot, forceNotify);
    }

    private void ApplySnapshotFromCore(ProfileSnapshot snapshot, bool forceNotify)
    {
        if (snapshot == null || !snapshot.IsValid())
            return;

        bool changed =
            activeProfile == null ||
            activeProfile.profileId != snapshot.PlayerId ||
            activeProfile.displayName != snapshot.DisplayName ||
            activeProfile.xp != snapshot.Xp ||
            activeProfile.level != snapshot.Level ||
            activeProfile.softCurrency != snapshot.SoftCurrency ||
            activeProfile.premiumCurrency != snapshot.HardCurrency ||
            activeProfile.totalMatchesPlayed != snapshot.TotalMatches ||
            activeProfile.totalWins != snapshot.TotalWins ||
            activeProfile.multiplayerMatchesPlayed != snapshot.MultiplayerMatches ||
            activeProfile.multiplayerWins != snapshot.MultiplayerWins ||
            activeProfile.rankedMatchesPlayed != snapshot.RankedMatches ||
            activeProfile.rankedWins != snapshot.RankedWins;

        int legacyVersusMatches = activeProfile != null ? activeProfile.versusMatchesPlayed : 0;
        int legacyVersusWins = activeProfile != null ? activeProfile.versusWins : 0;
        int legacyVersusTimeMatches = activeProfile != null ? activeProfile.versusTimeMatchesPlayed : 0;
        int legacyVersusScoreMatches = activeProfile != null ? activeProfile.versusScoreMatchesPlayed : 0;
        int legacyBotMatches = activeProfile != null ? activeProfile.botMatchesPlayed : 0;
        int legacyBotWins = activeProfile != null ? activeProfile.botWins : 0;
        int legacyRankedLp = activeProfile != null ? activeProfile.rankedLp : 1000;
        string legacyLastDailyClaim = activeProfile != null ? activeProfile.lastDailyLoginClaimDateUtc : string.Empty;
        int legacyConsecutiveDays = activeProfile != null ? activeProfile.consecutiveLoginDays : 0;

        activeProfile = new PlayerProfileRuntimeData
        {
            saveVersion = 3,
            profileId = snapshot.PlayerId,
            displayName = string.IsNullOrWhiteSpace(snapshot.DisplayName) ? ResolvePreferredDisplayName() : snapshot.DisplayName.Trim(),
            xp = Mathf.Max(0, snapshot.Xp),
            level = Mathf.Max(1, snapshot.Level),
            softCurrency = Mathf.Max(0, snapshot.SoftCurrency),
            premiumCurrency = Mathf.Max(0, snapshot.HardCurrency),
            totalMatchesPlayed = Mathf.Max(0, snapshot.TotalMatches),
            totalWins = Mathf.Max(0, snapshot.TotalWins),
            versusMatchesPlayed = Mathf.Max(0, legacyVersusMatches),
            versusWins = Mathf.Max(0, legacyVersusWins),
            versusTimeMatchesPlayed = Mathf.Max(0, legacyVersusTimeMatches),
            versusScoreMatchesPlayed = Mathf.Max(0, legacyVersusScoreMatches),
            botMatchesPlayed = Mathf.Max(0, legacyBotMatches),
            botWins = Mathf.Max(0, legacyBotWins),
            multiplayerMatchesPlayed = Mathf.Max(0, snapshot.MultiplayerMatches),
            multiplayerWins = Mathf.Max(0, snapshot.MultiplayerWins),
            rankedMatchesPlayed = Mathf.Max(0, snapshot.RankedMatches),
            rankedWins = Mathf.Max(0, snapshot.RankedWins),
            rankedLp = Mathf.Max(0, legacyRankedLp),
            lastDailyLoginClaimDateUtc = legacyLastDailyClaim,
            consecutiveLoginDays = Mathf.Max(0, legacyConsecutiveDays),
            createdFromServer = false,
            pendingServerSync = false,
            lastServerSyncUnixTimeSeconds = GetNowUnixSeconds(),
            lastLocalSaveUnixTimeSeconds = GetNowUnixSeconds()
        };

        EnsureRuntimeStructure();
        ApplyActiveProfileToSystems();

        if (changed || forceNotify)
            NotifyActiveProfileChanged();
    }

    private void LoadOrCreateLegacyProfile(string profileId, string fallbackDisplayName)
    {
        string sanitizedProfileId = SanitizeProfileId(profileId);
        string saveKey = GetLegacyResolvedSaveKey(sanitizedProfileId);

        if (!PlayerPrefs.HasKey(saveKey))
        {
            CreateDefaultRuntimeProfile(sanitizedProfileId, fallbackDisplayName);
            SaveLegacyActiveProfile();
            return;
        }

        string json = PlayerPrefs.GetString(saveKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            CreateDefaultRuntimeProfile(sanitizedProfileId, fallbackDisplayName);
            SaveLegacyActiveProfile();
            return;
        }

        PlayerProfileSaveData saveData = JsonUtility.FromJson<PlayerProfileSaveData>(json);

        if (saveData == null)
        {
            CreateDefaultRuntimeProfile(sanitizedProfileId, fallbackDisplayName);
            SaveLegacyActiveProfile();
            return;
        }

        activeProfile = new PlayerProfileRuntimeData
        {
            saveVersion = Mathf.Max(1, saveData.saveVersion),
            profileId = SanitizeProfileId(saveData.profileId),
            displayName = string.IsNullOrWhiteSpace(saveData.displayName)
                ? ResolvePreferredDisplayName()
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
            rankedLp = Mathf.Max(0, saveData.rankedLp <= 0 ? 1000 : saveData.rankedLp),
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

    private void CreateDefaultRuntimeProfile(string profileId, string displayName)
    {
        activeProfile = new PlayerProfileRuntimeData
        {
            saveVersion = 3,
            profileId = SanitizeProfileId(profileId),
            displayName = string.IsNullOrWhiteSpace(displayName) ? ResolvePreferredDisplayName() : displayName.Trim(),
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
            rankedLp = 1000,
            lastDailyLoginClaimDateUtc = string.Empty,
            consecutiveLoginDays = 0,
            createdFromServer = false,
            pendingServerSync = false,
            lastServerSyncUnixTimeSeconds = 0,
            lastLocalSaveUnixTimeSeconds = GetNowUnixSeconds()
        };
    }

    private void SaveLegacyActiveProfile()
    {
        EnsureRuntimeStructure();

        activeProfile.profileId = ResolvePreferredProfileId();
        activeProfile.displayName = ResolvePreferredDisplayName();
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
            rankedLp = activeProfile.rankedLp,
            lastDailyLoginClaimDateUtc = activeProfile.lastDailyLoginClaimDateUtc,
            consecutiveLoginDays = activeProfile.consecutiveLoginDays,
            createdFromServer = activeProfile.createdFromServer,
            pendingServerSync = activeProfile.pendingServerSync,
            lastServerSyncUnixTimeSeconds = activeProfile.lastServerSyncUnixTimeSeconds,
            lastLocalSaveUnixTimeSeconds = activeProfile.lastLocalSaveUnixTimeSeconds
        };

        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(GetLegacyResolvedSaveKey(activeProfile.profileId), json);
        PlayerPrefs.Save();
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
            activeProfile.profileId = ResolvePreferredProfileId();

        if (string.IsNullOrWhiteSpace(activeProfile.displayName))
            activeProfile.displayName = ResolvePreferredDisplayName();

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
        activeProfile.rankedLp = Mathf.Max(0, activeProfile.rankedLp <= 0 ? 1000 : activeProfile.rankedLp);

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
            rankedLp = source.rankedLp,
            lastDailyLoginClaimDateUtc = source.lastDailyLoginClaimDateUtc,
            consecutiveLoginDays = source.consecutiveLoginDays,
            createdFromServer = source.createdFromServer,
            pendingServerSync = source.pendingServerSync,
            lastServerSyncUnixTimeSeconds = source.lastServerSyncUnixTimeSeconds,
            lastLocalSaveUnixTimeSeconds = source.lastLocalSaveUnixTimeSeconds
        };
    }

    private string GetLegacyResolvedSaveKey(string profileId)
    {
        return legacySaveKeyPrefix + "_" + SanitizeProfileId(profileId);
    }

    private string SanitizeProfileId(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return ResolvePreferredProfileId();

        return profileId.Trim();
    }

    private string ResolvePreferredProfileId()
    {
        if (OnlineLocalPlayerContext.IsAvailable && !string.IsNullOrWhiteSpace(OnlineLocalPlayerContext.PlayerId))
            return OnlineLocalPlayerContext.PlayerId.Trim();

        if (GameCompositionRoot.Instance != null &&
            GameCompositionRoot.Instance.AuthService != null &&
            GameCompositionRoot.Instance.AuthService.CurrentSession != null &&
            GameCompositionRoot.Instance.AuthService.CurrentSession.Identity != null &&
            !string.IsNullOrWhiteSpace(GameCompositionRoot.Instance.AuthService.CurrentSession.Identity.PlayerId))
        {
            return GameCompositionRoot.Instance.AuthService.CurrentSession.Identity.PlayerId.Trim();
        }

        if (activeProfile != null && !string.IsNullOrWhiteSpace(activeProfile.profileId))
            return activeProfile.profileId.Trim();

        return string.IsNullOrWhiteSpace(defaultProfileId) ? "guest_fallback" : defaultProfileId.Trim();
    }

    private string ResolvePreferredDisplayName()
    {
        if (GameCompositionRoot.Instance != null &&
            GameCompositionRoot.Instance.ProfileService != null &&
            GameCompositionRoot.Instance.ProfileService.CurrentProfile != null &&
            !string.IsNullOrWhiteSpace(GameCompositionRoot.Instance.ProfileService.CurrentProfile.DisplayName))
        {
            return GameCompositionRoot.Instance.ProfileService.CurrentProfile.DisplayName.Trim();
        }

        if (GameCompositionRoot.Instance != null &&
            GameCompositionRoot.Instance.AuthService != null &&
            GameCompositionRoot.Instance.AuthService.CurrentSession != null &&
            GameCompositionRoot.Instance.AuthService.CurrentSession.Identity != null &&
            !string.IsNullOrWhiteSpace(GameCompositionRoot.Instance.AuthService.CurrentSession.Identity.DisplayName))
        {
            return GameCompositionRoot.Instance.AuthService.CurrentSession.Identity.DisplayName.Trim();
        }

        if (activeProfile != null && !string.IsNullOrWhiteSpace(activeProfile.displayName))
            return activeProfile.displayName.Trim();

        return string.IsNullOrWhiteSpace(defaultDisplayName) ? "Player" : defaultDisplayName.Trim();
    }

    private long GetNowUnixSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private async void PushDisplayNameToCore(string newDisplayName)
    {
        if (!IsUsingCoreProfile())
            return;

        await GameCompositionRoot.Instance.ProfileService.SetDisplayNameAsync(newDisplayName);
    }

    private async void PushProfileStatsToCompositionRoot()
    {
        if (!IsUsingCoreProfile())
            return;

        await GameCompositionRoot.Instance.ProfileService.SetProgressionAsync(activeProfile.xp, activeProfile.level);
        await GameCompositionRoot.Instance.ProfileService.SetStatsAsync(
            activeProfile.totalMatchesPlayed,
            activeProfile.totalWins,
            activeProfile.multiplayerMatchesPlayed,
            activeProfile.multiplayerWins,
            activeProfile.rankedMatchesPlayed,
            activeProfile.rankedWins
        );
    }

    private async void PushFullProfileToCompositionRoot()
    {
        if (!IsUsingCoreProfile())
            return;

        ProfileSnapshot current = GameCompositionRoot.Instance.ProfileService.CurrentProfile;
        if (current == null || !current.IsValid())
            return;

        ProfileSnapshot updated = new ProfileSnapshot(
            current.ProfileId,
            ResolvePreferredProfileId(),
            activeProfile.displayName,
            activeProfile.xp,
            activeProfile.level,
            activeProfile.totalMatchesPlayed,
            activeProfile.totalWins,
            activeProfile.multiplayerMatchesPlayed,
            activeProfile.multiplayerWins,
            activeProfile.rankedMatchesPlayed,
            activeProfile.rankedWins,
            current.EquippedBallSkinId,
            current.EquippedTableSkinId,
            activeProfile.softCurrency,
            activeProfile.premiumCurrency,
            current.CreatedAtUnixSeconds,
            GetNowUnixSeconds()
        );

        await GameCompositionRoot.Instance.ProfileService.ApplyAuthoritativeSnapshotAsync(updated);
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