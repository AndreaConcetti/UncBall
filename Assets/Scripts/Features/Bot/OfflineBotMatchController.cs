using UnityEngine;

public sealed class OfflineBotMatchController : MonoBehaviour
{
    public enum SeedTestSideMode
    {
        Disabled = 0,
        HumanLeft = 1,
        BotLeft = 2
    }

    private enum PendingBotLaunchState
    {
        None = 0,
        WaitingDelay = 1
    }

    [Header("Scene References")]
    [SerializeField] private BallTurnSpawner ballTurnSpawner;
    [SerializeField] private FusionOnlineMatchHUD hud;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BottomBarOrderSwapper bottomBarOrderSwapper;
    [SerializeField] private BotShotSolver botShotSolver;
    [SerializeField] private PlayerShotDebugRecorder playerShotDebugRecorder;

    [Header("Seed Test Override")]
    [SerializeField] private SeedTestSideMode seedTestSideMode = SeedTestSideMode.Disabled;
    [SerializeField] private bool forceBotStartsFirst = false;

    [Header("Bot Test Determinism")]
    [SerializeField] private bool deterministicBotTestMode = true;
    [SerializeField] private bool applySeedStartPositionBeforeBotLaunch = true;
    [SerializeField] private bool reSolveRightBeforeBotLaunch = false;
    [SerializeField] private float deterministicPreLaunchDelaySeconds = 1.00f;
    [SerializeField] private float preLaunchDelayMinSeconds = 0.50f;
    [SerializeField] private float preLaunchDelayMaxSeconds = 3.00f;
    [SerializeField] private bool logBotPlacementAdjustment = true;

    [Header("Shot Completion")]
    [SerializeField] private bool enableStuckBallCheck = true;
    [SerializeField] private float stuckTimeout = 2.5f;
    [SerializeField] private float stuckVelocityThreshold = 0.05f;

    [Header("Safety")]
    [SerializeField] private bool continueBootstrapIfScoreManagerThrows = true;

    [Header("Bot Brain")]
    [SerializeField] private bool enableBotBrain = true;
    [SerializeField] private float easyThinkDelayMin = 0.75f;
    [SerializeField] private float easyThinkDelayMax = 1.55f;
    [SerializeField] private float mediumThinkDelayMin = 0.55f;
    [SerializeField] private float mediumThinkDelayMax = 1.15f;
    [SerializeField] private float hardThinkDelayMin = 0.35f;
    [SerializeField] private float hardThinkDelayMax = 0.85f;
    [SerializeField] private int maxAdaptiveMissStacks = 5;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;
    [SerializeField] private bool drawBotTargetGizmos = true;
    [SerializeField] private float gizmoSphereRadius = 0.08f;

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
    [SerializeField] private float botThinkTimeRemaining;
    [SerializeField] private int consecutiveBotMisses;

    [Header("Pending Bot Launch")]
    [SerializeField] private PendingBotLaunchState pendingBotLaunchState = PendingBotLaunchState.None;
    [SerializeField] private float pendingBotLaunchDelayRemaining;
    [SerializeField] private Vector2 pendingBotSwipe;
    [SerializeField] private Vector3 pendingBotDirection;
    [SerializeField] private float pendingBotForce;
    [SerializeField] private string pendingBotSeedId = "";
    [SerializeField] private Vector3 pendingBotSeedStartPosition;
    [SerializeField] private bool pendingBotSeedStartPositionValid;
    [SerializeField] private int pendingBotTargetPlateIndex = -1;
    [SerializeField] private int pendingBotTargetSlotIndex = -1;

