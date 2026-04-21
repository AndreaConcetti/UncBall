using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UncballArena.Core.Profile.Models;

public sealed class LuckyShotEntryService : MonoBehaviour
{
    public static LuckyShotEntryService Instance { get; private set; }

    public event Action<int> TokensChanged;
    public event Action<bool> BusyStateChanged;

    // Compatibilità con UI legacy
    public event Action<string> FeedbackRaised;
    public event Action EntryConsumedAndGameplayStarted;

    // Evento nuovo
    public event Action EntryStarted;

    [Header("Scene")]
    [SerializeField] private string luckyShotSceneName = "LuckyShot";
    [SerializeField] private bool loadLuckyShotSceneOnConsume = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private LuckyShotBackendService backendService;
    private CancellationTokenSource consumeCts;
    private bool isBusy;

    public bool IsBusy => isBusy;
    public int CachedTokens { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad && transform.parent == null)
            DontDestroyOnLoad(gameObject);

        ResolveBackendService();
        _ = RefreshTokens();

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotEntryService] Awake -> " +
                "BackendReady=" + (backendService != null && backendService.IsReady) +
                " | CachedTokens=" + CachedTokens +
                " | LoadSceneOnConsume=" + loadLuckyShotSceneOnConsume +
                " | SceneName=" + luckyShotSceneName,
                this);
        }
    }

    private void OnDestroy()
    {
        if (consumeCts != null)
        {
            consumeCts.Cancel();
            consumeCts.Dispose();
            consumeCts = null;
        }

        if (Instance == this)
            Instance = null;
    }

    public Task<int> RefreshTokens(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ResolveBackendService();

        if (backendService == null)
        {
            CachedTokens = 0;
            TokensChanged?.Invoke(CachedTokens);
            return Task.FromResult(CachedTokens);
        }

        CachedTokens = Mathf.Max(0, backendService.GetAvailableTokens());
        TokensChanged?.Invoke(CachedTokens);

        if (verboseLogs)
            Debug.Log("[LuckyShotEntryService] RefreshTokens -> " + CachedTokens, this);

        return Task.FromResult(CachedTokens);
    }

    public bool CanEnter()
    {
        return !isBusy && CachedTokens > 0;
    }

    public async Task<bool> TryEnterLuckyShotAsync(CancellationToken cancellationToken = default)
    {
        ResolveBackendService();

        if (backendService == null)
        {
            RaiseFeedback("Lucky Shot backend missing.");
            return false;
        }

        if (isBusy)
        {
            RaiseFeedback("Lucky Shot is busy.");
            return false;
        }

        await RefreshTokens(cancellationToken);

        if (CachedTokens <= 0)
        {
            RaiseFeedback("No Lucky Shot tokens available.");
            return false;
        }

        SetBusy(true);

        if (consumeCts != null)
        {
            consumeCts.Cancel();
            consumeCts.Dispose();
        }

        consumeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            bool consumed = await backendService.TryConsumeEntryTokenAsync(consumeCts.Token);

            await RefreshTokens(consumeCts.Token);

            if (!consumed)
            {
                RaiseFeedback("Unable to consume Lucky Shot token.");
                return false;
            }

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotEntryService] TryEnterLuckyShotAsync -> token consumed successfully. Remaining=" + CachedTokens,
                    this);
            }

            RaiseFeedback("Lucky Shot start acknowledged.");

            EntryStarted?.Invoke();
            EntryConsumedAndGameplayStarted?.Invoke();

            if (loadLuckyShotSceneOnConsume && !string.IsNullOrWhiteSpace(luckyShotSceneName))
                await LoadLuckyShotSceneAsync(consumeCts.Token);

            return true;
        }
        catch (OperationCanceledException)
        {
            RaiseFeedback("Lucky Shot entry cancelled.");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError("[LuckyShotEntryService] TryEnterLuckyShotAsync failed -> " + ex, this);
            RaiseFeedback("Lucky Shot entry failed.");
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    // Compatibilità con EntryPanelUI legacy
    public async Task DebugGrantTokensAsync(int amount, CancellationToken cancellationToken = default)
    {
        await GrantDebugTokensAsync(amount, cancellationToken);
    }

    public async Task<bool> GrantDebugTokensAsync(int amount, CancellationToken cancellationToken = default)
    {
        ResolveBackendService();

        if (backendService == null)
        {
            RaiseFeedback("Lucky Shot backend missing.");
            return false;
        }

        ProfileSnapshot snapshot = await backendService.GrantTokensAsync(Mathf.Max(1, amount), cancellationToken);
        bool ok = snapshot != null;

        await RefreshTokens(cancellationToken);

        if (ok)
            RaiseFeedback("Lucky Shot tokens granted.");
        else
            RaiseFeedback("Failed to grant Lucky Shot tokens.");

        return ok;
    }

    private async Task LoadLuckyShotSceneAsync(CancellationToken cancellationToken)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(luckyShotSceneName, LoadSceneMode.Single);
        if (operation == null)
            return;

        while (!operation.isDone)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private void ResolveBackendService()
    {
        if (backendService != null)
            return;

        backendService = LuckyShotBackendService.Instance;

#if UNITY_2023_1_OR_NEWER
        if (backendService == null)
            backendService = FindFirstObjectByType<LuckyShotBackendService>();
#else
        if (backendService == null)
            backendService = FindObjectOfType<LuckyShotBackendService>();
#endif
    }

    private void SetBusy(bool value)
    {
        if (isBusy == value)
            return;

        isBusy = value;
        BusyStateChanged?.Invoke(isBusy);
    }

    private void RaiseFeedback(string message)
    {
        FeedbackRaised?.Invoke(message);

        if (verboseLogs && !string.IsNullOrWhiteSpace(message))
            Debug.Log("[LuckyShotEntryService] Feedback -> " + message, this);
    }
}