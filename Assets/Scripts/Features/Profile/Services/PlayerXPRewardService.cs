using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UncballArena.Core.Bootstrap;
using UncballArena.Core.Profile.Models;

[Serializable]
public class PlayerXPGrantResult
{
    public bool success;
    public string source = "";
    public string reason = "";
    public int grantedXp = 0;
    public int newTotalXp = 0;
    public int newLevel = 1;
    public int previousLevel = 1;
    public bool leveledUp = false;
    public int consecutiveLoginDays = 0;
}

public class PlayerXPRewardService : MonoBehaviour
{
    public static PlayerXPRewardService Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private PlayerProgressionRules progressionRules;

    [Header("Behavior")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool autoClaimDailyLoginOnStart = true;

    [Header("Daily Login Persistence")]
    [SerializeField] private string dailyLoginClaimDateKeyPrefix = "PLAYER_DAILY_LOGIN_LAST_DATE_UTC";
    [SerializeField] private string consecutiveLoginDaysKeyPrefix = "PLAYER_DAILY_LOGIN_STREAK";
    [SerializeField] private string localSessionClaimGuardKeyPrefix = "PLAYER_DAILY_LOGIN_SESSION_GUARD";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool dailyLoginCheckedThisSession;
    private Coroutine delayedAutoClaimRoutine;

    public event Action<PlayerXPGrantResult> OnXpGranted;

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
    }

    private void Start()
    {
        if (!autoClaimDailyLoginOnStart)
            return;

        StartDelayedAutoClaimIfNeeded();
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (profileManager != null)
        {
            profileManager.OnActiveProfileChanged -= HandleProfileChanged;
            profileManager.OnActiveProfileChanged += HandleProfileChanged;
        }
    }

    private void OnDisable()
    {
        if (profileManager != null)
            profileManager.OnActiveProfileChanged -= HandleProfileChanged;
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        if (!autoClaimDailyLoginOnStart)
            return;

        if (dailyLoginCheckedThisSession)
            return;

        if (delayedAutoClaimRoutine == null)
            StartDelayedAutoClaimIfNeeded();
    }

    private void StartDelayedAutoClaimIfNeeded()
    {
        if (delayedAutoClaimRoutine != null)
            StopCoroutine(delayedAutoClaimRoutine);

        delayedAutoClaimRoutine = StartCoroutine(AutoClaimDailyLoginWhenReadyCoroutine());
    }

    private IEnumerator AutoClaimDailyLoginWhenReadyCoroutine()
    {
        float timeout = 15f;
        float elapsed = 0f;

        while (!HasUsableResolvedProfile())
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= timeout)
                break;

