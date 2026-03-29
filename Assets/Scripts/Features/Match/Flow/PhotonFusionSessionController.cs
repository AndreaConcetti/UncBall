using System;
using UnityEngine;

public class PhotonFusionSessionController : MonoBehaviour
{
    public static PhotonFusionSessionController Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PhotonFusionRunnerManager runnerManager;
    [SerializeField] private OnlineMatchSession onlineMatchSession;
    [SerializeField] private MatchRuntimeConfig matchRuntimeConfig;

    [Header("Config")]
    [SerializeField] private string gameplaySceneName = "Gameplay";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public bool IsBusy { get; private set; }

    public event Action<bool, string> OnOperationCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;
    }

    public void HostPreparedSession()
    {
        if (IsBusy)
            return;

        ResolveDependencies();

        if (!ValidateDependencies())
        {
            Complete(false, "Missing required dependencies.");
            return;
        }

        if (!onlineMatchSession.HasPreparedSession)
        {
            Complete(false, "No prepared session.");
            return;
        }

        if (!onlineMatchSession.CurrentSession.isHost)
        {
            Complete(false, "Only host can start the match.");
            return;
        }

        if (!onlineMatchSession.CanHostStartMatch())
        {
            Complete(false, "Lobby is not ready.");
            return;
        }

        if (!runnerManager.IsRunning)
        {
            Complete(false, "Fusion lobby runner is not active.");
            return;
        }

        IsBusy = true;

        onlineMatchSession.RequestHostStartMatch();

        bool pushed = onlineMatchSession.PushPreparedSessionIntoMatchRuntimeConfig();
        if (!pushed)
        {
            Complete(false, "Failed to push prepared session into runtime config.");
            return;
        }

        bool loaded = runnerManager.LoadNetworkScene(gameplaySceneName);
        if (!loaded)
        {
            Complete(false, "Failed to load gameplay scene.");
            return;
        }

        Complete(true, "Gameplay scene loading started.");
    }

    public async void ShutdownSession()
    {
        ResolveDependencies();

        if (runnerManager == null)
        {
            Complete(false, "Runner manager missing.");
            return;
        }

        IsBusy = true;
        await runnerManager.ShutdownRunnerAsync();
        Complete(true, "Fusion runner stopped.");
    }

    private bool ValidateDependencies()
    {
        return runnerManager != null &&
               onlineMatchSession != null &&
               matchRuntimeConfig != null;
    }

    private void ResolveDependencies()
    {
        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

        if (onlineMatchSession == null)
            onlineMatchSession = OnlineMatchSession.Instance;

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = MatchRuntimeConfig.Instance;

#if UNITY_2023_1_OR_NEWER
        if (runnerManager == null)
            runnerManager = FindFirstObjectByType<PhotonFusionRunnerManager>();

        if (onlineMatchSession == null)
            onlineMatchSession = FindFirstObjectByType<OnlineMatchSession>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindFirstObjectByType<MatchRuntimeConfig>();
#else
        if (runnerManager == null)
            runnerManager = FindObjectOfType<PhotonFusionRunnerManager>();

        if (onlineMatchSession == null)
            onlineMatchSession = FindObjectOfType<OnlineMatchSession>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindObjectOfType<MatchRuntimeConfig>();
#endif
    }

    private void Complete(bool success, string message)
    {
        IsBusy = false;

        if (logDebug)
        {
            Debug.Log(
                "[PhotonFusionSessionController] " +
                (success ? "SUCCESS" : "FAIL") +
                " -> " + message,
                this
            );
        }

        OnOperationCompleted?.Invoke(success, message);
    }
}