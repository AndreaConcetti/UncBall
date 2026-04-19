using System;
using System.Threading.Tasks;
using UnityEngine;

public sealed class LuckyShotSessionRuntime : MonoBehaviour
{
    public static LuckyShotSessionRuntime Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private LuckyShotSlotRegistry slotRegistry;
    [SerializeField] private Transform launchZoneLeft;
    [SerializeField] private Transform launchZoneRight;

    [Header("Options")]
    [SerializeField] private bool dontDestroyOnLoad = false;
    [SerializeField] private bool autoResolveServicesOnAwake = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private LuckyShotBackendService backendService;
    private LuckyShotEntryService entryService;

    private LuckyShotActiveSession currentSession;
    private bool hasActiveSession;
    private bool isLoadingSession;
    private bool resolveLocked;

    public event Action<LuckyShotActiveSession> SessionLoaded;
    public event Action<LuckyShotResolvedResult> SessionResolved;
    public event Action<LuckyShotSessionPreview> SessionPreviewChanged;
    public event Action<string> FeedbackRaised;

    public LuckyShotActiveSession CurrentSession => currentSession;
    public bool HasActiveSession => hasActiveSession;
    public LuckyShotLaunchSide CurrentLaunchSide => hasActiveSession ? currentSession.launchSide : LuckyShotLaunchSide.Left;

