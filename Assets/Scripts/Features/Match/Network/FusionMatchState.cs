using Fusion;
using UnityEngine;

public class FusionMatchState : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private StartEndController startEndController;
    [SerializeField] private OnlineGameplayAuthority onlineGameplayAuthority;

    [Header("Networked")]
    [Networked] private int NetScoreP1 { get; set; }
    [Networked] private int NetScoreP2 { get; set; }
    [Networked] private byte NetCurrentTurnOwnerRaw { get; set; }
    [Networked] private NetworkBool NetMatchStarted { get; set; }
    [Networked] private NetworkBool NetMatchEnded { get; set; }

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool subscribed;

    public bool IsNetworkSpawnReady => Object != null && Runner != null;
    public PlayerID NetworkCurrentTurnOwner => (PlayerID)NetCurrentTurnOwnerRaw;

    public override void Spawned()
    {
        ResolveDependencies();
        SubscribeIfNeeded();

        if (Object != null && Object.HasStateAuthority)
        {
            NetScoreP1 = scoreManager != null ? scoreManager.ScoreP1 : 0;
            NetScoreP2 = scoreManager != null ? scoreManager.ScoreP2 : 0;
            NetCurrentTurnOwnerRaw = (byte)PlayerID.Player1;
            NetMatchStarted = startEndController != null && startEndController.IsMatchStarted();
            NetMatchEnded = startEndController != null && startEndController.IsMatchEnded();
        }

        ApplyReplicatedStateToLocalSystems();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unsubscribe();
    }

    public override void Render()
    {
        ResolveDependencies();

        if (!IsNetworkSpawnReady)
            return;

        ApplyReplicatedStateToLocalSystems();
    }

    private void ResolveDependencies()
    {
        if (scoreManager == null)
            scoreManager = ScoreManager.Instance;

        if (turnManager == null)
            turnManager = TurnManager.Instance;

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;

        if (startEndController == null)
            startEndController = StartEndController.InstanceOrFind();

#if UNITY_2023_1_OR_NEWER
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();

        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = FindFirstObjectByType<OnlineGameplayAuthority>();

        if (startEndController == null)
            startEndController = FindFirstObjectByType<StartEndController>();
#else
        if (scoreManager == null)
            scoreManager = FindObjectOfType<ScoreManager>();

        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = FindObjectOfType<OnlineGameplayAuthority>();

        if (startEndController == null)
            startEndController = FindObjectOfType<StartEndController>();
#endif
    }

    private void SubscribeIfNeeded()
    {
        if (subscribed || scoreManager == null)
            return;

        scoreManager.onPointsScored.AddListener(OnLocalPointsScored);
        scoreManager.onMatchEnd.AddListener(OnLocalMatchEnded);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (scoreManager != null)
        {
            scoreManager.onPointsScored.RemoveListener(OnLocalPointsScored);
            scoreManager.onMatchEnd.RemoveListener(OnLocalMatchEnded);
        }

        subscribed = false;
    }

    private void ApplyReplicatedStateToLocalSystems()
    {
        if (onlineGameplayAuthority != null)
        {
            onlineGameplayAuthority.SetStateAuthority(Object.HasStateAuthority);
            onlineGameplayAuthority.SetCurrentTurnOwner((PlayerID)NetCurrentTurnOwnerRaw);
        }

        if (scoreManager != null)
            scoreManager.SetReplicatedScores(NetScoreP1, NetScoreP2);
    }

    private void OnLocalPointsScored(PlayerID player, int newTotal)
    {
        if (!IsNetworkSpawnReady || !Object.HasStateAuthority || scoreManager == null)
            return;

        NetScoreP1 = scoreManager.ScoreP1;
        NetScoreP2 = scoreManager.ScoreP2;
    }

    private void OnLocalMatchEnded(PlayerID winner)
    {
        if (!IsNetworkSpawnReady || !Object.HasStateAuthority)
            return;

        NetMatchEnded = true;
    }

    public void PublishTurnOwner(PlayerID owner)
    {
        if (!IsNetworkSpawnReady || !Object.HasStateAuthority)
            return;

        NetCurrentTurnOwnerRaw = (byte)owner;
    }

    public void PublishMatchStarted(bool started)
    {
        if (!IsNetworkSpawnReady || !Object.HasStateAuthority)
            return;

        NetMatchStarted = started;
    }

    public void PublishMatchEnded(bool ended)
    {
        if (!IsNetworkSpawnReady || !Object.HasStateAuthority)
            return;

        NetMatchEnded = ended;
    }

    public void RequestLaunchCurrentBall(Vector3 direction, float force)
    {
        if (!IsNetworkSpawnReady)
            return;

        if (Object.HasStateAuthority)
        {
            ApplyLaunchInternal(GetExpectedTurnOwnerOnAuthority(), direction, force);
            return;
        }

        RPC_RequestLaunch(direction, force);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestLaunch(Vector3 direction, float force, RpcInfo info = default)
    {
        PlayerID sender = ResolvePlayerIdFromRpcInfo(info);
        ApplyLaunchInternal(sender, direction, force);
    }

    private PlayerID ResolvePlayerIdFromRpcInfo(RpcInfo info)
    {
        if (Runner == null)
            return PlayerID.None;

        if (turnManager == null)
            return PlayerID.None;

        if (info.Source == Runner.LocalPlayer)
            return onlineGameplayAuthority != null ? onlineGameplayAuthority.LocalPlayerId : PlayerID.Player1;

        return turnManager.GetRemotePlayerIdFallback();
    }

    private PlayerID GetExpectedTurnOwnerOnAuthority()
    {
        return (PlayerID)NetCurrentTurnOwnerRaw;
    }

    private void ApplyLaunchInternal(PlayerID requester, Vector3 direction, float force)
    {
        ResolveDependencies();

        if (!IsNetworkSpawnReady || !Object.HasStateAuthority || turnManager == null)
            return;

        PlayerID expectedOwner = (PlayerID)NetCurrentTurnOwnerRaw;
        if (requester != expectedOwner)
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[FusionMatchState] Launch rejected. Requester=" + requester +
                    " | Expected=" + expectedOwner,
                    this
                );
            }

            return;
        }

        if (turnManager.currentPlayer == null || turnManager.currentPlayer.ball == null)
            return;

        BallPhysics ball = turnManager.currentPlayer.ball;
        BallOwnership ownership = ball.GetComponent<BallOwnership>();

        if (ownership == null || ownership.Owner != expectedOwner)
        {
            if (logDebug)
            {
                Debug.LogWarning(
                    "[FusionMatchState] Launch rejected because current authoritative ball owner is invalid. " +
                    "ExpectedOwner=" + expectedOwner,
                    this
                );
            }

            return;
        }

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }

        Vector3 launchDirection = direction.normalized;
        launchDirection.y = 0f;
        force = Mathf.Max(0f, force);

        turnManager.NotifyBallLaunched(ball);
        ball.Launch(launchDirection * force);
        turnManager.PauseTimer();

        if (logDebug)
        {
            Debug.Log(
                "[FusionMatchState] Launch accepted -> Owner=" + expectedOwner +
                " | Force=" + force +
                " | Dir=" + launchDirection,
                this
            );
        }
    }
}