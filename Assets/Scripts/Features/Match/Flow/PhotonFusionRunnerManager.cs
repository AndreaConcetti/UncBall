using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonFusionRunnerManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static PhotonFusionRunnerManager Instance { get; private set; }

    [Header("Persistence")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Runner")]
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private string runnerObjectName = "FusionNetworkRunner";
    [SerializeField] private bool provideInput = false;

    [Header("Lobby")]
    [SerializeField] private string privateLobbyName = "uncball_private_lobby";
    [SerializeField] private string matchmakingLobbyName = "uncball_matchmaking_lobby";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private NetworkRunner activeRunner;
    private bool callbacksRegistered = false;
    private bool shutdownInProgress = false;
    private Task currentShutdownTask;

    public NetworkRunner ActiveRunner => activeRunner;
    public bool HasActiveRunner => activeRunner != null;
    public bool IsRunning => activeRunner != null && activeRunner.IsRunning;
    public bool IsShutdownInProgress => shutdownInProgress;
    public string PrivateLobbyName => privateLobbyName;
    public string MatchmakingLobbyName => matchmakingLobbyName;

    public event Action OnConnectedToServerEvent;
    public event Action OnDisconnectedFromServerEvent;
    public event Action<PlayerRef> OnPlayerJoinedEvent;
    public event Action<PlayerRef> OnPlayerLeftEvent;
    public event Action<ShutdownReason> OnShutdownEvent;
    public event Action<NetworkRunner, List<SessionInfo>> OnSessionListUpdatedEvent;
    public event Action OnSceneLoadDoneEvent;
    public event Action OnSceneLoadStartEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            GameObject runtimeRoot = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(runtimeRoot);
        }
    }

    public async Task<bool> StartHostLobbyAsync(
        string sessionName,
        int maxPlayers = 2,
        Dictionary<string, SessionProperty> sessionProperties = null,
        string customLobbyNameOverride = null)
    {
        SceneRef currentScene = GetActiveSceneRef();
        if (!currentScene.IsValid)
        {
            Debug.LogError("[PhotonFusionRunnerManager] Current active scene is not valid for Fusion lobby start.", this);
            return false;
        }

        return await StartGameInternalAsync(
            GameMode.Host,
            sessionName,
            currentScene,
            maxPlayers,
            sessionProperties,
            string.IsNullOrWhiteSpace(customLobbyNameOverride) ? privateLobbyName : customLobbyNameOverride,
            connectionToken: null
        );
    }

    public async Task<bool> StartClientLobbyAsync(
        string sessionName,
        int maxPlayers = 2,
        string customLobbyNameOverride = null,
        byte[] connectionToken = null)
    {
        SceneRef currentScene = GetActiveSceneRef();
        if (!currentScene.IsValid)
        {
            Debug.LogError("[PhotonFusionRunnerManager] Current active scene is not valid for Fusion lobby join.", this);
            return false;
        }

        return await StartGameInternalAsync(
            GameMode.Client,
            sessionName,
            currentScene,
            maxPlayers,
            null,
            string.IsNullOrWhiteSpace(customLobbyNameOverride) ? privateLobbyName : customLobbyNameOverride,
            connectionToken
        );
    }

    public async Task<bool> StartMatchmakingAsync(
        string bucketSessionName,
        Dictionary<string, SessionProperty> matchmakingProperties,
        int maxPlayers = 2,
        string customLobbyNameOverride = null,
        byte[] connectionToken = null)
    {
        SceneRef currentScene = GetActiveSceneRef();
        if (!currentScene.IsValid)
        {
            Debug.LogError("[PhotonFusionRunnerManager] Current active scene is not valid for Fusion matchmaking.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(bucketSessionName))
        {
            Debug.LogError("[PhotonFusionRunnerManager] Matchmaking bucket session name is empty.", this);
            return false;
        }

        return await StartGameInternalAsync(
            GameMode.AutoHostOrClient,
            bucketSessionName,
            currentScene,
            maxPlayers,
            matchmakingProperties,
            string.IsNullOrWhiteSpace(customLobbyNameOverride) ? matchmakingLobbyName : customLobbyNameOverride,
            connectionToken
        );
    }

    public async Task ShutdownRunnerAsync()
    {
        ForceCleanupStaleRunnerIfNeeded();

        if (activeRunner == null)
            return;

        if (shutdownInProgress)
        {
            if (currentShutdownTask != null)
                await currentShutdownTask;

            return;
        }

        shutdownInProgress = true;
        NetworkRunner runnerToShutdown = activeRunner;

        if (logDebug)
            Debug.Log("[PhotonFusionRunnerManager] ShutdownRunnerAsync called.", this);

        currentShutdownTask = InternalShutdownRunnerAsync(runnerToShutdown);
        await currentShutdownTask;

        currentShutdownTask = null;
        shutdownInProgress = false;

        ForceCleanupStaleRunnerIfNeeded();
    }

    private async Task InternalShutdownRunnerAsync(NetworkRunner runnerToShutdown)
    {
        if (runnerToShutdown == null)
            return;

        try
        {
            await runnerToShutdown.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PhotonFusionRunnerManager] Exception during ShutdownRunnerAsync: " + ex.Message, this);
        }

        CleanupRunnerReference(runnerToShutdown);
    }

    public bool LoadNetworkScene(string sceneName)
    {
        ForceCleanupStaleRunnerIfNeeded();

        if (activeRunner == null || !activeRunner.IsRunning)
        {
            Debug.LogError("[PhotonFusionRunnerManager] Cannot load network scene. Runner missing or not running.", this);
            return false;
        }

        SceneRef sceneRef = ResolveSceneRefByName(sceneName);
        if (!sceneRef.IsValid)
        {
            Debug.LogError("[PhotonFusionRunnerManager] Scene not found in Build Settings: " + sceneName, this);
            return false;
        }

        if (!activeRunner.IsSceneAuthority)
        {
            Debug.LogWarning("[PhotonFusionRunnerManager] Current peer is not scene authority. Scene load ignored.", this);
            return false;
        }

        if (logDebug)
            Debug.Log("[PhotonFusionRunnerManager] Loading network scene -> " + sceneName, this);

        activeRunner.LoadScene(sceneRef, LoadSceneMode.Single);
        return true;
    }

    public int GetCurrentPlayerCount()
    {
        ForceCleanupStaleRunnerIfNeeded();

        if (activeRunner == null || !activeRunner.IsRunning)
            return 0;

        int count = 0;
        foreach (PlayerRef player in activeRunner.ActivePlayers)
            count++;

        return count;
    }

    public string GetCurrentSessionName()
    {
        ForceCleanupStaleRunnerIfNeeded();

        if (activeRunner == null || activeRunner.SessionInfo == null)
            return string.Empty;

        return activeRunner.SessionInfo.Name;
    }

    public bool IsCurrentRunnerServer()
    {
        ForceCleanupStaleRunnerIfNeeded();
        return activeRunner != null && activeRunner.IsRunning && activeRunner.IsServer;
    }

    public bool IsCurrentRunnerClientOnly()
    {
        ForceCleanupStaleRunnerIfNeeded();
        return activeRunner != null && activeRunner.IsRunning && activeRunner.IsClient && !activeRunner.IsServer;
    }

    public bool TryGetCurrentSessionProperty(string key, out SessionProperty value)
    {
        value = default;

        ForceCleanupStaleRunnerIfNeeded();

        if (activeRunner == null || activeRunner.SessionInfo == null || !activeRunner.SessionInfo.IsValid)
            return false;

        ReadOnlyDictionary<string, SessionProperty> props = activeRunner.SessionInfo.Properties;
        if (props == null)
            return false;

        return props.TryGetValue(key, out value);
    }

    public bool TryUpdateSessionProperties(Dictionary<string, SessionProperty> properties)
    {
        ForceCleanupStaleRunnerIfNeeded();

        if (activeRunner == null || activeRunner.SessionInfo == null || !activeRunner.SessionInfo.IsValid)
            return false;

        try
        {
            return activeRunner.SessionInfo.UpdateCustomProperties(properties);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PhotonFusionRunnerManager] Failed to update session properties: " + ex.Message, this);
            return false;
        }
    }

    public byte[] GetPlayerConnectionToken(PlayerRef player)
    {
        ForceCleanupStaleRunnerIfNeeded();

        if (activeRunner == null || !activeRunner.IsRunning)
            return null;

        try
        {
            return activeRunner.GetPlayerConnectionToken(player);
        }
        catch
        {
            return null;
        }
    }

    public void ForceCleanupStaleRunnerIfNeeded()
    {
        if (activeRunner == null)
            return;

        bool shouldCleanup = false;

        try
        {
            if (!activeRunner.IsRunning)
                shouldCleanup = true;
        }
        catch
        {
            shouldCleanup = true;
        }

        if (!shouldCleanup)
            return;

        if (logDebug)
            Debug.Log("[PhotonFusionRunnerManager] ForceCleanupStaleRunnerIfNeeded -> cleaning stale runner.", this);

        CleanupRunnerReference(activeRunner);
        shutdownInProgress = false;
        currentShutdownTask = null;
    }

    private async Task<bool> StartGameInternalAsync(
        GameMode gameMode,
        string sessionName,
        SceneRef sceneRef,
        int maxPlayers,
        Dictionary<string, SessionProperty> sessionProperties,
        string customLobbyName,
        byte[] connectionToken)
    {
        if (!sceneRef.IsValid)
        {
            Debug.LogError("[PhotonFusionRunnerManager] SceneRef is invalid.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sessionName))
        {
            Debug.LogError("[PhotonFusionRunnerManager] Session name is empty.", this);
            return false;
        }

        ForceCleanupStaleRunnerIfNeeded();

        if (shutdownInProgress && currentShutdownTask != null)
            await currentShutdownTask;

        ForceCleanupStaleRunnerIfNeeded();

        if (activeRunner != null)
        {
            if (logDebug)
                Debug.Log("[PhotonFusionRunnerManager] Existing runner found. Shutting down before restart.", this);

            await ShutdownRunnerAsync();
        }

        ForceCleanupStaleRunnerIfNeeded();

        activeRunner = CreateRunnerInstance();
        RegisterCallbacks();

        NetworkSceneManagerDefault sceneManager = activeRunner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
            sceneManager = activeRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        StartGameArgs args = new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = sessionName,
            Scene = sceneRef,
            SceneManager = sceneManager,
            PlayerCount = Mathf.Max(2, maxPlayers),
            SessionProperties = sessionProperties,
            CustomLobbyName = customLobbyName,
            ConnectionToken = connectionToken
        };

        if (logDebug)
        {
            Debug.Log(
                "[PhotonFusionRunnerManager] Starting Fusion game. " +
                "Mode=" + gameMode +
                " | SessionName=" + sessionName +
                " | SceneRef=" + sceneRef +
                " | MaxPlayers=" + maxPlayers +
                " | CustomLobbyName=" + customLobbyName,
                this
            );
        }

        StartGameResult result = await activeRunner.StartGame(args);

        if (!result.Ok)
        {
            Debug.LogError(
                "[PhotonFusionRunnerManager] StartGame failed. ShutdownReason=" + result.ShutdownReason,
                this
            );

            CleanupRunnerReference(activeRunner);
            shutdownInProgress = false;
            currentShutdownTask = null;
            return false;
        }

        return true;
    }

    private NetworkRunner CreateRunnerInstance()
    {
        NetworkRunner runnerInstance;

        if (runnerPrefab != null)
        {
            runnerInstance = Instantiate(runnerPrefab);
        }
        else
        {
            GameObject go = new GameObject(runnerObjectName);
            runnerInstance = go.AddComponent<NetworkRunner>();
            go.AddComponent<NetworkSceneManagerDefault>();
        }

        runnerInstance.name = runnerObjectName;
        runnerInstance.ProvideInput = provideInput;

        if (runnerInstance.GetComponent<NetworkSceneManagerDefault>() == null)
            runnerInstance.gameObject.AddComponent<NetworkSceneManagerDefault>();

        return runnerInstance;
    }

    private void RegisterCallbacks()
    {
        if (activeRunner == null || callbacksRegistered)
            return;

        activeRunner.AddCallbacks(this);
        callbacksRegistered = true;
    }

    private void CleanupRunnerReference(NetworkRunner runnerToCleanup)
    {
        if (runnerToCleanup == null)
            return;

        if (activeRunner != runnerToCleanup)
            return;

        if (callbacksRegistered)
        {
            try
            {
                activeRunner.RemoveCallbacks(this);
            }
            catch
            {
            }

            callbacksRegistered = false;
        }

        if (activeRunner != null && activeRunner.gameObject != null)
            Destroy(activeRunner.gameObject);

        activeRunner = null;
    }

    private SceneRef GetActiveSceneRef()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return default;

        int buildIndex = activeScene.buildIndex;
        if (buildIndex < 0)
            return default;

        return SceneRef.FromIndex(buildIndex);
    }

    private SceneRef ResolveSceneRefByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return default;

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(name, sceneName, StringComparison.Ordinal))
                return SceneRef.FromIndex(i);
        }

        return default;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        if (logDebug)
            Debug.Log("[PhotonFusionRunnerManager] Connected to server.", this);

        OnConnectedToServerEvent?.Invoke();
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        if (logDebug)
            Debug.LogWarning("[PhotonFusionRunnerManager] Disconnected from server. Reason=" + reason, this);

        OnDisconnectedFromServerEvent?.Invoke();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (logDebug)
            Debug.Log("[PhotonFusionRunnerManager] Player joined: " + player, this);

        OnPlayerJoinedEvent?.Invoke(player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (logDebug)
            Debug.Log("[PhotonFusionRunnerManager] Player left: " + player, this);

        OnPlayerLeftEvent?.Invoke(player);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (logDebug)
            Debug.LogWarning("[PhotonFusionRunnerManager] Shutdown: " + shutdownReason, this);

        OnShutdownEvent?.Invoke(shutdownReason);
        CleanupRunnerReference(runner);
        shutdownInProgress = false;
        currentShutdownTask = null;
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        OnSessionListUpdatedEvent?.Invoke(runner, sessionList);
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (logDebug)
            Debug.Log("[PhotonFusionRunnerManager] Scene load done.", this);

        OnSceneLoadDoneEvent?.Invoke();
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        if (logDebug)
            Debug.Log("[PhotonFusionRunnerManager] Scene load start.", this);

        OnSceneLoadStartEvent?.Invoke();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}