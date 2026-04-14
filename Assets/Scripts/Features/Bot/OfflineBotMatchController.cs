using System.Collections.Generic;
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
        WaitingPostSpawnDelay = 1,
        WaitingPreLaunchDelay = 2
    }

    private struct BotShotErrorSettings
    {
        public float majorMissChanceBoard1;
        public float majorMissChanceBoard2;
        public float majorMissChanceBoard3;

        public float minorSwipeXBoard1;
        public float minorSwipeXBoard2;
        public float minorSwipeXBoard3;

        public float minorSwipeYBoard1;
        public float minorSwipeYBoard2;
        public float minorSwipeYBoard3;

        public float majorSwipeXBoard1;
        public float majorSwipeXBoard2;
        public float majorSwipeXBoard3;

        public float majorSwipeYBoard1;
        public float majorSwipeYBoard2;
        public float majorSwipeYBoard3;
    }

    [Header("Scene References")]
    [SerializeField] private BallTurnSpawner ballTurnSpawner;
    [SerializeField] private FusionOnlineMatchHUD hud;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private BottomBarOrderSwapper bottomBarOrderSwapper;
    [SerializeField] private BotShotSolver botShotSolver;
    [SerializeField] private PlayerShotDebugRecorder playerShotDebugRecorder;
    [SerializeField] private BotTargetDecisionService botTargetDecisionService;

    [Header("Seed Test Override")]
    [SerializeField] private SeedTestSideMode seedTestSideMode = SeedTestSideMode.Disabled;
    [SerializeField] private bool forceBotStartsFirst = false;

    [Header("Bot Test Determinism")]
    [SerializeField] private bool deterministicBotTestMode = true;
    [SerializeField] private bool applySeedStartPositionBeforeBotLaunch = true;
    [SerializeField] private bool reSolveRightBeforeBotLaunch = false;

    [Header("Bot Humanization Delays")]
    [SerializeField] private float deterministicPostSpawnDelaySeconds = 0.50f;
    [SerializeField] private float postSpawnDelayMinSeconds = 0.35f;
    [SerializeField] private float postSpawnDelayMaxSeconds = 1.00f;
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
    [SerializeField] private float unbeatableThinkDelayMin = 0.20f;
    [SerializeField] private float unbeatableThinkDelayMax = 0.45f;
    [SerializeField] private int maxAdaptiveMissStacks = 5;
    [SerializeField] private bool forceBrainSelectedTarget = true;
    [SerializeField] private bool fallbackToGeneralSolveWhenForcedTargetFails = true;

    [Header("Bot Shot Error")]
    [SerializeField] private bool enableDifficultyShotError = true;
    [SerializeField] private bool logShotError = true;

    [Header("Easy Error Tuning")]
    [SerializeField] private float easyMajorMissChanceBoard1 = 0.18f;
    [SerializeField] private float easyMajorMissChanceBoard2 = 0.35f;
    [SerializeField] private float easyMajorMissChanceBoard3 = 0.55f;
    [SerializeField] private float easyMinorSwipeXBoard1 = 28f;
    [SerializeField] private float easyMinorSwipeXBoard2 = 52f;
    [SerializeField] private float easyMinorSwipeXBoard3 = 86f;
    [SerializeField] private float easyMinorSwipeYBoard1 = 22f;
    [SerializeField] private float easyMinorSwipeYBoard2 = 46f;
    [SerializeField] private float easyMinorSwipeYBoard3 = 82f;
    [SerializeField] private float easyMajorSwipeXBoard1 = 90f;
    [SerializeField] private float easyMajorSwipeXBoard2 = 155f;
    [SerializeField] private float easyMajorSwipeXBoard3 = 240f;
    [SerializeField] private float easyMajorSwipeYBoard1 = 70f;
    [SerializeField] private float easyMajorSwipeYBoard2 = 130f;
    [SerializeField] private float easyMajorSwipeYBoard3 = 210f;

    [Header("Medium Error Tuning")]
    [SerializeField] private float mediumMajorMissChanceBoard1 = 0.08f;
    [SerializeField] private float mediumMajorMissChanceBoard2 = 0.18f;
    [SerializeField] private float mediumMajorMissChanceBoard3 = 0.30f;
    [SerializeField] private float mediumMinorSwipeXBoard1 = 16f;
    [SerializeField] private float mediumMinorSwipeXBoard2 = 30f;
    [SerializeField] private float mediumMinorSwipeXBoard3 = 54f;
    [SerializeField] private float mediumMinorSwipeYBoard1 = 14f;
    [SerializeField] private float mediumMinorSwipeYBoard2 = 24f;
    [SerializeField] private float mediumMinorSwipeYBoard3 = 45f;
    [SerializeField] private float mediumMajorSwipeXBoard1 = 60f;
    [SerializeField] private float mediumMajorSwipeXBoard2 = 95f;
    [SerializeField] private float mediumMajorSwipeXBoard3 = 145f;
    [SerializeField] private float mediumMajorSwipeYBoard1 = 42f;
    [SerializeField] private float mediumMajorSwipeYBoard2 = 76f;
    [SerializeField] private float mediumMajorSwipeYBoard3 = 118f;

    [Header("Hard Error Tuning")]
    [SerializeField] private float hardMajorMissChanceBoard1 = 0.03f;
    [SerializeField] private float hardMajorMissChanceBoard2 = 0.08f;
    [SerializeField] private float hardMajorMissChanceBoard3 = 0.16f;
    [SerializeField] private float hardMinorSwipeXBoard1 = 8f;
    [SerializeField] private float hardMinorSwipeXBoard2 = 16f;
    [SerializeField] private float hardMinorSwipeXBoard3 = 28f;
    [SerializeField] private float hardMinorSwipeYBoard1 = 8f;
    [SerializeField] private float hardMinorSwipeYBoard2 = 14f;
    [SerializeField] private float hardMinorSwipeYBoard3 = 26f;
    [SerializeField] private float hardMajorSwipeXBoard1 = 28f;
    [SerializeField] private float hardMajorSwipeXBoard2 = 48f;
    [SerializeField] private float hardMajorSwipeXBoard3 = 78f;
    [SerializeField] private float hardMajorSwipeYBoard1 = 20f;
    [SerializeField] private float hardMajorSwipeYBoard2 = 36f;
    [SerializeField] private float hardMajorSwipeYBoard3 = 58f;

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
    [SerializeField] private string pendingBotDecisionReason = "";

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
    [SerializeField] private string lastDecisionReason = "";

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

        switch (pendingBotLaunchState)
        {
            case PendingBotLaunchState.WaitingPostSpawnDelay:
                pendingBotLaunchDelayRemaining -= Time.deltaTime;
                if (pendingBotLaunchDelayRemaining > 0f)
                    return;

                pendingBotLaunchState = PendingBotLaunchState.None;

                if (applySeedStartPositionBeforeBotLaunch)
                    TryApplyBotSeedStartPosition(pendingBotSeedId, pendingBotSeedStartPosition, pendingBotSeedStartPositionValid);

                pendingBotLaunchDelayRemaining = GetBotPreLaunchDelaySeconds();
                pendingBotLaunchState = PendingBotLaunchState.WaitingPreLaunchDelay;

                if (logDebug)
                {
                    Debug.Log(
                        "[OfflineBotMatchController] Bot post-spawn delay completed -> " +
                        "SeedId=" + pendingBotSeedId +
                        " | TargetPlate=" + pendingBotTargetPlateIndex +
                        " | TargetSlot=" + pendingBotTargetSlotIndex +
                        " | PreLaunchDelay=" + pendingBotLaunchDelayRemaining,
                        this);
                }
                return;

            case PendingBotLaunchState.WaitingPreLaunchDelay:
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

        BotTargetDecisionService.BotTargetDecision serviceDecision = default;
        bool hasStrategicTarget = false;

        if (botTargetDecisionService != null)
        {
            serviceDecision = botTargetDecisionService.DecideBestTarget(botPlayerId, localPlayerId, activeRequest.Difficulty);
            hasStrategicTarget = serviceDecision.hasTarget;
        }

        BotShotSolution solution = default;
        bool usedForcedStrategicTarget = false;

        if (hasStrategicTarget && forceBrainSelectedTarget)
        {
            solution = botShotSolver.SolveBestShotForTarget(
                currentBall,
                launcher,
                scoreManager,
                activeRequest.Difficulty,
                Mathf.Clamp(consecutiveBotMisses, 0, Mathf.Max(0, maxAdaptiveMissStacks)),
                serviceDecision.targetPlateIndex,
                serviceDecision.targetSlotIndex);

            usedForcedStrategicTarget = solution.hasSolution;
        }

        if (!solution.hasSolution && fallbackToGeneralSolveWhenForcedTargetFails)
        {
            solution = botShotSolver.SolveBestShot(
                currentBall,
                launcher,
                scoreManager,
                activeRequest.Difficulty,
                Mathf.Clamp(consecutiveBotMisses, 0, Mathf.Max(0, maxAdaptiveMissStacks)));
        }

        if (!solution.hasSolution)
        {
            if (logDebug)
            {
                string targetInfo = hasStrategicTarget
                    ? " | BrainTargetPlate=" + serviceDecision.targetPlateIndex +
                      " | BrainTargetSlot=" + serviceDecision.targetSlotIndex +
                      " | Reason=" + serviceDecision.reason
                    : string.Empty;

                Debug.LogWarning(
                    "[OfflineBotMatchController] BotShotSolver returned no solution." +
                    targetInfo +
                    " | MissStacks=" + consecutiveBotMisses,
                    this);
            }

            return;
        }

        Vector2 finalSwipe = solution.bestCandidate.swipeDelta;
        Vector3 finalDirection = solution.bestCandidate.launchDirection;
        float finalForce = solution.bestCandidate.launchForce;

        ApplyDifficultyShotError(
            launcher,
            activeRequest.Difficulty,
            solution.bestCandidate.targetPlateIndex,
            ref finalSwipe,
            ref finalDirection,
            ref finalForce);

        pendingBotSwipe = finalSwipe;
        pendingBotDirection = finalDirection;
        pendingBotForce = finalForce;
        pendingBotSeedId = botShotSolver.GetLastSeedId();
        pendingBotTargetPlateIndex = solution.bestCandidate.targetPlateIndex;
        pendingBotTargetSlotIndex = solution.bestCandidate.targetSlotIndex;
        pendingBotSeedStartPositionValid = botShotSolver.TryGetLastChosenSeedStartPosition(out pendingBotSeedStartPosition);

        if (usedForcedStrategicTarget)
            pendingBotDecisionReason = serviceDecision.reason;
        else if (hasStrategicTarget)
            pendingBotDecisionReason = serviceDecision.reason + " | FallbackGeneralSolve";
        else
            pendingBotDecisionReason = "GeneralSolve";

        lastTargetPlateIndex = solution.bestCandidate.targetPlateIndex;
        lastTargetPlateName = solution.bestCandidate.targetPlateName;
        lastTargetSlotIndex = solution.bestCandidate.targetSlotIndex;
        lastTargetSlotName = solution.bestCandidate.targetSlotName;
        lastTargetSlotCenter = solution.bestCandidate.targetSlotCenter;
        lastBestSamplePosition = solution.bestCandidate.bestSamplePosition;
        lastSwipeDelta = finalSwipe;
        lastLaunchDirection = finalDirection;
        lastLaunchForce = finalForce;
        lastTargetDistance = solution.bestCandidate.bestDistanceToTarget;
        lastEnteredTargetTrigger = solution.bestCandidate.enteredTargetTrigger;
        lastDescendingAtEntry = solution.bestCandidate.descendingAtEntry;
        lastHitBlockingBoardBeforeEntry = solution.bestCandidate.hitBlockingBoardBeforeEntry;
        lastCandidateScore = solution.bestCandidate.score;
        lastDecisionReason = pendingBotDecisionReason;

        pendingBotLaunchDelayRemaining = GetBotPostSpawnDelaySeconds();
        pendingBotLaunchState = PendingBotLaunchState.WaitingPostSpawnDelay;

        if (logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchController] Bot launch queued -> " +
                "SeedId=" + pendingBotSeedId +
                " | TargetPlate=" + pendingBotTargetPlateIndex +
                " | TargetSlot=" + pendingBotTargetSlotIndex +
                " | DecisionReason=" + pendingBotDecisionReason +
                " | PostSpawnDelay=" + pendingBotLaunchDelayRemaining,
                this);
        }
    }

    private void ApplyDifficultyShotError(
        BallLauncher launcher,
        BotDifficulty difficulty,
        int targetPlateIndex,
        ref Vector2 swipe,
        ref Vector3 direction,
        ref float force)
    {
        if (!enableDifficultyShotError)
            return;

        if (launcher == null)
            return;

        if (difficulty == BotDifficulty.Unbeatable)
            return;

        BotShotErrorSettings settings = GetShotErrorSettings(difficulty);
        float missChance = GetMajorMissChance(settings, targetPlateIndex);
        float minorX = GetMinorSwipeX(settings, targetPlateIndex);
        float minorY = GetMinorSwipeY(settings, targetPlateIndex);
        float majorX = GetMajorSwipeX(settings, targetPlateIndex);
        float majorY = GetMajorSwipeY(settings, targetPlateIndex);

        bool majorMiss = Random.value < missChance;

        float deltaX;
        float deltaY;

        if (majorMiss)
        {
            deltaX = RandomSignedMagnitude(majorX * 0.55f, majorX);
            deltaY = RandomSignedMagnitude(majorY * 0.45f, majorY);
        }
        else
        {
            deltaX = Random.Range(-minorX, minorX);
            deltaY = Random.Range(-minorY, minorY);
        }

        Vector2 originalSwipe = swipe;
        swipe += new Vector2(deltaX, deltaY);

        if (!launcher.TryBuildLaunchFromSwipe(swipe, out Vector3 rebuiltDirection, out float rebuiltForce))
        {
            swipe = originalSwipe;
            return;
        }

        direction = rebuiltDirection;
        force = rebuiltForce;

        if (logShotError)
        {
            Debug.Log(
                "[OfflineBotMatchController] ApplyDifficultyShotError -> " +
                "Difficulty=" + difficulty +
                " | Plate=" + targetPlateIndex +
                " | MajorMiss=" + majorMiss +
                " | SwipeBefore=" + originalSwipe +
                " | SwipeAfter=" + swipe +
                " | Delta=(" + deltaX + "," + deltaY + ")",
                this);
        }
    }

    private BotShotErrorSettings GetShotErrorSettings(BotDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BotDifficulty.Easy:
                return new BotShotErrorSettings
                {
                    majorMissChanceBoard1 = easyMajorMissChanceBoard1,
                    majorMissChanceBoard2 = easyMajorMissChanceBoard2,
                    majorMissChanceBoard3 = easyMajorMissChanceBoard3,

                    minorSwipeXBoard1 = easyMinorSwipeXBoard1,
                    minorSwipeXBoard2 = easyMinorSwipeXBoard2,
                    minorSwipeXBoard3 = easyMinorSwipeXBoard3,

                    minorSwipeYBoard1 = easyMinorSwipeYBoard1,
                    minorSwipeYBoard2 = easyMinorSwipeYBoard2,
                    minorSwipeYBoard3 = easyMinorSwipeYBoard3,

                    majorSwipeXBoard1 = easyMajorSwipeXBoard1,
                    majorSwipeXBoard2 = easyMajorSwipeXBoard2,
                    majorSwipeXBoard3 = easyMajorSwipeXBoard3,

                    majorSwipeYBoard1 = easyMajorSwipeYBoard1,
                    majorSwipeYBoard2 = easyMajorSwipeYBoard2,
                    majorSwipeYBoard3 = easyMajorSwipeYBoard3
                };

            case BotDifficulty.Medium:
                return new BotShotErrorSettings
                {
                    majorMissChanceBoard1 = mediumMajorMissChanceBoard1,
                    majorMissChanceBoard2 = mediumMajorMissChanceBoard2,
                    majorMissChanceBoard3 = mediumMajorMissChanceBoard3,

                    minorSwipeXBoard1 = mediumMinorSwipeXBoard1,
                    minorSwipeXBoard2 = mediumMinorSwipeXBoard2,
                    minorSwipeXBoard3 = mediumMinorSwipeXBoard3,

                    minorSwipeYBoard1 = mediumMinorSwipeYBoard1,
                    minorSwipeYBoard2 = mediumMinorSwipeYBoard2,
                    minorSwipeYBoard3 = mediumMinorSwipeYBoard3,

                    majorSwipeXBoard1 = mediumMajorSwipeXBoard1,
                    majorSwipeXBoard2 = mediumMajorSwipeXBoard2,
                    majorSwipeXBoard3 = mediumMajorSwipeXBoard3,

                    majorSwipeYBoard1 = mediumMajorSwipeYBoard1,
                    majorSwipeYBoard2 = mediumMajorSwipeYBoard2,
                    majorSwipeYBoard3 = mediumMajorSwipeYBoard3
                };

            case BotDifficulty.Hard:
            default:
                return new BotShotErrorSettings
                {
                    majorMissChanceBoard1 = hardMajorMissChanceBoard1,
                    majorMissChanceBoard2 = hardMajorMissChanceBoard2,
                    majorMissChanceBoard3 = hardMajorMissChanceBoard3,

                    minorSwipeXBoard1 = hardMinorSwipeXBoard1,
                    minorSwipeXBoard2 = hardMinorSwipeXBoard2,
                    minorSwipeXBoard3 = hardMinorSwipeXBoard3,

                    minorSwipeYBoard1 = hardMinorSwipeYBoard1,
                    minorSwipeYBoard2 = hardMinorSwipeYBoard2,
                    minorSwipeYBoard3 = hardMinorSwipeYBoard3,

                    majorSwipeXBoard1 = hardMajorSwipeXBoard1,
                    majorSwipeXBoard2 = hardMajorSwipeXBoard2,
                    majorSwipeXBoard3 = hardMajorSwipeXBoard3,

                    majorSwipeYBoard1 = hardMajorSwipeYBoard1,
                    majorSwipeYBoard2 = hardMajorSwipeYBoard2,
                    majorSwipeYBoard3 = hardMajorSwipeYBoard3
                };
        }
    }

    private float GetMajorMissChance(BotShotErrorSettings settings, int targetPlateIndex)
    {
        switch (targetPlateIndex)
        {
            case 0: return settings.majorMissChanceBoard1;
            case 1: return settings.majorMissChanceBoard2;
            case 2: return settings.majorMissChanceBoard3;
            default: return settings.majorMissChanceBoard3;
        }
    }

    private float GetMinorSwipeX(BotShotErrorSettings settings, int targetPlateIndex)
    {
        switch (targetPlateIndex)
        {
            case 0: return settings.minorSwipeXBoard1;
            case 1: return settings.minorSwipeXBoard2;
            case 2: return settings.minorSwipeXBoard3;
            default: return settings.minorSwipeXBoard3;
        }
    }

    private float GetMinorSwipeY(BotShotErrorSettings settings, int targetPlateIndex)
    {
        switch (targetPlateIndex)
        {
            case 0: return settings.minorSwipeYBoard1;
            case 1: return settings.minorSwipeYBoard2;
            case 2: return settings.minorSwipeYBoard3;
            default: return settings.minorSwipeYBoard3;
        }
    }

    private float GetMajorSwipeX(BotShotErrorSettings settings, int targetPlateIndex)
    {
        switch (targetPlateIndex)
        {
            case 0: return settings.majorSwipeXBoard1;
            case 1: return settings.majorSwipeXBoard2;
            case 2: return settings.majorSwipeXBoard3;
            default: return settings.majorSwipeXBoard3;
        }
    }

    private float GetMajorSwipeY(BotShotErrorSettings settings, int targetPlateIndex)
    {
        switch (targetPlateIndex)
        {
            case 0: return settings.majorSwipeYBoard1;
            case 1: return settings.majorSwipeYBoard2;
            case 2: return settings.majorSwipeYBoard3;
            default: return settings.majorSwipeYBoard3;
        }
    }

    private float RandomSignedMagnitude(float minAbs, float maxAbs)
    {
        float magnitude = Random.Range(minAbs, maxAbs);
        return Random.value < 0.5f ? -magnitude : magnitude;
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

            Vector2 refreshedSwipe = refreshed.bestCandidate.swipeDelta;
            Vector3 refreshedDirection = refreshed.bestCandidate.launchDirection;
            float refreshedForce = refreshed.bestCandidate.launchForce;

            ApplyDifficultyShotError(
                launcher,
                activeRequest.Difficulty,
                refreshed.bestCandidate.targetPlateIndex,
                ref refreshedSwipe,
                ref refreshedDirection,
                ref refreshedForce);

            pendingBotSwipe = refreshedSwipe;
            pendingBotDirection = refreshedDirection;
            pendingBotForce = refreshedForce;
            pendingBotSeedId = botShotSolver.GetLastSeedId();
            pendingBotTargetPlateIndex = refreshed.bestCandidate.targetPlateIndex;
            pendingBotTargetSlotIndex = refreshed.bestCandidate.targetSlotIndex;
            pendingBotSeedStartPositionValid = botShotSolver.TryGetLastChosenSeedStartPosition(out pendingBotSeedStartPosition);
        }

        if (launcher.SimulateOfflineBotSwipe(currentBall, pendingBotSwipe) && logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchController] Bot solver launch -> " +
                "MissStacks=" + consecutiveBotMisses +
                " | SeedId=" + pendingBotSeedId +
                " | TargetPlate=" + pendingBotTargetPlateIndex +
                " | TargetSlot=" + pendingBotTargetSlotIndex +
                " | DecisionReason=" + pendingBotDecisionReason +
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

    private float GetBotPostSpawnDelaySeconds()
    {
        float minDelay = Mathf.Max(0.10f, postSpawnDelayMinSeconds);
        float maxDelay = Mathf.Max(minDelay, postSpawnDelayMaxSeconds);

        if (deterministicBotTestMode)
            return Mathf.Clamp(deterministicPostSpawnDelaySeconds, minDelay, maxDelay);

        return Random.Range(minDelay, maxDelay);
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
            case BotDifficulty.Easy:
                return Random.Range(easyThinkDelayMin, easyThinkDelayMax);

            case BotDifficulty.Medium:
                return Random.Range(mediumThinkDelayMin, mediumThinkDelayMax);

            case BotDifficulty.Hard:
                return Random.Range(hardThinkDelayMin, hardThinkDelayMax);

            case BotDifficulty.Unbeatable:
                return Random.Range(unbeatableThinkDelayMin, unbeatableThinkDelayMax);

            default:
                return Random.Range(mediumThinkDelayMin, mediumThinkDelayMax);
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

        if (logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchController] SpawnNewActiveBall -> " +
                "Owner=" + owner +
                " | Pos=" + currentBall.transform.position,
                this);
        }

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
        pendingBotDecisionReason = string.Empty;
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
        if (botTargetDecisionService == null) botTargetDecisionService = FindFirstObjectByType<BotTargetDecisionService>();
#else
        if (ballTurnSpawner == null) ballTurnSpawner = FindObjectOfType<BallTurnSpawner>();
        if (hud == null) hud = FindObjectOfType<FusionOnlineMatchHUD>();
        if (scoreManager == null) scoreManager = FindObjectOfType<ScoreManager>();
        if (bottomBarOrderSwapper == null) bottomBarOrderSwapper = FindObjectOfType<BottomBarOrderSwapper>();
        if (botShotSolver == null) botShotSolver = FindObjectOfType<BotShotSolver>();
        if (playerShotDebugRecorder == null) playerShotDebugRecorder = FindObjectOfType<PlayerShotDebugRecorder>();
        if (botTargetDecisionService == null) botTargetDecisionService = FindObjectOfType<BotTargetDecisionService>();
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
        lastDecisionReason = string.Empty;
    }
}