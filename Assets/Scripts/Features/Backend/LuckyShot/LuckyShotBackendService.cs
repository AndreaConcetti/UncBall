using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UncballArena.Core.Bootstrap;
using UncballArena.Core.Profile.Models;
using UncballArena.Core.Profile.Services;

public sealed class LuckyShotBackendService : MonoBehaviour
{
    public static LuckyShotBackendService Instance { get; private set; }

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Daily token rules")]
    [SerializeField] private int dailyFreeTokenAmount = 1;
    [SerializeField] private bool verboseLogs = true;

    private const string KeyDailyFreeTokenDateUtc = "LuckyShot_DailyFreeTokenDateUtc";
    private const string KeyBonusTokens = "LuckyShot_BonusTokens";
    private const string KeyActiveSessionJson = "LuckyShot_ActiveSessionJson";
    private const string KeyLastLuckyShotScore = "LuckyShot_LastScore";
    private const string KeyTotalLuckyShotPlays = "LuckyShot_TotalPlays";

    private const string LocalDailyFreeTokenDateUtc = "LUCKYSHOT_LOCAL_DAILY_FREE_DATE";
    private const string LocalBonusTokens = "LUCKYSHOT_LOCAL_BONUS_TOKENS";
    private const string LocalActiveSessionJson = "LUCKYSHOT_LOCAL_ACTIVE_SESSION_JSON";
    private const string LocalLastLuckyShotScore = "LUCKYSHOT_LOCAL_LAST_SCORE";
    private const string LocalTotalLuckyShotPlays = "LUCKYSHOT_LOCAL_TOTAL_PLAYS";

    private IProfileService profileService;

    public bool IsReady => TryResolveProfileService(out _);

    [Serializable]
    private sealed class LuckyShotSessionWrapper
    {
        public LuckyShotActiveSession session;
    }

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

