using Fusion;
using UnityEngine;

public class FusionOnlineMatchController : NetworkBehaviour
{
    [Header("Resolved Scene References")]
    [SerializeField] private BallTurnSpawner ballTurnSpawner;
    [SerializeField] private OnlineGameplayAuthority onlineGameplayAuthority;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private FusionOnlineMatchHUD hud;

    [Header("Online Rules")]
    [SerializeField] private float defaultTurnDuration = 15f;
    [SerializeField] private float defaultMatchDuration = 360f;
    [SerializeField] private bool enableStuckBallCheck = true;
    [SerializeField] private float stuckTimeout = 2.5f;
    [SerializeField] private float stuckVelocityThreshold = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    [Networked] private byte NetCurrentTurnOwnerRaw { get; set; }
    [Networked] private float NetTurnTimeRemaining { get; set; }
    [Networked] private float NetMatchTimeRemaining { get; set; }
    [Networked] private int NetScoreP1 { get; set; }
    [Networked] private int NetScoreP2 { get; set; }
    [Networked] private NetworkBool NetMatchStarted { get; set; }
    [Networked] private NetworkBool NetMatchEnded { get; set; }
    [Networked] private NetworkId NetActiveBallId { get; set; }

    private BallPhysics watchedBall;
    private Rigidbody watchedBallRb;
    private float stuckTimer;
    private bool currentPlayerScoredThisTurn;
    private bool subscribedToScore;

    public PlayerID CurrentTurnOwner => (PlayerID)NetCurrentTurnOwnerRaw;
    public float CurrentTurnTimeRemaining => NetTurnTimeRemaining;
    public float CurrentMatchTimeRemaining => NetMatchTimeRemaining;
    public int ScoreP1 => NetScoreP1;
    public int ScoreP2 => NetScoreP2;
    public bool MatchStarted => NetMatchStarted;
    public bool MatchEnded => NetMatchEnded;

    public PlayerID EffectiveLocalPlayerId
    {
        get
        {
            if (Runner != null)
                return Runner.IsServer ? PlayerID.Player1 : PlayerID.Player2;

            if (onlineGameplayAuthority != null)
                return onlineGameplayAuthority.LocalPlayerId;

            return PlayerID.Player1;
        }
    }

    public PlayerID EffectiveRemotePlayerId
    {
        get
        {
            return EffectiveLocalPlayerId == PlayerID.Player1
                ? PlayerID.Player2
                : PlayerID.Player1;
        }
    }

    public BallPhysics CurrentBall
    {
        get
        {
            if (Runner == null || NetActiveBallId == default)
                return null;

            if (Runner.TryFindObject(NetActiveBallId, out NetworkObject obj))
                return obj != null ? obj.GetComponent<BallPhysics>() : null;

            return null;
        }
    }

