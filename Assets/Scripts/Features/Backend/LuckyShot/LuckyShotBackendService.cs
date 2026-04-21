using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UncballArena.Core.Bootstrap;
using UncballArena.Core.Profile.Models;
using UncballArena.Core.Profile.Services;

public sealed class LuckyShotBackendService : MonoBehaviour
{
    public static LuckyShotBackendService Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool verboseLogs = true;

    private IProfileService profileService;

    private const string ActiveSessionKeyPrefix = "LUCKY_SHOT_ACTIVE_SESSION_V1_";

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
        if (!TryResolveProfileService(out IProfileService resolved) || resolved.CurrentProfile == null)
            return 0;

        return resolved.CurrentProfile.LuckyShotTokens;
    }

    public async Task<bool> TryConsumeEntryTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!TryResolveProfileService(out IProfileService resolved))
        {
            Debug.LogWarning("[LuckyShotBackendService] TryConsumeEntryTokenAsync -> ProfileService missing.");
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        bool consumed = await resolved.TryConsumeLuckyShotTokenAsync();

        if (verboseLogs)
            Debug.Log("[LuckyShotBackendService] TryConsumeEntryTokenAsync -> Consumed=" + consumed, this);

        return consumed;
    }

    public async Task<ProfileSnapshot> GrantTokensAsync(int amount, CancellationToken cancellationToken = default)
    {
        if (!TryResolveProfileService(out IProfileService resolved))
        {
            Debug.LogWarning("[LuckyShotBackendService] GrantTokensAsync -> ProfileService missing.");
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        ProfileSnapshot snapshot = await resolved.AddLuckyShotTokensAsync(amount);

        if (verboseLogs)
            Debug.Log("[LuckyShotBackendService] GrantTokensAsync -> Amount=" + amount, this);

        return snapshot;
    }

    public async Task<ProfileSnapshot> RegisterPlayResultAsync(int score, CancellationToken cancellationToken = default)
    {
        if (!TryResolveProfileService(out IProfileService resolved))
        {
            Debug.LogWarning("[LuckyShotBackendService] RegisterPlayResultAsync -> ProfileService missing.");
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        ProfileSnapshot snapshot = await resolved.RegisterLuckyShotPlayAsync(score);

        if (verboseLogs)
            Debug.Log("[LuckyShotBackendService] RegisterPlayResultAsync -> Score=" + score, this);

        return snapshot;
    }

    public Task<LuckyShotActiveSession> TryGetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string key = BuildActiveSessionKey();
        if (string.IsNullOrWhiteSpace(key) || !PlayerPrefs.HasKey(key))
        {
            if (verboseLogs)
                Debug.Log("[LuckyShotBackendService] TryGetActiveSessionAsync -> no saved session.", this);

            return Task.FromResult(default(LuckyShotActiveSession));
        }

        try
        {
            string json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return Task.FromResult(default(LuckyShotActiveSession));

            LuckyShotSessionWrapper wrapper = JsonUtility.FromJson<LuckyShotSessionWrapper>(json);
            if (wrapper == null)
                return Task.FromResult(default(LuckyShotActiveSession));

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotBackendService] TryGetActiveSessionAsync -> loaded session " +
                    wrapper.session.sessionId,
                    this);
            }

            return Task.FromResult(wrapper.session);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LuckyShotBackendService] TryGetActiveSessionAsync failed -> " + ex, this);
            return Task.FromResult(default(LuckyShotActiveSession));
        }
    }

    public Task<bool> SaveActiveSessionAsync(LuckyShotActiveSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string key = BuildActiveSessionKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning("[LuckyShotBackendService] SaveActiveSessionAsync -> key unavailable.");
            return Task.FromResult(false);
        }

        try
        {
            if (!session.IsValid())
            {
                if (PlayerPrefs.HasKey(key))
                    PlayerPrefs.DeleteKey(key);

                PlayerPrefs.Save();

                if (verboseLogs)
                    Debug.Log("[LuckyShotBackendService] SaveActiveSessionAsync -> cleared persisted session.", this);

                return Task.FromResult(true);
            }

            LuckyShotSessionWrapper wrapper = new LuckyShotSessionWrapper
            {
                session = session
            };

            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotBackendService] SaveActiveSessionAsync -> ok=True | HasActive=True | SessionId=" +
                    session.sessionId,
                    this);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LuckyShotBackendService] SaveActiveSessionAsync failed -> " + ex, this);
            return Task.FromResult(false);
        }
    }

    public Task<bool> ClearActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string key = BuildActiveSessionKey();
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult(false);

        if (PlayerPrefs.HasKey(key))
            PlayerPrefs.DeleteKey(key);

        PlayerPrefs.Save();

        if (verboseLogs)
            Debug.Log("[LuckyShotBackendService] ClearActiveSessionAsync -> ok=True", this);

        return Task.FromResult(true);
    }

    private string BuildActiveSessionKey()
    {
        if (!TryResolveProfileService(out IProfileService resolved) || resolved.CurrentProfile == null)
            return ActiveSessionKeyPrefix + "guest";

        string profileId = resolved.CurrentProfile.ProfileId;
        string playerId = resolved.CurrentProfile.PlayerId;

        if (!string.IsNullOrWhiteSpace(profileId))
            return ActiveSessionKeyPrefix + profileId;

        if (!string.IsNullOrWhiteSpace(playerId))
            return ActiveSessionKeyPrefix + playerId;

        return ActiveSessionKeyPrefix + "guest";
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
}