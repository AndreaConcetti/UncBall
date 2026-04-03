using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class FusionConnectionMetadataListener : MonoBehaviour, INetworkRunnerCallbacks
{
    public static FusionConnectionMetadataListener Instance { get; private set; }

    private const int ReliableChannelKind_HostSnapshot = 7001;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private NetworkRunner boundRunner;
    private bool callbacksRegistered;

    private PhotonFusionRunnerManager runnerManager;
    private bool runnerManagerSubscribed;

    private OnlinePlayerMatchStatsSnapshot latestJoinerSnapshot;
    private OnlinePlayerMatchStatsSnapshot latestHostSnapshot;

    private string lastSentHostSnapshotSessionName = string.Empty;
    private string lastReceivedHostSnapshotSessionName = string.Empty;

    public bool TryGetLatestJoinerSnapshot(out OnlinePlayerMatchStatsSnapshot snapshot)
    {
        snapshot = latestJoinerSnapshot;
        return snapshot != null && snapshot.IsValid;
    }

    public bool TryGetLatestHostSnapshot(out OnlinePlayerMatchStatsSnapshot snapshot)
    {
        snapshot = latestHostSnapshot;
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
        TryBindToRunnerManager();
        TrySendHostSnapshotIfPossible();
    }

    private void OnDestroy()
    {
        UnbindRunner();
        UnsubscribeRunnerManager();

        if (Instance == this)
            Instance = null;
    }

    private void TryBindToRunnerManager()
    {
        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

        if (runnerManager == null || runnerManagerSubscribed)
            return;

        runnerManager.OnReliableDataReceivedEvent += HandleReliableDataReceived;
        runnerManager.OnShutdownEvent += HandleRunnerShutdown;
        runnerManagerSubscribed = true;
    }

    private void UnsubscribeRunnerManager()
    {
        if (runnerManager != null && runnerManagerSubscribed)
        {
            runnerManager.OnReliableDataReceivedEvent -= HandleReliableDataReceived;
            runnerManager.OnShutdownEvent -= HandleRunnerShutdown;
        }

        runnerManagerSubscribed = false;
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
        latestHostSnapshot = null;
        lastSentHostSnapshotSessionName = string.Empty;
        lastReceivedHostSnapshotSessionName = string.Empty;
    }

    private void HandleRunnerShutdown(ShutdownReason _)
    {
        ClearCachedData();
    }

    private void TrySendHostSnapshotIfPossible()
    {
        if (runnerManager == null || !runnerManager.IsRunning || !runnerManager.IsCurrentRunnerServer())
            return;

        string sessionName = runnerManager.GetCurrentSessionName();
        if (string.IsNullOrWhiteSpace(sessionName))
            return;

        int playerCount = runnerManager.GetCurrentPlayerCount();
        if (playerCount < 2)
            return;

        if (string.Equals(lastSentHostSnapshotSessionName, sessionName, StringComparison.Ordinal))
            return;

        OnlinePlayerMatchStatsSnapshot hostSnapshot = BuildLocalSnapshot();
        if (hostSnapshot == null || !hostSnapshot.IsValid)
            return;

        byte[] payload = OnlinePlayerTokenCodec.Encode(hostSnapshot);
        if (payload == null || payload.Length == 0)
            return;

        NetworkRunner runner = runnerManager.ActiveRunner;
        if (runner == null)
            return;

        bool sentAtLeastOnce = false;

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (player == runner.LocalPlayer)
                continue;

            ReliableKey key = ReliableKey.FromInts(ReliableChannelKind_HostSnapshot, 0, 0, 0);
            bool ok = runnerManager.SendReliableDataToPlayer(player, key, payload);

            if (logDebug)
            {
                Debug.Log(
                    "[FusionConnectionMetadataListener] Send host snapshot -> " +
                    "Target=" + player +
                    " | Ok=" + ok +
                    " | Name=" + hostSnapshot.displayName +
                    " | Level=" + hostSnapshot.level +
                    " | W=" + hostSnapshot.totalWins +
                    " | L=" + hostSnapshot.totalLosses +
                    " | WR=" + hostSnapshot.winRatePercent + "%",
                    this
                );
            }

            sentAtLeastOnce |= ok;
        }

        if (sentAtLeastOnce)
            lastSentHostSnapshotSessionName = sessionName;
    }

    private OnlinePlayerMatchStatsSnapshot BuildLocalSnapshot()
    {
        OnlinePlayerIdentity identity = null;

        OnlineFlowController flow = OnlineFlowController.Instance;
        if (flow != null &&
            flow.RuntimeContext != null &&
            flow.RuntimeContext.currentAssignment != null)
        {
            identity = flow.RuntimeContext.currentAssignment.localPlayer;
        }

        OnlinePlayerMatchStatsSnapshot snapshot =
            OnlinePlayerStatsSnapshotFactory.BuildFromLocalProfile(identity);

        if (snapshot != null)
            snapshot.Normalize();

        return snapshot;
    }

    private void HandleReliableDataReceived(PlayerRef player, ReliableKey key, byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        key.GetInts(out int key0, out int key1, out int key2, out int key3);

        if (key0 != ReliableChannelKind_HostSnapshot)
            return;

        if (!OnlinePlayerTokenCodec.TryDecode(data, out OnlinePlayerMatchStatsSnapshot snapshot))
        {
            if (logDebug)
                Debug.LogWarning("[FusionConnectionMetadataListener] Reliable host snapshot decode failed.", this);

            return;
        }

        if (snapshot == null || !snapshot.IsValid)
            return;

        snapshot.Normalize();
        latestHostSnapshot = snapshot;

        if (runnerManager != null)
            lastReceivedHostSnapshotSessionName = runnerManager.GetCurrentSessionName();

        if (logDebug)
        {
            Debug.Log(
                "[FusionConnectionMetadataListener] Reliable host snapshot received -> " +
                "From=" + player +
                " | Name=" + snapshot.displayName +
                " | Level=" + snapshot.level +
                " | W=" + snapshot.totalWins +
                " | L=" + snapshot.totalLosses +
                " | WR=" + snapshot.winRatePercent + "%",
                this
            );
        }
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

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        string sessionName = runnerManager != null ? runnerManager.GetCurrentSessionName() : string.Empty;
        if (!string.IsNullOrWhiteSpace(sessionName) &&
            string.Equals(lastSentHostSnapshotSessionName, sessionName, StringComparison.Ordinal))
        {
            lastSentHostSnapshotSessionName = string.Empty;
        }
    }

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
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