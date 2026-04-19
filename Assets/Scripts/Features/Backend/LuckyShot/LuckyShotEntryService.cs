using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LuckyShotEntryService : MonoBehaviour
{
    public static LuckyShotEntryService Instance { get; private set; }

    public event Action<int> TokensChanged;
    public event Action<bool> BusyStateChanged;
    public event Action<string> FeedbackRaised;
    public event Action EntryConsumedAndGameplayStarted;

    [Header("Scene")]
    [SerializeField] private string luckyShotSceneName = "LuckyShot";
    [SerializeField] private bool loadLuckyShotSceneOnConsume = false;
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

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        ResolveBackendService();
        RefreshTokens();

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotEntryService] Awake -> " +
                "BackendReady=" + (backendService != null) +
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

    public void RefreshTokens()
    {
        ResolveBackendService();

        CachedTokens = backendService != null ? Mathf.Max(0, backendService.GetAvailableTokens()) : 0;
        TokensChanged?.Invoke(CachedTokens);

        if (verboseLogs)
            Debug.Log("[LuckyShotEntryService] RefreshTokens -> " + CachedTokens, this);
    }

    public bool CanEnter()
    {
        RefreshTokens();
        return CachedTokens > 0 && !isBusy;
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

        RefreshTokens();

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

            RefreshTokens();

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

            EntryConsumedAndGameplayStarted?.Invoke();

            if (loadLuckyShotSceneOnConsume && !string.IsNullOrWhiteSpace(luckyShotSceneName))
            {
                SceneManager.LoadScene(luckyShotSceneName);
            }

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

    public async Task DebugGrantTokensAsync(int amount, CancellationToken cancellationToken = default)
    {
        ResolveBackendService();

        if (backendService == null)
        {
            RaiseFeedback("Lucky Shot backend missing.");
            return;
        }

        await backendService.GrantTokensAsync(Mathf.Max(1, amount), cancellationToken);
        RefreshTokens();
        RaiseFeedback("Lucky Shot tokens granted.");
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

        if (verboseLogs)
            Debug.Log("[LuckyShotEntryService] Feedback -> " + message, this);
    }
}
