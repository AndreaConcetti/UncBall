using System;
using UnityEngine;
using UncballArena.Core.Bootstrap;
using UncballArena.Core.Profile.Models;
using UncballArena.Core.Profile.Services;

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

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

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

    public PlayerXPGrantResult TryGrantManualReward(int grantedXp, string source, string reason)
    {
        ResolveDependencies();

        PlayerXPGrantResult result = new PlayerXPGrantResult
        {
            success = false,
            source = source ?? "manual_reward",
            reason = reason ?? string.Empty
        };

        if (!HasUsableResolvedProfile())
        {
            result.reason = "profile_not_ready";
            return result;
        }

        if (grantedXp <= 0)
        {
            result.reason = "invalid_xp_amount";
            return result;
        }

        result = GrantXpInternal(
            grantedXp,
            source ?? "manual_reward",
            reason ?? "manual_reward");

        if (logDebug)
        {
            Debug.Log(
                "[PlayerXPRewardService] Manual reward granted. " +
                "XP=" + result.grantedXp +
                " | NewXP=" + result.newTotalXp +
                " | NewLevel=" + result.newLevel +
                " | Source=" + result.source +
                " | Reason=" + result.reason,
                this);
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
            wonMatch ? "match_played_and_won" : "match_played");

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
                this);
        }

        return result;
    }

    private PlayerXPGrantResult GrantXpInternal(
        int grantedXp,
        string source,
        string reason)
    {
        PlayerXPGrantResult result = new PlayerXPGrantResult
        {
            success = false,
            source = source,
            reason = reason,
            grantedXp = Mathf.Max(0, grantedXp),
            consecutiveLoginDays = GetCurrentConsecutiveLoginDays()
        };

        int previousLevel = GetCurrentLevel();
        int currentXp = GetCurrentTotalXp();

        int newTotalXp = Mathf.Max(0, currentXp + result.grantedXp);
        int newLevel = progressionRules != null
            ? progressionRules.CalculateLevelFromTotalXp(newTotalXp)
            : Mathf.Max(1, previousLevel);

        ApplyProgressionToBestAvailableStore(newTotalXp, newLevel);

        result.success = true;
        result.newTotalXp = newTotalXp;
        result.newLevel = newLevel;
        result.previousLevel = previousLevel;
        result.leveledUp = newLevel > previousLevel;

        OnXpGranted?.Invoke(result);
        return result;
    }

    private void ApplyProgressionToBestAvailableStore(int newTotalXp, int newLevel)
    {
        if (IsUsingCoreProfile())
        {
            ApplyToCoreProfile(newTotalXp, newLevel);
            return;
        }

        if (profileManager != null)
        {
            profileManager.ApplyProgressionState(
                totalXp: newTotalXp,
                totalLevel: newLevel,
                lastDailyLoginClaimDateUtc: null,
                consecutiveLoginDays: null);
        }
    }

    private async void ApplyToCoreProfile(int newTotalXp, int newLevel)
    {
        if (!IsUsingCoreProfile())
            return;

        ProfileService profileService = GameCompositionRoot.Instance.ProfileService as ProfileService;
        if (profileService == null)
            return;

        ProfileSnapshot current = profileService.CurrentProfile;
        if (current == null || !current.IsValid())
            return;

        ProfileSnapshot updated = current.WithProgression(newTotalXp, newLevel);
        await profileService.ApplyAuthoritativeSnapshotAsync(updated);

        if (profileManager != null)
        {
            profileManager.ApplyProgressionState(
                totalXp: updated.Xp,
                totalLevel: updated.Level,
                lastDailyLoginClaimDateUtc: updated.LastDailyLoginClaimDateUtc,
                consecutiveLoginDays: updated.ConsecutiveLoginDays);
        }
    }

    private bool IsUsingCoreProfile()
    {
        return GameCompositionRoot.Instance != null &&
               GameCompositionRoot.Instance.IsReady &&
               GameCompositionRoot.Instance.ProfileService != null &&
               GameCompositionRoot.Instance.ProfileService.CurrentProfile != null &&
               GameCompositionRoot.Instance.ProfileService.CurrentProfile.IsValid();
    }

    private bool HasUsableResolvedProfile()
    {
        return !string.IsNullOrWhiteSpace(GetResolvedProfileId());
    }

    private string GetResolvedProfileId()
    {
        if (GameCompositionRoot.Instance != null &&
            GameCompositionRoot.Instance.IsReady &&
            GameCompositionRoot.Instance.AuthService != null &&
            GameCompositionRoot.Instance.AuthService.CurrentSession != null &&
            !string.IsNullOrWhiteSpace(GameCompositionRoot.Instance.AuthService.CurrentSession.EffectivePlayerId))
        {
            return GameCompositionRoot.Instance.AuthService.CurrentSession.EffectivePlayerId;
        }

        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
            return profileManager.ActiveProfileId;

        return string.Empty;
    }

    private int GetCurrentTotalXp()
    {
        if (IsUsingCoreProfile())
        {
            ProfileSnapshot current = GameCompositionRoot.Instance.ProfileService.CurrentProfile;
            if (current != null && current.IsValid())
                return Mathf.Max(0, current.Xp);
        }

        if (profileManager != null && profileManager.ActiveProfile != null)
            return Mathf.Max(0, profileManager.ActiveProfile.xp);

        return 0;
    }

    private int GetCurrentLevel()
    {
        if (IsUsingCoreProfile())
        {
            ProfileSnapshot current = GameCompositionRoot.Instance.ProfileService.CurrentProfile;
            if (current != null && current.IsValid())
                return Mathf.Max(1, current.Level);
        }

        if (profileManager != null && profileManager.ActiveProfile != null)
            return Mathf.Max(1, profileManager.ActiveProfile.level);

        return 1;
    }

    private int GetCurrentConsecutiveLoginDays()
    {
        if (IsUsingCoreProfile())
        {
            ProfileSnapshot current = GameCompositionRoot.Instance.ProfileService.CurrentProfile;
            if (current != null && current.IsValid())
                return Mathf.Max(0, current.ConsecutiveLoginDays);
        }

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