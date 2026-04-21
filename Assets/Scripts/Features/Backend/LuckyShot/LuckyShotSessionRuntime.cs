using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static LuckyShotSlotRegistry;

public sealed class LuckyShotSessionRuntime : MonoBehaviour
{
    public static LuckyShotSessionRuntime Instance { get; private set; }

    public event Action<LuckyShotSessionPreview> SessionPreviewChanged;
    public event Action<LuckyShotActiveSession> SessionLoaded;
    public event Action<LuckyShotResolvedResult> SessionResolved;
    public event Action<string> FeedbackRaised;

    [Header("Scene References")]
    [SerializeField] private LuckyShotSlotRegistry slotRegistry;
    [SerializeField] private Transform launchZoneLeft;
    [SerializeField] private Transform launchZoneRight;

    [Header("Options")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool autoResolveServicesOnAwake = true;
    [SerializeField] private int baseShotsPerSession = 3;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private LuckyShotBackendService backendService;
    private LuckyShotEntryService entryService;
    private LuckyShotActiveSession currentSession;
    private bool isBusy;
    private bool resolveLocked;

    public bool IsBusy => isBusy;
    public bool HasActiveSession => currentSession.hasActiveSession && currentSession.IsValid();
    public LuckyShotActiveSession CurrentSession => currentSession;
    public LuckyShotLaunchSide CurrentLaunchSide => HasActiveSession ? currentSession.launchSide : LuckyShotLaunchSide.Left;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            Transform root = transform.root;
            if (root != null)
                DontDestroyOnLoad(root.gameObject);
            else
                DontDestroyOnLoad(gameObject);
        }

        if (autoResolveServicesOnAwake)
            ResolveServices();

