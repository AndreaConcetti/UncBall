
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

    private IProfileService profileService;

    public bool IsReady => TryResolveProfileService(out _);

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
        return await resolved.TryConsumeLuckyShotTokenAsync();
    }

    public async Task<ProfileSnapshot> GrantTokensAsync(int amount, CancellationToken cancellationToken = default)
    {
        if (!TryResolveProfileService(out IProfileService resolved))
        {
            Debug.LogWarning("[LuckyShotBackendService] GrantTokensAsync -> ProfileService missing.");
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await resolved.AddLuckyShotTokensAsync(amount);
    }

    public async Task<ProfileSnapshot> RegisterPlayResultAsync(int score, CancellationToken cancellationToken = default)
    {
        if (!TryResolveProfileService(out IProfileService resolved))
        {
            Debug.LogWarning("[LuckyShotBackendService] RegisterPlayResultAsync -> ProfileService missing.");
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await resolved.RegisterLuckyShotPlayAsync(score);
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
