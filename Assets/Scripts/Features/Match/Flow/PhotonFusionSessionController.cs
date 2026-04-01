using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonFusionSessionController : MonoBehaviour
{
    public static PhotonFusionSessionController Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PhotonFusionRunnerManager runnerManager;
    [SerializeField] private OnlineMatchSession onlineMatchSession;
    [SerializeField] private MatchRuntimeConfig matchRuntimeConfig;
    [SerializeField] private FusionLobbyService fusionLobbyService;

    [Header("Config")]
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private int hostStartSceneLoadDelayMs = 800;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public bool IsBusy { get; private set; }
    public bool IsShuttingDown { get; private set; }

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

    public void ForceResetLocalState(string reason = "Forced reset")
    {
        IsBusy = false;
        IsShuttingDown = false;

        if (logDebug)
            Debug.Log("[PhotonFusionSessionController] ForceResetLocalState -> " + reason, this);
    }

    public async void HostPreparedSession()
    {
        if (IsBusy || IsShuttingDown)
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

        await Task.Delay(Mathf.Max(100, hostStartSceneLoadDelayMs));

        if (IsShuttingDown)
        {
            Complete(false, "Cancelled while starting match.");
            return;
        }

        if (runnerManager == null || !runnerManager.IsRunning)
        {
            Complete(false, "Fusion runner is no longer active.");
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

    public bool LoadGameplaySceneOnActiveRunner()
    {
        ResolveDependencies();

        if (IsShuttingDown)
        {
            Complete(false, "Cannot load gameplay scene while shutting down.");
            return false;
        }

        if (runnerManager == null || !runnerManager.IsRunning)
        {
            Complete(false, "Fusion runner is not active.");
            return false;
        }

        bool loaded = runnerManager.LoadNetworkScene(gameplaySceneName);
        if (!loaded)
        {
            Complete(false, "Failed to load gameplay scene.");
            return false;
        }

        Complete(true, "Gameplay scene reload started.");
        return true;
    }

    public async void ShutdownSession()
    {
        ResolveDependencies();

        if (IsShuttingDown)
            return;

        if (runnerManager == null)
        {
            Complete(false, "Runner manager missing.");
            return;
        }

        IsBusy = true;
        IsShuttingDown = true;

        try
        {
            await runnerManager.ShutdownRunnerAsync();
            fusionLobbyService?.ForceResetRuntimeState("ShutdownSession");
            Complete(true, "Fusion runner stopped.");
        }
        catch (Exception ex)
        {
            Complete(false, "Shutdown failed: " + ex.Message);
        }
        finally
        {
            IsShuttingDown = false;
        }
    }

    public async void ShutdownSessionAndReturnToMenu(bool clearPreparedSession = true)
    {
        ResolveDependencies();

        if (IsShuttingDown)
            return;

        IsBusy = true;
        IsShuttingDown = true;

        try
        {
            if (runnerManager != null)
                await runnerManager.ShutdownRunnerAsync();

            fusionLobbyService?.ForceResetRuntimeState("ReturnToMenu");

            if (clearPreparedSession && onlineMatchSession != null)
                onlineMatchSession.ClearPreparedSession();

            ForceResetLocalState("Before loading main menu");

            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
            Complete(true, "Fusion runner stopped and returned to menu.");
        }
        catch (Exception ex)
        {
            Complete(false, "Shutdown and return failed: " + ex.Message);
        }
        finally
        {
            IsShuttingDown = false;
        }
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

        if (fusionLobbyService == null)
            fusionLobbyService = FusionLobbyService.Instance;

#if UNITY_2023_1_OR_NEWER
        if (runnerManager == null)
            runnerManager = FindFirstObjectByType<PhotonFusionRunnerManager>();

        if (onlineMatchSession == null)
            onlineMatchSession = FindFirstObjectByType<OnlineMatchSession>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindFirstObjectByType<MatchRuntimeConfig>();

        if (fusionLobbyService == null)
            fusionLobbyService = FindFirstObjectByType<FusionLobbyService>();
#else
        if (runnerManager == null)
            runnerManager = FindObjectOfType<PhotonFusionRunnerManager>();

        if (onlineMatchSession == null)
            onlineMatchSession = FindObjectOfType<OnlineMatchSession>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindObjectOfType<MatchRuntimeConfig>();

        if (fusionLobbyService == null)
            fusionLobbyService = FindObjectOfType<FusionLobbyService>();
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