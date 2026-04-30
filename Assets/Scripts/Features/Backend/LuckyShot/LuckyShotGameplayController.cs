using System;
using System.Threading;
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
    [SerializeField] private LuckyShotHighlightController highlightController;

    [Header("Lucky Shot Highlight Roots")]
    [SerializeField] private Transform winningHighlightBoard1;
    [SerializeField] private Transform winningHighlightBoard2;
    [SerializeField] private Transform winningHighlightBoard3;

    [Header("Lucky Shot Rules")]
    [SerializeField] private PlayerID localLuckyShotPlayer = PlayerID.Player1;
    [SerializeField] private bool autoSpawnBallOnSessionLoaded = true;
    [SerializeField] private float retryRespawnDelaySeconds = 1.1f;

    [Header("Intro Reveal")]
    [SerializeField] private bool waitForIntroRevealBeforeFirstSpawn = true;
    [SerializeField] private bool playIntroRevealOnlyOncePerSession = true;
    [SerializeField] private float revealInitialDelaySeconds = 0.25f;
    [SerializeField] private float revealStepDelaySeconds = 0.55f;
    [SerializeField] private float revealAfterAllLightsDelaySeconds = 0.35f;
    [SerializeField] private bool keepHighlightsVisibleAfterIntro = true;

    [Header("Runtime State")]
    [SerializeField] private BallPhysics currentBall;
    [SerializeField] private bool shotLaunchObserved;
    [SerializeField] private bool shotResolutionPending;
    [SerializeField] private bool spawnInProgress;
    [SerializeField] private bool retryRespawnScheduled;
    [SerializeField] private bool introRevealInProgress;
    [SerializeField] private bool introRevealCompleted;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    public BallPhysics CurrentBall => currentBall;
    public bool HasActiveBall => currentBall != null;
    public bool ShotLaunchObserved => shotLaunchObserved;
    public bool ShotResolutionPending => shotResolutionPending;
    public bool IntroRevealInProgress => introRevealInProgress;
    public bool IntroRevealCompleted => introRevealCompleted;

    private string introSessionId;
    private CancellationTokenSource introRevealCancellation;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        ForceSetAllHighlightRootsVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();

        if (sessionRuntime != null && sessionRuntime.HasActiveSession && autoSpawnBallOnSessionLoaded)
            BeginIntroRevealThenEnsurePlayableState(sessionRuntime.CurrentSession);
    }

    private void OnDisable()
    {
        Unsubscribe();
        CancelIntroRevealFlow();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        CancelIntroRevealFlow();
    }

    private void Update()
    {
        MaintainHighlightVisibilityAfterIntro();

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
        if (sessionRuntime == null)
            return;

        if (!sessionRuntime.HasActiveSession)
            return;

        if (!session.IsValid())
            return;

        ConfigureLaunchSide(session.launchSide);

        bool shouldHaveBall = session.remainingShots > 0 &&
                              !shotResolutionPending &&
                              !retryRespawnScheduled &&
                              !introRevealInProgress;

        if (!shouldHaveBall)
            return;

        if (currentBall != null)
        {
            BindCurrentBall();
            return;
        }

        _ = SpawnFreshBallAsync();
    }

    public void BeginIntroRevealThenEnsurePlayableState(LuckyShotActiveSession session)
    {
        if (!autoSpawnBallOnSessionLoaded)
            return;

        if (!session.IsValid())
            return;

        ConfigureLaunchSide(session.launchSide);

        if (!waitForIntroRevealBeforeFirstSpawn)
        {
            introRevealCompleted = true;
            ForceSetAllHighlightRootsVisible(true);
            EnsurePlayableStateFromSession(session);
            return;
        }

        string currentSessionId = string.IsNullOrWhiteSpace(session.sessionId) ? "NO_SESSION_ID" : session.sessionId;

        if (playIntroRevealOnlyOncePerSession && introRevealCompleted && string.Equals(introSessionId, currentSessionId, StringComparison.Ordinal))
        {
            ForceSetAllHighlightRootsVisible(true);
            EnsurePlayableStateFromSession(session);
            return;
        }

        if (introRevealInProgress)
        {
            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotGameplayController] BeginIntroRevealThenEnsurePlayableState -> intro already running. " +
                    "SessionId=" + currentSessionId,
                    this);
            }

            return;
        }

        introSessionId = currentSessionId;
        _ = PlayIntroRevealThenSpawnAsync(session);
    }

    public async Task SpawnFreshBallAsync()
    {
        ResolveReferences();

        if (spawnInProgress)
            return;

        if (introRevealInProgress)
        {
            if (verboseLogs)
                Debug.Log("[LuckyShotGameplayController] SpawnFreshBallAsync -> blocked because intro reveal is in progress.", this);

            return;
        }

        if (ballTurnSpawner == null || ballLauncher == null)
        {
            Debug.LogError("[LuckyShotGameplayController] Missing BallTurnSpawner or BallLauncher.", this);
            return;
        }

        if (sessionRuntime != null && sessionRuntime.HasActiveSession)
        {
            LuckyShotActiveSession session = sessionRuntime.CurrentSession;
            if (session.IsValid())
                ConfigureLaunchSide(session.launchSide);
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
            shotLaunchObserved = false;
            shotResolutionPending = false;

            await Task.Yield();
            await Task.Yield();

            BindCurrentBall();

            if (shotResolver != null)
                shotResolver.PrepareForNewBall(spawned);

            ForceSetAllHighlightRootsVisible(true);

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

        CleanupCurrentBallReference();

        if (sessionRuntime != null)
            await sessionRuntime.ResolveMissAsync();

        TryScheduleRetryBallSpawn();
    }

    public void NotifyHitResolved(BallPhysics resolvedBall)
    {
        if (resolvedBall == null)
            return;

        if (currentBall == resolvedBall)
            CleanupCurrentBallReference();
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

        if (result.isFinalResolution || !result.canRetry)
        {
            CleanupCurrentBallReference();
            return;
        }

        if (result.success && result.canRetry && !result.isFinalResolution)
            TryScheduleRetryBallSpawn();
    }

    private async Task PlayIntroRevealThenSpawnAsync(LuckyShotActiveSession session)
    {
        CancelIntroRevealFlow();

        introRevealCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = introRevealCancellation.Token;

        introRevealInProgress = true;
        introRevealCompleted = false;

        try
        {
            ResolveReferences();
            ClearLauncherBindingOnly();
            TryApplySessionToHighlightController(session);

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotGameplayController] PlayIntroRevealThenSpawnAsync -> forced intro reveal started. " +
                    "SessionId=" + session.sessionId +
                    " | RemainingShots=" + session.remainingShots +
                    " | Side=" + session.launchSide +
                    " | HighlightController=" + (highlightController != null) +
                    " | B1=" + (winningHighlightBoard1 != null) +
                    " | B2=" + (winningHighlightBoard2 != null) +
                    " | B3=" + (winningHighlightBoard3 != null),
                    this);
            }

            await PlayHardIntroRevealSequenceAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            introRevealCompleted = true;
            introRevealInProgress = false;

            ForceSetAllHighlightRootsVisible(true);

            if (verboseLogs)
                Debug.Log("[LuckyShotGameplayController] PlayIntroRevealThenSpawnAsync -> forced intro reveal completed. Gameplay unlocked.", this);

            EnsurePlayableStateFromSession(session);
        }
        catch (OperationCanceledException)
        {
            introRevealInProgress = false;

            if (verboseLogs)
                Debug.Log("[LuckyShotGameplayController] PlayIntroRevealThenSpawnAsync -> cancelled.", this);
        }
        catch (Exception exception)
        {
            introRevealInProgress = false;
            introRevealCompleted = true;

            Debug.LogError("[LuckyShotGameplayController] PlayIntroRevealThenSpawnAsync -> failed. Spawning ball anyway.\n" + exception, this);
            ForceSetAllHighlightRootsVisible(true);
            EnsurePlayableStateFromSession(session);
        }
    }

    private async Task PlayHardIntroRevealSequenceAsync(CancellationToken cancellationToken)
    {
        ResolveHighlightRoots();
        ForceSetAllHighlightRootsVisible(false);

        if (revealInitialDelaySeconds > 0f)
            await DelaySecondsAsync(revealInitialDelaySeconds, cancellationToken);

        ForceSetBoardHighlightVisible(1, true);
        if (verboseLogs)
            Debug.Log("[LuckyShotGameplayController] HardIntroReveal -> Board 1 highlight enabled.", this);

        if (revealStepDelaySeconds > 0f)
            await DelaySecondsAsync(revealStepDelaySeconds, cancellationToken);

        ForceSetBoardHighlightVisible(2, true);
        if (verboseLogs)
            Debug.Log("[LuckyShotGameplayController] HardIntroReveal -> Board 2 highlight enabled.", this);

        if (revealStepDelaySeconds > 0f)
            await DelaySecondsAsync(revealStepDelaySeconds, cancellationToken);

        ForceSetBoardHighlightVisible(3, true);
        if (verboseLogs)
            Debug.Log("[LuckyShotGameplayController] HardIntroReveal -> Board 3 highlight enabled.", this);

        if (revealAfterAllLightsDelaySeconds > 0f)
            await DelaySecondsAsync(revealAfterAllLightsDelaySeconds, cancellationToken);
    }

    private async Task MarkShotConsumedAsync()
    {
        if (sessionRuntime == null)
            return;

        await sessionRuntime.MarkShotConsumedAsync();
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        BeginIntroRevealThenEnsurePlayableState(session);
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (!preview.hasActiveSession)
        {
            ForceSetAllHighlightRootsVisible(false);
            return;
        }

        ConfigureLaunchSide(preview.launchSide);

        if (introRevealCompleted && keepHighlightsVisibleAfterIntro)
            ForceSetAllHighlightRootsVisible(true);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] HandleSessionPreviewChanged -> " +
                "Remaining=" + preview.remainingShots +
                " | HasCurrentBall=" + (currentBall != null) +
                " | SpawnInProgress=" + spawnInProgress +
                " | RetryScheduled=" + retryRespawnScheduled +
                " | IntroCompleted=" + introRevealCompleted +
                " | IntroInProgress=" + introRevealInProgress,
                this);
        }

        if (preview.remainingShots > 0 &&
            currentBall == null &&
            !shotResolutionPending &&
            !spawnInProgress &&
            !retryRespawnScheduled &&
            !introRevealInProgress &&
            introRevealCompleted)
        {
            _ = SpawnFreshBallAsync();
        }
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        CleanupCurrentBallReference();

        if (result.isFinalResolution)
        {
            ForceSetAllHighlightRootsVisible(false);
            return;
        }

        if (result.success && result.canRetry && !result.isFinalResolution)
            TryScheduleRetryBallSpawn();
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

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotGameplayController] BindCurrentBall -> " +
                "Ball=" + currentBall.name +
                " | Owner=" + localLuckyShotPlayer,
                this);
        }
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
        ballLauncher.enabled = true;
        ballLauncher.SetStandaloneOfflineLaunchEnabled(true);
    }

    private void TryScheduleRetryBallSpawn()
    {
        if (retryRespawnScheduled)
            return;

        if (spawnInProgress)
            return;

        if (introRevealInProgress)
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
            await DelaySecondsAsync(retryRespawnDelaySeconds, CancellationToken.None);

        retryRespawnScheduled = false;

        if (currentBall != null)
            return;

        if (introRevealInProgress)
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

    private async Task DelaySecondsAsync(float seconds, CancellationToken cancellationToken)
    {
        if (seconds <= 0f)
            return;

        float elapsed = 0f;

        while (elapsed < seconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }
    }

    private void MaintainHighlightVisibilityAfterIntro()
    {
        if (!keepHighlightsVisibleAfterIntro)
            return;

        if (!introRevealCompleted || introRevealInProgress)
            return;

        if (sessionRuntime == null || !sessionRuntime.HasActiveSession)
            return;

        LuckyShotActiveSession session = sessionRuntime.CurrentSession;
        if (!session.IsValid() || session.remainingShots <= 0)
            return;

        ForceSetAllHighlightRootsVisible(true);
    }

    private void TryApplySessionToHighlightController(LuckyShotActiveSession session)
    {
        if (highlightController == null)
            return;

        try
        {
            highlightController.ApplySession(session);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[LuckyShotGameplayController] TryApplySessionToHighlightController -> failed. Direct light reveal will continue.\n" + exception, this);
        }
    }

    private void ForceSetAllHighlightRootsVisible(bool visible)
    {
        ResolveHighlightRoots();
        ForceSetBoardHighlightVisible(1, visible);
        ForceSetBoardHighlightVisible(2, visible);
        ForceSetBoardHighlightVisible(3, visible);
    }

    private void ForceSetBoardHighlightVisible(int board, bool visible)
    {
        Transform root = GetHighlightRoot(board);
        if (root == null)
        {
            if (visible && verboseLogs)
                Debug.LogWarning("[LuckyShotGameplayController] ForceSetBoardHighlightVisible -> Missing highlight root for Board " + board + ".", this);

            return;
        }

        SetTransformTreeVisible(root, visible);
    }

    private Transform GetHighlightRoot(int board)
    {
        ResolveHighlightRoots();

        switch (board)
        {
            case 1:
                return winningHighlightBoard1;
            case 2:
                return winningHighlightBoard2;
            case 3:
                return winningHighlightBoard3;
            default:
                return null;
        }
    }

    private void ResolveHighlightRoots()
    {
        if (winningHighlightBoard1 == null)
            winningHighlightBoard1 = FindSceneTransformByExactName("WinningHighlight_Board1");

        if (winningHighlightBoard2 == null)
            winningHighlightBoard2 = FindSceneTransformByExactName("WinningHighlight_Board2");

        if (winningHighlightBoard3 == null)
            winningHighlightBoard3 = FindSceneTransformByExactName("WinningHighlight_Board3");
    }

    private Transform FindSceneTransformByExactName(string exactName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null)
                continue;

            if (!candidate.gameObject.scene.IsValid())
                continue;

            if (!string.Equals(candidate.name, exactName, StringComparison.Ordinal))
                continue;

            return candidate;
        }

        return null;
    }

    private void SetTransformTreeVisible(Transform root, bool visible)
    {
        if (root == null)
            return;

        GameObject rootObject = root.gameObject;
        if (rootObject.activeSelf != visible)
            rootObject.SetActive(visible);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light lightComponent = lights[i];
            if (lightComponent == null)
                continue;

            lightComponent.enabled = visible;
            if (visible && lightComponent.intensity <= 0f)
                lightComponent.intensity = 1f;
        }

        ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = visible;

            if (visible && !particleSystem.isPlaying)
                particleSystem.Play(true);
            else if (!visible && particleSystem.isPlaying)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void CancelIntroRevealFlow()
    {
        if (introRevealCancellation != null)
        {
            introRevealCancellation.Cancel();
            introRevealCancellation.Dispose();
            introRevealCancellation = null;
        }

        introRevealInProgress = false;
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

        if (highlightController == null)
            highlightController = FindFirstObjectByType<LuckyShotHighlightController>(FindObjectsInactive.Include);
#else
        if (sessionRuntime == null)
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();

        if (shotResolver == null)
            shotResolver = FindObjectOfType<LuckyShotShotResolver>();

        if (ballTurnSpawner == null)
            ballTurnSpawner = FindObjectOfType<BallTurnSpawner>(true);

        if (ballLauncher == null)
            ballLauncher = FindObjectOfType<BallLauncher>(true);

        if (highlightController == null)
            highlightController = FindObjectOfType<LuckyShotHighlightController>(true);
#endif

        ResolveHighlightRoots();
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
