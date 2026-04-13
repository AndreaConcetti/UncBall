using UnityEngine;

public sealed class OfflineBotMatchController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private BallTurnSpawner ballTurnSpawner;
    [SerializeField] private FusionOnlineMatchHUD hud;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BottomBarOrderSwapper bottomBarOrderSwapper;

    [Header("Shot Completion")]
    [SerializeField] private bool enableStuckBallCheck = true;
    [SerializeField] private float stuckTimeout = 2.5f;
    [SerializeField] private float stuckVelocityThreshold = 0.05f;

    [Header("Safety")]
    [SerializeField] private bool continueBootstrapIfScoreManagerThrows = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    [Header("Runtime")]
    [SerializeField] private bool isOfflineBotSessionActive;
    [SerializeField] private BotOfflineMatchRequest activeRequest;
    [SerializeField] private PlayerID localPlayerId = PlayerID.Player1;
    [SerializeField] private PlayerID botPlayerId = PlayerID.Player2;
    [SerializeField] private PlayerID currentTurnOwner = PlayerID.Player1;
    [SerializeField] private bool player1OnLeft = true;
    [SerializeField] private bool matchStarted;
    [SerializeField] private bool matchEnded;
    [SerializeField] private PlayerID winner = PlayerID.None;
    [SerializeField] private float turnTimeRemaining;
    [SerializeField] private float matchTimeRemaining;
    [SerializeField] private int scoreP1;
    [SerializeField] private int scoreP2;
    [SerializeField] private bool shotInFlight;
    [SerializeField] private bool currentPlayerScoredThisTurn;

    private BallPhysics currentBall;
    private Rigidbody currentBallRb;
    private float stuckTimer;
    private bool scoreSubscribed;

    public bool IsOfflineBotSessionActive => isOfflineBotSessionActive;
    public BotOfflineMatchRequest ActiveRequest => activeRequest;
    public PlayerID LocalPlayerId => localPlayerId;
    public PlayerID BotPlayerId => botPlayerId;
    public PlayerID CurrentTurnOwner => currentTurnOwner;
    public bool MatchStarted => matchStarted;
    public bool MatchEnded => matchEnded;
    public bool MidMatchBreakActive => false;
    public BallPhysics CurrentBall => currentBall;

    private void Awake()
    {
        ResolveReferences();
        SubscribeToScoreIfNeeded();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToScoreIfNeeded();
    }

    private void OnDisable()
    {
        UnsubscribeFromScore();
    }

    private void Update()
    {
        if (!isOfflineBotSessionActive || activeRequest == null)
            return;

        TickOfflineMatch();
        ApplyHudState();
    }

    public void ConfigureOfflineMatch(BotOfflineMatchRequest request)
    {
        ResolveReferences();
        SubscribeToScoreIfNeeded();

        if (request == null)
        {
            Debug.LogError("[OfflineBotMatchController] ConfigureOfflineMatch called with null request.", this);
            return;
        }

        activeRequest = request;
        isOfflineBotSessionActive = true;

        localPlayerId = request.LocalPlayerIsPlayer1 ? PlayerID.Player1 : PlayerID.Player2;
        botPlayerId = localPlayerId == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        currentTurnOwner = request.InitialTurnOwner == PlayerID.None ? PlayerID.Player1 : request.InitialTurnOwner;
        player1OnLeft = request.Player1StartsOnLeft;

        matchStarted = true;
        matchEnded = false;
        winner = PlayerID.None;

        turnTimeRemaining = Mathf.Max(1f, request.TurnDurationSeconds);
        matchTimeRemaining = Mathf.Max(1f, request.MatchDurationSeconds);

        scoreP1 = 0;
        scoreP2 = 0;
        shotInFlight = false;
        currentPlayerScoredThisTurn = false;

        currentBall = null;
        currentBallRb = null;
        stuckTimer = 0f;

        if (ballTurnSpawner != null)
        {
            ballTurnSpawner.SetPlayer1Side(player1OnLeft);
            ballTurnSpawner.ClearAllBallsInScene();
        }
        else
        {
            Debug.LogError("[OfflineBotMatchController] BallTurnSpawner missing during ConfigureOfflineMatch.", this);
            return;
        }

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(player1OnLeft);

        SpawnNewActiveBall(currentTurnOwner);

        bool scoreManagerStartedOk = TryStartScoreManagerSafely();
        if (!scoreManagerStartedOk && !continueBootstrapIfScoreManagerThrows)
        {
            Debug.LogError("[OfflineBotMatchController] Bootstrap aborted because ScoreManager.StartMatch threw and continueBootstrapIfScoreManagerThrows is false.", this);
            return;
        }

        ApplyHudState();

        if (logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchController] ConfigureOfflineMatch -> " +
                "LocalPlayer=" + localPlayerId +
                " | BotPlayer=" + botPlayerId +
                " | InitialTurn=" + currentTurnOwner +
                " | Player1OnLeft=" + player1OnLeft +
                " | ScoreManagerStartedOk=" + scoreManagerStartedOk +
                " | Request=" + request,
                this);
        }
    }

    public void RequestLaunchCurrentBall(Vector3 direction, float force)
    {
        if (!isOfflineBotSessionActive || activeRequest == null)
            return;

        if (!matchStarted || matchEnded)
            return;

        if (currentTurnOwner != localPlayerId)
            return;

        if (currentBall == null)
            return;

        BallOwnership ownership = currentBall.GetComponent<BallOwnership>();
        if (ownership == null || ownership.Owner != localPlayerId)
            return;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        Vector3 launchDir = direction.normalized;
        launchDir.y = 0f;

        currentBall.Launch(launchDir * Mathf.Max(0f, force));

        currentBallRb = rb;
        shotInFlight = true;
        currentPlayerScoredThisTurn = false;
        stuckTimer = 0f;

        if (logDebug)
        {
            Debug.Log("[OfflineBotMatchController] RequestLaunchCurrentBall -> Owner=" + currentTurnOwner + " | Force=" + force, this);
        }
    }

    public void NotifyOfflineBallLost(BallPhysics ball)
    {
        if (!isOfflineBotSessionActive || activeRequest == null)
            return;

        if (!matchStarted || matchEnded)
            return;

        bool wasTrackedBall = currentBall == ball || currentBall == null;
        if (!wasTrackedBall)
            return;

        currentBall = null;
        currentBallRb = null;
        shotInFlight = false;
        stuckTimer = 0f;

        PlayerID nextOwner = currentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        SpawnNewActiveBall(nextOwner);

        if (logDebug)
        {
            Debug.Log("[OfflineBotMatchController] NotifyOfflineBallLost -> NextOwner=" + nextOwner, this);
        }
    }

    private bool TryStartScoreManagerSafely()
    {
        if (scoreManager == null)
        {
            Debug.LogWarning("[OfflineBotMatchController] ScoreManager missing. Match can bootstrap visually but scoring will not work.", this);
            return false;
        }

        try
        {
            scoreManager.StartMatch();
            scoreManager.SetReplicatedScores(0, 0);

            if (logDebug)
                Debug.Log("[OfflineBotMatchController] ScoreManager.StartMatch completed.", this);

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[OfflineBotMatchController] ScoreManager.StartMatch threw -> " + ex, this);
            return false;
        }
    }

    private void TickOfflineMatch()
    {
        if (!matchStarted || matchEnded || activeRequest == null)
            return;

        if (activeRequest.MatchMode == MatchMode.TimeLimit)
        {
            matchTimeRemaining -= Time.deltaTime;
            if (matchTimeRemaining < 0f)
                matchTimeRemaining = 0f;
        }

        if (!shotInFlight)
        {
            turnTimeRemaining -= Time.deltaTime;
            if (turnTimeRemaining < 0f)
                turnTimeRemaining = 0f;
        }

        RefreshCurrentBallCache();

        if (activeRequest.MatchMode == MatchMode.TimeLimit && matchTimeRemaining <= 0f && !shotInFlight)
        {
            EndOfflineMatch();
            return;
        }

        if (ShouldEndMatchNow() && !shotInFlight)
        {
            EndOfflineMatch();
            return;
        }

        if (!shotInFlight)
        {
            if (turnTimeRemaining <= 0f)
                ResolveIdleTimeout();

            return;
        }

        // Deathzone or manual destroy
        if (currentBall == null)
        {
            ResolveMissTurnCompleted();
            return;
        }

        if (enableStuckBallCheck && currentBallRb != null && !currentBallRb.isKinematic)
        {
            if (currentBallRb.linearVelocity.magnitude <= stuckVelocityThreshold)
            {
                stuckTimer += Time.deltaTime;

                if (stuckTimer >= stuckTimeout)
                {
                    if (currentPlayerScoredThisTurn)
                        ResolveScoringTurnCompleted();
                    else
                        ResolveMissTurnCompleted();
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
    }

    private void ResolveIdleTimeout()
    {
        PlayerID nextOwner = currentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;

        // No shot fired: this temporary ball should not stay in scene.
        DestroyTrackedBallIfNeeded();
        ReleaseCurrentBallTracking();
        SpawnNewActiveBall(nextOwner);

        if (logDebug)
        {
            Debug.Log("[OfflineBotMatchController] ResolveIdleTimeout -> NextOwner=" + nextOwner, this);
        }
    }

    private void ResolveScoringTurnCompleted()
    {
        PlayerID sameOwner = currentTurnOwner;

        // Important:
        // scored balls must remain in the board exactly like normal game rules.
        currentBall = null;
        currentBallRb = null;
        ReleaseCurrentBallTracking();

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        SpawnNewActiveBall(sameOwner);

        if (logDebug)
        {
            Debug.Log("[OfflineBotMatchController] ResolveScoringTurnCompleted -> SameOwner=" + sameOwner, this);
        }
    }

    private void ResolveMissTurnCompleted()
    {
        PlayerID nextOwner = currentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;

        // Important:
        // if the shot did not score but the ball stayed valid in the board,
        // it must remain in scene. If it fell into deathzone, currentBall is already null.
        currentBall = null;
        currentBallRb = null;
        ReleaseCurrentBallTracking();

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        SpawnNewActiveBall(nextOwner);

        if (logDebug)
        {
            Debug.Log("[OfflineBotMatchController] ResolveMissTurnCompleted -> NextOwner=" + nextOwner, this);
        }
    }

    private void SpawnNewActiveBall(PlayerID owner)
    {
        if (ballTurnSpawner == null)
        {
            Debug.LogError("[OfflineBotMatchController] BallTurnSpawner missing.", this);
            return;
        }

        BallPhysics spawned = ballTurnSpawner.SpawnOfflineBallForOwner(owner);
        if (spawned == null)
        {
            Debug.LogError("[OfflineBotMatchController] Failed to spawn offline ball for owner=" + owner, this);
            return;
        }

        currentBall = spawned;
        currentBallRb = spawned.GetComponent<Rigidbody>();
        currentTurnOwner = owner;
        turnTimeRemaining = Mathf.Max(1f, activeRequest.TurnDurationSeconds);
        shotInFlight = false;
        currentPlayerScoredThisTurn = false;
        stuckTimer = 0f;

        ballTurnSpawner.TryBindCurrentOfflineBallForLocalControl(currentBall, owner, localPlayerId);
        ballTurnSpawner.ClearOfflineLauncherBindingIfNeeded(currentTurnOwner, localPlayerId);

        if (logDebug)
        {
            Debug.Log("[OfflineBotMatchController] SpawnNewActiveBall -> Owner=" + owner + " | LocalPlayer=" + localPlayerId, this);
        }
    }

    private void ReleaseCurrentBallTracking()
    {
        shotInFlight = false;
        currentPlayerScoredThisTurn = false;
        turnTimeRemaining = Mathf.Max(1f, activeRequest != null ? activeRequest.TurnDurationSeconds : 10f);
        stuckTimer = 0f;
    }

    private void DestroyTrackedBallIfNeeded()
    {
        if (currentBall != null)
            Destroy(currentBall.gameObject);

        currentBall = null;
        currentBallRb = null;
    }

    private void RefreshCurrentBallCache()
    {
        if (currentBall == null)
        {
            currentBallRb = null;
            return;
        }

        if (currentBallRb == null)
            currentBallRb = currentBall.GetComponent<Rigidbody>();
    }

    private bool ShouldEndMatchNow()
    {
        if (activeRequest == null)
            return false;

        if (activeRequest.MatchMode == MatchMode.ScoreTarget)
        {
            if (scoreP1 >= Mathf.Max(1, activeRequest.PointsToWin) ||
                scoreP2 >= Mathf.Max(1, activeRequest.PointsToWin))
            {
                return true;
            }
        }

        if (scoreManager != null && scoreManager.AreAllBoardsFull())
            return true;

        return false;
    }

    private void EndOfflineMatch()
    {
        if (matchEnded)
            return;

        matchEnded = true;
        matchStarted = false;
        shotInFlight = false;

        winner = ResolveWinner();

        ballTurnSpawner.ClearOfflineLauncherBindingIfNeeded(PlayerID.None, localPlayerId);

        if (scoreManager != null)
            scoreManager.EndMatch(winner);

        ApplyHudState();

        if (logDebug)
        {
            Debug.Log("[OfflineBotMatchController] EndOfflineMatch -> Winner=" + winner + " | ScoreP1=" + scoreP1 + " | ScoreP2=" + scoreP2, this);
        }
    }

    private PlayerID ResolveWinner()
    {
        if (scoreP1 > scoreP2)
            return PlayerID.Player1;

        if (scoreP2 > scoreP1)
            return PlayerID.Player2;

        return PlayerID.None;
    }

    private void ApplyHudState()
    {
        if (hud == null || activeRequest == null)
            return;

        hud.ApplyState(
            activeRequest.MatchMode,
            activeRequest.PointsToWin,
            activeRequest.MatchDurationSeconds,
            currentTurnOwner,
            turnTimeRemaining,
            matchTimeRemaining,
            scoreP1,
            scoreP2,
            matchStarted,
            matchEnded,
            false,
            false,
            false,
            0f,
            activeRequest.GetPlayer1DisplayName(),
            activeRequest.GetPlayer2DisplayName(),
            winner,
            player1OnLeft,
            false,
            PlayerID.None,
            0f,
            OnlineMatchEndReason.NormalCompletion
        );
    }

    private void ResolveReferences()
    {
#if UNITY_2023_1_OR_NEWER
        if (ballTurnSpawner == null)
            ballTurnSpawner = FindFirstObjectByType<BallTurnSpawner>();

        if (hud == null)
            hud = FindFirstObjectByType<FusionOnlineMatchHUD>();

        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();

        if (bottomBarOrderSwapper == null)
            bottomBarOrderSwapper = FindFirstObjectByType<BottomBarOrderSwapper>();
#else
        if (ballTurnSpawner == null)
            ballTurnSpawner = FindObjectOfType<BallTurnSpawner>();

        if (hud == null)
            hud = FindObjectOfType<FusionOnlineMatchHUD>();

        if (scoreManager == null)
            scoreManager = FindObjectOfType<ScoreManager>();

        if (bottomBarOrderSwapper == null)
            bottomBarOrderSwapper = FindObjectOfType<BottomBarOrderSwapper>();
#endif
    }

    private void SubscribeToScoreIfNeeded()
    {
        if (scoreSubscribed || scoreManager == null)
            return;

        scoreManager.onPointsScored.AddListener(OnScoreManagerPointsScored);
        scoreSubscribed = true;
    }

    private void UnsubscribeFromScore()
    {
        if (!scoreSubscribed || scoreManager == null)
            return;

        scoreManager.onPointsScored.RemoveListener(OnScoreManagerPointsScored);
        scoreSubscribed = false;
    }

    private void OnScoreManagerPointsScored(PlayerID player, int newTotal)
    {
        if (!isOfflineBotSessionActive || activeRequest == null)
            return;

        if (player == PlayerID.Player1)
            scoreP1 = newTotal;
        else if (player == PlayerID.Player2)
            scoreP2 = newTotal;

        if (player == currentTurnOwner)
            currentPlayerScoredThisTurn = true;

        if (logDebug)
        {
            Debug.Log("[OfflineBotMatchController] OnScoreManagerPointsScored -> Player=" + player + " | NewTotal=" + newTotal + " | ScoreP1=" + scoreP1 + " | ScoreP2=" + scoreP2, this);
        }
    }
}