        if (verboseLogs)
            Debug.Log("[LuckyShotBackendService] Awake -> ready.", this);
    }

    public int GetAvailableTokens()
    {
        return GetDailyFreeTokenCount() + GetBonusTokens();
    }

    public int GetDailyFreeTokenCount()
    {
        EnsureDailyFreeTokenState();
        return HasDailyFreeTokenAvailableForToday() ? dailyFreeTokenAmount : 0;
    }

    public int GetBonusTokens()
    {
        if (TryResolveProfileService(out IProfileService resolved) && resolved.CurrentProfile != null)
        {
            int fromProfile = TryReadLuckyShotTokensFromProfile(resolved.CurrentProfile);
            if (fromProfile >= 0)
                return fromProfile;
        }

        if (PlayFabClientAPI.IsClientLoggedIn())
        {
            Dictionary<string, UserDataRecord> data = GetUserDataSync(
                KeyBonusTokens,
                KeyDailyFreeTokenDateUtc,
                KeyActiveSessionJson);

            if (TryReadInt(data, KeyBonusTokens, out int remoteBonus))
                return Mathf.Max(0, remoteBonus);
        }

        return Mathf.Max(0, PlayerPrefs.GetInt(LocalBonusTokens, 0));
    }

    public async Task<bool> TryConsumeEntryTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EnsureDailyFreeTokenState();

        if (HasDailyFreeTokenAvailableForToday())
        {
            bool freeConsumed = await ConsumeDailyFreeTokenAsync(cancellationToken);
            if (freeConsumed)
            {
                if (verboseLogs)
                    Debug.Log("[LuckyShotBackendService] TryConsumeEntryTokenAsync -> consumed daily free token.", this);

                return true;
            }
        }

        bool bonusConsumed = await ConsumeBonusTokenAsync(cancellationToken);

        if (verboseLogs)
            Debug.Log("[LuckyShotBackendService] TryConsumeEntryTokenAsync -> consumed bonus token=" + bonusConsumed, this);

        return bonusConsumed;
    }

    public async Task<ProfileSnapshot> GrantTokensAsync(int amount, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return GetCurrentProfileOrNull();

        if (TryResolveProfileService(out IProfileService resolved) && resolved.CurrentProfile != null)
        {
            ProfileSnapshot updated = await TryGrantTokensViaProfileServiceAsync(resolved, safeAmount);
            if (updated != null)
                return updated;
        }

        int newBonus = GetBonusTokens() + safeAmount;
        await SaveBonusTokensAsync(newBonus, cancellationToken);

        if (verboseLogs)
            Debug.Log("[LuckyShotBackendService] GrantTokensAsync -> +" + safeAmount + " bonus tokens. NewBonus=" + newBonus, this);

        return GetCurrentProfileOrNull();
    }

    public async Task<ProfileSnapshot> RegisterPlayResultAsync(int score, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (TryResolveProfileService(out IProfileService resolved))
        {
            ProfileSnapshot profileResult = await TryRegisterPlayViaProfileServiceAsync(resolved, score);
            if (profileResult != null)
                return profileResult;
        }

        int totalPlays = GetLocalOrRemoteInt(KeyTotalLuckyShotPlays, LocalTotalLuckyShotPlays) + 1;
        await SaveKeyValueAsync(KeyLastLuckyShotScore, Mathf.Max(0, score).ToString(), LocalLastLuckyShotScore, Mathf.Max(0, score).ToString(), cancellationToken);
        await SaveKeyValueAsync(KeyTotalLuckyShotPlays, totalPlays.ToString(), LocalTotalLuckyShotPlays, totalPlays.ToString(), cancellationToken);

        if (verboseLogs)
            Debug.Log("[LuckyShotBackendService] RegisterPlayResultAsync -> fallback saved. Score=" + score + " | TotalPlays=" + totalPlays, this);

        return GetCurrentProfileOrNull();
    }

    public async Task<LuckyShotActiveSession> TryGetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string json = string.Empty;

        if (PlayFabClientAPI.IsClientLoggedIn())
        {
            Dictionary<string, UserDataRecord> data = await GetUserDataAsync(cancellationToken, KeyActiveSessionJson);
            if (data != null && data.TryGetValue(KeyActiveSessionJson, out UserDataRecord record))
                json = record.Value ?? string.Empty;
        }
        else
        {
            json = PlayerPrefs.GetString(LocalActiveSessionJson, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            LuckyShotSessionWrapper wrapper = JsonUtility.FromJson<LuckyShotSessionWrapper>(json);
            if (wrapper != null && wrapper.session.IsValid())
            {
                if (verboseLogs)
                    Debug.Log("[LuckyShotBackendService] TryGetActiveSessionAsync -> loaded session " + wrapper.session.sessionId, this);

                return wrapper.session;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LuckyShotBackendService] TryGetActiveSessionAsync -> invalid session json. " + ex, this);
        }

        return default;
    }

    public async Task<bool> SaveActiveSessionAsync(LuckyShotActiveSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string json = string.Empty;
        if (session.IsValid() || session.hasActiveSession)
        {
            LuckyShotSessionWrapper wrapper = new LuckyShotSessionWrapper { session = session };
            json = JsonUtility.ToJson(wrapper);
        }

        bool ok = await SaveKeyValueAsync(
            KeyActiveSessionJson,
            json,
            LocalActiveSessionJson,
            json,
            cancellationToken);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotBackendService] SaveActiveSessionAsync -> ok=" + ok +
                " | HasActive=" + session.hasActiveSession +
                " | SessionId=" + (session.sessionId ?? string.Empty),
                this);
        }

        return ok;
    }

    private bool TryResolveProfileService(out IProfileService resolved)
    {
        if (profileService != null)
        {
            resolved = profileService;
            return true;
        }

        if (GameCompositionRoot.Instance != null)
            profileService = GameCompositionRoot.Instance.ProfileService;

        resolved = profileService;
        return resolved != null;
    }

    private async Task<bool> ConsumeDailyFreeTokenAsync(CancellationToken cancellationToken)
    {
        string today = GetUtcTodayString();
        bool ok = await SaveKeyValueAsync(
            KeyDailyFreeTokenDateUtc,
            today,
            LocalDailyFreeTokenDateUtc,
            today,
            cancellationToken);

        return ok;
    }

    private async Task<bool> ConsumeBonusTokenAsync(CancellationToken cancellationToken)
    {
        int currentBonus = GetBonusTokens();
        if (currentBonus <= 0)
            return false;

        int newBonus = Mathf.Max(0, currentBonus - 1);
        return await SaveBonusTokensAsync(newBonus, cancellationToken);
    }

    private async Task<bool> SaveBonusTokensAsync(int newBonus, CancellationToken cancellationToken)
    {
        bool ok = await SaveKeyValueAsync(
            KeyBonusTokens,
            Mathf.Max(0, newBonus).ToString(),
            LocalBonusTokens,
            Mathf.Max(0, newBonus),
            cancellationToken);

        if (verboseLogs)
            Debug.Log("[LuckyShotBackendService] SaveBonusTokensAsync -> NewBonus=" + Mathf.Max(0, newBonus) + " | Ok=" + ok, this);

        return ok;
    }

    private void EnsureDailyFreeTokenState()
    {
        string today = GetUtcTodayString();
        string savedDate = GetStoredDailyFreeTokenDate();

        if (string.IsNullOrWhiteSpace(savedDate))
            return;

        if (string.Equals(savedDate, today, StringComparison.Ordinal))
            return;

        // A free token is available whenever stored date is not today.
        // We intentionally do not roll over or accumulate.
    }

    private bool HasDailyFreeTokenAvailableForToday()
    {
        string today = GetUtcTodayString();
        string savedDate = GetStoredDailyFreeTokenDate();
        return !string.Equals(savedDate, today, StringComparison.Ordinal);
    }

    private string GetStoredDailyFreeTokenDate()
    {
        if (PlayFabClientAPI.IsClientLoggedIn())
        {
            Dictionary<string, UserDataRecord> data = GetUserDataSync(KeyDailyFreeTokenDateUtc);
            if (data != null && data.TryGetValue(KeyDailyFreeTokenDateUtc, out UserDataRecord record))
                return record.Value ?? string.Empty;
        }

        return PlayerPrefs.GetString(LocalDailyFreeTokenDateUtc, string.Empty);
    }

    private int GetLocalOrRemoteInt(string remoteKey, string localKey)
    {
        if (PlayFabClientAPI.IsClientLoggedIn())
        {
            Dictionary<string, UserDataRecord> data = GetUserDataSync(remoteKey);
            if (TryReadInt(data, remoteKey, out int remote))
                return remote;
        }

        return Mathf.Max(0, PlayerPrefs.GetInt(localKey, 0));
    }

    private async Task<bool> SaveKeyValueAsync(
        string remoteKey,
        string remoteValue,
        string localKey,
        object localValue,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool localSaved = SaveLocal(localKey, localValue);
        if (!PlayFabClientAPI.IsClientLoggedIn())
            return localSaved;

        var tcs = new TaskCompletionSource<bool>();

        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                [remoteKey] = remoteValue ?? string.Empty
            },
            Permission = UserDataPermission.Private
        };

        PlayFabClientAPI.UpdateUserData(
            request,
            result => tcs.TrySetResult(true),
            error =>
            {
                Debug.LogWarning("[LuckyShotBackendService] SaveKeyValueAsync PlayFab failed -> " + error.GenerateErrorReport(), this);
                tcs.TrySetResult(false);
            });

        bool remoteSaved = await tcs.Task;
        return localSaved || remoteSaved;
    }

    private async Task<Dictionary<string, UserDataRecord>> GetUserDataAsync(CancellationToken cancellationToken, params string[] keys)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!PlayFabClientAPI.IsClientLoggedIn())
            return null;

        var tcs = new TaskCompletionSource<Dictionary<string, UserDataRecord>>();

        var request = new GetUserDataRequest
        {
            Keys = keys != null && keys.Length > 0 ? new List<string>(keys) : null
        };

        PlayFabClientAPI.GetUserData(
            request,
            result => tcs.TrySetResult(result.Data),
            error =>
            {
                Debug.LogWarning("[LuckyShotBackendService] GetUserDataAsync failed -> " + error.GenerateErrorReport(), this);
                tcs.TrySetResult(null);
            });

        return await tcs.Task;
    }

    private Dictionary<string, UserDataRecord> GetUserDataSync(params string[] keys)
    {
        if (!PlayFabClientAPI.IsClientLoggedIn())
            return null;

        var request = new GetUserDataRequest
        {
            Keys = keys != null && keys.Length > 0 ? new List<string>(keys) : null
        };

        Dictionary<string, UserDataRecord> data = null;
        bool done = false;

        PlayFabClientAPI.GetUserData(
            request,
            result =>
            {
                data = result.Data;
                done = true;
            },
            error =>
            {
                if (verboseLogs)
                    Debug.LogWarning("[LuckyShotBackendService] GetUserDataSync failed -> " + error.GenerateErrorReport(), this);

                done = true;
            });

        float timeoutAt = Time.realtimeSinceStartup + 2.0f;
        while (!done && Time.realtimeSinceStartup < timeoutAt)
        {
        }

        return data;
    }

    private static bool TryReadInt(Dictionary<string, UserDataRecord> data, string key, out int value)
    {
        value = 0;

        if (data == null || string.IsNullOrWhiteSpace(key))
            return false;

        if (!data.TryGetValue(key, out UserDataRecord record) || record == null)
            return false;

        return int.TryParse(record.Value, out value);
    }

    private static bool SaveLocal(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        switch (value)
        {
            case int i:
                PlayerPrefs.SetInt(key, i);
                break;
            case string s:
                PlayerPrefs.SetString(key, s ?? string.Empty);
                break;
            default:
                PlayerPrefs.SetString(key, value != null ? value.ToString() : string.Empty);
                break;
        }

        PlayerPrefs.Save();
        return true;
    }

    private ProfileSnapshot GetCurrentProfileOrNull()
    {
        return TryResolveProfileService(out IProfileService resolved) ? resolved.CurrentProfile : null;
    }

    private async Task<ProfileSnapshot> TryGrantTokensViaProfileServiceAsync(IProfileService resolved, int amount)
    {
        var method = resolved.GetType().GetMethod("AddLuckyShotTokensAsync");
        if (method == null)
            return null;

        object invoked = method.Invoke(resolved, new object[] { amount });
        if (invoked is Task<ProfileSnapshot> typedTask)
            return await typedTask;

        return null;
    }

    private async Task<ProfileSnapshot> TryRegisterPlayViaProfileServiceAsync(IProfileService resolved, int score)
    {
        var method = resolved.GetType().GetMethod("RegisterLuckyShotPlayAsync");
        if (method == null)
            return null;

        object invoked = method.Invoke(resolved, new object[] { score });
        if (invoked is Task<ProfileSnapshot> typedTask)
            return await typedTask;

        return null;
    }

    private int TryReadLuckyShotTokensFromProfile(ProfileSnapshot snapshot)
    {
        if (snapshot == null)
            return -1;

        var prop = snapshot.GetType().GetProperty("LuckyShotTokens");
        if (prop != null && prop.PropertyType == typeof(int))
        {
            object value = prop.GetValue(snapshot);
            if (value is int i)
                return Mathf.Max(0, i);
        }

        var field = snapshot.GetType().GetField("luckyShotTokens", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(int))
        {
            object value = field.GetValue(snapshot);
            if (value is int i)
                return Mathf.Max(0, i);
        }

        return -1;
    }

    private static string GetUtcTodayString()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd");
    }
}
