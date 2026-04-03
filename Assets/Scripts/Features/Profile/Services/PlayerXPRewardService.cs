using System;
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

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool dailyLoginCheckedThisSession = false;

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
        if (autoClaimDailyLoginOnStart)
            TryClaimDailyLoginReward();
    }

    public PlayerXPGrantResult TryClaimDailyLoginReward()
    {
        ResolveDependencies();

        PlayerXPGrantResult result = new PlayerXPGrantResult
        {
            success = false,
            source = "daily_login",
            reason = ""
        };

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

        string todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string lastClaimDate = GetLastDailyLoginClaimDateUtc();
        if (!string.IsNullOrWhiteSpace(lastClaimDate) && lastClaimDate == todayUtc)
        {
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
            reason = ""
        };

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
            profileManager.ApplyProgressionState(
                totalXp: newTotalXp,
                totalLevel: newLevel,
                lastDailyLoginClaimDateUtc: newDailyLoginDateUtc,
                consecutiveLoginDays: newConsecutiveLoginDays
            );
    }

    private bool IsUsingCoreProfile()
    {
        return GameCompositionRoot.Instance != null &&
               GameCompositionRoot.Instance.IsReady &&
               GameCompositionRoot.Instance.ProfileService != null &&
               GameCompositionRoot.Instance.ProfileService.CurrentProfile != null;
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
        if (profileManager != null && profileManager.ActiveProfile != null)
            return profileManager.ActiveProfile.lastDailyLoginClaimDateUtc;

        return string.Empty;
    }

    private int GetCurrentConsecutiveLoginDays()
    {
        if (profileManager != null && profileManager.ActiveProfile != null)
            return Mathf.Max(0, profileManager.ActiveProfile.consecutiveLoginDays);

        return 0;
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