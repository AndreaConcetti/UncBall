using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine;

public sealed class LuckyShotSceneBootstrap : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private LuckyShotSessionRuntime sessionRuntime;
    [SerializeField] private LuckyShotGameplayController gameplayController;
    [SerializeField] private BallLauncher ballLauncher;
    [SerializeField] private PlayerSkinLoadout playerSkinLoadout;
    [SerializeField] private MonoBehaviour highlightController;

    [Header("Optional HUD")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private bool bootstrapped;

    private void Awake()
    {
        ResolveReferences();
        EnsureStandaloneOfflineLauncherMode();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureStandaloneOfflineLauncherMode();
        Subscribe();
        BootstrapNow();
    }

    private void OnDisable()
    {
        Unsubscribe();

        // FIX: resetta il flag in modo che se la scena viene ricaricata
        // (es. secondo ingresso dopo reward), il bootstrap parta di nuovo.
        bootstrapped = false;
    }

    private void OnDestroy()
    {
        bootstrapped = false;
    }

    public void BootstrapNow()
    {
        if (bootstrapped)
            return;

        _ = BootstrapAsync();
    }

    private async Task BootstrapAsync()
    {
        bootstrapped = true;

        EnsureStandaloneOfflineLauncherMode();
        SetFeedback("Loading Lucky Shot session...");

        if (sessionRuntime == null)
        {
            SetFeedback("Lucky Shot runtime missing.");
            return;
        }

        // FIX: notifica il runtime che siamo in una nuova scena,
        // così può accettare una nuova sessione anche se l'istanza
        // è sopravvissuta al cambio scena via DontDestroyOnLoad.
        sessionRuntime.NotifySceneReloaded();

        LuckyShotActiveSession session = await sessionRuntime.EnsureSessionLoadedAsync(CancellationToken.None);
        if (!session.IsValid())
        {
            SetFeedback("Failed to create Lucky Shot session.");
            return;
        }

        ApplySkinToCurrentLuckyShotBall();
        ApplyHighlightTargets(session);

        SetFeedback($"Shots: {session.remainingShots} | Side: {session.launchSide}");

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSceneBootstrap] BootstrapAsync -> SessionId=" + session.sessionId +
                " | Side=" + session.launchSide +
                " | RemainingShots=" + session.remainingShots,
                this);
        }
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        ApplySkinToCurrentLuckyShotBall();
        ApplyHighlightTargets(session);
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (!preview.hasActiveSession)
            return;

        SetFeedback($"Shots: {preview.remainingShots} | Side: {preview.launchSide}");
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        if (!result.success)
            return;

        if (result.isWin)
        {
            SetFeedback($"Target hit on Board {result.hitBoardNumber}. Reward weight: {result.rewardWeight}");
            TriggerHighlightHitEffect(result.hitBoardNumber, result.hitSlotId);
        }
        else if (result.canRetry)
        {
            SetFeedback($"Miss on Board {Mathf.Max(1, result.hitBoardNumber)}. Try again.");
        }
        else
        {
            SetFeedback("Lucky Shot finished.");
        }
    }

    private void HandleFeedbackRaised(string message)
    {
        SetFeedback(message);
    }

    private void EnsureStandaloneOfflineLauncherMode()
    {
        if (ballLauncher == null)
            return;

        ballLauncher.SetStandaloneOfflineLaunchEnabled(true);

        if (verboseLogs)
            Debug.Log("[LuckyShotSceneBootstrap] Standalone offline launch enabled for Lucky Shot.", this);
    }

    private void ApplyHighlightTargets(LuckyShotActiveSession session)
    {
        if (!session.IsValid() || highlightController == null)
            return;

        bool handled = false;
        handled = TryInvokeHighlightMethod("ApplySession", session) || handled;
        handled = TryInvokeHighlightMethod("ApplySelections", session) || handled;
        handled = TryInvokeHighlightMethod("RefreshFromSession", session) || handled;

        if (!handled && verboseLogs)
            Debug.Log("[LuckyShotSceneBootstrap] ApplyHighlightTargets -> no compatible method found.", this);
    }

    private void TriggerHighlightHitEffect(int boardNumber, string slotId)
    {
        if (highlightController == null)
            return;

        bool handled = false;
        handled = TryInvokeHighlightMethod("PlayHitEffect", boardNumber, slotId) || handled;
        handled = TryInvokeHighlightMethod("NotifyTargetHit", boardNumber, slotId) || handled;

        if (!handled && verboseLogs)
            Debug.Log("[LuckyShotSceneBootstrap] TriggerHighlightHitEffect -> no compatible method found.", this);
    }

    private bool TryInvokeHighlightMethod(string methodName, params object[] args)
    {
        if (highlightController == null)
            return false;

        MethodInfo method = highlightController.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (method == null)
            return false;

        try
        {
            method.Invoke(highlightController, args);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ApplySkinToCurrentLuckyShotBall()
    {
        BallPhysics targetBall = ResolveCurrentLuckyShotBall();
        if (targetBall == null || playerSkinLoadout == null)
            return;

        BallSkinData equippedSkin = playerSkinLoadout.GetEquippedSkinForPlayer1();
        if (equippedSkin == null)
        {
            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSceneBootstrap] ApplySkinToCurrentLuckyShotBall -> Ball=" + targetBall.name +
                    " | Applied=False | Skin=none",
                    this);
            }

            return;
        }

        BallSkinDatabase database = playerSkinLoadout.Database;
        if (database == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotSceneBootstrap] Skin database missing in PlayerSkinLoadout.", this);

            return;
        }

        BallSkinApplier applier = targetBall.GetComponent<BallSkinApplier>();
        if (applier == null)
            applier = targetBall.GetComponentInChildren<BallSkinApplier>(true);

        if (applier == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotSceneBootstrap] BallSkinApplier missing on Lucky Shot ball.", targetBall);

            return;
        }

        bool applied = applier.ApplySkinData(database, equippedSkin);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSceneBootstrap] ApplySkinToCurrentLuckyShotBall -> Ball=" + targetBall.name +
                " | Applied=" + applied +
                " | SkinId=" + equippedSkin.skinUniqueId,
                this);
        }
    }

    private BallPhysics ResolveCurrentLuckyShotBall()
    {
        if (ballLauncher != null && ballLauncher.ball != null)
            return ballLauncher.ball;

#if UNITY_2023_1_OR_NEWER
        BallPhysics[] allBalls = FindObjectsByType<BallPhysics>(FindObjectsSortMode.None);
#else
        BallPhysics[] allBalls = FindObjectsOfType<BallPhysics>();
#endif

        if (allBalls == null || allBalls.Length == 0)
            return null;

        for (int i = allBalls.Length - 1; i >= 0; i--)
        {
            if (allBalls[i] == null)
                continue;

            BallOwnership ownership = allBalls[i].GetComponent<BallOwnership>();
            if (ownership != null && ownership.Owner == PlayerID.Player1)
                return allBalls[i];
        }

        return allBalls[allBalls.Length - 1];
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message ?? string.Empty;

        if (verboseLogs && !string.IsNullOrWhiteSpace(message))
            Debug.Log("[LuckyShotSceneBootstrap] Feedback -> " + message, this);
    }

    private void ResolveReferences()
    {
        sessionRuntime = ResolveSceneComponent(sessionRuntime);
        gameplayController = ResolveSceneComponent(gameplayController);
        ballLauncher = ResolveSceneComponent(ballLauncher);
        playerSkinLoadout = ResolveSceneComponent(playerSkinLoadout);
        highlightController = ResolveSceneComponent(highlightController as LuckyShotHighlightController);

        if (sessionRuntime == null)
            sessionRuntime = LuckyShotSessionRuntime.Instance;

        if (gameplayController == null)
            gameplayController = LuckyShotGameplayController.Instance;
    }

    private T ResolveSceneComponent<T>(T current) where T : Component
    {
        if (current != null && current.gameObject.scene == gameObject.scene)
            return current;

#if UNITY_2023_1_OR_NEWER
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        T[] all = FindObjectsOfType<T>(true);
#endif

        if (all == null)
            return null;

        Scene myScene = gameObject.scene;
        for (int i = 0; i < all.Length; i++)
        {
            T candidate = all[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.scene == myScene)
                return candidate;
        }

        return current;
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

        sessionRuntime.FeedbackRaised -= HandleFeedbackRaised;
        sessionRuntime.FeedbackRaised += HandleFeedbackRaised;
    }

    private void Unsubscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionResolved -= HandleSessionResolved;
        sessionRuntime.FeedbackRaised -= HandleFeedbackRaised;
    }
}