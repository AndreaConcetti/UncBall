using System.Threading.Tasks;
using UnityEngine;

public sealed class LuckyShotGameplayController : MonoBehaviour
{
    public static LuckyShotGameplayController Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private LuckyShotSessionRuntime sessionRuntime;
    [SerializeField] private LuckyShotShotResolver shotResolver;
    [SerializeField] private BallTurnSpawner ballTurnSpawner;
    [SerializeField] private BallLauncher ballLauncher;

    [Header("Lucky Shot Rules")]
    [SerializeField] private PlayerID localLuckyShotPlayer = PlayerID.Player1;
    [SerializeField] private bool autoSpawnBallOnSessionLoaded = true;

    [Tooltip("Delay solo visivo/pratico tra una ball risolta e lo spawn della successiva. Non controlla la fine sessione.")]
    [SerializeField] private float retryRespawnDelaySeconds = 1.1f;

    [Header("Ball Settlement Watchdog")]
    [Tooltip("Abilita il controllo fisico della ball lanciata. Serve per risolvere una ball ferma/stuck senza usare un delay cieco.")]
    [SerializeField] private bool useBallSettlementWatchdog = true;

    [Tooltip("Tempo minimo dopo il lancio prima di considerare valida la valutazione di ball ferma.")]
    [SerializeField] private float minSecondsAfterLaunchBeforeSettlementCheck = 0.35f;

    [Tooltip("La ball deve restare sotto le soglie fisiche per questo tempo continuo prima di essere considerata ferma.")]
    [SerializeField] private float requiredStableSeconds = 0.75f;

    [Tooltip("Velocità lineare massima per considerare la ball ferma.")]
    [SerializeField] private float linearVelocityThreshold = 0.08f;

    [Tooltip("Velocità angolare massima per considerare la ball ferma.")]
    [SerializeField] private float angularVelocityThreshold = 0.35f;

    [Tooltip("Movimento massimo tra due frame per considerare la ball stabile.")]
    [SerializeField] private float positionDeltaThreshold = 0.0125f;

    [Tooltip("Se true, Rigidbody.IsSleeping viene considerato come segnale forte di ball stabile.")]
    [SerializeField] private bool acceptRigidbodySleepAsStable = true;

    [Tooltip("Se true, quando la ball risulta stabile ma non è entrata in slot/death zone, il tiro viene risolto come miss.")]
    [SerializeField] private bool resolveSettledBallAsMiss = true;

    [Header("Runtime State")]
    [SerializeField] private BallPhysics currentBall;
    [SerializeField] private bool shotLaunchObserved;
    [SerializeField] private bool shotResolutionPending;
    [SerializeField] private bool spawnInProgress;
    [SerializeField] private bool retryRespawnScheduled;
    [SerializeField] private bool currentBallAlreadyResolved;

    [Header("Settlement Runtime")]
    [SerializeField] private Rigidbody currentBallRigidbody;
    [SerializeField] private float launchedElapsedSeconds;
    [SerializeField] private float stableElapsedSeconds;
    [SerializeField] private Vector3 lastObservedBallPosition;
    [SerializeField] private bool hasLastObservedBallPosition;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    public BallPhysics CurrentBall => currentBall;
    public bool HasActiveBall => currentBall != null;
    public bool ShotLaunchObserved => shotLaunchObserved;
    public bool ShotResolutionPending => shotResolutionPending;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResetControllerStateForSceneEntry();
        Subscribe();

        if (sessionRuntime != null && sessionRuntime.HasActiveSession && autoSpawnBallOnSessionLoaded)
            EnsurePlayableStateFromSession(sessionRuntime.CurrentSession);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (ballLauncher == null || sessionRuntime == null || currentBall == null)
            return;

        if (!shotLaunchObserved && ballLauncher.CurrentPhase == BallLauncher.LaunchPhase.Launched)
        {
            shotLaunchObserved = true;
            ResetSettlementRuntimeForCurrentBall();

            _ = MarkShotConsumedAsync();

            if (verboseLogs)
                Debug.Log("[LuckyShotGameplayController] Launch observed for current Lucky Shot ball.", this);
        }

