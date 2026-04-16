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

    private enum MidMatchBreakReason
    {
        None = 0,
        TimeHalftime = 1,
        ScoreHalfPoint = 2
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

    [Header("Mid Match Break")]
    [SerializeField] private float defaultBreakDuration = 8f;
    [SerializeField] private float postShotPanelDelay = 1.5f;

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
    [SerializeField] private float easyMajorMissChanceBoard1 = 0.20f;
    [SerializeField] private float easyMajorMissChanceBoard2 = 0.40f;
    [SerializeField] private float easyMajorMissChanceBoard3 = 0.60f;
    [SerializeField] private float easyMinorSwipeXBoard1 = 32f;
    [SerializeField] private float easyMinorSwipeXBoard2 = 58f;
    [SerializeField] private float easyMinorSwipeXBoard3 = 94f;
    [SerializeField] private float easyMinorSwipeYBoard1 = 24f;
    [SerializeField] private float easyMinorSwipeYBoard2 = 50f;
    [SerializeField] private float easyMinorSwipeYBoard3 = 88f;
    [SerializeField] private float easyMajorSwipeXBoard1 = 100f;
    [SerializeField] private float easyMajorSwipeXBoard2 = 170f;
    [SerializeField] private float easyMajorSwipeXBoard3 = 255f;
    [SerializeField] private float easyMajorSwipeYBoard1 = 78f;
    [SerializeField] private float easyMajorSwipeYBoard2 = 140f;
    [SerializeField] private float easyMajorSwipeYBoard3 = 220f;

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
    [SerializeField] private PlayerID initialMatchStartingOwner = PlayerID.Player1;
    [SerializeField] private bool player1OnLeft = true;
    [SerializeField] private bool initialPlayer1OnLeft = true;
    [SerializeField] private bool matchStarted;
    [SerializeField] private bool matchEnded;
    [SerializeField] private PlayerID winner = PlayerID.None;
    [SerializeField] private OnlineMatchEndReason matchEndReason = OnlineMatchEndReason.NormalCompletion;
    [SerializeField] private float turnTimeRemaining;
    [SerializeField] private float matchTimeRemaining;
    [SerializeField] private int scoreP1;
    [SerializeField] private int scoreP2;
    [SerializeField] private bool shotInFlight;
    [SerializeField] private bool currentPlayerScoredThisTurn;
    [SerializeField] private float botThinkTimeRemaining;
    [SerializeField] private int consecutiveBotMisses;

    [Header("Mid Match Break Runtime")]
    [SerializeField] private bool midMatchBreakTriggered;
    [SerializeField] private bool midMatchBreakActive;
    [SerializeField] private bool pendingBreakAfterShot;
    [SerializeField] private bool pendingEndAfterShot;
    [SerializeField] private float breakTimeRemaining;
    [SerializeField] private float postShotDelayRemaining;
    [SerializeField] private MidMatchBreakReason midMatchBreakReason = MidMatchBreakReason.None;

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
    public bool MidMatchBreakActive => midMatchBreakActive;
    public bool IsTimeHalftimeActive => midMatchBreakActive && midMatchBreakReason == MidMatchBreakReason.TimeHalftime;
    public bool IsHalfPointActive => midMatchBreakActive && midMatchBreakReason == MidMatchBreakReason.ScoreHalfPoint;
    public float CurrentBreakTimeRemaining => breakTimeRemaining;
    public BallPhysics CurrentBall => currentBall;
    public OnlineMatchEndReason MatchEndReason => matchEndReason;
    public PlayerID Winner => winner;

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
        initialMatchStartingOwner = currentTurnOwner;
        initialPlayer1OnLeft = player1OnLeft;

        matchStarted = true;
        matchEnded = false;
        winner = PlayerID.None;
        matchEndReason = OnlineMatchEndReason.NormalCompletion;
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

        midMatchBreakTriggered = false;
        midMatchBreakActive = false;
        pendingBreakAfterShot = false;
        pendingEndAfterShot = false;
        breakTimeRemaining = Mathf.Max(0f, defaultBreakDuration);
        postShotDelayRemaining = 0f;
        midMatchBreakReason = MidMatchBreakReason.None;

        ClearPendingBotLaunch();
        ResetBotDebug();

        if (ballTurnSpawner != null)
        {
            ballTurnSpawner.SetPlayer1Side(player1OnLeft);
            ballTurnSpawner.ClearAllBallsInScene();
        }

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(player1OnLeft);

        bool scoreManagerStartedOk = TryStartScoreManagerSafely();
        if (!scoreManagerStartedOk && !continueBootstrapIfScoreManagerThrows)
            return;

        SpawnNewActiveBall(currentTurnOwner);
        ApplyHudState();
    }

    public void RequestResumeAfterHalftime()
    {
        if (!midMatchBreakActive || matchEnded)
            return;

        ResumeAfterMidMatchBreak();
    }

    public void RequestLocalSurrender()
    {
        if (!isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded)
            return;

        if (logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchController] RequestLocalSurrender -> " +
                "LocalPlayer=" + localPlayerId +
                " | BotPlayer=" + botPlayerId,
                this);
        }

        if (playerShotDebugRecorder != null)
            playerShotDebugRecorder.FinalizePendingShot("LocalSurrender");

        winner = botPlayerId;
        matchEndReason = OnlineMatchEndReason.SurrenderLoss;

        ForceEndOfflineMatch();
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
        if (!isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded || midMatchBreakActive)
            return;

        if (currentTurnOwner != localPlayerId)
            return;

        LaunchTrackedBallForOwner(localPlayerId, direction, force);
    }

    public void RequestBotLaunchCurrentBall(Vector3 direction, float force)
    {
        if (!isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded || midMatchBreakActive)
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

        if (pendingEndAfterShot)
        {
            postShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (pendingBreakAfterShot)
        {
            postShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        PlayerID nextOwner = currentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        if (!midMatchBreakTriggered && ShouldTriggerMidMatchBreak())
        {
            StartMidMatchBreak();
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
        catch
        {
            return false;
        }
    }

    private void TickOfflineMatch()
    {
        if (!matchStarted || matchEnded || activeRequest == null)
            return;

        if (postShotDelayRemaining > 0f)
        {
            postShotDelayRemaining -= Time.deltaTime;
            if (postShotDelayRemaining < 0f)
                postShotDelayRemaining = 0f;

            if (postShotDelayRemaining <= 0f)
            {
                if (pendingBreakAfterShot)
                {
                    pendingBreakAfterShot = false;
                    StartMidMatchBreak();
                    return;
                }

                if (pendingEndAfterShot)
                {
                    pendingEndAfterShot = false;
                    EndOfflineMatch();
                    return;
                }
            }
        }

        if (midMatchBreakActive)
        {
            breakTimeRemaining = Mathf.Max(0f, breakTimeRemaining - Time.deltaTime);

            if (breakTimeRemaining <= 0f)
                ResumeAfterMidMatchBreak();

            return;
        }

        if (activeRequest.MatchMode == MatchMode.TimeLimit)
        {
            float halftimeThreshold = activeRequest.MatchDurationSeconds * 0.5f;

            if (shotInFlight)
            {
                if (!midMatchBreakTriggered)
                {
                    float nextTime = matchTimeRemaining - Time.deltaTime;
                    if (nextTime <= halftimeThreshold)
                    {
                        matchTimeRemaining = halftimeThreshold;
                        pendingBreakAfterShot = true;
                    }
                    else
                    {
                        matchTimeRemaining = nextTime;
                    }
                }
                else
                {
                    float nextTime = matchTimeRemaining - Time.deltaTime;
                    if (nextTime <= 0f)
                    {
                        matchTimeRemaining = 0f;
                        pendingEndAfterShot = true;
                    }
                    else
                    {
                        matchTimeRemaining = nextTime;
                    }
                }
            }
            else
            {
                matchTimeRemaining = Mathf.Max(0f, matchTimeRemaining - Time.deltaTime);
            }
        }

        if (!shotInFlight)
            turnTimeRemaining = Mathf.Max(0f, turnTimeRemaining - Time.deltaTime);

        RefreshCurrentBallCache();

        if (!shotInFlight)
        {
            if (!midMatchBreakTriggered && AreAllBoardsFullNow())
            {
                StartMidMatchBreak();
                return;
            }

            if (midMatchBreakTriggered)
            {
                if (ShouldForceSecondHalfMathematicalEnd(out PlayerID forcedWinner))
                {
                    winner = forcedWinner;
                    EndOfflineMatch();
                    return;
                }

                if (AreAllBoardsFullNow())
                {
                    EndOfflineMatch();
                    return;
                }
            }

            if (activeRequest.MatchMode == MatchMode.TimeLimit)
            {
                float halftimeThreshold = activeRequest.MatchDurationSeconds * 0.5f;

                if (!midMatchBreakTriggered && matchTimeRemaining <= halftimeThreshold)
                {
                    matchTimeRemaining = halftimeThreshold;
                    StartMidMatchBreak();
                    return;
                }

                if (midMatchBreakTriggered && matchTimeRemaining <= 0f)
                {
                    matchTimeRemaining = 0f;
                    EndOfflineMatch();
                    return;
                }
            }
            else
            {
                if (ShouldEndMatchNow())
                {
                    EndOfflineMatch();
                    return;
                }

                if (!midMatchBreakTriggered && ShouldTriggerMidMatchBreak())
                {
                    StartMidMatchBreak();
                    return;
                }
            }

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

    private bool ShouldTriggerMidMatchBreak()
    {
        if (midMatchBreakTriggered || shotInFlight || activeRequest == null)
            return false;

        if (AreAllBoardsFullNow())
            return true;

        if (activeRequest.MatchMode == MatchMode.TimeLimit)
        {
            float threshold = activeRequest.MatchDurationSeconds * 0.5f;
            return matchTimeRemaining <= threshold;
        }

        int halfTarget = Mathf.Max(1, Mathf.CeilToInt(activeRequest.PointsToWin * 0.5f));
        return scoreP1 >= halfTarget || scoreP2 >= halfTarget;
    }

    private void StartMidMatchBreak()
    {
        if (matchEnded || midMatchBreakTriggered || activeRequest == null)
            return;

        midMatchBreakTriggered = true;
        midMatchBreakActive = true;
        breakTimeRemaining = Mathf.Max(0f, defaultBreakDuration);
        midMatchBreakReason = activeRequest.MatchMode == MatchMode.TimeLimit
            ? MidMatchBreakReason.TimeHalftime
            : MidMatchBreakReason.ScoreHalfPoint;

        pendingBreakAfterShot = false;
        pendingEndAfterShot = false;
        postShotDelayRemaining = 0f;
        shotInFlight = false;
        currentPlayerScoredThisTurn = false;
        botThinkTimeRemaining = 0f;

        ClearPendingBotLaunch();
        DestroyTrackedBallIfNeeded();

        if (scoreManager != null)
            scoreManager.BeginHalftime();

        player1OnLeft = !player1OnLeft;

        if (ballTurnSpawner != null)
        {
            ballTurnSpawner.SetPlayer1Side(player1OnLeft);
            ballTurnSpawner.ClearAllBallsInScene();
        }

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(player1OnLeft);

        if (logDebug)
        {
            Debug.Log(
                "[OfflineBotMatchController] StartMidMatchBreak -> " +
                "Reason=" + midMatchBreakReason +
                " | ScoreP1=" + scoreP1 +
                " | ScoreP2=" + scoreP2 +
                " | AllBoardsFull=" + AreAllBoardsFullNow(),
                this);
        }
    }

    private void ResumeAfterMidMatchBreak()
    {
        if (!midMatchBreakActive || matchEnded)
            return;

        midMatchBreakActive = false;
        breakTimeRemaining = Mathf.Max(0f, defaultBreakDuration);
        midMatchBreakReason = MidMatchBreakReason.None;
        turnTimeRemaining = Mathf.Max(1f, activeRequest != null ? activeRequest.TurnDurationSeconds : 1f);
        shotInFlight = false;
        currentPlayerScoredThisTurn = false;
        botThinkTimeRemaining = 0f;

        if (scoreManager != null)
            scoreManager.EndHalftime();

        PlayerID secondHalfStarter = initialMatchStartingOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        SpawnNewActiveBall(secondHalfStarter);

        if (bottomBarOrderSwapper != null)
            bottomBarOrderSwapper.SetOrder(player1OnLeft);
    }

    private void TickBotBrain()
    {
        if (!enableBotBrain || !isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded || midMatchBreakActive)
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
            return;

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

        pendingBotDecisionReason = usedForcedStrategicTarget
            ? serviceDecision.reason
            : (hasStrategicTarget ? serviceDecision.reason + " | FallbackGeneralSolve" : "GeneralSolve");

        pendingBotLaunchDelayRemaining = GetBotPostSpawnDelaySeconds();
        pendingBotLaunchState = PendingBotLaunchState.WaitingPostSpawnDelay;
    }

    private void ApplyDifficultyShotError(BallLauncher launcher, BotDifficulty difficulty, int targetPlateIndex, ref Vector2 swipe, ref Vector3 direction, ref float force)
    {
        if (!enableDifficultyShotError || launcher == null || difficulty == BotDifficulty.Unbeatable)
            return;

        BotShotErrorSettings settings = GetShotErrorSettings(difficulty);
        float missChance = GetMajorMissChance(settings, targetPlateIndex);
        float minorX = GetMinorSwipeX(settings, targetPlateIndex);
        float minorY = GetMinorSwipeY(settings, targetPlateIndex);
        float majorX = GetMajorSwipeX(settings, targetPlateIndex);
        float majorY = GetMajorSwipeY(settings, targetPlateIndex);

        bool majorMiss = Random.value < missChance;
        float deltaX = majorMiss ? RandomSignedMagnitude(majorX * 0.55f, majorX) : Random.Range(-minorX, minorX);
        float deltaY = majorMiss ? RandomSignedMagnitude(majorY * 0.45f, majorY) : Random.Range(-minorY, minorY);

        Vector2 originalSwipe = swipe;
        swipe += new Vector2(deltaX, deltaY);

        if (!launcher.TryBuildLaunchFromSwipe(swipe, out Vector3 rebuiltDirection, out float rebuiltForce))
        {
            swipe = originalSwipe;
            return;
        }

        direction = rebuiltDirection;
        force = rebuiltForce;
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

    private float GetMajorMissChance(BotShotErrorSettings settings, int targetPlateIndex) =>
        targetPlateIndex == 0 ? settings.majorMissChanceBoard1 :
        targetPlateIndex == 1 ? settings.majorMissChanceBoard2 :
        settings.majorMissChanceBoard3;

    private float GetMinorSwipeX(BotShotErrorSettings settings, int targetPlateIndex) =>
        targetPlateIndex == 0 ? settings.minorSwipeXBoard1 :
        targetPlateIndex == 1 ? settings.minorSwipeXBoard2 :
        settings.minorSwipeXBoard3;

    private float GetMinorSwipeY(BotShotErrorSettings settings, int targetPlateIndex) =>
        targetPlateIndex == 0 ? settings.minorSwipeYBoard1 :
        targetPlateIndex == 1 ? settings.minorSwipeYBoard2 :
        settings.minorSwipeYBoard3;

    private float GetMajorSwipeX(BotShotErrorSettings settings, int targetPlateIndex) =>
        targetPlateIndex == 0 ? settings.majorSwipeXBoard1 :
        targetPlateIndex == 1 ? settings.majorSwipeXBoard2 :
        settings.majorSwipeXBoard3;

    private float GetMajorSwipeY(BotShotErrorSettings settings, int targetPlateIndex) =>
        targetPlateIndex == 0 ? settings.majorSwipeYBoard1 :
        targetPlateIndex == 1 ? settings.majorSwipeYBoard2 :
        settings.majorSwipeYBoard3;

    private float RandomSignedMagnitude(float minAbs, float maxAbs)
    {
        float magnitude = Random.Range(minAbs, maxAbs);
        return Random.value < 0.5f ? -magnitude : magnitude;
    }

    private void ExecutePendingBotLaunch()
    {
        pendingBotLaunchState = PendingBotLaunchState.None;

        if (currentBall == null || currentTurnOwner != botPlayerId || shotInFlight || matchEnded || midMatchBreakActive)
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

        launcher.SimulateOfflineBotSwipe(currentBall, pendingBotSwipe);
        ClearPendingBotLaunch();
    }

    private void TryApplyBotSeedStartPosition(string seedId, Vector3 desiredSeedStart, bool isValid)
    {
        if (currentBall == null || currentTurnOwner != botPlayerId || !isValid)
            return;

        Vector3 adjusted = desiredSeedStart;
        adjusted.y = currentBall.transform.position.y;

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
    }

    private float GetBotPostSpawnDelaySeconds()
    {
        float minDelay = Mathf.Max(0.10f, postSpawnDelayMinSeconds);
        float maxDelay = Mathf.Max(minDelay, postSpawnDelayMaxSeconds);
        return deterministicBotTestMode ? Mathf.Clamp(deterministicPostSpawnDelaySeconds, minDelay, maxDelay) : Random.Range(minDelay, maxDelay);
    }

    private float GetBotPreLaunchDelaySeconds()
    {
        float minDelay = Mathf.Max(0.50f, preLaunchDelayMinSeconds);
        float maxDelay = Mathf.Max(minDelay, preLaunchDelayMaxSeconds);
        return deterministicBotTestMode ? Mathf.Clamp(deterministicPreLaunchDelaySeconds, minDelay, maxDelay) : Random.Range(minDelay, maxDelay);
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
            case BotDifficulty.Unbeatable: return Random.Range(unbeatableThinkDelayMin, unbeatableThinkDelayMax);
            default: return Random.Range(mediumThinkDelayMin, mediumThinkDelayMax);
        }
    }

    private void ResolveIdleTimeout()
    {
        bool botMiss = currentTurnOwner == botPlayerId;

        if (playerShotDebugRecorder != null && currentTurnOwner == localPlayerId)
            playerShotDebugRecorder.FinalizePendingShot("IdleTimeout");

        DestroyTrackedBallIfNeeded();
        ReleaseCurrentBallTracking();

        if (botMiss)
            consecutiveBotMisses++;

        if (pendingEndAfterShot)
        {
            postShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (pendingBreakAfterShot)
        {
            postShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        if (!midMatchBreakTriggered && ShouldTriggerMidMatchBreak())
        {
            StartMidMatchBreak();
            return;
        }

        PlayerID nextOwner = currentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
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

        if (pendingEndAfterShot)
        {
            postShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (pendingBreakAfterShot)
        {
            postShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        if (!midMatchBreakTriggered && ShouldTriggerMidMatchBreak())
        {
            StartMidMatchBreak();
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

        currentBall = null;
        currentBallRb = null;
        ReleaseCurrentBallTracking();

        if (pendingEndAfterShot)
        {
            postShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (pendingBreakAfterShot)
        {
            postShotDelayRemaining = Mathf.Max(0f, postShotPanelDelay);
            return;
        }

        if (ShouldEndMatchNow())
        {
            EndOfflineMatch();
            return;
        }

        if (!midMatchBreakTriggered && ShouldTriggerMidMatchBreak())
        {
            StartMidMatchBreak();
            return;
        }

        if (botMiss)
            consecutiveBotMisses++;

        PlayerID nextOwner = currentTurnOwner == PlayerID.Player1 ? PlayerID.Player2 : PlayerID.Player1;
        SpawnNewActiveBall(nextOwner);
    }

    private void SpawnNewActiveBall(PlayerID owner)
    {
        if (ballTurnSpawner == null || activeRequest == null || midMatchBreakActive || matchEnded)
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
        if (!isOfflineBotSessionActive || activeRequest == null || !matchStarted || matchEnded || midMatchBreakActive)
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

        if (activeRequest.MatchMode == MatchMode.TimeLimit)
        {
            if (midMatchBreakTriggered && matchTimeRemaining <= 0f)
                return true;

            if (midMatchBreakTriggered && AreAllBoardsFullNow())
                return true;

            if (midMatchBreakTriggered && ShouldForceSecondHalfMathematicalEnd(out PlayerID forcedWinner))
            {
                winner = forcedWinner;
                return true;
            }

            return false;
        }

        if (scoreP1 >= Mathf.Max(1, activeRequest.PointsToWin) ||
            scoreP2 >= Mathf.Max(1, activeRequest.PointsToWin))
            return true;

        if (midMatchBreakTriggered && AreAllBoardsFullNow())
            return true;

        if (midMatchBreakTriggered && ShouldForceSecondHalfMathematicalEnd(out PlayerID secondHalfForcedWinner))
        {
            winner = secondHalfForcedWinner;
            return true;
        }

        return false;
    }

    private bool AreAllBoardsFullNow()
    {
        return scoreManager != null && scoreManager.AreAllBoardsFull();
    }

    private bool ShouldForceSecondHalfMathematicalEnd(out PlayerID forcedWinner)
    {
        forcedWinner = PlayerID.None;

        if (!midMatchBreakTriggered || midMatchBreakActive || scoreManager == null)
            return false;

        int p1Remaining = scoreManager.GetMaxAdditionalPointsAvailable(PlayerID.Player1);
        int p2Remaining = scoreManager.GetMaxAdditionalPointsAvailable(PlayerID.Player2);

        bool p1CannotWin = scoreP1 + p1Remaining < scoreP2;
        bool p2CannotWin = scoreP2 + p2Remaining < scoreP1;

        if (p1CannotWin && !p2CannotWin)
        {
            forcedWinner = PlayerID.Player2;
            if (logDebug)
            {
                Debug.Log(
                    "[OfflineBotMatchController] Mathematical end -> Player2 wins. " +
                    "ScoreP1=" + scoreP1 + " | RemainingP1=" + p1Remaining +
                    " | ScoreP2=" + scoreP2 + " | RemainingP2=" + p2Remaining,
                    this);
            }
            return true;
        }

        if (p2CannotWin && !p1CannotWin)
        {
            forcedWinner = PlayerID.Player1;
            if (logDebug)
            {
                Debug.Log(
                    "[OfflineBotMatchController] Mathematical end -> Player1 wins. " +
                    "ScoreP1=" + scoreP1 + " | RemainingP1=" + p1Remaining +
                    " | ScoreP2=" + scoreP2 + " | RemainingP2=" + p2Remaining,
                    this);
            }
            return true;
        }

        return false;
    }

    private void ForceEndOfflineMatch()
    {
        if (matchEnded)
            return;

        ClearPendingBotLaunch();
        DestroyTrackedBallIfNeeded();

        shotInFlight = false;
        currentPlayerScoredThisTurn = false;
        midMatchBreakActive = false;
        pendingBreakAfterShot = false;
        pendingEndAfterShot = false;
        breakTimeRemaining = 0f;
        postShotDelayRemaining = 0f;
        midMatchBreakReason = MidMatchBreakReason.None;
        botThinkTimeRemaining = 0f;

        EndOfflineMatch();
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
        midMatchBreakActive = false;
        botThinkTimeRemaining = 0f;
        pendingBreakAfterShot = false;
        pendingEndAfterShot = false;
        postShotDelayRemaining = 0f;
        breakTimeRemaining = 0f;
        midMatchBreakReason = MidMatchBreakReason.None;

        if (winner == PlayerID.None)
            winner = ResolveWinner();

        ClearPendingBotLaunch();

        if (ballTurnSpawner != null)
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

        bool presentationPlayer1OnLeft = (midMatchBreakActive || matchEnded)
            ? initialPlayer1OnLeft
            : player1OnLeft;

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
            midMatchBreakActive,
            IsTimeHalftimeActive,
            IsHalfPointActive,
            breakTimeRemaining,
            activeRequest.GetPlayer1DisplayName(),
            activeRequest.GetPlayer2DisplayName(),
            winner,
            presentationPlayer1OnLeft,
            false,
            PlayerID.None,
            0f,
            matchEndReason
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