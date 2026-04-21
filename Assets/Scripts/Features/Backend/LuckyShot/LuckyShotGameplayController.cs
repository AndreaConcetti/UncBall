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
    [SerializeField] private bool respawnBallAfterAdExtraShot = true;
    [SerializeField] private float retryRespawnDelaySeconds = 0.9f;

    [Header("Runtime State")]
    [SerializeField] private BallPhysics currentBall;
    [SerializeField] private bool shotLaunchObserved;
    [SerializeField] private bool shotResolutionPending;
    [SerializeField] private bool spawnInProgress;
    [SerializeField] private bool retryRespawnScheduled;

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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();

        if (sessionRuntime != null && sessionRuntime.HasActiveSession && autoSpawnBallOnSessionLoaded)
            EnsurePlayableStateFromSession(sessionRuntime.CurrentSession);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (ballLauncher == null || sessionRuntime == null || currentBall == null)
            return;

        if (!shotLaunchObserved && ballLauncher.CurrentPhase == BallLauncher.LaunchPhase.Launched)
        {
            shotLaunchObserved = true;
            _ = MarkShotConsumedAsync();

            if (verboseLogs)
                Debug.Log("[LuckyShotGameplayController] Launch observed for current Lucky Shot ball.", this);
        }
    }

    public void EnsurePlayableStateFromSession(LuckyShotActiveSession session)
    {
        if (!session.IsValid())
            return;

        ConfigureLaunchSide(session.launchSide);

        bool shouldHaveBall = session.remainingShots > 0 && !shotResolutionPending && !retryRespawnScheduled;
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

        spawnInProgress = true;

        try
        {
            ClearLauncherBindingOnly();

            ballTurnSpawner.ClearAllBallsInScene();
            currentBall = null;
            shotLaunchObserved = false;
            shotResolutionPending = false;

            BallPhysics spawned = ballTurnSpawner.SpawnOfflineBallForOwner(localLuckyShotPlayer);
            if (spawned == null)
            {
                Debug.LogError("[LuckyShotGameplayController] SpawnOfflineBallForOwner returned null.", this);
                return;
            }

            await Task.Yield();

            currentBall = spawned;
            BindCurrentBall();

            if (shotResolver != null)
                shotResolver.PrepareForNewBall(spawned);

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotGameplayController] SpawnFreshBallAsync -> Spawned new Lucky Shot ball. " +
                    "Owner=" + localLuckyShotPlayer +
                    " | Ball=" + currentBall.name,
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
            return;

        if (shotResolutionPending)
            return;

        shotResolutionPending = true;

        if (verboseLogs)
            Debug.Log("[LuckyShotGameplayController] NotifyBallLost -> resolving miss.", this);

        if (sessionRuntime != null)
            await sessionRuntime.ResolveMissAsync();

        CleanupCurrentBallReference();
    }

    public void NotifyHitResolved(BallPhysics resolvedBall)
    {
        if (resolvedBall == null)
            return;

        if (currentBall == resolvedBall)
        {
            CleanupCurrentBallReference();

            if (verboseLogs)
                Debug.Log("[LuckyShotGameplayController] NotifyHitResolved(Ball) -> current ball cleared.", this);
        }
    }

    public void NotifyHitResolved(LuckyShotResolvedResult result)
    {
        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] NotifyHitResolved(Result) -> " +
                "Success=" + result.success +
                " | Win=" + result.isWin +
                " | CanRetry=" + result.canRetry +
                " | Final=" + result.isFinalResolution,
                this);
        }
    }

    private async Task MarkShotConsumedAsync()
    {
        if (sessionRuntime == null)
            return;

        await sessionRuntime.MarkShotConsumedAsync();
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

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        EnsurePlayableStateFromSession(session);
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (!preview.hasActiveSession)
            return;

        if (preview.remainingShots > 0 && currentBall == null && !shotResolutionPending && !retryRespawnScheduled)
        {
            _ = SpawnFreshBallAsync();
        }
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        if (result.canRetry)
        {
            CleanupCurrentBallReference();

            if (!retryRespawnScheduled)
            {
                retryRespawnScheduled = true;
                _ = SpawnNextBallDelayedAsync();
            }

            return;
        }

        CleanupCurrentBallReference();
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
        ballLauncher.SetStandaloneOfflineLaunchEnabled(true);

        if (verboseLogs)
            Debug.Log("[LuckyShotGameplayController] BindCurrentBall -> Ball=" + currentBall.name, this);
    }

    private void CleanupCurrentBallReference()
    {
        currentBall = null;
        shotLaunchObserved = false;
        shotResolutionPending = false;
        ClearLauncherBindingOnly();
    }

    private void ClearLauncherBindingOnly()
    {
        if (ballLauncher == null)
            return;

        ballLauncher.ball = null;
        ballLauncher.SetActivePlacementArea(null);
        ballLauncher.ResetLaunch();
        ballLauncher.SetStandaloneOfflineLaunchEnabled(true);
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
            ballTurnSpawner = FindFirstObjectByType<BallTurnSpawner>();

        if (ballLauncher == null)
            ballLauncher = FindFirstObjectByType<BallLauncher>();
#else
        if (sessionRuntime == null)
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();

        if (shotResolver == null)
            shotResolver = FindObjectOfType<LuckyShotShotResolver>();

        if (ballTurnSpawner == null)
            ballTurnSpawner = FindObjectOfType<BallTurnSpawner>();

        if (ballLauncher == null)
            ballLauncher = FindObjectOfType<BallLauncher>();
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
}