        if (slotRegistry == null)
        {
#if UNITY_2023_1_OR_NEWER
            slotRegistry = FindFirstObjectByType<LuckyShotSlotRegistry>();
#else
            slotRegistry = FindObjectOfType<LuckyShotSlotRegistry>();
#endif
        }

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSessionRuntime] Awake -> " +
                "BackendService=" + (backendService != null) +
                " | EntryService=" + (entryService != null) +
                " | SlotRegistry=" + (slotRegistry != null) +
                " | LaunchLeft=" + GetName(launchZoneLeft) +
                " | LaunchRight=" + GetName(launchZoneRight),
                this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public Transform GetLaunchTransformForCurrentSession()
    {
        return CurrentLaunchSide == LuckyShotLaunchSide.Right
            ? (launchZoneRight != null ? launchZoneRight : launchZoneLeft)
            : (launchZoneLeft != null ? launchZoneLeft : launchZoneRight);
    }

    public void RefreshPreview()
    {
        LuckyShotSessionPreview preview = BuildPreview(currentSession);
        SessionPreviewChanged?.Invoke(preview);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotSessionRuntime] RefreshPreview -> " +
                "HasActiveSession=" + preview.hasActiveSession +
                " | RemainingShots=" + preview.remainingShots +
                " | LaunchSide=" + preview.launchSide,
                this);
        }
    }

    public async Task<LuckyShotActiveSession> EnsureSessionLoadedAsync(CancellationToken cancellationToken = default)
    {
        ResolveServices();
        SetBusy(true);

        try
        {
            if (slotRegistry == null)
            {
                RaiseFeedback("Lucky Shot slot registry missing.");
                return default;
            }

            if (slotRegistry.GetBoardCount(1) == 0 &&
                slotRegistry.GetBoardCount(2) == 0 &&
                slotRegistry.GetBoardCount(3) == 0)
            {
                slotRegistry.ScanScene();
            }

            LuckyShotActiveSession loaded = await TryLoadActiveSessionFromBackendAsync(cancellationToken);

            if (loaded.IsValid())
            {
                bool discardLoaded = ShouldDiscardPersistedSession(loaded);

                if (!discardLoaded)
                {
                    currentSession = loaded;

                    if (verboseLogs)
                    {
                        Debug.Log(
                            "[LuckyShotSessionRuntime] EnsureSessionLoadedAsync -> Loaded existing session " +
                            currentSession.sessionId +
                            " | Side=" + currentSession.launchSide +
                            " | RemainingShots=" + currentSession.remainingShots,
                            this);
                    }

                    SessionLoaded?.Invoke(currentSession);
                    RefreshPreview();
                    return currentSession;
                }

                if (verboseLogs)
                {
                    Debug.Log(
                        "[LuckyShotSessionRuntime] EnsureSessionLoadedAsync -> Discarding persisted session " +
                        loaded.sessionId +
                        " | SessionDate=" + loaded.sessionDateUtc +
                        " | Today=" + DateTime.UtcNow.ToString("yyyy-MM-dd") +
                        " | AvailableTokensNow=" + GetAvailableTokensNow(),
                        this);
                }

                await ClearActiveSessionInBackendAsync(cancellationToken);
            }

            LuckyShotDailyLayout layout = BuildDailyLayout();
            currentSession = CreateSessionFromLayout(layout);

            bool saveOk = await SaveSessionToBackendAsync(currentSession, cancellationToken);
            if (!saveOk)
                RaiseFeedback("Unable to save Lucky Shot session.");

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSessionRuntime] EnsureSessionLoadedAsync -> Created new session " +
                    currentSession.sessionId +
                    " | Side=" + currentSession.launchSide +
                    " | B1=" + currentSession.board1WinningSlotId +
                    " | B2=" + currentSession.board2WinningSlotId +
                    " | B3=" + currentSession.board3WinningSlotId +
                    " | RemainingShots=" + currentSession.remainingShots,
                    this);
            }

            SessionLoaded?.Invoke(currentSession);
            RefreshPreview();
            return currentSession;
        }
        finally
        {
            SetBusy(false);
        }
    }

    public async Task<bool> MarkShotConsumedAsync(CancellationToken cancellationToken = default)
    {
        if (!HasActiveSession)
        {
            RaiseFeedback("No active Lucky Shot session.");
            return false;
        }

        if (currentSession.shotAlreadyTaken)
        {
            if (verboseLogs)
                Debug.Log("[LuckyShotSessionRuntime] MarkShotConsumedAsync -> already consumed for current ball.", this);

            return true;
        }

        if (currentSession.remainingShots <= 0)
        {
            RaiseFeedback("No Lucky Shot shots remaining.");
            return false;
        }

        currentSession.remainingShots = Mathf.Max(0, currentSession.remainingShots - 1);
        currentSession.shotAlreadyTaken = true;

        bool ok = await SaveSessionToBackendAsync(currentSession, cancellationToken);
        RefreshPreview();

        if (verboseLogs)
            Debug.Log("[LuckyShotSessionRuntime] MarkShotConsumedAsync -> RemainingShots=" + currentSession.remainingShots, this);

        return ok;
    }

    public async Task<bool> GrantAdExtraShotAsync(CancellationToken cancellationToken = default)
    {
        if (!HasActiveSession)
        {
            RaiseFeedback("No active Lucky Shot session.");
            return false;
        }

        if (currentSession.extraAdShotUsed)
        {
            RaiseFeedback("Extra Lucky Shot already used.");
            return false;
        }

        currentSession.extraAdShotUsed = true;
        currentSession.remainingShots += 1;
        currentSession.shotAlreadyTaken = false;

        bool ok = await SaveSessionToBackendAsync(currentSession, cancellationToken);
        RefreshPreview();

        if (verboseLogs)
            Debug.Log("[LuckyShotSessionRuntime] GrantAdExtraShotAsync -> RemainingShots=" + currentSession.remainingShots, this);

        return ok;
    }

    public async Task<LuckyShotResolvedResult> ResolveHitAsync(int hitBoardNumber, string hitSlotId, CancellationToken cancellationToken = default)
    {
        if (resolveLocked)
            return CreateIgnoredResult("Resolve locked.");

        if (!HasActiveSession)
        {
            RaiseFeedback("No active Lucky Shot session.");
            return CreateIgnoredResult("No active session.");
        }

        resolveLocked = true;

        try
        {
            bool isWinningHit = IsWinningHit(hitBoardNumber, hitSlotId);
            int rewardWeight = CalculateRewardWeight(hitBoardNumber, hitSlotId);

            bool hadRewardAlready = currentSession.rewardGranted;
            int previousWinningBoard = currentSession.lastHitBoardNumber;
            string previousWinningSlot = currentSession.lastHitSlotId ?? string.Empty;

            if (isWinningHit)
            {
                currentSession.rewardGranted = true;
                currentSession.lastHitBoardNumber = hitBoardNumber;
                currentSession.lastHitSlotId = hitSlotId ?? string.Empty;
            }
            else
            {
                currentSession.rewardGranted = hadRewardAlready;
                currentSession.lastHitBoardNumber = hadRewardAlready ? previousWinningBoard : 0;
                currentSession.lastHitSlotId = hadRewardAlready ? previousWinningSlot : string.Empty;
            }

            currentSession.shotAlreadyTaken = false;

            bool canRetry = currentSession.remainingShots > 0;
            currentSession.hasActiveSession = canRetry;

            bool saveOk = await SaveSessionToBackendAsync(currentSession, cancellationToken);
            if (!saveOk)
                RaiseFeedback("Unable to save Lucky Shot result.");

            if (!canRetry)
            {
                int finalScore = currentSession.rewardGranted ? GetFinalRewardWeightForSession() : 0;
                await RegisterPlayResultInternalAsync(finalScore, cancellationToken);
                await ClearActiveSessionInBackendAsync(cancellationToken);
            }

            LuckyShotResolvedResult result = new LuckyShotResolvedResult
            {
                success = saveOk,
                isWin = isWinningHit,
                rewardGranted = currentSession.rewardGranted,
                hitBoardNumber = isWinningHit ? hitBoardNumber : 0,
                hitSlotId = isWinningHit ? (hitSlotId ?? string.Empty) : string.Empty,
                rewardWeight = isWinningHit ? rewardWeight : 0,
                rewardLabel = BuildRewardLabel(isWinningHit, hitBoardNumber, rewardWeight, canRetry),
                remainingShotsAfterResolve = currentSession.remainingShots,
                canRetry = canRetry,
                isFinalResolution = !canRetry,
                sessionAfterResolve = currentSession
            };

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSessionRuntime] ResolveHitAsync -> " +
                    "Board=" + hitBoardNumber +
                    " | SlotId=" + hitSlotId +
                    " | IsWinningHit=" + isWinningHit +
                    " | RewardWeight=" + rewardWeight +
                    " | RemainingShots=" + currentSession.remainingShots +
                    " | RewardGrantedInSession=" + currentSession.rewardGranted +
                    " | HasActiveSession=" + currentSession.hasActiveSession,
                    this);
            }

            SessionResolved?.Invoke(result);
            RefreshPreview();
            return result;
        }
        finally
        {
            resolveLocked = false;
        }
    }

    public async Task<LuckyShotResolvedResult> ResolveMissAsync(CancellationToken cancellationToken = default)
    {
        if (resolveLocked)
            return CreateIgnoredResult("Resolve locked.");

        if (!HasActiveSession)
        {
            RaiseFeedback("No active Lucky Shot session.");
            return CreateIgnoredResult("No active session.");
        }

        resolveLocked = true;

        try
        {
            currentSession.shotAlreadyTaken = false;

            bool canRetry = currentSession.remainingShots > 0;
            currentSession.hasActiveSession = canRetry;

            bool saveOk = await SaveSessionToBackendAsync(currentSession, cancellationToken);
            if (!saveOk)
                RaiseFeedback("Unable to save Lucky Shot miss.");

            if (!canRetry)
            {
                int finalScore = currentSession.rewardGranted ? GetFinalRewardWeightForSession() : 0;
                await RegisterPlayResultInternalAsync(finalScore, cancellationToken);
                await ClearActiveSessionInBackendAsync(cancellationToken);
            }

            LuckyShotResolvedResult result = new LuckyShotResolvedResult
            {
                success = saveOk,
                isWin = false,
                rewardGranted = currentSession.rewardGranted,
                hitBoardNumber = 0,
                hitSlotId = string.Empty,
                rewardWeight = 0,
                rewardLabel = canRetry ? "Retry" : "Miss",
                remainingShotsAfterResolve = currentSession.remainingShots,
                canRetry = canRetry,
                isFinalResolution = !canRetry,
                sessionAfterResolve = currentSession
            };

            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotSessionRuntime] ResolveMissAsync -> " +
                    "RemainingShots=" + currentSession.remainingShots +
                    " | RewardGrantedInSession=" + currentSession.rewardGranted +
                    " | CanRetry=" + canRetry,
                    this);
            }

            SessionResolved?.Invoke(result);
            RefreshPreview();
            return result;
        }
        finally
        {
            resolveLocked = false;
        }
    }

    private LuckyShotResolvedResult CreateIgnoredResult(string message)
    {
        return new LuckyShotResolvedResult
        {
            success = false,
            isWin = false,
            rewardGranted = currentSession.rewardGranted,
            hitBoardNumber = 0,
            hitSlotId = string.Empty,
            rewardWeight = 0,
            rewardLabel = "Ignored",
            remainingShotsAfterResolve = currentSession.remainingShots,
            canRetry = false,
            isFinalResolution = false,
            sessionAfterResolve = currentSession
        };
    }

    private LuckyShotSessionPreview BuildPreview(LuckyShotActiveSession session)
    {
        return new LuckyShotSessionPreview
        {
            hasActiveSession = session.hasActiveSession,
            canPlayNow = session.hasActiveSession && session.remainingShots > 0 && !isBusy,
            canWatchAdForExtraShot = session.hasActiveSession && session.remainingShots <= 0 && !session.extraAdShotUsed && !isBusy,
            remainingShots = session.remainingShots,
            launchSide = session.launchSide,
            sessionDateUtc = session.sessionDateUtc ?? string.Empty,
            board1WinningSlotId = session.board1WinningSlotId ?? string.Empty,
            board2WinningSlotId = session.board2WinningSlotId ?? string.Empty,
            board3WinningSlotId = session.board3WinningSlotId ?? string.Empty
        };
    }

    private LuckyShotDailyLayout BuildDailyLayout()
    {
        if (slotRegistry == null)
            throw new InvalidOperationException("LuckyShotSlotRegistry missing.");

        LuckyShotRegisteredSlot b1 = slotRegistry.GetRandomSlotForBoard(1);
        LuckyShotRegisteredSlot b2 = slotRegistry.GetRandomSlotForBoard(2);
        LuckyShotRegisteredSlot b3 = slotRegistry.GetRandomSlotForBoard(3);

        if (b1 == null || b2 == null || b3 == null)
            throw new InvalidOperationException("Lucky Shot registry does not have all 3 boards populated.");

        LuckyShotLaunchSide side = UnityEngine.Random.Range(0, 2) == 0
            ? LuckyShotLaunchSide.Left
            : LuckyShotLaunchSide.Right;

        return new LuckyShotDailyLayout
        {
            layoutDateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            launchSide = side,
            board1WinningSlotId = b1.slotId,
            board2WinningSlotId = b2.slotId,
            board3WinningSlotId = b3.slotId
        };
    }

    private LuckyShotActiveSession CreateSessionFromLayout(LuckyShotDailyLayout layout)
    {
        return new LuckyShotActiveSession
        {
            hasActiveSession = true,
            sessionId = "lucky_" + Guid.NewGuid().ToString("N"),
            sessionDateUtc = layout.layoutDateUtc,
            launchSide = layout.launchSide,
            board1WinningSlotId = layout.board1WinningSlotId,
            board2WinningSlotId = layout.board2WinningSlotId,
            board3WinningSlotId = layout.board3WinningSlotId,
            remainingShots = Mathf.Max(1, baseShotsPerSession),
            extraAdShotUsed = false,
            shotAlreadyTaken = false,
            rewardGranted = false,
            lastHitBoardNumber = 0,
            lastHitSlotId = string.Empty
        };
    }

    private bool IsWinningHit(int boardNumber, string hitSlotId)
    {
        if (string.IsNullOrWhiteSpace(hitSlotId))
            return false;

        switch (boardNumber)
        {
            case 1: return string.Equals(currentSession.board1WinningSlotId, hitSlotId, StringComparison.Ordinal);
            case 2: return string.Equals(currentSession.board2WinningSlotId, hitSlotId, StringComparison.Ordinal);
            case 3: return string.Equals(currentSession.board3WinningSlotId, hitSlotId, StringComparison.Ordinal);
            default: return false;
        }
    }

    private int CalculateRewardWeight(int boardNumber, string hitSlotId)
    {
        if (!IsWinningHit(boardNumber, hitSlotId))
            return 0;

        if (slotRegistry == null)
            return boardNumber * 10;

        LuckyShotRegisteredSlot slot = slotRegistry.GetSlot(boardNumber, hitSlotId);
        if (slot == null)
            return boardNumber * 10;

        Transform launch = GetLaunchTransformForCurrentSession();
        if (launch == null || slot.slotTransform == null)
            return boardNumber * 10 + slot.slotIndex;

        float distance = Vector3.Distance(launch.position, slot.slotTransform.position);

        int baseWeight = boardNumber switch
        {
            1 => 100,
            2 => 200,
            3 => 300,
            _ => 0
        };

        return baseWeight + Mathf.RoundToInt(distance * 10f);
    }

    private int GetFinalRewardWeightForSession()
    {
        if (!currentSession.rewardGranted)
            return 0;

        return CalculateRewardWeight(currentSession.lastHitBoardNumber, currentSession.lastHitSlotId);
    }

    private string BuildRewardLabel(bool isWinningHit, int boardNumber, int rewardWeight, bool canRetry)
    {
        if (isWinningHit)
            return "Board " + boardNumber + " Reward " + rewardWeight;

        return canRetry ? "Retry" : "Miss";
    }

    private bool ShouldDiscardPersistedSession(LuckyShotActiveSession loaded)
    {
        string todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd");

        if (!string.Equals(loaded.sessionDateUtc, todayUtc, StringComparison.Ordinal))
            return true;

        int availableTokensNow = GetAvailableTokensNow();

        if (availableTokensNow > 0)
            return true;

        return false;
    }

    private int GetAvailableTokensNow()
    {
        ResolveServices();

        if (entryService != null)
        {
            entryService.RefreshTokens();
            return Mathf.Max(0, entryService.CachedTokens);
        }

        if (backendService != null)
            return Mathf.Max(0, backendService.GetAvailableTokens());

        return 0;
    }

    private async Task RegisterPlayResultInternalAsync(int score, CancellationToken cancellationToken)
    {
        ResolveServices();

        if (backendService == null)
            return;

        await backendService.RegisterPlayResultAsync(score, cancellationToken);
    }

    private async Task<LuckyShotActiveSession> TryLoadActiveSessionFromBackendAsync(CancellationToken cancellationToken)
    {
        ResolveServices();

        if (backendService == null)
            return default;

        return await backendService.TryGetActiveSessionAsync(cancellationToken);
    }

    private async Task<bool> SaveSessionToBackendAsync(LuckyShotActiveSession session, CancellationToken cancellationToken)
    {
        ResolveServices();

        if (backendService == null)
            return false;

        return await backendService.SaveActiveSessionAsync(session, cancellationToken);
    }

    private async Task<bool> ClearActiveSessionInBackendAsync(CancellationToken cancellationToken)
    {
        ResolveServices();

        if (backendService == null)
            return false;

        LuckyShotActiveSession cleared = default;
        return await backendService.SaveActiveSessionAsync(cleared, cancellationToken);
    }

    private void ResolveServices()
    {
        if (backendService == null)
        {
            backendService = LuckyShotBackendService.Instance;
#if UNITY_2023_1_OR_NEWER
            if (backendService == null)
                backendService = FindFirstObjectByType<LuckyShotBackendService>();
#else
            if (backendService == null)
                backendService = FindObjectOfType<LuckyShotBackendService>();
#endif
        }

        if (entryService == null)
        {
            entryService = LuckyShotEntryService.Instance;
#if UNITY_2023_1_OR_NEWER
            if (entryService == null)
                entryService = FindFirstObjectByType<LuckyShotEntryService>();
#else
            if (entryService == null)
                entryService = FindObjectOfType<LuckyShotEntryService>();
#endif
        }
    }

    private void SetBusy(bool value)
    {
        isBusy = value;
    }

    private void RaiseFeedback(string message)
    {
        FeedbackRaised?.Invoke(message);

        if (verboseLogs)
            Debug.Log("[LuckyShotSessionRuntime] Feedback -> " + message, this);
    }

    private string GetName(UnityEngine.Object target)
    {
        return target == null ? "<null>" : target.name;
    }
}