    public Transform CurrentLaunchTransform
    {
        get
        {
            if (CurrentLaunchSide == LuckyShotLaunchSide.Right)
                return launchZoneRight != null ? launchZoneRight : launchZoneLeft;

            return launchZoneLeft != null ? launchZoneLeft : launchZoneRight;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (autoResolveServicesOnAwake)
            ResolveServices();

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSessionRuntime] Awake -> " +
                "BackendService=" + (backendService != null) +
                " | EntryService=" + (entryService != null) +
                " | SlotRegistry=" + (slotRegistry != null) +
                " | LaunchLeft=" + SafeName(launchZoneLeft) +
                " | LaunchRight=" + SafeName(launchZoneRight),
                this);
        }
    }

    public async Task<LuckyShotActiveSession> EnsureSessionLoadedAsync()
    {
        if (hasActiveSession && currentSession.IsValid())
        {
            RaisePreviewChanged();
            return currentSession;
        }

        if (isLoadingSession)
        {
            while (isLoadingSession)
                await Task.Yield();

            return currentSession;
        }

        isLoadingSession = true;

        try
        {
            ResolveServices();

            if (slotRegistry == null)
            {
                RaiseFeedback("Lucky Shot slot registry missing.");
                return currentSession;
            }

            if (slotRegistry.GetBoardCount(1) == 0 &&
                slotRegistry.GetBoardCount(2) == 0 &&
                slotRegistry.GetBoardCount(3) == 0)
            {
                slotRegistry.ScanScene();
            }

            LuckyShotActiveSession loaded = default;

            if (backendService != null)
                loaded = await backendService.TryGetActiveSessionAsync();

            if (loaded.IsValid())
            {
                ApplySessionInternal(loaded);

                if (verboseLogs)
                    Debug.Log("[LuckyShotSessionRuntime] EnsureSessionLoadedAsync -> loaded persisted session " + loaded.sessionId, this);

                return currentSession;
            }

            LuckyShotDailyLayout layout = BuildDailyLayout();
            if (!layout.IsValid())
            {
                RaiseFeedback("Lucky Shot daily layout invalid.");
                return currentSession;
            }

            LuckyShotActiveSession created = CreateSession(layout);

            if (backendService != null)
                await backendService.SaveActiveSessionAsync(created);

            ApplySessionInternal(created);

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSessionRuntime] EnsureSessionLoadedAsync -> created new session " +
                    created.sessionId +
                    " | Side=" + created.launchSide +
                    " | B1=" + created.board1WinningSlotId +
                    " | B2=" + created.board2WinningSlotId +
                    " | B3=" + created.board3WinningSlotId,
                    this);
            }

            return currentSession;
        }
        finally
        {
            isLoadingSession = false;
        }
    }

    public async Task<bool> MarkShotConsumedAsync()
    {
        if (!hasActiveSession || !currentSession.IsValid())
            return false;

        if (currentSession.shotAlreadyTaken)
            return true;

        if (currentSession.remainingShots <= 0)
            return false;

        currentSession.remainingShots = Mathf.Max(0, currentSession.remainingShots - 1);
        currentSession.shotAlreadyTaken = true;

        if (backendService != null)
            await backendService.SaveActiveSessionAsync(currentSession);

        RaisePreviewChanged();

        if (verboseLogs)
            Debug.Log("[LuckyShotSessionRuntime] MarkShotConsumedAsync -> RemainingShots=" + currentSession.remainingShots, this);

        return true;
    }

    public async Task<LuckyShotResolvedResult> ResolveHitAsync(int boardNumber, string slotId)
    {
        if (resolveLocked)
            return CreateIgnoredResult("Resolve locked.");

        if (!hasActiveSession || !currentSession.IsValid())
            return CreateIgnoredResult("No active session.");

        resolveLocked = true;

        try
        {
            if (!currentSession.shotAlreadyTaken)
                await MarkShotConsumedAsync();

            bool isWin = IsWinningHit(boardNumber, slotId);
            int rewardWeight = isWin ? GetRewardWeight(boardNumber, slotId) : 0;

            currentSession.lastHitBoardNumber = boardNumber;
            currentSession.lastHitSlotId = slotId ?? string.Empty;
            currentSession.rewardGranted = isWin;
            currentSession.shotAlreadyTaken = true;

            if (backendService != null)
            {
                await backendService.SaveActiveSessionAsync(currentSession);
                await backendService.RegisterPlayResultAsync(isWin ? rewardWeight : 0);
            }

            LuckyShotResolvedResult result = new LuckyShotResolvedResult
            {
                success = true,
                isWin = isWin,
                rewardGranted = isWin,
                hitBoardNumber = boardNumber,
                hitSlotId = slotId ?? string.Empty,
                rewardWeight = rewardWeight,
                rewardLabel = BuildRewardLabel(isWin, boardNumber, rewardWeight),
                remainingShotsAfterResolve = currentSession.remainingShots,
                sessionAfterResolve = currentSession
            };

            RaisePreviewChanged();
            SessionResolved?.Invoke(result);

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSessionRuntime] ResolveHitAsync -> " +
                    "Board=" + boardNumber +
                    " | SlotId=" + slotId +
                    " | Win=" + isWin +
                    " | RewardWeight=" + rewardWeight +
                    " | Remaining=" + currentSession.remainingShots,
                    this);
            }

            return result;
        }
        finally
        {
            resolveLocked = false;
        }
    }

    public async Task<LuckyShotResolvedResult> ResolveMissAsync()
    {
        if (resolveLocked)
            return CreateIgnoredResult("Resolve locked.");

        if (!hasActiveSession || !currentSession.IsValid())
            return CreateIgnoredResult("No active session.");

        resolveLocked = true;

        try
        {
            if (!currentSession.shotAlreadyTaken)
                await MarkShotConsumedAsync();

            currentSession.lastHitBoardNumber = 0;
            currentSession.lastHitSlotId = string.Empty;
            currentSession.rewardGranted = false;

            bool canRetryWithAd = currentSession.remainingShots <= 0 && !currentSession.extraAdShotUsed;

            if (backendService != null)
            {
                await backendService.SaveActiveSessionAsync(currentSession);

                if (!canRetryWithAd)
                    await backendService.RegisterPlayResultAsync(0);
            }

            LuckyShotResolvedResult result = new LuckyShotResolvedResult
            {
                success = true,
                isWin = false,
                rewardGranted = false,
                hitBoardNumber = 0,
                hitSlotId = string.Empty,
                rewardWeight = 0,
                rewardLabel = canRetryWithAd ? "EXTRA SHOT AVAILABLE" : "MISS",
                remainingShotsAfterResolve = currentSession.remainingShots,
                sessionAfterResolve = currentSession
            };

            RaisePreviewChanged();
            SessionResolved?.Invoke(result);

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSessionRuntime] ResolveMissAsync -> " +
                    "RemainingShots=" + currentSession.remainingShots +
                    " | ExtraAdUsed=" + currentSession.extraAdShotUsed +
                    " | CanRetryWithAd=" + canRetryWithAd,
                    this);
            }

            return result;
        }
        finally
        {
            resolveLocked = false;
        }
    }

    public async Task<bool> GrantExtraAdShotAsync()
    {
        if (!hasActiveSession || !currentSession.IsValid())
            return false;

        if (currentSession.extraAdShotUsed)
            return false;

        currentSession.extraAdShotUsed = true;
        currentSession.remainingShots += 1;
        currentSession.shotAlreadyTaken = false;

        if (backendService != null)
            await backendService.SaveActiveSessionAsync(currentSession);

        RaisePreviewChanged();

        if (verboseLogs)
            Debug.Log("[LuckyShotSessionRuntime] GrantExtraAdShotAsync -> granted. RemainingShots=" + currentSession.remainingShots, this);

        return true;
    }

    private void ApplySessionInternal(LuckyShotActiveSession session)
    {
        currentSession = session;
        hasActiveSession = session.IsValid();

        if (hasActiveSession)
        {
            SessionLoaded?.Invoke(currentSession);
            RaisePreviewChanged();
        }
    }

    private LuckyShotDailyLayout BuildDailyLayout()
    {
        LuckyShotSlotRegistry.LuckyShotRegisteredSlot b1 = slotRegistry.GetRandomSlotForBoard(1);
        LuckyShotSlotRegistry.LuckyShotRegisteredSlot b2 = slotRegistry.GetRandomSlotForBoard(2);
        LuckyShotSlotRegistry.LuckyShotRegisteredSlot b3 = slotRegistry.GetRandomSlotForBoard(3);

        if (b1 == null || b2 == null || b3 == null)
        {
            RaiseFeedback("Lucky Shot slots missing on one or more boards.");
            return default;
        }

        LuckyShotDailyLayout layout = new LuckyShotDailyLayout
        {
            layoutDateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            launchSide = UnityEngine.Random.value < 0.5f ? LuckyShotLaunchSide.Left : LuckyShotLaunchSide.Right,
            board1WinningSlotId = b1.slotId,
            board2WinningSlotId = b2.slotId,
            board3WinningSlotId = b3.slotId
        };

        return layout;
    }

    private LuckyShotActiveSession CreateSession(LuckyShotDailyLayout layout)
    {
        LuckyShotActiveSession session = new LuckyShotActiveSession
        {
            hasActiveSession = true,
            sessionId = Guid.NewGuid().ToString("N"),
            sessionDateUtc = string.IsNullOrWhiteSpace(layout.layoutDateUtc) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : layout.layoutDateUtc,
            launchSide = layout.launchSide,
            board1WinningSlotId = layout.board1WinningSlotId ?? string.Empty,
            board2WinningSlotId = layout.board2WinningSlotId ?? string.Empty,
            board3WinningSlotId = layout.board3WinningSlotId ?? string.Empty,
            remainingShots = 1,
            extraAdShotUsed = false,
            shotAlreadyTaken = false,
            rewardGranted = false,
            lastHitBoardNumber = 0,
            lastHitSlotId = string.Empty
        };

        return session;
    }

    private bool IsWinningHit(int boardNumber, string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return false;

        switch (boardNumber)
        {
            case 1: return string.Equals(currentSession.board1WinningSlotId, slotId, StringComparison.OrdinalIgnoreCase);
            case 2: return string.Equals(currentSession.board2WinningSlotId, slotId, StringComparison.OrdinalIgnoreCase);
            case 3: return string.Equals(currentSession.board3WinningSlotId, slotId, StringComparison.OrdinalIgnoreCase);
            default: return false;
        }
    }

    private int GetRewardWeight(int boardNumber, string slotId)
    {
        if (slotRegistry == null)
            return Mathf.Max(1, boardNumber);

        LuckyShotSlotRegistry.LuckyShotRegisteredSlot slot = slotRegistry.GetSlot(boardNumber, slotId);
        if (slot == null || slot.slotTarget == null)
            return Mathf.Max(1, boardNumber);

        return Mathf.Max(1, slot.slotTarget.RewardWeight);
    }

    private LuckyShotResolvedResult CreateIgnoredResult(string reason)
    {
        if (verboseLogs)
            Debug.LogWarning("[LuckyShotSessionRuntime] " + reason, this);

        return new LuckyShotResolvedResult
        {
            success = false,
            isWin = false,
            rewardGranted = false,
            hitBoardNumber = 0,
            hitSlotId = string.Empty,
            rewardWeight = 0,
            rewardLabel = reason ?? string.Empty,
            remainingShotsAfterResolve = hasActiveSession ? currentSession.remainingShots : 0,
            sessionAfterResolve = currentSession
        };
    }

    private string BuildRewardLabel(bool isWin, int boardNumber, int rewardWeight)
    {
        if (!isWin)
            return "MISS";

        return "BOARD " + boardNumber + " WIN";
    }

    private void RaisePreviewChanged()
    {
        LuckyShotSessionPreview preview = new LuckyShotSessionPreview
        {
            hasActiveSession = hasActiveSession,
            canPlayNow = hasActiveSession && currentSession.remainingShots > 0,
            canWatchAdForExtraShot = hasActiveSession &&
                                     currentSession.remainingShots <= 0 &&
                                     !currentSession.extraAdShotUsed &&
                                     !currentSession.rewardGranted,
            remainingShots = hasActiveSession ? currentSession.remainingShots : 0,
            launchSide = hasActiveSession ? currentSession.launchSide : LuckyShotLaunchSide.Left,
            sessionDateUtc = hasActiveSession ? currentSession.sessionDateUtc : string.Empty,
            board1WinningSlotId = hasActiveSession ? currentSession.board1WinningSlotId : string.Empty,
            board2WinningSlotId = hasActiveSession ? currentSession.board2WinningSlotId : string.Empty,
            board3WinningSlotId = hasActiveSession ? currentSession.board3WinningSlotId : string.Empty
        };

        SessionPreviewChanged?.Invoke(preview);
    }

    private void RaiseFeedback(string message)
    {
        FeedbackRaised?.Invoke(message);

        if (verboseLogs && !string.IsNullOrWhiteSpace(message))
            Debug.LogWarning("[LuckyShotSessionRuntime] " + message, this);
    }

    private void ResolveServices()
    {
        if (backendService == null)
        {
#if UNITY_2023_1_OR_NEWER
            backendService = FindFirstObjectByType<LuckyShotBackendService>();
            if (backendService == null)
                backendService = FindFirstObjectByType<LuckyShotBackendService>(FindObjectsInactive.Include);
#else
            backendService = FindObjectOfType<LuckyShotBackendService>(true);
#endif
        }

        if (entryService == null)
        {
#if UNITY_2023_1_OR_NEWER
            entryService = FindFirstObjectByType<LuckyShotEntryService>();
            if (entryService == null)
                entryService = FindFirstObjectByType<LuckyShotEntryService>(FindObjectsInactive.Include);
#else
            entryService = FindObjectOfType<LuckyShotEntryService>(true);
#endif
        }

        if (slotRegistry == null)
        {
#if UNITY_2023_1_OR_NEWER
            slotRegistry = FindFirstObjectByType<LuckyShotSlotRegistry>();
            if (slotRegistry == null)
                slotRegistry = FindFirstObjectByType<LuckyShotSlotRegistry>(FindObjectsInactive.Include);
#else
            slotRegistry = FindObjectOfType<LuckyShotSlotRegistry>(true);
#endif
        }
    }

    private string SafeName(UnityEngine.Object obj)
    {
        return obj == null ? "<null>" : obj.name;
    }
}
