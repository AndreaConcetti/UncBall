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

    [Header("Runtime State")]
    [SerializeField] private BallPhysics currentBall;
    [SerializeField] private bool shotLaunchObserved;
    [SerializeField] private bool shotResolutionPending;
    [SerializeField] private bool spawnInProgress;

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
        if (!session.hasActiveSession)
            return;

        ConfigureLaunchSide(session.launchSide);

        bool shouldHaveBall = session.remainingShots > 0 && !shotResolutionPending;
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

            currentBall = spawned;
            BindCurrentBall();

            if (shotResolver != null)
                shotResolver.PrepareForNewBall(spawned);

            await Task.Yield();

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
            CleanupCurrentBallReference();
    }

    private async Task MarkShotConsumedAsync()
    {
        if (sessionRuntime == null)
            return;

        await sessionRuntime.MarkShotConsumedAsync();
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        EnsurePlayableStateFromSession(session);
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (!preview.hasActiveSession)
            return;

        if (preview.remainingShots > 0 && currentBall == null && !shotResolutionPending)
            _ = SpawnFreshBallAsync();
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        if (currentBall != null)
            Destroy(currentBall.gameObject);

        CleanupCurrentBallReference();

        if (respawnBallAfterAdExtraShot &&
            result.success &&
            !result.isWin &&
            result.sessionAfterResolve.hasActiveSession &&
            result.sessionAfterResolve.remainingShots > 0)
        {
            _ = SpawnFreshBallAsync();
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
    }

    private void CleanupCurrentBallReference()
    {
        currentBall = null;
        shotLaunchObserved = false;
        shotResolutionPending = false;

        if (ballLauncher != null)
        {
            ballLauncher.ball = null;
            ballLauncher.SetActivePlacementArea(null);
        }
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
}
