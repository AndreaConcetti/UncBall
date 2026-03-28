using System;
using System.Globalization;
using UnityEngine;

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
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

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

        if (profileManager == null || progressionRules == null || profileManager.ActiveProfile == null)
        {
            result.reason = "missing_dependencies";
            return result;
        }

        string todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string lastClaimDate = profileManager.ActiveProfile.lastDailyLoginClaimDateUtc;

        if (!string.IsNullOrWhiteSpace(lastClaimDate) && lastClaimDate == todayUtc)
        {
            result.reason = "already_claimed_today";
            return result;
        }

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
                    newStreak = Mathf.Max(1, profileManager.ActiveProfile.consecutiveLoginDays + 1);
            }
        }

        int grantedXp = progressionRules.GetDailyLoginXp(newStreak);
        result = GrantXpInternal(
            grantedXp,
            "daily_login",
            "granted",
            additionalMatchesPlayed: 0,
            additionalWins: 0,
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

        if (profileManager == null || progressionRules == null || profileManager.ActiveProfile == null)
        {
            result.reason = "missing_dependencies";
            return result;
        }

        bool wonMatch = winner == localPlayer;
        int grantedXp = progressionRules.GetMatchCompletionXp(wonMatch);

        result = GrantXpInternal(
            grantedXp,
            "match_completion",
            wonMatch ? "match_played_and_won" : "match_played",
            additionalMatchesPlayed: 1,
            additionalWins: wonMatch ? 1 : 0,
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
        int additionalMatchesPlayed,
        int additionalWins,
        string newDailyLoginDateUtc,
        int? newConsecutiveLoginDays)
    {
        PlayerXPGrantResult result = new PlayerXPGrantResult
        {
            success = false,
            source = source,
            reason = reason,
            grantedXp = Mathf.Max(0, grantedXp),
            consecutiveLoginDays = newConsecutiveLoginDays ?? (profileManager != null && profileManager.ActiveProfile != null
                ? profileManager.ActiveProfile.consecutiveLoginDays
                : 0)
        };

        if (profileManager == null || progressionRules == null || profileManager.ActiveProfile == null)
        {
            result.reason = "missing_dependencies";
            return result;
        }

        PlayerProfileRuntimeData profile = profileManager.ActiveProfile;

        int previousLevel = Mathf.Max(1, profile.level);
        int newTotalXp = Mathf.Max(0, profile.xp + result.grantedXp);
        int newLevel = progressionRules.CalculateLevelFromTotalXp(newTotalXp);

        profileManager.ApplyProgressionState(
            totalXp: newTotalXp,
            totalLevel: newLevel,
            totalMatchesPlayed: Mathf.Max(0, profile.totalMatchesPlayed + additionalMatchesPlayed),
            totalWins: Mathf.Max(0, profile.totalWins + additionalWins),
            lastDailyLoginClaimDateUtc: newDailyLoginDateUtc,
            consecutiveLoginDays: newConsecutiveLoginDays
        );

        result.success = true;
        result.newTotalXp = newTotalXp;
        result.newLevel = newLevel;
        result.previousLevel = previousLevel;
        result.leveledUp = newLevel > previousLevel;

        OnXpGranted?.Invoke(result);

        return result;
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (progressionRules == null)
            progressionRules = PlayerProgressionRules.Instance;
    }
}