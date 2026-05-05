using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class DailyFreeLuckyShotRewardService : MonoBehaviour
{
    public static DailyFreeLuckyShotRewardService Instance { get; private set; }

    public event Action<bool> ClaimAvailabilityChanged;
    public event Action<int> DailyFreeLuckyShotGranted;

    [Header("Schedule")]
    [SerializeField] private bool useUtcDay = true;
    [SerializeField][Range(0, 23)] private int resetHourUtc = 0;

    [Header("Reward")]
    [SerializeField] private int dailyTokenAmount = 1;

    [Header("Persistence")]
    [SerializeField] private string playerPrefsKeyPrefix = "DAILY_FREE_LUCKY_SHOT_CLAIMED_DAY_V1_";

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private LuckyShotBackendService luckyShotBackendService;
    private bool claimInProgress;

    public bool IsClaimInProgress => claimInProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveServices();

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyFreeLuckyShotRewardService] Awake -> " +
                "BackendService=" + (luckyShotBackendService != null) +
                " | CanClaimToday=" + CanClaimToday(),
                this);
        }
    }

    private void OnEnable()
    {
        ResolveServices();
        ClaimAvailabilityChanged?.Invoke(CanClaimToday());
    }

    public bool CanClaimToday()
    {
        string todayKey = GetCurrentDayKey();
        string savedKey = PlayerPrefs.GetString(GetStorageKey(), string.Empty);

        bool canClaim = !string.Equals(savedKey, todayKey, StringComparison.Ordinal);

        if (verboseLogs)
        {
            Debug.Log(
                "[DailyFreeLuckyShotRewardService] CanClaimToday -> " +
                "Today=" + todayKey +
                " | Saved=" + savedKey +
                " | CanClaim=" + canClaim,
                this);
        }

        return canClaim;
    }

    public bool HasClaimedToday()
    {
        return !CanClaimToday();
    }

    public async Task<bool> TryGrantDailyFreeLuckyShotAsync(CancellationToken cancellationToken = default)
    {
        if (claimInProgress)
        {
            if (verboseLogs)
                Debug.Log("[DailyFreeLuckyShotRewardService] TryGrantDailyFreeLuckyShotAsync blocked -> claim already in progress.", this);

            return false;
        }

        if (!CanClaimToday())
        {
            if (verboseLogs)
                Debug.Log("[DailyFreeLuckyShotRewardService] TryGrantDailyFreeLuckyShotAsync blocked -> already claimed today.", this);

            ClaimAvailabilityChanged?.Invoke(false);
            return false;
        }

        ResolveServices();

        if (luckyShotBackendService == null)
        {
            Debug.LogWarning("[DailyFreeLuckyShotRewardService] TryGrantDailyFreeLuckyShotAsync blocked -> LuckyShotBackendService missing.", this);
            return false;
        }

        claimInProgress = true;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            int amount = Mathf.Max(1, dailyTokenAmount);

            object snapshot = await luckyShotBackendService.GrantTokensAsync(amount, cancellationToken);
            bool granted = snapshot != null;

            if (!granted)
            {
                Debug.LogWarning("[DailyFreeLuckyShotRewardService] TryGrantDailyFreeLuckyShotAsync failed -> GrantTokensAsync returned null.", this);
                return false;
            }

            string todayKey = GetCurrentDayKey();

            PlayerPrefs.SetString(GetStorageKey(), todayKey);
            PlayerPrefs.Save();

            DailyFreeLuckyShotGranted?.Invoke(amount);
            ClaimAvailabilityChanged?.Invoke(false);

            if (verboseLogs)
            {
                Debug.Log(
                    "[DailyFreeLuckyShotRewardService] TryGrantDailyFreeLuckyShotAsync -> granted daily Lucky Shot token. " +
                    "Amount=" + amount +
                    " | DayKey=" + todayKey,
                    this);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            if (verboseLogs)
                Debug.Log("[DailyFreeLuckyShotRewardService] TryGrantDailyFreeLuckyShotAsync cancelled.", this);

            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError("[DailyFreeLuckyShotRewardService] TryGrantDailyFreeLuckyShotAsync exception -> " + ex, this);
            return false;
        }
        finally
        {
            claimInProgress = false;
        }
    }

    [ContextMenu("Debug/Reset Daily Free Lucky Shot")]
    public void DebugResetDailyFreeLuckyShot()
    {
        PlayerPrefs.DeleteKey(GetStorageKey());
        PlayerPrefs.Save();

        ClaimAvailabilityChanged?.Invoke(CanClaimToday());

        if (verboseLogs)
            Debug.Log("[DailyFreeLuckyShotRewardService] DebugResetDailyFreeLuckyShot -> reset completed.", this);
    }

    [ContextMenu("Debug/Force Claim As Done Today")]
    public void DebugForceClaimAsDoneToday()
    {
        PlayerPrefs.SetString(GetStorageKey(), GetCurrentDayKey());
        PlayerPrefs.Save();

        ClaimAvailabilityChanged?.Invoke(false);

        if (verboseLogs)
            Debug.Log("[DailyFreeLuckyShotRewardService] DebugForceClaimAsDoneToday -> saved today.", this);
    }

    private void ResolveServices()
    {
        if (luckyShotBackendService == null)
        {
            luckyShotBackendService = LuckyShotBackendService.Instance;

#if UNITY_2023_1_OR_NEWER
            if (luckyShotBackendService == null)
                luckyShotBackendService = FindFirstObjectByType<LuckyShotBackendService>();
#else
            if (luckyShotBackendService == null)
                luckyShotBackendService = FindObjectOfType<LuckyShotBackendService>();
#endif
        }
    }

    private string GetStorageKey()
    {
        return playerPrefsKeyPrefix + ResolvePlayerKey();
    }

    private string ResolvePlayerKey()
    {
        object manager = ResolvePlayerProfileManagerObject();

        if (manager != null)
        {
            string profileId = TryReadStringMember(manager, "ProfileId", "profileId", "ActiveProfileId", "activeProfileId");
            if (!string.IsNullOrWhiteSpace(profileId))
                return SanitizeKey(profileId);

            string playerId = TryReadStringMember(manager, "PlayerId", "playerId", "ActivePlayerId", "activePlayerId");
            if (!string.IsNullOrWhiteSpace(playerId))
                return SanitizeKey(playerId);

            object runtimeData = TryReadObjectMember(manager, "ActiveProfile", "activeProfile", "RuntimeData", "runtimeData", "CurrentProfile", "currentProfile");
            if (runtimeData != null)
            {
                profileId = TryReadStringMember(runtimeData, "ProfileId", "profileId");
                if (!string.IsNullOrWhiteSpace(profileId))
                    return SanitizeKey(profileId);

                playerId = TryReadStringMember(runtimeData, "PlayerId", "playerId");
                if (!string.IsNullOrWhiteSpace(playerId))
                    return SanitizeKey(playerId);
            }
        }

        return SanitizeKey(SystemInfo.deviceUniqueIdentifier);
    }

    private object ResolvePlayerProfileManagerObject()
    {
        Type type = Type.GetType("PlayerProfileManager");

        if (type != null)
        {
            PropertyInfo instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty != null)
            {
                object instance = instanceProperty.GetValue(null);
                if (instance != null)
                    return instance;
            }
        }