            yield return null;
        }

        delayedAutoClaimRoutine = null;

        if (!HasUsableResolvedProfile())
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[PlayerXPRewardService] Auto-claim skipped because no resolved profile became available in time.",
                    this
                );
            }

            yield break;
        }

        if (logDebug)
        {
            Debug.Log(
                "[PlayerXPRewardService] Core/profile ready. Executing delayed daily login claim.",
                this
            );
        }

        TryClaimDailyLoginReward();
    }

    public PlayerXPGrantResult TryClaimDailyLoginReward()
    {
        ResolveDependencies();

        PlayerXPGrantResult result = new PlayerXPGrantResult
        {
            success = false,
            source = "daily_login",
            reason = string.Empty
        };

        if (!HasUsableResolvedProfile())
        {
            result.reason = "profile_not_ready";
            return result;
        }

        if (dailyLoginCheckedThisSession)
        {
            result.reason = "already_checked_this_session";
            return result;
        }

        dailyLoginCheckedThisSession = true;

        if (progressionRules == null)
        {
            result.reason = "missing_progression_rules";
            return result;
        }

        string profileId = GetResolvedProfileId();
        if (string.IsNullOrWhiteSpace(profileId))
        {
            result.reason = "missing_profile_id";
            return result;
        }

        string todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string sessionGuard = GetLocalSessionGuardDate(profileId);
        if (!string.IsNullOrWhiteSpace(sessionGuard) && sessionGuard == todayUtc)
        {
            result.reason = "already_claimed_today_session_guard";
            return result;
        }

        string lastClaimDate = GetLastDailyLoginClaimDateUtc();
        if (!string.IsNullOrWhiteSpace(lastClaimDate) && lastClaimDate == todayUtc)
        {
            SetLocalSessionGuardDate(profileId, todayUtc);
            result.reason = "already_claimed_today";
            return result;
        }

        int currentStreak = GetCurrentConsecutiveLoginDays();
        int newStreak = 1;

        if (!string.IsNullOrWhiteSpace(lastClaimDate))
        {
            if (DateTime.TryParseExact(
                    lastClaimDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime lastClaimParsed))
            {
                DateTime yesterdayUtc = DateTime.UtcNow.Date.AddDays(-1);

                if (lastClaimParsed.Date == yesterdayUtc)
                    newStreak = Mathf.Max(1, currentStreak + 1);
            }
        }

        int grantedXp = progressionRules.GetDailyLoginXp(newStreak);

        result = GrantXpInternal(
            grantedXp,
            "daily_login",
            "granted",
            newDailyLoginDateUtc: todayUtc,
            newConsecutiveLoginDays: newStreak
        );

        if (result.success)
            SetLocalSessionGuardDate(profileId, todayUtc);

        if (logDebug)
        {
            Debug.Log(
                "[PlayerXPRewardService] Daily login granted. " +
                "XP=" + result.grantedXp +
                " | NewXP=" + result.newTotalXp +
                " | NewLevel=" + result.newLevel +
                " | Streak=" + result.consecutiveLoginDays,
                this
            );
        }

        return result;
    }

    public PlayerXPGrantResult TryGrantMatchCompletionRewards(PlayerID winner, PlayerID localPlayer = PlayerID.Player1)
    {
        ResolveDependencies();

        PlayerXPGrantResult result = new PlayerXPGrantResult
        {
            success = false,
            source = "match_completion",
            reason = string.Empty
        };

        if (!HasUsableResolvedProfile())
        {
            result.reason = "profile_not_ready";
            return result;
        }

        if (progressionRules == null)
        {
            result.reason = "missing_progression_rules";
            return result;
        }

        bool wonMatch = winner == localPlayer;
        int grantedXp = progressionRules.GetMatchCompletionXp(wonMatch);

        result = GrantXpInternal(
            grantedXp,
            "match_completion",
            wonMatch ? "match_played_and_won" : "match_played",
            newDailyLoginDateUtc: null,
            newConsecutiveLoginDays: null
        );

        if (logDebug)
        {
            Debug.Log(
                "[PlayerXPRewardService] Match completion granted. " +
                "Winner=" + winner +
                " | LocalPlayer=" + localPlayer +
                " | XP=" + result.grantedXp +
                " | NewXP=" + result.newTotalXp +
                " | NewLevel=" + result.newLevel +
                " | LeveledUp=" + result.leveledUp,
                this
            );
        }

        return result;
    }

    private PlayerXPGrantResult GrantXpInternal(
        int grantedXp,
        string source,
        string reason,
        string newDailyLoginDateUtc,
        int? newConsecutiveLoginDays)
    {
        PlayerXPGrantResult result = new PlayerXPGrantResult
        {
            success = false,
            source = source,
            reason = reason,
            grantedXp = Mathf.Max(0, grantedXp),
            consecutiveLoginDays = newConsecutiveLoginDays ?? GetCurrentConsecutiveLoginDays()
        };

        int previousLevel = GetCurrentLevel();
        int currentXp = GetCurrentTotalXp();

        int newTotalXp = Mathf.Max(0, currentXp + result.grantedXp);
        int newLevel = progressionRules != null
            ? progressionRules.CalculateLevelFromTotalXp(newTotalXp)
            : Mathf.Max(1, previousLevel);

        ApplyProgressionToBestAvailableStore(
            newTotalXp,
            newLevel,
            newDailyLoginDateUtc,
            newConsecutiveLoginDays
        );

        if (!string.IsNullOrWhiteSpace(newDailyLoginDateUtc))
            SaveDailyLoginLocalState(newDailyLoginDateUtc, newConsecutiveLoginDays ?? result.consecutiveLoginDays);

        result.success = true;
        result.newTotalXp = newTotalXp;
        result.newLevel = newLevel;
        result.previousLevel = previousLevel;
        result.leveledUp = newLevel > previousLevel;

        OnXpGranted?.Invoke(result);
        return result;
    }

    private void ApplyProgressionToBestAvailableStore(
        int newTotalXp,
        int newLevel,
        string newDailyLoginDateUtc,
        int? newConsecutiveLoginDays)
    {
        if (IsUsingCoreProfile())
        {
            ApplyToCoreProfile(newTotalXp, newLevel, newDailyLoginDateUtc, newConsecutiveLoginDays);
            return;
        }

        if (profileManager != null)
        {
            profileManager.ApplyProgressionState(
                totalXp: newTotalXp,
                totalLevel: newLevel,
                lastDailyLoginClaimDateUtc: newDailyLoginDateUtc,
                consecutiveLoginDays: newConsecutiveLoginDays
            );
        }
    }

    private async void ApplyToCoreProfile(
        int newTotalXp,
        int newLevel,
        string newDailyLoginDateUtc,
        int? newConsecutiveLoginDays)
    {
        if (!IsUsingCoreProfile())
            return;

        ProfileSnapshot current = GameCompositionRoot.Instance.ProfileService.CurrentProfile;
        if (current == null || !current.IsValid())
            return;

        ProfileSnapshot updated = new ProfileSnapshot(
            current.ProfileId,
            current.PlayerId,
            current.DisplayName,
            newTotalXp,
            newLevel,
            current.TotalMatches,
            current.TotalWins,
            current.MultiplayerMatches,
            current.MultiplayerWins,
            current.RankedMatches,
            current.RankedWins,
            current.EquippedBallSkinId,
            current.EquippedTableSkinId,
            current.SoftCurrency,
            current.HardCurrency,
            current.CreatedAtUnixSeconds,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        );

        await GameCompositionRoot.Instance.ProfileService.ApplyAuthoritativeSnapshotAsync(updated);

        if (profileManager != null)
        {
            profileManager.ApplyProgressionState(
                totalXp: newTotalXp,
                totalLevel: newLevel,
                lastDailyLoginClaimDateUtc: newDailyLoginDateUtc,
                consecutiveLoginDays: newConsecutiveLoginDays
            );
        }
    }

    private bool HasUsableResolvedProfile()
    {
        string resolvedProfileId = GetResolvedProfileId();
        if (string.IsNullOrWhiteSpace(resolvedProfileId))
            return false;

        if (string.Equals(resolvedProfileId, "local_player_1", StringComparison.OrdinalIgnoreCase))
        {
            if (IsUsingCoreProfile())
                return true;

            if (profileManager != null &&
                profileManager.ActiveProfile != null &&
                !string.IsNullOrWhiteSpace(profileManager.ActiveProfile.profileId) &&
                !string.Equals(profileManager.ActiveProfile.profileId, "local_player_1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        return true;
    }

    private bool IsUsingCoreProfile()
    {
        return GameCompositionRoot.Instance != null &&
               GameCompositionRoot.Instance.ProfileService != null &&
               GameCompositionRoot.Instance.ProfileService.CurrentProfile != null &&
               GameCompositionRoot.Instance.ProfileService.CurrentProfile.IsValid();
    }

    private int GetCurrentTotalXp()
    {
        if (IsUsingCoreProfile())
            return Mathf.Max(0, GameCompositionRoot.Instance.ProfileService.CurrentProfile.Xp);

        if (profileManager != null && profileManager.ActiveProfile != null)
            return Mathf.Max(0, profileManager.ActiveProfile.xp);

        return 0;
    }

    private int GetCurrentLevel()
    {
        if (IsUsingCoreProfile())
            return Mathf.Max(1, GameCompositionRoot.Instance.ProfileService.CurrentProfile.Level);

        if (profileManager != null && profileManager.ActiveProfile != null)
            return Mathf.Max(1, profileManager.ActiveProfile.level);

        return 1;
    }

    private string GetLastDailyLoginClaimDateUtc()
    {
        string local = GetStoredDailyLoginDateForResolvedProfile();
        if (!string.IsNullOrWhiteSpace(local))
            return local;

        if (profileManager != null && profileManager.ActiveProfile != null)
        {
            string legacy = profileManager.ActiveProfile.lastDailyLoginClaimDateUtc;
            if (!string.IsNullOrWhiteSpace(legacy))
            {
                SaveDailyLoginLocalState(legacy, GetCurrentConsecutiveLoginDays());
                return legacy;
            }
        }

        return string.Empty;
    }

    private int GetCurrentConsecutiveLoginDays()
    {
        int local = GetStoredConsecutiveLoginDaysForResolvedProfile();
        if (local > 0)
            return local;

        if (profileManager != null && profileManager.ActiveProfile != null)
        {
            int legacy = Mathf.Max(0, profileManager.ActiveProfile.consecutiveLoginDays);
            if (legacy > 0)
            {
                string lastDate = profileManager.ActiveProfile.lastDailyLoginClaimDateUtc;
                if (!string.IsNullOrWhiteSpace(lastDate))
                    SaveDailyLoginLocalState(lastDate, legacy);
            }

            return legacy;
        }

        return 0;
    }

    private void SaveDailyLoginLocalState(string claimDateUtc, int streak)
    {
        string profileId = GetResolvedProfileId();
        if (string.IsNullOrWhiteSpace(profileId))
            return;

        PlayerPrefs.SetString(BuildDailyLoginClaimDateKey(profileId), claimDateUtc ?? string.Empty);
        PlayerPrefs.SetInt(BuildConsecutiveLoginDaysKey(profileId), Mathf.Max(0, streak));
        PlayerPrefs.Save();
    }

    private string GetStoredDailyLoginDateForResolvedProfile()
    {
        string profileId = GetResolvedProfileId();
        if (string.IsNullOrWhiteSpace(profileId))
            return string.Empty;

        return PlayerPrefs.GetString(BuildDailyLoginClaimDateKey(profileId), string.Empty);
    }

    private int GetStoredConsecutiveLoginDaysForResolvedProfile()
    {
        string profileId = GetResolvedProfileId();
        if (string.IsNullOrWhiteSpace(profileId))
            return 0;

        return Mathf.Max(0, PlayerPrefs.GetInt(BuildConsecutiveLoginDaysKey(profileId), 0));
    }

    private string GetLocalSessionGuardDate(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return string.Empty;

        return PlayerPrefs.GetString(BuildLocalSessionGuardKey(profileId), string.Empty);
    }

    private void SetLocalSessionGuardDate(string profileId, string dateUtc)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return;

        PlayerPrefs.SetString(BuildLocalSessionGuardKey(profileId), dateUtc ?? string.Empty);
        PlayerPrefs.Save();
    }

    private string GetResolvedProfileId()
    {
        if (IsUsingCoreProfile())
            return GameCompositionRoot.Instance.ProfileService.CurrentProfile.PlayerId;

        if (profileManager != null)
        {
            if (profileManager.ActiveProfile != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfile.profileId))
                return profileManager.ActiveProfile.profileId;

            if (!string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
                return profileManager.ActiveProfileId;
        }

        return string.Empty;
    }

    private string BuildDailyLoginClaimDateKey(string profileId)
    {
        return $"{dailyLoginClaimDateKeyPrefix}_{profileId}";
    }

    private string BuildConsecutiveLoginDaysKey(string profileId)
    {
        return $"{consecutiveLoginDaysKeyPrefix}_{profileId}";
    }

    private string BuildLocalSessionGuardKey(string profileId)
    {
        return $"{localSessionClaimGuardKeyPrefix}_{profileId}";
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (progressionRules == null)
            progressionRules = PlayerProgressionRules.Instance;
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