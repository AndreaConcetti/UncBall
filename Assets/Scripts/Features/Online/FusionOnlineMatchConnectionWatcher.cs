using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class FusionOnlineMatchConnectionWatcher : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController matchController;
    [SerializeField] private NetworkRunner boundRunner;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private NetworkRunner subscribedRunner;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        TryBindRunner();
    }

    private void Update()
    {
        ResolveReferences();

        if (subscribedRunner == null || !subscribedRunner || !subscribedRunner.IsRunning)
            TryBindRunner();
    }

    private void OnDisable()
    {
        UnbindRunner();
    }

    private void OnDestroy()
    {
        UnbindRunner();
    }

    private void ResolveReferences()
    {
        if (matchController == null)
        {
#if UNITY_2023_1_OR_NEWER
            matchController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
            matchController = FindObjectOfType<FusionOnlineMatchController>();
#endif
        }

        if (boundRunner == null)
        {
#if UNITY_2023_1_OR_NEWER
            boundRunner = FindFirstObjectByType<NetworkRunner>();
#else
            boundRunner = FindObjectOfType<NetworkRunner>();
#endif
        }
    }

    private void TryBindRunner()
    {
        if (boundRunner == null)
        {
#if UNITY_2023_1_OR_NEWER
            boundRunner = FindFirstObjectByType<NetworkRunner>();
#else
            boundRunner = FindObjectOfType<NetworkRunner>();
#endif
        }

        if (boundRunner == null)
            return;

        if (subscribedRunner == boundRunner)
            return;

        UnbindRunner();

        subscribedRunner = boundRunner;
        subscribedRunner.AddCallbacks(this);

        if (logDebug)
            Debug.Log("[FusionOnlineMatchConnectionWatcher] Bound directly to NetworkRunner callbacks.", this);
    }

    private void UnbindRunner()
    {
        if (subscribedRunner == null)
            return;

        subscribedRunner.RemoveCallbacks(this);
        subscribedRunner = null;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        ResolveReferences();

        if (matchController == null)
            return;

        if (logDebug)
            Debug.LogWarning("[FusionOnlineMatchConnectionWatcher] OnPlayerLeft -> " + player, this);

        matchController.NotifyRunnerPlayerLeft(player);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        ResolveReferences();

        if (matchController == null)
            return;

        if (logDebug)
            Debug.LogWarning("[FusionOnlineMatchConnectionWatcher] OnShutdown -> " + shutdownReason, this);

        matchController.NotifyRunnerShutdown(shutdownReason);
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        ResolveReferences();

        if (matchController == null)
            return;

        if (logDebug)
            Debug.LogWarning("[FusionOnlineMatchConnectionWatcher] OnDisconnectedFromServer -> " + reason, this);

        bool localNetworkUnavailable = Application.internetReachability == NetworkReachability.NotReachable;

        if (localNetworkUnavailable)
            matchController.NotifyLocalDisconnectedFromSession();
        else
            matchController.NotifyRemoteAuthorityLostAsClient();
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        ResolveReferences();

        if (matchController == null)
            return;

        matchController.NotifyLocalReconnectedToSession();
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
}