        TickBallSettlementWatchdog();
    }

    [ContextMenu("Lucky Shot/Force Finish Current Session As Miss")]
    private void InspectorForceFinishCurrentSessionAsMiss()
    {
        _ = DebugForceFinishCurrentSessionAsMissAsync();
    }

    [ContextMenu("Lucky Shot/Reset Local Gameplay Controller State")]
    private void InspectorResetLocalGameplayControllerState()
    {
        ResetControllerStateForSceneEntry();

        if (verboseLogs)
            Debug.Log("[LuckyShotGameplayController] InspectorResetLocalGameplayControllerState", this);
    }

    public void EnsurePlayableStateFromSession(LuckyShotActiveSession session)
    {
        if (sessionRuntime == null)
            return;

        if (!sessionRuntime.HasActiveSession)
            return;

        if (!session.IsValid())
            return;

        ConfigureLaunchSide(session.launchSide);

        bool shouldHaveBall =
            session.remainingShots > 0 &&
            !shotResolutionPending &&
            !retryRespawnScheduled;

        if (!shouldHaveBall)
            return;

        if (currentBall != null)
        {
            BindCurrentBall();
            return;
        }

        _ = SpawnFreshBallAsync();
    }

    public async Task SpawnFreshBallAsync()
    {
        ResolveReferences();

        if (spawnInProgress)
            return;

        if (ballTurnSpawner == null || ballLauncher == null)
        {
            Debug.LogError("[LuckyShotGameplayController] Missing BallTurnSpawner or BallLauncher.", this);
            return;
        }

        if (sessionRuntime != null)
        {
            if (!sessionRuntime.HasActiveSession)
                return;

            LuckyShotActiveSession session = sessionRuntime.CurrentSession;
            if (!session.IsValid() || session.remainingShots <= 0)
                return;
        }

        spawnInProgress = true;

        try
        {
            ClearLauncherBindingOnly();

            BallPhysics spawned = ballTurnSpawner.SpawnOfflineBallForOwner(localLuckyShotPlayer);
            if (spawned == null)
            {
                Debug.LogError("[LuckyShotGameplayController] SpawnOfflineBallForOwner returned null.", this);
                return;
            }

            currentBall = spawned;
            currentBallRigidbody = ResolveBallRigidbody(currentBall);

            shotLaunchObserved = false;
            shotResolutionPending = false;
            currentBallAlreadyResolved = false;

            ResetSettlementRuntimeForCurrentBall();

            await Task.Yield();
            await Task.Yield();

            BindCurrentBall();

            if (shotResolver != null)
                shotResolver.PrepareForNewBall(spawned);

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotGameplayController] SpawnFreshBallAsync -> Spawned new Lucky Shot ball. " +
                    "Owner=" + localLuckyShotPlayer +
                    " | Ball=" + currentBall.name +
                    " | Rigidbody=" + (currentBallRigidbody != null),
                    this);
            }
        }
        finally
        {
            spawnInProgress = false;
        }
    }

    public async void NotifyBallLost(BallPhysics lostBall)
    {
        if (lostBall == null)
            return;

        if (currentBall == null || lostBall != currentBall)
        {
            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotGameplayController] NotifyBallLost ignored -> not current logical ball. " +
                    "Lost=" + lostBall.name +
                    " | Current=" + (currentBall != null ? currentBall.name : "<null>"),
                    this);
            }

            return;
        }

        if (shotResolutionPending)
            return;

        shotResolutionPending = true;

        if (verboseLogs)
            Debug.Log("[LuckyShotGameplayController] NotifyBallLost -> resolving miss by death zone.", this);

        CleanupCurrentBallReference();

        LuckyShotResolvedResult result = default;

        if (sessionRuntime != null)
            result = await sessionRuntime.ResolveMissAsync();

        if (result.success && result.isFinalResolution)
            return;

        TryScheduleRetryBallSpawn();
    }

    public void NotifyHitResolved(BallPhysics resolvedBall)
    {
        if (resolvedBall == null)
            return;

        if (currentBall == resolvedBall)
            currentBallAlreadyResolved = true;
    }

    public void NotifyHitResolved(LuckyShotResolvedResult result)
    {
        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] NotifyHitResolved(Result) -> " +
                "Success=" + result.success +
                " | Win=" + result.isWin +
                " | RewardGranted=" + result.rewardGranted +
                " | Remaining=" + result.remainingShotsAfterResolve +
                " | Final=" + result.isFinalResolution,
                this);
        }

        if (!result.success)
            return;

        if (currentBall != null)
        {
            currentBallAlreadyResolved = true;

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotGameplayController] NotifyHitResolved(Result) -> detaching resolved ball without destroying it. " +
                    "Ball=" + currentBall.name,
                    this);
            }

            CleanupCurrentBallReference();
        }

        if (result.isFinalResolution)
            return;

        if (result.canRetry && result.remainingShotsAfterResolve > 0)
            TryScheduleRetryBallSpawn();
    }

    private async Task MarkShotConsumedAsync()
    {
        if (sessionRuntime == null)
            return;

        bool consumed = await sessionRuntime.MarkShotConsumedAsync();
        if (!consumed)
            return;

        if (verboseLogs && sessionRuntime.CurrentSession.IsValid())
        {
            Debug.Log(
                "[LuckyShotGameplayController] MarkShotConsumedAsync -> RemainingShots=" +
                sessionRuntime.CurrentSession.remainingShots,
                this);
        }
    }

    private void TickBallSettlementWatchdog()
    {
        if (!useBallSettlementWatchdog)
            return;

        if (!resolveSettledBallAsMiss)
            return;

        if (sessionRuntime == null)
            return;

        if (currentBall == null)
            return;

        if (!shotLaunchObserved)
            return;

        if (shotResolutionPending)
            return;

        if (currentBallAlreadyResolved)
            return;

        if (!sessionRuntime.HasActiveSession)
            return;

        LuckyShotActiveSession session = sessionRuntime.CurrentSession;
        if (!session.IsValid())
            return;

        launchedElapsedSeconds += Time.deltaTime;

        if (launchedElapsedSeconds < Mathf.Max(0f, minSecondsAfterLaunchBeforeSettlementCheck))
            return;

        bool stable = IsCurrentBallPhysicallyStable();

        if (stable)
        {
            stableElapsedSeconds += Time.deltaTime;
        }
        else
        {
            stableElapsedSeconds = 0f;
        }

        if (stableElapsedSeconds < Mathf.Max(0.05f, requiredStableSeconds))
            return;

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] TickBallSettlementWatchdog -> ball settled without slot/death-zone resolution. " +
                "Ball=" + currentBall.name +
                " | RemainingShots=" + session.remainingShots +
                " | StableElapsed=" + stableElapsedSeconds.ToString("0.00"),
                this);
        }

        _ = ResolveCurrentSettledBallAsMissAsync();
    }

    private bool IsCurrentBallPhysicallyStable()
    {
        if (currentBall == null)
            return false;

        if (currentBallRigidbody == null)
            currentBallRigidbody = ResolveBallRigidbody(currentBall);

        Vector3 currentPosition = currentBall.transform.position;

        float positionDelta = 0f;
        if (hasLastObservedBallPosition)
            positionDelta = Vector3.Distance(lastObservedBallPosition, currentPosition);

        lastObservedBallPosition = currentPosition;
        hasLastObservedBallPosition = true;

        if (currentBallRigidbody == null)
        {
            return positionDelta <= Mathf.Max(0.0001f, positionDeltaThreshold);
        }

        float linearVelocity = currentBallRigidbody.linearVelocity.magnitude;
        float angularVelocity = currentBallRigidbody.angularVelocity.magnitude;

        bool sleeping = acceptRigidbodySleepAsStable && currentBallRigidbody.IsSleeping();

        bool belowVelocityThresholds =
            linearVelocity <= Mathf.Max(0.0001f, linearVelocityThreshold) &&
            angularVelocity <= Mathf.Max(0.0001f, angularVelocityThreshold);

        bool belowPositionDelta =
            positionDelta <= Mathf.Max(0.0001f, positionDeltaThreshold);

        return sleeping || (belowVelocityThresholds && belowPositionDelta);
    }

    private async Task ResolveCurrentSettledBallAsMissAsync()
    {
        if (shotResolutionPending)
            return;

        if (currentBall == null)
            return;

        if (sessionRuntime == null)
            return;

        if (!sessionRuntime.HasActiveSession)
            return;

        shotResolutionPending = true;

        BallPhysics settledBall = currentBall;

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] ResolveCurrentSettledBallAsMissAsync -> resolving settled/stuck ball as miss. " +
                "Ball=" + settledBall.name,
                this);
        }

        CleanupCurrentBallReference();

        LuckyShotResolvedResult result = await sessionRuntime.ResolveMissAsync();

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] ResolveCurrentSettledBallAsMissAsync -> Result " +
                "Success=" + result.success +
                " | Final=" + result.isFinalResolution +
                " | CanRetry=" + result.canRetry +
                " | Remaining=" + result.remainingShotsAfterResolve,
                this);
        }

        if (result.success && result.isFinalResolution)
            return;

        TryScheduleRetryBallSpawn();
    }

    private async Task DebugForceFinishCurrentSessionAsMissAsync()
    {
        ResolveReferences();

        if (sessionRuntime == null)
        {
            Debug.LogWarning("[LuckyShotGameplayController] DebugForceFinishCurrentSessionAsMissAsync -> SessionRuntime missing.", this);
            return;
        }

        if (!sessionRuntime.CurrentSession.IsValid())
        {
            Debug.LogWarning("[LuckyShotGameplayController] DebugForceFinishCurrentSessionAsMissAsync -> No valid session.", this);
            return;
        }

        CleanupCurrentBallReference();

        int guard = 0;

        while (sessionRuntime.CurrentSession.IsValid() &&
               sessionRuntime.CurrentSession.hasActiveSession &&
               sessionRuntime.CurrentSession.remainingShots > 0 &&
               guard < 10)
        {
            guard++;

            await sessionRuntime.MarkShotConsumedAsync();

            LuckyShotResolvedResult partial = await sessionRuntime.ResolveMissAsync();

            if (partial.success && partial.isFinalResolution)
                break;
        }

        if (sessionRuntime.CurrentSession.IsValid() &&
            sessionRuntime.CurrentSession.hasActiveSession &&
            sessionRuntime.CurrentSession.remainingShots <= 0)
        {
            await sessionRuntime.ResolveMissAsync();
        }

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] DebugForceFinishCurrentSessionAsMissAsync -> completed. Guard=" + guard,
                this);
        }
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        EnsurePlayableStateFromSession(session);
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (!preview.hasActiveSession)
            return;

        ConfigureLaunchSide(preview.launchSide);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] HandleSessionPreviewChanged -> " +
                "Remaining=" + preview.remainingShots +
                " | HasCurrentBall=" + (currentBall != null) +
                " | SpawnInProgress=" + spawnInProgress +
                " | RetryScheduled=" + retryRespawnScheduled +
                " | ShotLaunchObserved=" + shotLaunchObserved +
                " | ShotResolutionPending=" + shotResolutionPending,
                this);
        }

        if (preview.remainingShots > 0 &&
            currentBall == null &&
            !shotResolutionPending &&
            !spawnInProgress &&
            !retryRespawnScheduled)
        {
            _ = SpawnFreshBallAsync();
        }
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        if (result.success && currentBall != null)
            currentBallAlreadyResolved = true;

        if (result.success && result.isFinalResolution)
        {
            CleanupCurrentBallReference();
            return;
        }

        if (result.success && result.canRetry && result.remainingShotsAfterResolve > 0)
        {
            CleanupCurrentBallReference();
            TryScheduleRetryBallSpawn();
        }
    }

    private void ConfigureLaunchSide(LuckyShotLaunchSide side)
    {
        if (ballTurnSpawner == null)
            return;

        bool player1OnLeft = side == LuckyShotLaunchSide.Left;
        ballTurnSpawner.SetPlayer1Side(player1OnLeft);

        if (verboseLogs)
            Debug.Log("[LuckyShotGameplayController] ConfigureLaunchSide -> " + side, this);
    }

    private void BindCurrentBall()
    {
        if (ballTurnSpawner == null || ballLauncher == null || currentBall == null)
            return;

        ballTurnSpawner.TryBindCurrentOfflineBallForLocalControl(currentBall, localLuckyShotPlayer, localLuckyShotPlayer);

        ballLauncher.ball = currentBall;
        ballLauncher.SetActivePlacementArea(ballTurnSpawner.GetPlacementAreaForOwner(localLuckyShotPlayer));
        ballLauncher.ResetLaunch();
        ballLauncher.enabled = true;
        ballLauncher.SetStandaloneOfflineLaunchEnabled(true);

        currentBallRigidbody = ResolveBallRigidbody(currentBall);
        ResetSettlementRuntimeForCurrentBall();

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] BindCurrentBall -> " +
                "Ball=" + currentBall.name +
                " | Owner=" + localLuckyShotPlayer +
                " | Rigidbody=" + (currentBallRigidbody != null),
                this);
        }
    }

    private void CleanupCurrentBallReference()
    {
        currentBall = null;
        currentBallRigidbody = null;

        shotLaunchObserved = false;
        shotResolutionPending = false;
        currentBallAlreadyResolved = false;

        launchedElapsedSeconds = 0f;
        stableElapsedSeconds = 0f;
        lastObservedBallPosition = Vector3.zero;
        hasLastObservedBallPosition = false;

        ClearLauncherBindingOnly();
    }

    private void ClearLauncherBindingOnly()
    {
        if (ballLauncher == null)
            return;

        ballLauncher.ball = null;
        ballLauncher.SetActivePlacementArea(null);
        ballLauncher.ResetLaunch();
        ballLauncher.enabled = true;
        ballLauncher.SetStandaloneOfflineLaunchEnabled(true);
    }

    private void TryScheduleRetryBallSpawn()
    {
        if (retryRespawnScheduled)
            return;

        if (spawnInProgress)
            return;

        if (currentBall != null)
            return;

        if (sessionRuntime == null || !sessionRuntime.HasActiveSession)
            return;

        LuckyShotActiveSession session = sessionRuntime.CurrentSession;
        if (!session.IsValid())
            return;

        if (session.remainingShots <= 0)
            return;

        retryRespawnScheduled = true;
        _ = SpawnNextBallDelayedAsync();
    }

    private async Task SpawnNextBallDelayedAsync()
    {
        if (retryRespawnDelaySeconds > 0f)
            await Task.Delay(Mathf.RoundToInt(retryRespawnDelaySeconds * 1000f));

        retryRespawnScheduled = false;

        if (currentBall != null)
            return;

        if (sessionRuntime == null || !sessionRuntime.HasActiveSession)
            return;

        LuckyShotActiveSession session = sessionRuntime.CurrentSession;
        if (!session.IsValid())
            return;

        if (session.remainingShots <= 0)
            return;

        await SpawnFreshBallAsync();
    }

    private void ResetSettlementRuntimeForCurrentBall()
    {
        launchedElapsedSeconds = 0f;
        stableElapsedSeconds = 0f;

        if (currentBall != null)
        {
            lastObservedBallPosition = currentBall.transform.position;
            hasLastObservedBallPosition = true;
        }
        else
        {
            lastObservedBallPosition = Vector3.zero;
            hasLastObservedBallPosition = false;
        }
    }

    private Rigidbody ResolveBallRigidbody(BallPhysics ball)
    {
        if (ball == null)
            return null;

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
            return rb;

        return ball.GetComponentInChildren<Rigidbody>(true);
    }

    private void ResolveReferences()
    {
        if (sessionRuntime == null)
            sessionRuntime = LuckyShotSessionRuntime.Instance;

#if UNITY_2023_1_OR_NEWER
        if (sessionRuntime == null)
            sessionRuntime = FindFirstObjectByType<LuckyShotSessionRuntime>();

        if (shotResolver == null)
            shotResolver = FindFirstObjectByType<LuckyShotShotResolver>();

        if (ballTurnSpawner == null)
            ballTurnSpawner = FindFirstObjectByType<BallTurnSpawner>(FindObjectsInactive.Include);

        if (ballLauncher == null)
            ballLauncher = FindFirstObjectByType<BallLauncher>(FindObjectsInactive.Include);
#else
        if (sessionRuntime == null)
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();

        if (shotResolver == null)
            shotResolver = FindObjectOfType<LuckyShotShotResolver>();

        if (ballTurnSpawner == null)
            ballTurnSpawner = FindObjectOfType<BallTurnSpawner>(true);

        if (ballLauncher == null)
            ballLauncher = FindObjectOfType<BallLauncher>(true);
#endif
    }

    private void Subscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionLoaded += HandleSessionLoaded;

        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionPreviewChanged += HandleSessionPreviewChanged;

        sessionRuntime.SessionResolved -= HandleSessionResolved;
        sessionRuntime.SessionResolved += HandleSessionResolved;
    }

    private void Unsubscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionResolved -= HandleSessionResolved;
    }

    private void ResetControllerStateForSceneEntry()
    {
        currentBall = null;
        currentBallRigidbody = null;

        shotLaunchObserved = false;
        shotResolutionPending = false;
        spawnInProgress = false;
        retryRespawnScheduled = false;
        currentBallAlreadyResolved = false;

        launchedElapsedSeconds = 0f;
        stableElapsedSeconds = 0f;
        lastObservedBallPosition = Vector3.zero;
        hasLastObservedBallPosition = false;

        ClearLauncherBindingOnly();
    }
}