    public override void Spawned()
    {
        ResolveSceneReferences();
        ResolveAndApplyLocalIdentity();
        RegisterLocalAuthority();
        SubscribeToScoreIfNeeded();

        if (Object.HasStateAuthority)
            InitializeAuthoritativeMatch();

        ApplyReplicaToLocalSystems();

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchController] Spawned -> " +
                "HasStateAuthority=" + Object.HasStateAuthority +
                " | EffectiveLocalPlayer=" + EffectiveLocalPlayerId,
                this
            );
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnsubscribeFromScore();
    }

    public override void FixedUpdateNetwork()
    {
        ResolveSceneReferences();
        ResolveAndApplyLocalIdentity();

        if (Object.HasStateAuthority)
            TickAuthority();

        ApplyReplicaToLocalSystems();
    }

    private void ResolveSceneReferences()
    {
        if (ballTurnSpawner == null)
        {
#if UNITY_2023_1_OR_NEWER
            ballTurnSpawner = FindFirstObjectByType<BallTurnSpawner>();
#else
            ballTurnSpawner = FindObjectOfType<BallTurnSpawner>();
#endif
        }

        if (onlineGameplayAuthority == null)
        {
#if UNITY_2023_1_OR_NEWER
            onlineGameplayAuthority = FindFirstObjectByType<OnlineGameplayAuthority>();
#else
            onlineGameplayAuthority = FindObjectOfType<OnlineGameplayAuthority>();
#endif
        }

        if (scoreManager == null)
            scoreManager = ScoreManager.Instance;

        if (hud == null)
        {
#if UNITY_2023_1_OR_NEWER
            hud = FindFirstObjectByType<FusionOnlineMatchHUD>();
#else
            hud = FindObjectOfType<FusionOnlineMatchHUD>();
#endif
        }
    }

    private void ResolveAndApplyLocalIdentity()
    {
        if (onlineGameplayAuthority == null)
            return;

        PlayerID localPlayer = EffectiveLocalPlayerId;
        PlayerID remotePlayer = EffectiveRemotePlayerId;

        string p1Name = "Player 1";
        string p2Name = "Player 2";

        OnlineMatchSession session = OnlineMatchSession.Instance;
        if (session != null && session.HasPreparedSession)
        {
            p1Name = session.CurrentSession.hostDisplayName;
            p2Name = session.CurrentSession.joinDisplayName;
        }
        else
        {
            MatchRuntimeConfig runtimeConfig = MatchRuntimeConfig.Instance;
            if (runtimeConfig != null)
            {
                p1Name = runtimeConfig.SelectedPlayer1Name;
                p2Name = runtimeConfig.SelectedPlayer2Name;
            }
        }

        string localName = localPlayer == PlayerID.Player1 ? p1Name : p2Name;
        string remoteName = localPlayer == PlayerID.Player1 ? p2Name : p1Name;

        onlineGameplayAuthority.SetResolvedLocalPlayer(localPlayer, remotePlayer, localName, remoteName);
        onlineGameplayAuthority.SetStateAuthority(Object != null && Object.HasStateAuthority);
    }

    private void RegisterLocalAuthority()
    {
        if (onlineGameplayAuthority != null)
        {
            onlineGameplayAuthority.SetOnlineMatchController(this);
            onlineGameplayAuthority.SetStateAuthority(Object != null && Object.HasStateAuthority);
        }
    }

    private void SubscribeToScoreIfNeeded()
    {
        if (subscribedToScore || scoreManager == null)
            return;

        scoreManager.onPointsScored.AddListener(OnScoreManagerPointsScored);
        subscribedToScore = true;
    }

    private void UnsubscribeFromScore()
    {
        if (!subscribedToScore || scoreManager == null)
            return;

        scoreManager.onPointsScored.RemoveListener(OnScoreManagerPointsScored);
        subscribedToScore = false;
    }

    private void InitializeAuthoritativeMatch()
    {
        float resolvedMatchDuration = defaultMatchDuration;

        MatchRuntimeConfig runtimeConfig = MatchRuntimeConfig.Instance;
        if (runtimeConfig != null)
            resolvedMatchDuration = Mathf.Max(1f, runtimeConfig.SelectedMatchDuration);

        NetCurrentTurnOwnerRaw = (byte)PlayerID.Player1;
        NetTurnTimeRemaining = defaultTurnDuration;
        NetMatchTimeRemaining = resolvedMatchDuration;
        NetScoreP1 = 0;
        NetScoreP2 = 0;
        NetMatchStarted = true;
        NetMatchEnded = false;
        NetActiveBallId = default;

        if (scoreManager != null)
            scoreManager.StartMatch();

        SpawnNewActiveBallAuthority(PlayerID.Player1);

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchController] InitializeAuthoritativeMatch -> " +
                "MatchDuration=" + resolvedMatchDuration +
                " | TurnDuration=" + defaultTurnDuration,
                this
            );
        }
    }

    private void TickAuthority()
    {
        if (!NetMatchStarted || NetMatchEnded)
            return;

        NetTurnTimeRemaining -= Runner.DeltaTime;
        NetMatchTimeRemaining -= Runner.DeltaTime;

        if (NetTurnTimeRemaining < 0f)
            NetTurnTimeRemaining = 0f;

        if (NetMatchTimeRemaining < 0f)
            NetMatchTimeRemaining = 0f;

        RefreshWatchedBallCache();

        if (NetMatchTimeRemaining <= 0f)
        {
            EndMatchAuthority();
            return;
        }

        if (NetTurnTimeRemaining <= 0f)
        {
            ResolveMissAndAdvanceAuthority();
            return;
        }

        if (enableStuckBallCheck && watchedBall != null && watchedBallRb != null && !watchedBallRb.isKinematic)
        {
            if (watchedBallRb.linearVelocity.magnitude <= stuckVelocityThreshold)
            {
                stuckTimer += Runner.DeltaTime;

                if (stuckTimer >= stuckTimeout)
                {
                    if (currentPlayerScoredThisTurn)
                        ResolveSuccessfulShotAndContinueAuthority();
                    else
                        ResolveMissAndAdvanceAuthority();
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
    }

    private void RefreshWatchedBallCache()
    {
        BallPhysics current = CurrentBall;
        watchedBall = current;
        watchedBallRb = watchedBall != null ? watchedBall.GetComponent<Rigidbody>() : null;
    }

    private void ApplyReplicaToLocalSystems()
    {
        if (onlineGameplayAuthority != null)
        {
            onlineGameplayAuthority.SetCurrentTurnOwner(CurrentTurnOwner);
            onlineGameplayAuthority.SetStateAuthority(Object != null && Object.HasStateAuthority);
            onlineGameplayAuthority.SetOnlineMatchController(this);
        }

        if (scoreManager != null)
            scoreManager.SetReplicatedScores(NetScoreP1, NetScoreP2);

        if (ballTurnSpawner != null)
        {
            ballTurnSpawner.TryBindCurrentOnlineBallForLocalControl(CurrentBall, CurrentTurnOwner);
            ballTurnSpawner.ClearOnlineLauncherBindingIfNeeded(CurrentTurnOwner);
        }

        if (hud != null)
        {
            hud.ApplyState(
                CurrentTurnOwner,
                NetTurnTimeRemaining,
                NetMatchTimeRemaining,
                NetScoreP1,
                NetScoreP2,
                NetMatchStarted,
                NetMatchEnded
            );
        }
    }

    private void SpawnNewActiveBallAuthority(PlayerID owner)
    {
        if (!Object.HasStateAuthority || ballTurnSpawner == null)
            return;

        BallPhysics spawned = ballTurnSpawner.SpawnOnlineBallForOwner(owner);
        if (spawned == null)
        {
            Debug.LogError("[FusionOnlineMatchController] Failed spawning online ball for owner " + owner, this);
            return;
        }

        NetworkObject netObj = spawned.GetComponent<NetworkObject>();
        NetActiveBallId = netObj != null ? netObj.Id : default;

        watchedBall = spawned;
        watchedBallRb = spawned.GetComponent<Rigidbody>();
        stuckTimer = 0f;
        currentPlayerScoredThisTurn = false;

        NetCurrentTurnOwnerRaw = (byte)owner;
        NetTurnTimeRemaining = defaultTurnDuration;

        if (logDebug)
            Debug.Log("[FusionOnlineMatchController] SpawnNewActiveBallAuthority -> " + owner, this);
    }

    private void DespawnActiveBallAuthority()
    {
        BallPhysics current = CurrentBall;
        if (current == null)
        {
            NetActiveBallId = default;
            watchedBall = null;
            watchedBallRb = null;
            stuckTimer = 0f;
            return;
        }

        NetworkObject netObj = current.GetComponent<NetworkObject>();
        if (netObj != null && Runner != null && Runner.IsServer)
            Runner.Despawn(netObj);

        NetActiveBallId = default;
        watchedBall = null;
        watchedBallRb = null;
        stuckTimer = 0f;
    }

    private void FreezeCurrentBallInPlaceAuthority()
    {
        BallPhysics current = CurrentBall;
        if (current == null)
            return;

        Rigidbody rb = current.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        rb.isKinematic = true;
    }

    public void RequestLaunchCurrentBall(Vector3 direction, float force)
    {
        if (!NetMatchStarted || NetMatchEnded)
            return;

        if (Object.HasStateAuthority)
        {
            ApplyLaunchAuthority(CurrentTurnOwner, direction, force);
            return;
        }

        RPC_RequestLaunch(direction, force);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestLaunch(Vector3 direction, float force, RpcInfo info = default)
    {
        PlayerID requester = EffectiveRemotePlayerId;

        if (Runner != null && Runner.IsServer)
        {
            requester = info.Source == Runner.LocalPlayer
                ? EffectiveLocalPlayerId
                : EffectiveRemotePlayerId;
        }

        ApplyLaunchAuthority(requester, direction, force);
    }

    private void ApplyLaunchAuthority(PlayerID requester, Vector3 direction, float force)
    {
        if (!Object.HasStateAuthority)
            return;

        if (requester != CurrentTurnOwner)
        {
            if (logDebug)
                Debug.LogWarning("[FusionOnlineMatchController] Launch rejected for requester " + requester, this);

            return;
        }

        BallPhysics ball = CurrentBall;
        if (ball == null)
            return;

        BallOwnership ownership = ball.GetComponent<BallOwnership>();
        if (ownership == null || ownership.Owner != CurrentTurnOwner)
            return;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        Vector3 launchDirection = direction.normalized;
        launchDirection.y = 0f;
        force = Mathf.Max(0f, force);

        ball.Launch(launchDirection * force);

        watchedBall = ball;
        watchedBallRb = rb;
        stuckTimer = 0f;

        if (logDebug)
        {
            Debug.Log(
                "[FusionOnlineMatchController] Launch accepted -> Owner=" + requester +
                " | Force=" + force,
                this
            );
        }
    }

    public void NotifyAuthoritativeBallLost(BallPhysics ball)
    {
        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        if (ball == null || ball != CurrentBall)
            return;

        ResolveMissAndAdvanceAuthority();
    }

    private void ResolveSuccessfulShotAndContinueAuthority()
    {
        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        FreezeCurrentBallInPlaceAuthority();

        PlayerID sameOwner = CurrentTurnOwner;

        watchedBall = null;
        watchedBallRb = null;
        stuckTimer = 0f;
        NetActiveBallId = default;

        SpawnNewActiveBallAuthority(sameOwner);
    }

    private void ResolveMissAndAdvanceAuthority()
    {
        if (!Object.HasStateAuthority || NetMatchEnded)
            return;

        DespawnActiveBallAuthority();

        PlayerID nextOwner = CurrentTurnOwner == PlayerID.Player1
            ? PlayerID.Player2
            : PlayerID.Player1;

        SpawnNewActiveBallAuthority(nextOwner);
    }

    private void EndMatchAuthority()
    {
        if (!Object.HasStateAuthority)
            return;

        NetMatchEnded = true;
        NetMatchStarted = false;
        DespawnActiveBallAuthority();

        if (logDebug)
            Debug.Log("[FusionOnlineMatchController] Match ended.", this);
    }

    private void OnScoreManagerPointsScored(PlayerID player, int newTotal)
    {
        if (!Object.HasStateAuthority || scoreManager == null)
            return;

        NetScoreP1 = scoreManager.ScoreP1;
        NetScoreP2 = scoreManager.ScoreP2;

        if (player == CurrentTurnOwner)
        {
            currentPlayerScoredThisTurn = true;

            if (logDebug)
            {
                Debug.Log(
                    "[FusionOnlineMatchController] Score registered -> " +
                    "Player=" + player +
                    " | ScoreP1=" + NetScoreP1 +
                    " | ScoreP2=" + NetScoreP2,
                    this
                );
            }
        }
    }
}