using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class FusionConnectionMetadataListener : MonoBehaviour, INetworkRunnerCallbacks
{
    public static FusionConnectionMetadataListener Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private NetworkRunner boundRunner;
    private bool callbacksRegistered;

    private OnlinePlayerMatchStatsSnapshot latestJoinerSnapshot;

    public bool TryGetLatestJoinerSnapshot(out OnlinePlayerMatchStatsSnapshot snapshot)
    {
        snapshot = latestJoinerSnapshot;
        return snapshot != null && snapshot.IsValid;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameObject duplicateRoot = transform.root != null ? transform.root.gameObject : gameObject;
            Destroy(duplicateRoot);
            return;
        }

        Instance = this;

        GameObject persistentRoot = transform.root != null ? transform.root.gameObject : gameObject;
        DontDestroyOnLoad(persistentRoot);

        ClearCachedData();
    }

    private void Update()
    {
        TryBindToActiveRunner();
    }

    private void OnDestroy()
    {
        UnbindRunner();

        if (Instance == this)
            Instance = null;
    }

    private void TryBindToActiveRunner()
    {
        NetworkRunner activeRunner = FindAnyObjectByType<NetworkRunner>();
        if (activeRunner == null)
            return;

        if (boundRunner == activeRunner && callbacksRegistered)
            return;

        UnbindRunner();

        boundRunner = activeRunner;
        boundRunner.AddCallbacks(this);
        callbacksRegistered = true;
        ClearCachedData();

        if (logDebug)
            Debug.Log("[FusionConnectionMetadataListener] Bound to runner.", this);
    }

    private void UnbindRunner()
    {
        if (boundRunner != null && callbacksRegistered)
        {
            try
            {
                boundRunner.RemoveCallbacks(this);
            }
            catch
            {
            }
        }

        boundRunner = null;
        callbacksRegistered = false;
    }

    private void ClearCachedData()
    {
        latestJoinerSnapshot = null;
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        if (runner == null || !runner.IsServer)
            return;

        if (!OnlinePlayerTokenCodec.TryDecode(token, out OnlinePlayerMatchStatsSnapshot snapshot))
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[FusionConnectionMetadataListener] Join token decode failed. " +
                    "TokenLength=" + (token != null ? token.Length : 0),
                    this);
            }

            return;
        }

        if (snapshot == null || !snapshot.IsValid)
        {
            if (logDebug)
                Debug.LogWarning("[FusionConnectionMetadataListener] Decoded join token but snapshot is invalid.", this);

            return;
        }

        snapshot.Normalize();
        latestJoinerSnapshot = snapshot;

        if (logDebug)
        {
            Debug.Log(
                "[FusionConnectionMetadataListener] Join token received -> " +
                "Name=" + snapshot.displayName +
                " | Level=" + snapshot.level +
                " | Matches=" + snapshot.totalMatches +
                " | W=" + snapshot.totalWins +
                " | L=" + snapshot.totalLosses +
                " | WR=" + snapshot.winRatePercent + "%",
                this
            );
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        ClearCachedData();
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        ClearCachedData();
    }

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}