#if UNITY_2023_1_OR_NEWER
        PlayerProfileManager found = FindFirstObjectByType<PlayerProfileManager>();
#else
        PlayerProfileManager found = FindObjectOfType<PlayerProfileManager>();
#endif

        return found;
    }

    private string TryReadStringMember(object target, params string[] names)
    {
        object value = TryReadObjectMember(target, names);
        return value == null ? string.Empty : value.ToString();
    }

    private object TryReadObjectMember(object target, params string[] names)
    {
        if (target == null || names == null)
            return null;

        Type type = target.GetType();

        for (int i = 0; i < names.Length; i++)
        {
            PropertyInfo property = type.GetProperty(names[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
                return property.GetValue(target);

            FieldInfo field = type.GetField(names[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(target);
        }

        return null;
    }

    private string GetCurrentDayKey()
    {
        DateTime nowUtc = useUtcDay ? DateTime.UtcNow : DateTime.Now.ToUniversalTime();
        DateTime resetAnchor = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, resetHourUtc, 0, 0, DateTimeKind.Utc);

        if (nowUtc < resetAnchor)
            resetAnchor = resetAnchor.AddDays(-1);

        return resetAnchor.ToString("yyyy-MM-dd");
    }

    private string SanitizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        return value
            .Replace(" ", "_")
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace(":", "_")
            .Replace("|", "_")
            .Replace(".", "_");
    }
}