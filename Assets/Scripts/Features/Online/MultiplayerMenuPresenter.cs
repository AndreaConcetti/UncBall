using System;
using TMPro;
using UncballArena.Core.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerMenuPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlineFlowController onlineFlowController;
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private BotSessionRuntime botSessionRuntime;

    [Header("Main Roots")]
    [SerializeField] private GameObject onlinePanelRoot;
    [SerializeField] private GameObject idlePanelRoot;
    [SerializeField] private GameObject homeVisualRoot;

    [Header("Online State Panels")]
    [SerializeField] private GameObject onlineButtonsRoot;
    [SerializeField] private GameObject matchmakingLoadingPanel;
    [SerializeField] private GameObject matchFoundPanel;

    [Header("Optional Legacy Panels To Force Off")]
    [SerializeField] private GameObject privateLobbyPanel;
    [SerializeField] private GameObject insertOnlinePlayerNamePanel;

    [Header("Shared Cancel / Result Panel")]
    [SerializeField] private bool enableQueueTimeout = false;
    [SerializeField] private float queueTimeoutSeconds = 0f;
    [SerializeField] private GameObject queueTimeoutPanel;
    [SerializeField] private TMP_Text queueTimeoutTitleText;
    [SerializeField] private TMP_Text queueTimeoutMessageText;
    [SerializeField] private TMP_Text queueTimeoutDetailText;

    [Header("Queue Timeout Copy")]
    [SerializeField][TextArea] private string queueTimeoutTitle = "MATCHMAKING CANCELED";
    [SerializeField][TextArea] private string queueTimeoutMessage = "NO OPPONENT FOUND...";
    [SerializeField][TextArea] private string queueTimeoutDetail = "PLEASE TRY AGAIN LATER";

    [Header("Prematch Host Left Copy")]
    [SerializeField][TextArea] private string prematchHostLeftTitle = "MATCH CANCELED";
    [SerializeField][TextArea] private string prematchHostLeftMessage = "OPPONENT LEFT THE MATCH";
    [SerializeField][TextArea] private string prematchHostLeftDetail = "NO LP CHANGE YET";

    [Header("Texts - Status")]
    [SerializeField] private TMP_Text globalStatusText;

    [Header("Texts - Player Name")]
    [SerializeField] private TMP_Text primaryPlayerNameText;
    [SerializeField] private TMP_Text secondaryPlayerNameText;

    [Header("Texts - Matchmaking")]
    [SerializeField] private TMP_Text matchmakingTimerText;
    [SerializeField] private TMP_Text matchmakingProgressText;
    [SerializeField] private TMP_Text matchFoundPlayerNamesText;
    [SerializeField] private TMP_Text matchFoundCountdownText;

    [Header("Match Found Detailed Left Slot")]
    [SerializeField] private TMP_Text matchFoundLeftNameText;
    [SerializeField] private TMP_Text matchFoundLeftWinLoseText;
    [SerializeField] private TMP_Text matchFoundLeftWinRateText;

    [Header("Match Found Detailed Right Slot")]
    [SerializeField] private TMP_Text matchFoundRightNameText;
    [SerializeField] private TMP_Text matchFoundRightWinLoseText;
    [SerializeField] private TMP_Text matchFoundRightWinRateText;

    [Header("Countdown")]
    [SerializeField] private bool syncCountdownWithOnlineFlowDelay = true;
    [SerializeField] private float fallbackMatchFoundCountdownDuration = 2f;

    [Header("Formatting")]
    [SerializeField] private bool uppercaseNames = true;
    [SerializeField] private string fallbackLocalName = "PLAYER";
    [SerializeField] private string fallbackOpponentName = "OPPONENT";
    [SerializeField] private string fallbackPlayer1Name = "PLAYER 1";
    [SerializeField] private string fallbackPlayer2Name = "PLAYER 2";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private float queueElapsedTime;
    private bool queueTimerRunning;
    private bool queueTimeoutTriggered;
    private bool queueTimeoutPanelVisible;

    private bool matchFoundCountdownRunning;
    private float matchFoundCountdownRemaining;
    private bool hasTriggeredMatchFoundEnd;

    private string lastRenderedPlayerName = string.Empty;
    private OnlineFlowState lastRenderedState = OnlineFlowState.Offline;

    private bool subscribedToProfileEvents;
    private bool subscribedToFlowEvents;
    private bool subscribedToSceneEvents;

    private string lastMatchFoundSnapshotKey = string.Empty;

    private void Awake()
    {
        ResolveDependencies(true);
        SubscribeSceneEventsIfNeeded();
        ForceRefreshAll();
    }

    private void OnEnable()
    {
        ResolveDependencies(true);
        SubscribeIfNeeded();
        SubscribeSceneEventsIfNeeded();
        ForceRefreshAll();
    }

    private void Start()
    {
        ResolveDependencies(true);
        SubscribeIfNeeded();
        SubscribeSceneEventsIfNeeded();
        ForceRefreshAll();
    }

    private void OnDisable()
    {
        UnsubscribeIfNeeded();
        UnsubscribeSceneEventsIfNeeded();
    }

    private void OnDestroy()
    {
        UnsubscribeIfNeeded();
        UnsubscribeSceneEventsIfNeeded();
    }

    private void Update()
    {
        ResolveDependencies(false);
        SubscribeIfNeeded();

        RefreshPlayerNameIfNeeded();
        UpdateQueueTimer();
        UpdateMatchFoundCountdown();

        OnlineFlowState currentState = GetCurrentState();
        if (currentState != lastRenderedState)
            RefreshStateUi();
    }

    public void QueueNormal()
    {
        ResolveDependencies(true);

        if (onlineFlowController == null)
        {
            Debug.LogError("[MultiplayerMenuPresenter] OnlineFlowController missing.", this);
            return;
        }

        HideQueueTimeoutPanel();
        onlineFlowController.EnterQueue(QueueType.Normal);
    }

    public void QueueRanked()
    {
        ResolveDependencies(true);

        if (onlineFlowController == null)
        {
            Debug.LogError("[MultiplayerMenuPresenter] OnlineFlowController missing.", this);
            return;
        }

        HideQueueTimeoutPanel();
        onlineFlowController.EnterQueue(QueueType.Ranked);
    }

    public void CancelQueue()
    {
        ResolveDependencies(true);

        if (onlineFlowController == null)
        {
            Debug.LogError("[MultiplayerMenuPresenter] OnlineFlowController missing.", this);
            return;
        }

        onlineFlowController.CancelQueue();
    }

    public void DismissQueueTimeoutPanel()
    {
        HideQueueTimeoutPanel();
        RefreshStateUi();
    }

    public void OpenOnlinePanel()
    {
        if (onlinePanelRoot != null)
            onlinePanelRoot.SetActive(true);

        ResolveDependencies(true);
        ForceRefreshAll();
    }

    public void CloseOnlinePanel()
    {
        if (onlinePanelRoot != null)
            onlinePanelRoot.SetActive(false);

        StopQueueTimer();
        StopMatchFoundCountdown();
        HideQueueTimeoutPanel();
        ForceLegacyUnusedObjectsOff();
    }

    private void ResolveDependencies(bool force)
    {
        if (force || onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;

        if (force || profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (force || botSessionRuntime == null)
            botSessionRuntime = BotSessionRuntime.Instance;
    }

    private void SubscribeIfNeeded()
    {
        if (onlineFlowController != null && !subscribedToFlowEvents)
        {
            onlineFlowController.OnStateChanged += HandleStateChanged;
            subscribedToFlowEvents = true;
        }

        if (profileManager != null && !subscribedToProfileEvents)
        {
            profileManager.OnActiveProfileChanged += HandleProfileChanged;
            profileManager.OnActiveProfileDataChanged += HandleProfileChanged;
            subscribedToProfileEvents = true;
        }
    }

    private void UnsubscribeIfNeeded()
    {
        if (onlineFlowController != null && subscribedToFlowEvents)
        {
            onlineFlowController.OnStateChanged -= HandleStateChanged;
            subscribedToFlowEvents = false;
        }

        if (profileManager != null && subscribedToProfileEvents)
        {
            profileManager.OnActiveProfileChanged -= HandleProfileChanged;
            profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
            subscribedToProfileEvents = false;
        }
    }

    private void SubscribeSceneEventsIfNeeded()
    {
        if (subscribedToSceneEvents)
            return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        subscribedToSceneEvents = true;
    }

    private void UnsubscribeSceneEventsIfNeeded()
    {
        if (!subscribedToSceneEvents)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        subscribedToSceneEvents = false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveDependencies(true);
        SubscribeIfNeeded();

        if (logDebug)
        {
            Debug.Log(
                "[MultiplayerMenuPresenter] Scene loaded -> " + scene.name +
                " | Refreshing menu presenter bindings.",
                this);
        }

        ForceRefreshAll();
    }

    private void HandleStateChanged(OnlineRuntimeContext context)
    {
        RefreshStateUi();

        if (logDebug && context != null)
        {
            Debug.Log(
                "[MultiplayerMenuPresenter] StateChanged -> State=" + context.state +
                " | Status=" + context.statusMessage,
                this);
        }
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        RefreshPlayerNameImmediate();
        RefreshMatchFoundTexts(force: true);
    }

    private void ForceRefreshAll()
    {
        lastMatchFoundSnapshotKey = string.Empty;
        RefreshPlayerNameImmediate();
        RefreshStateUi();
    }

    private void RefreshPlayerNameIfNeeded()
    {
        string currentName = ResolveCurrentPlayerName();
        if (string.Equals(lastRenderedPlayerName, currentName, StringComparison.Ordinal))
            return;

        ApplyPlayerName(currentName);
    }

    private void RefreshPlayerNameImmediate()
    {
        ApplyPlayerName(ResolveCurrentPlayerName());
    }

    private string ResolveCurrentPlayerName()
    {
        if (OnlineLocalPlayerContext.IsAvailable && !string.IsNullOrWhiteSpace(OnlineLocalPlayerContext.DisplayName))
            return NormalizeName(OnlineLocalPlayerContext.DisplayName);

        ResolveDependencies(false);

        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName))
            return NormalizeName(profileManager.ActiveDisplayName);

        return NormalizeName(fallbackLocalName);
    }

    private void ApplyPlayerName(string value)
    {
        lastRenderedPlayerName = value;

        if (primaryPlayerNameText != null)
            primaryPlayerNameText.text = value;

        if (secondaryPlayerNameText != null)
            secondaryPlayerNameText.text = value;
    }

    private OnlineFlowState GetCurrentState()
    {
        if (onlineFlowController == null || onlineFlowController.RuntimeContext == null)
            return OnlineFlowState.Offline;

        return onlineFlowController.RuntimeContext.state;
    }

    private void RefreshStateUi()
    {
        ResolveDependencies(false);

        OnlineRuntimeContext context = onlineFlowController != null ? onlineFlowController.RuntimeContext : null;
        OnlineFlowState state = context != null ? context.state : OnlineFlowState.Offline;
        lastRenderedState = state;

        string status = context != null && !string.IsNullOrWhiteSpace(context.statusMessage)
            ? context.statusMessage
            : "Idle";

        if (globalStatusText != null)
            globalStatusText.text = status.ToUpperInvariant();

        RefreshPlayerNameImmediate();
        ForceLegacyUnusedObjectsOff();

        bool showPrematchHostLeftPanel = ShouldShowPrematchHostLeftPanel(context);

        bool isIdle =
            state == OnlineFlowState.Idle ||
            state == OnlineFlowState.Offline ||
            state == OnlineFlowState.Error ||
            state == OnlineFlowState.EndingMatch;

        bool isQueueSearching = state == OnlineFlowState.Queueing;

        bool shouldKeepMatchFoundVisible =
            state == OnlineFlowState.MatchAssigned ||
            state == OnlineFlowState.JoiningSession ||
            state == OnlineFlowState.LoadingGameplay;

        if (homeVisualRoot != null)
            homeVisualRoot.SetActive(true);

        if (idlePanelRoot != null)
            idlePanelRoot.SetActive(isIdle);

        if (showPrematchHostLeftPanel)
        {
            ShowSharedResultPanel(
                prematchHostLeftTitle,
                prematchHostLeftMessage,
                prematchHostLeftDetail);

            if (onlineButtonsRoot != null)
                onlineButtonsRoot.SetActive(false);

            if (matchmakingLoadingPanel != null)
                matchmakingLoadingPanel.SetActive(false);

            if (matchFoundPanel != null)
                matchFoundPanel.SetActive(false);

            StopQueueTimer();
            StopMatchFoundCountdown();
            return;
        }

        if (queueTimeoutPanelVisible)
        {
            if (onlineButtonsRoot != null)
                onlineButtonsRoot.SetActive(false);

            if (matchmakingLoadingPanel != null)
                matchmakingLoadingPanel.SetActive(false);

            if (matchFoundPanel != null)
                matchFoundPanel.SetActive(false);

            return;
        }

        if (onlineButtonsRoot != null)
            onlineButtonsRoot.SetActive(isIdle);

        if (matchmakingLoadingPanel != null)
            matchmakingLoadingPanel.SetActive(isQueueSearching);

        if (matchFoundPanel != null)
            matchFoundPanel.SetActive(shouldKeepMatchFoundVisible);

        if (isQueueSearching)
            EnsureQueueTimerRunning();
        else
            StopQueueTimer();

        if (shouldKeepMatchFoundVisible)
        {
            HideQueueTimeoutPanel();
            RefreshMatchFoundTexts(force: false);
            StartMatchFoundCountdownIfNeeded();
        }
        else
        {
            StopMatchFoundCountdown();
            lastMatchFoundSnapshotKey = string.Empty;
        }

        if (!isQueueSearching && matchmakingProgressText != null)
            matchmakingProgressText.text = string.Empty;
    }

    private bool ShouldShowPrematchHostLeftPanel(OnlineRuntimeContext context)
    {
        if (context == null)
            return false;

        if (context.state != OnlineFlowState.EndingMatch)
            return false;

        string status = context.statusMessage ?? string.Empty;
        return status.IndexOf("Host disconnected before gameplay start.", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ForceLegacyUnusedObjectsOff()
    {
        if (privateLobbyPanel != null)
            privateLobbyPanel.SetActive(false);

        if (insertOnlinePlayerNamePanel != null)
            insertOnlinePlayerNamePanel.SetActive(false);
    }

    private void EnsureQueueTimerRunning()
    {
        if (queueTimerRunning)
            return;

        queueElapsedTime = 0f;
        queueTimerRunning = true;
        queueTimeoutTriggered = false;
        RefreshQueueTimerText();
    }

    private void StopQueueTimer()
    {
        queueTimerRunning = false;
        queueElapsedTime = 0f;
        queueTimeoutTriggered = false;
        RefreshQueueTimerText();
    }

    private void UpdateQueueTimer()
    {
        if (!queueTimerRunning)
            return;

        queueElapsedTime += Time.unscaledDeltaTime;
        RefreshQueueTimerText();

        if (matchmakingProgressText != null)
            matchmakingProgressText.text = "SEARCHING FOR OPPONENT...";

        if (!enableQueueTimeout)
            return;

        if (queueTimeoutTriggered)
            return;

        if (queueTimeoutSeconds <= 0f)
            return;

        if (queueElapsedTime < queueTimeoutSeconds)
            return;

        queueTimeoutTriggered = true;

        if (logDebug)
        {
            Debug.Log(
                "[MultiplayerMenuPresenter] Queue timeout reached -> " +
                queueElapsedTime.ToString("F1") + "s",
                this);
        }

        if (onlineFlowController != null)
            onlineFlowController.CancelQueue();

        ShowQueueTimeoutPanel();
    }

    private void RefreshQueueTimerText()
    {
        if (matchmakingTimerText == null)
            return;

        int totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, queueElapsedTime));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        matchmakingTimerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    private void StartMatchFoundCountdownIfNeeded()
    {
        float configuredDuration = ResolveMatchFoundCountdownDuration();

        if (!matchFoundCountdownRunning)
        {
            matchFoundCountdownRunning = true;
            matchFoundCountdownRemaining = configuredDuration;
            hasTriggeredMatchFoundEnd = false;
            RefreshMatchFoundTexts(force: true);
            return;
        }

        if (Mathf.Abs(matchFoundCountdownRemaining - configuredDuration) > 0.01f &&
            !hasTriggeredMatchFoundEnd)
        {
            matchFoundCountdownRemaining = configuredDuration;
            RefreshMatchFoundTexts(force: true);
        }
    }

    private float ResolveMatchFoundCountdownDuration()
    {
        if (syncCountdownWithOnlineFlowDelay && onlineFlowController != null)
            return Mathf.Max(0f, onlineFlowController.MatchFoundUiDelaySeconds);

        return Mathf.Max(0f, fallbackMatchFoundCountdownDuration);
    }

    private void StopMatchFoundCountdown()
    {
        matchFoundCountdownRunning = false;
        matchFoundCountdownRemaining = 0f;
        hasTriggeredMatchFoundEnd = false;

        if (matchFoundCountdownText != null)
            matchFoundCountdownText.text = string.Empty;
    }

    private void UpdateMatchFoundCountdown()
    {
        if (!matchFoundCountdownRunning)
            return;

        if (!hasTriggeredMatchFoundEnd)
        {
            matchFoundCountdownRemaining -= Time.unscaledDeltaTime;
            if (matchFoundCountdownRemaining < 0f)
                matchFoundCountdownRemaining = 0f;

            RefreshMatchFoundTexts(force: false);

            if (matchFoundCountdownRemaining <= 0f)
            {
                hasTriggeredMatchFoundEnd = true;

                if (globalStatusText != null)
                    globalStatusText.text = "MATCH STARTING...";
            }
        }
        else
        {
            if (matchFoundCountdownText != null)
                matchFoundCountdownText.text = "0";
        }
    }

    private void RefreshMatchFoundTexts(bool force)
    {
        ResolveDependencies(false);

        if (onlineFlowController == null || onlineFlowController.RuntimeContext == null)
            return;

        MatchSessionContext session = onlineFlowController.RuntimeContext.currentSession;
        MatchAssignment assignment = onlineFlowController.RuntimeContext.currentAssignment;

        string leftName = NormalizeName(fallbackPlayer1Name);
        string rightName = NormalizeName(fallbackPlayer2Name);

        string leftWL = string.Empty;
        string rightWL = string.Empty;
        string leftWR = string.Empty;
        string rightWR = string.Empty;

        if (session != null)
        {
            string sessionP1 = NormalizeName(string.IsNullOrWhiteSpace(session.player1DisplayName) ? fallbackPlayer1Name : session.player1DisplayName);
            string sessionP2 = NormalizeName(string.IsNullOrWhiteSpace(session.player2DisplayName) ? fallbackPlayer2Name : session.player2DisplayName);

            leftName = session.player1StartsOnLeft ? sessionP1 : sessionP2;
            rightName = session.player1StartsOnLeft ? sessionP2 : sessionP1;
        }
        else if (assignment != null)
        {
            string localName =
                assignment.localPlayer != null && !string.IsNullOrWhiteSpace(assignment.localPlayer.displayName)
                    ? NormalizeName(assignment.localPlayer.displayName)
                    : NormalizeName(fallbackLocalName);

            string remoteName =
                assignment.remotePlayer != null && !string.IsNullOrWhiteSpace(assignment.remotePlayer.displayName)
                    ? NormalizeName(assignment.remotePlayer.displayName)
                    : NormalizeName(fallbackOpponentName);

            int localWins = 0;
            int localLosses = 0;
            int localWinRate = 0;

            if (assignment.localPlayerStats != null)
            {
                localWins = Mathf.Clamp(assignment.localPlayerStats.totalWins, 0, assignment.localPlayerStats.totalMatches);
                localLosses = Mathf.Max(0, assignment.localPlayerStats.totalMatches - localWins);
                localWinRate = Mathf.Clamp(assignment.localPlayerStats.winRatePercent, 0, 100);
            }

            int remoteWins = 0;
            int remoteLosses = 0;
            int remoteWinRate = 0;

            bool hasBotPresentation =
                botSessionRuntime != null &&
                botSessionRuntime.CurrentOpponentPresentation != null &&
                (assignment.runtimeType == MatchRuntimeType.RankedMaskedBot ||
                 assignment.runtimeType == MatchRuntimeType.NormalMaskedBot);

            if (hasBotPresentation)
            {
                OpponentPresentationProfile bot = botSessionRuntime.CurrentOpponentPresentation;
                remoteWins = Mathf.Clamp(bot.TotalWins, 0, bot.TotalMatches);
                remoteLosses = Mathf.Max(0, bot.TotalMatches - remoteWins);
                remoteWinRate = Mathf.Clamp(bot.WinRatePercent, 0, 100);

                if (!string.IsNullOrWhiteSpace(bot.DisplayName))
                    remoteName = NormalizeName(bot.DisplayName);
            }
            else if (assignment.remotePlayerStats != null)
            {
                remoteWins = Mathf.Clamp(assignment.remotePlayerStats.totalWins, 0, assignment.remotePlayerStats.totalMatches);
                remoteLosses = Mathf.Max(0, assignment.remotePlayerStats.totalMatches - remoteWins);
                remoteWinRate = Mathf.Clamp(assignment.remotePlayerStats.winRatePercent, 0, 100);
            }

            string p1Name = assignment.localPlayerIsPlayer1 ? localName : remoteName;
            string p2Name = assignment.localPlayerIsPlayer1 ? remoteName : localName;

            string p1WL = assignment.localPlayerIsPlayer1
                ? FormatWinLose(localWins, localLosses)
                : FormatWinLose(remoteWins, remoteLosses);

            string p2WL = assignment.localPlayerIsPlayer1
                ? FormatWinLose(remoteWins, remoteLosses)
                : FormatWinLose(localWins, localLosses);

            string p1WR = assignment.localPlayerIsPlayer1
                ? FormatWinRate(localWinRate)
                : FormatWinRate(remoteWinRate);

            string p2WR = assignment.localPlayerIsPlayer1
                ? FormatWinRate(remoteWinRate)
                : FormatWinRate(localWinRate);

            if (assignment.player1StartsOnLeft)
            {
                leftName = p1Name;
                rightName = p2Name;
                leftWL = p1WL;
                rightWL = p2WL;
                leftWR = p1WR;
                rightWR = p2WR;
            }
            else
            {
                leftName = p2Name;
                rightName = p1Name;
                leftWL = p2WL;
                rightWL = p1WL;
                leftWR = p2WR;
                rightWR = p1WR;
            }
        }

        string snapshotKey =
            leftName + "|" +
            rightName + "|" +
            leftWL + "|" +
            rightWL + "|" +
            leftWR + "|" +
            rightWR + "|" +
            Mathf.CeilToInt(Mathf.Max(0f, matchFoundCountdownRemaining));

        if (!force && snapshotKey == lastMatchFoundSnapshotKey)
            return;

        lastMatchFoundSnapshotKey = snapshotKey;

        if (matchFoundPlayerNamesText != null)
            matchFoundPlayerNamesText.text = leftName + "\nVS\n" + rightName;

        SetText(matchFoundLeftNameText, leftName);
        SetText(matchFoundLeftWinLoseText, leftWL);
        SetText(matchFoundLeftWinRateText, leftWR);

        SetText(matchFoundRightNameText, rightName);
        SetText(matchFoundRightWinLoseText, rightWL);
        SetText(matchFoundRightWinRateText, rightWR);

        if (matchFoundCountdownText != null)
        {
            if (hasTriggeredMatchFoundEnd)
                matchFoundCountdownText.text = "0";
            else
                matchFoundCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, matchFoundCountdownRemaining)).ToString();
        }

        if (logDebug && assignment != null)
        {
            Debug.Log(
                "[MultiplayerMenuPresenter] RefreshMatchFoundTexts -> " +
                "RuntimeType=" + assignment.runtimeType +
                " | LocalIsP1=" + assignment.localPlayerIsPlayer1 +
                " | P1StartsOnLeft=" + assignment.player1StartsOnLeft +
                " | Left=" + leftName +
                " | Right=" + rightName,
                this);
        }
    }

    private void ShowQueueTimeoutPanel()
    {
        queueTimeoutPanelVisible = true;
        ShowSharedResultPanel(queueTimeoutTitle, queueTimeoutMessage, queueTimeoutDetail);

        if (onlineButtonsRoot != null)
            onlineButtonsRoot.SetActive(false);

        if (matchmakingLoadingPanel != null)
            matchmakingLoadingPanel.SetActive(false);

        if (matchFoundPanel != null)
            matchFoundPanel.SetActive(false);
    }

    private void ShowSharedResultPanel(string title, string message, string detail)
    {
        if (queueTimeoutPanel != null)
            queueTimeoutPanel.SetActive(true);

        if (queueTimeoutTitleText != null)
            queueTimeoutTitleText.text = SafeUpper(title, "MATCHMAKING CANCELED");

        if (queueTimeoutMessageText != null)
            queueTimeoutMessageText.text = SafeUpper(message, "NO OPPONENT FOUND...");

        if (queueTimeoutDetailText != null)
            queueTimeoutDetailText.text = SafeUpper(detail, "PLEASE TRY AGAIN LATER");
    }

    private void HideQueueTimeoutPanel()
    {
        queueTimeoutPanelVisible = false;

        if (queueTimeoutPanel != null)
            queueTimeoutPanel.SetActive(false);
    }

    private string NormalizeName(string value)
    {
        string resolved = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (uppercaseNames && !string.IsNullOrWhiteSpace(resolved))
            resolved = resolved.ToUpperInvariant();

        return resolved;
    }

    private string FormatWinLose(int wins, int losses)
    {
        return wins + "W - " + losses + "L";
    }

    private string FormatWinRate(int winRatePercent)
    {
        return Mathf.Clamp(winRatePercent, 0, 100) + "%";
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target == null)
            return;

        target.text = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }

    private string SafeUpper(string value, string fallback)
    {
        string resolved = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return resolved.ToUpperInvariant();
    }
}