    [Header("Last Bot Debug")]
    [SerializeField] private int lastTargetPlateIndex = -1;
    [SerializeField] private string lastTargetPlateName = "";
    [SerializeField] private int lastTargetSlotIndex = -1;
    [SerializeField] private string lastTargetSlotName = "";
    [SerializeField] private Vector3 lastTargetSlotCenter;
    [SerializeField] private Vector3 lastBestSamplePosition;
    [SerializeField] private Vector2 lastSwipeDelta;
    [SerializeField] private Vector3 lastLaunchDirection;
    [SerializeField] private float lastLaunchForce;
    [SerializeField] private float lastTargetDistance;
    [SerializeField] private bool lastEnteredTargetTrigger;
    [SerializeField] private bool lastDescendingAtEntry;
    [SerializeField] private bool lastHitBlockingBoardBeforeEntry;
    [SerializeField] private float lastCandidateScore;

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
        TickBotBrain();
        ApplyHudState();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBotTargetGizmos)
            return;

        if (lastTargetSlotIndex < 0)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(lastTargetSlotCenter, gizmoSphereRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(lastBestSamplePosition, gizmoSphereRadius);

        if (currentBall != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(currentBall.transform.position, lastBestSamplePosition);
        }
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

        ApplySeedTestOrRequestSetup(request);

        matchStarted = true;
        matchEnded = false;
        winner = PlayerID.None;
        turnTimeRemaining = Mathf.Max(1f, request.TurnDurationSeconds);
        matchTimeRemaining = Mathf.Max(1f, request.MatchDurationSeconds);
        scoreP1 = 0;
        scoreP2 = 0;
        shotInFlight = false;
        currentPlayerScoredThisTurn = false;
        botThinkTimeRemaining = 0f;
        consecutiveBotMisses = 0;
        currentBall = null;
        currentBallRb = null;
        stuckTimer = 0f;

        ClearPendingBotLaunch();
        ResetBotDebug();

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
            return;

        ApplyHudState();

        if (logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchController] ConfigureOfflineMatch -> " +
                "LocalPlayer=" + localPlayerId +
                " | BotPlayer=" + botPlayerId +
                " | InitialTurn=" + currentTurnOwner +
                " | Player1OnLeft=" + player1OnLeft +
                " | SeedTestSideMode=" + seedTestSideMode,
                this);
        }
    }

    private void ApplySeedTestOrRequestSetup(BotOfflineMatchRequest request)
    {
        if (seedTestSideMode == SeedTestSideMode.Disabled)
        {
            localPlayerId = request.LocalPlayerIsPlayer1 ? PlayerID.Player1 : PlayerID.Player2;
            botPlayerId = localPlayerId == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
            currentTurnOwner = request.InitialTurnOwner == PlayerID.None ? PlayerID.Player1 : request.InitialTurnOwner;
            player1OnLeft = request.Player1StartsOnLeft;
            return;
        }

        if (seedTestSideMode == SeedTestSideMode.HumanLeft)
        {
            localPlayerId = PlayerID.Player1;
            botPlayerId = PlayerID.Player2;
            player1OnLeft = true;
            currentTurnOwner = forceBotStartsFirst ? botPlayerId : localPlayerId;
        }
        else
        {
            localPlayerId = PlayerID.Player1;
            botPlayerId = PlayerID.Player2;
            player1OnLeft = false;
            currentTurnOwner = forceBotStartsFirst ? botPlayerId : localPlayerId;
        }
    }

    public void RequestLaunchCurrentBall(Vector3 direction, float force)
    {
        if (!isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded)
            return;

        if (currentTurnOwner != localPlayerId)
            return;

        LaunchTrackedBallForOwner(localPlayerId, direction, force);
    }

    public void RequestBotLaunchCurrentBall(Vector3 direction, float force)
    {
        if (!isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded)
            return;

        if (currentTurnOwner != botPlayerId)
            return;

        LaunchTrackedBallForOwner(botPlayerId, direction, force);
    }

    public void NotifyOfflineBallLost(BallPhysics ball)
    {
        if (!isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded)
            return;

        bool wasTrackedBall = currentBall == ball || currentBall == null;
        if (!wasTrackedBall)
            return;

        if (playerShotDebugRecorder != null && currentTurnOwner == localPlayerId)
            playerShotDebugRecorder.FinalizePendingShot("BallLostOrDeathZone");

        currentBall = null;
        currentBallRb = null;
        shotInFlight = false;
        stuckTimer = 0f;
        ClearPendingBotLaunch();

        PlayerID nextOwner = currentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        SpawnNewActiveBall(nextOwner);
    }

    private bool TryStartScoreManagerSafely()
    {
        if (scoreManager == null)
            return false;

        try
        {
            scoreManager.StartMatch();
            scoreManager.SetReplicatedScores(0, 0);
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
            matchTimeRemaining = Mathf.Max(0f, matchTimeRemaining - Time.deltaTime);

        if (!shotInFlight)
            turnTimeRemaining = Mathf.Max(0f, turnTimeRemaining - Time.deltaTime);

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

    private void TickBotBrain()
    {
        if (!enableBotBrain || !isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded)
            return;

        if (shotInFlight || currentTurnOwner != botPlayerId || currentBall == null)
            return;

        if (pendingBotLaunchState == PendingBotLaunchState.WaitingDelay)
        {
            pendingBotLaunchDelayRemaining -= Time.deltaTime;

            if (pendingBotLaunchDelayRemaining > 0f)
                return;

            ExecutePendingBotLaunch();
            return;
        }

        if (botThinkTimeRemaining > 0f)
        {
            botThinkTimeRemaining -= Time.deltaTime;
            return;
        }

        QueueBotLaunchFromFreshSolve();
    }

    private void QueueBotLaunchFromFreshSolve()
    {
        BallLauncher launcher = ballTurnSpawner != null ? ballTurnSpawner.Launcher : null;
        if (launcher == null || botShotSolver == null)
            return;

        BotShotSolution solution = botShotSolver.SolveBestShot(
            currentBall,
            launcher,
            scoreManager,
            activeRequest.Difficulty,
            Mathf.Clamp(consecutiveBotMisses, 0, Mathf.Max(0, maxAdaptiveMissStacks)));

        if (!solution.hasSolution)
        {
            if (logDebug)
                Debug.LogWarning("[OfflineBotMatchController] BotShotSolver returned no solution.", this);

            return;
        }

        pendingBotSwipe = solution.bestCandidate.swipeDelta;
        pendingBotDirection = solution.bestCandidate.launchDirection;
        pendingBotForce = solution.bestCandidate.launchForce;
        pendingBotSeedId = botShotSolver.GetLastSeedId();
        pendingBotTargetPlateIndex = solution.bestCandidate.targetPlateIndex;
        pendingBotTargetSlotIndex = solution.bestCandidate.targetSlotIndex;
        pendingBotSeedStartPositionValid = botShotSolver.TryGetLastChosenSeedStartPosition(out pendingBotSeedStartPosition);

        lastTargetPlateIndex = solution.bestCandidate.targetPlateIndex;
        lastTargetPlateName = solution.bestCandidate.targetPlateName;
        lastTargetSlotIndex = solution.bestCandidate.targetSlotIndex;
        lastTargetSlotName = solution.bestCandidate.targetSlotName;
        lastTargetSlotCenter = solution.bestCandidate.targetSlotCenter;
        lastBestSamplePosition = solution.bestCandidate.bestSamplePosition;
        lastSwipeDelta = solution.bestCandidate.swipeDelta;
        lastLaunchDirection = solution.bestCandidate.launchDirection;
        lastLaunchForce = solution.bestCandidate.launchForce;
        lastTargetDistance = solution.bestCandidate.bestDistanceToTarget;
        lastEnteredTargetTrigger = solution.bestCandidate.enteredTargetTrigger;
        lastDescendingAtEntry = solution.bestCandidate.descendingAtEntry;
        lastHitBlockingBoardBeforeEntry = solution.bestCandidate.hitBlockingBoardBeforeEntry;
        lastCandidateScore = solution.bestCandidate.score;

        if (applySeedStartPositionBeforeBotLaunch)
            TryApplyBotSeedStartPosition(pendingBotSeedId, pendingBotSeedStartPosition, pendingBotSeedStartPositionValid);

        pendingBotLaunchDelayRemaining = GetBotPreLaunchDelaySeconds();
        pendingBotLaunchState = PendingBotLaunchState.WaitingDelay;

        if (logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchController] Bot launch queued -> " +
                "SeedId=" + pendingBotSeedId +
                " | TargetPlate=" + pendingBotTargetPlateIndex +
                " | TargetSlot=" + pendingBotTargetSlotIndex +
                " | DelaySeconds=" + pendingBotLaunchDelayRemaining,
                this);
        }
    }

    private void ExecutePendingBotLaunch()
    {
        pendingBotLaunchState = PendingBotLaunchState.None;

        if (currentBall == null || currentTurnOwner != botPlayerId || shotInFlight || matchEnded)
        {
            ClearPendingBotLaunch();
            return;
        }

        BallLauncher launcher = ballTurnSpawner != null ? ballTurnSpawner.Launcher : null;
        if (launcher == null || botShotSolver == null)
        {
            ClearPendingBotLaunch();
            return;
        }

        if (reSolveRightBeforeBotLaunch)
        {
            BotShotSolution refreshed = botShotSolver.SolveBestShot(
                currentBall,
                launcher,
                scoreManager,
                activeRequest.Difficulty,
                Mathf.Clamp(consecutiveBotMisses, 0, Mathf.Max(0, maxAdaptiveMissStacks)));

            if (!refreshed.hasSolution)
            {
                if (logDebug)
                    Debug.LogWarning("[OfflineBotMatchController] Re-solve before launch returned no solution.", this);

                ClearPendingBotLaunch();
                botThinkTimeRemaining = GetRandomThinkDelay();
                return;
            }

            pendingBotSwipe = refreshed.bestCandidate.swipeDelta;
            pendingBotDirection = refreshed.bestCandidate.launchDirection;
            pendingBotForce = refreshed.bestCandidate.launchForce;
            pendingBotSeedId = botShotSolver.GetLastSeedId();
            pendingBotTargetPlateIndex = refreshed.bestCandidate.targetPlateIndex;
            pendingBotTargetSlotIndex = refreshed.bestCandidate.targetSlotIndex;
            pendingBotSeedStartPositionValid = botShotSolver.TryGetLastChosenSeedStartPosition(out pendingBotSeedStartPosition);

            if (applySeedStartPositionBeforeBotLaunch)
                TryApplyBotSeedStartPosition(pendingBotSeedId, pendingBotSeedStartPosition, pendingBotSeedStartPositionValid);

            if (logDebug)
            {
                Debug.Log(
                    "[OfflineBotMatchController] Bot launch refreshed right before shot -> " +
                    "SeedId=" + pendingBotSeedId +
                    " | TargetPlate=" + pendingBotTargetPlateIndex +
                    " | TargetSlot=" + pendingBotTargetSlotIndex,
                    this);
            }
        }

        if (launcher.SimulateOfflineBotSwipe(currentBall, pendingBotSwipe) && logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchController] Bot solver launch -> " +
                "MissStacks=" + consecutiveBotMisses +
                " | SeedId=" + pendingBotSeedId +
                " | TargetPlate=" + pendingBotTargetPlateIndex +
                " | TargetSlot=" + pendingBotTargetSlotIndex +
                " | Swipe=" + pendingBotSwipe +
                " | Direction=" + pendingBotDirection +
                " | Force=" + pendingBotForce,
                this);
        }

        ClearPendingBotLaunch();
    }

    private void TryApplyBotSeedStartPosition(string seedId, Vector3 desiredSeedStart, bool isValid)
    {
        if (currentBall == null || currentTurnOwner != botPlayerId || !isValid)
            return;

        Vector3 original = currentBall.transform.position;
        Vector3 adjusted = desiredSeedStart;
        adjusted.y = original.y;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb != null)
        {
            bool previousKinematic = rb.isKinematic;
            RigidbodyConstraints previousConstraints = rb.constraints;

            rb.isKinematic = true;
            rb.position = adjusted;
            currentBall.transform.position = adjusted;
            rb.constraints = previousConstraints;
            rb.isKinematic = previousKinematic;
        }
        else
        {
            currentBall.transform.position = adjusted;
        }

        if (logBotPlacementAdjustment)
        {
            Debug.Log(
                "[OfflineBotMatchController] Applied bot seed start position -> " +
                "SeedId=" + seedId +
                " | From=" + original +
                " | To=" + adjusted,
                this);
        }
    }

    private float GetBotPreLaunchDelaySeconds()
    {
        float minDelay = Mathf.Max(0.50f, preLaunchDelayMinSeconds);
        float maxDelay = Mathf.Max(minDelay, preLaunchDelayMaxSeconds);

        if (deterministicBotTestMode)
            return Mathf.Clamp(deterministicPreLaunchDelaySeconds, minDelay, maxDelay);

        return Random.Range(minDelay, maxDelay);
    }

    private float GetRandomThinkDelay()
    {
        if (deterministicBotTestMode)
            return 0f;

        switch (activeRequest != null ? activeRequest.Difficulty : BotDifficulty.Medium)
        {
            case BotDifficulty.Easy: return Random.Range(easyThinkDelayMin, easyThinkDelayMax);
            case BotDifficulty.Medium: return Random.Range(mediumThinkDelayMin, mediumThinkDelayMax);
            case BotDifficulty.Hard: return Random.Range(hardThinkDelayMin, hardThinkDelayMax);
            default: return Random.Range(mediumThinkDelayMin, mediumThinkDelayMax);
        }
    }

    private void ResolveIdleTimeout()
    {
        bool botMiss = currentTurnOwner == botPlayerId;

        if (playerShotDebugRecorder != null && currentTurnOwner == localPlayerId)
            playerShotDebugRecorder.FinalizePendingShot("IdleTimeout");

        PlayerID nextOwner = currentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        DestroyTrackedBallIfNeeded();
        ReleaseCurrentBallTracking();

        if (botMiss)
            consecutiveBotMisses++;

        SpawnNewActiveBall(nextOwner);
    }

    private void ResolveScoringTurnCompleted()
    {
        PlayerID sameOwner = currentTurnOwner;
        bool botScored = sameOwner == botPlayerId;

        if (playerShotDebugRecorder != null && sameOwner == localPlayerId)
            playerShotDebugRecorder.FinalizePendingShot("ScoringTurnCompleted");

        currentBall = null;
        currentBallRb = null;
        ReleaseCurrentBallTracking();

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        if (botScored)
            consecutiveBotMisses = 0;

        SpawnNewActiveBall(sameOwner);
    }

    private void ResolveMissTurnCompleted()
    {
        bool botMiss = currentTurnOwner == botPlayerId;

        if (playerShotDebugRecorder != null && currentTurnOwner == localPlayerId)
            playerShotDebugRecorder.FinalizePendingShot("MissTurnCompleted");

        PlayerID nextOwner = currentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        currentBall = null;
        currentBallRb = null;
        ReleaseCurrentBallTracking();

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        if (botMiss)
            consecutiveBotMisses++;

        SpawnNewActiveBall(nextOwner);
    }

    private void SpawnNewActiveBall(PlayerID owner)
    {
        if (ballTurnSpawner == null)
            return;

        BallPhysics spawned = ballTurnSpawner.SpawnOfflineBallForOwner(owner);
        if (spawned == null)
            return;

        currentBall = spawned;
        currentBallRb = spawned.GetComponent<Rigidbody>();
        currentTurnOwner = owner;
        turnTimeRemaining = Mathf.Max(1f, activeRequest.TurnDurationSeconds);
        shotInFlight = false;
        currentPlayerScoredThisTurn = false;
        stuckTimer = 0f;
        botThinkTimeRemaining = owner == botPlayerId ? GetRandomThinkDelay() : 0f;

        ClearPendingBotLaunch();

        ballTurnSpawner.TryBindCurrentOfflineBallForLocalControl(currentBall, owner, localPlayerId);
        ballTurnSpawner.ClearOfflineLauncherBindingIfNeeded(currentTurnOwner, localPlayerId);
    }

    private bool LaunchTrackedBallForOwner(PlayerID owner, Vector3 direction, float force)
    {
        if (!isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded)
            return false;

        if (currentTurnOwner != owner || currentBall == null)
            return false;

        BallOwnership ownership = currentBall.GetComponent<BallOwnership>();
        if (ownership == null || ownership.Owner != owner)
            return false;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();
        if (rb == null)
            return false;

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
        botThinkTimeRemaining = 0f;
        return true;
    }

    private void ReleaseCurrentBallTracking()
    {
        shotInFlight = false;
        currentPlayerScoredThisTurn = false;
        turnTimeRemaining = Mathf.Max(1f, activeRequest != null ? activeRequest.TurnDurationSeconds : 10f);
        stuckTimer = 0f;
        botThinkTimeRemaining = 0f;
        ClearPendingBotLaunch();
    }

    private void ClearPendingBotLaunch()
    {
        pendingBotLaunchState = PendingBotLaunchState.None;
        pendingBotLaunchDelayRemaining = 0f;
        pendingBotSwipe = Vector2.zero;
        pendingBotDirection = Vector3.zero;
        pendingBotForce = 0f;
        pendingBotSeedId = string.Empty;
        pendingBotSeedStartPosition = Vector3.zero;
        pendingBotSeedStartPositionValid = false;
        pendingBotTargetPlateIndex = -1;
        pendingBotTargetSlotIndex = -1;
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

        if (playerShotDebugRecorder != null && currentTurnOwner == localPlayerId)
            playerShotDebugRecorder.FinalizePendingShot("MatchEnded");

        matchEnded = true;
        matchStarted = false;
        shotInFlight = false;
        botThinkTimeRemaining = 0f;
        winner = ResolveWinner();

        ClearPendingBotLaunch();
        ballTurnSpawner.ClearOfflineLauncherBindingIfNeeded(PlayerID.None, localPlayerId);

        if (scoreManager != null)
            scoreManager.EndMatch(winner);

        ApplyHudState();
    }

    private PlayerID ResolveWinner()
    {
        if (scoreP1 > scoreP2) return PlayerID.Player1;
        if (scoreP2 > scoreP1) return PlayerID.Player2;
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
        if (ballTurnSpawner == null) ballTurnSpawner = FindFirstObjectByType<BallTurnSpawner>();
        if (hud == null) hud = FindFirstObjectByType<FusionOnlineMatchHUD>();
        if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
        if (bottomBarOrderSwapper == null) bottomBarOrderSwapper = FindFirstObjectByType<BottomBarOrderSwapper>();
        if (botShotSolver == null) botShotSolver = FindFirstObjectByType<BotShotSolver>();
        if (playerShotDebugRecorder == null) playerShotDebugRecorder = FindFirstObjectByType<PlayerShotDebugRecorder>();
#else
        if (ballTurnSpawner == null) ballTurnSpawner = FindObjectOfType<BallTurnSpawner>();
        if (hud == null) hud = FindObjectOfType<FusionOnlineMatchHUD>();
        if (scoreManager == null) scoreManager = FindObjectOfType<ScoreManager>();
        if (bottomBarOrderSwapper == null) bottomBarOrderSwapper = FindObjectOfType<BottomBarOrderSwapper>();
        if (botShotSolver == null) botShotSolver = FindObjectOfType<BotShotSolver>();
        if (playerShotDebugRecorder == null) playerShotDebugRecorder = FindObjectOfType<PlayerShotDebugRecorder>();
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

        if (player == PlayerID.Player1) scoreP1 = newTotal;
        else if (player == PlayerID.Player2) scoreP2 = newTotal;

        if (player == currentTurnOwner)
        {
            currentPlayerScoredThisTurn = true;
            if (player == botPlayerId)
                consecutiveBotMisses = 0;
        }

        if (playerShotDebugRecorder != null && player == localPlayerId)
            playerShotDebugRecorder.RegisterPendingShotScored(-1, -1);
    }

    private void ResetBotDebug()
    {
        lastTargetPlateIndex = -1;
        lastTargetPlateName = string.Empty;
        lastTargetSlotIndex = -1;
        lastTargetSlotName = string.Empty;
        lastTargetSlotCenter = Vector3.zero;
        lastBestSamplePosition = Vector3.zero;
        lastSwipeDelta = Vector2.zero;
        lastLaunchDirection = Vector3.zero;
        lastLaunchForce = 0f;
        lastTargetDistance = 0f;
        lastEnteredTargetTrigger = false;
        lastDescendingAtEntry = false;
        lastHitBlockingBoardBeforeEntry = false;
        lastCandidateScore = 0f;
    }
}
