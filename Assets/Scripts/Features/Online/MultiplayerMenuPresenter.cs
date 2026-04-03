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

    [Header("Optional Queue Timeout")]
    [SerializeField] private bool enableQueueTimeout = false;
    [SerializeField] private float queueTimeoutSeconds = 0f;
    [SerializeField] private GameObject queueTimeoutPanel;
    [SerializeField] private TMP_Text queueTimeoutMessageText;
    [SerializeField][TextArea] private string queueTimeoutMessage = "NO OPPONENT FOUND.";

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

    [Header("Countdown")]
    [SerializeField] private bool syncCountdownWithOnlineFlowDelay = true;
    [SerializeField] private float fallbackMatchFoundCountdownDuration = 2f;

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
                this
            );
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
                this
            );
        }
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        RefreshPlayerNameImmediate();
    }

    private void ForceRefreshAll()
    {
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
            return OnlineLocalPlayerContext.DisplayName.Trim().ToUpperInvariant();

        ResolveDependencies(false);

        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName))
            return profileManager.ActiveDisplayName.Trim().ToUpperInvariant();

        return "PLAYER";
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

        bool isIdle =
            state == OnlineFlowState.Idle ||
            state == OnlineFlowState.Offline ||
            state == OnlineFlowState.Error ||
            state == OnlineFlowState.EndingMatch;

        bool isSearching =
            state == OnlineFlowState.Queueing ||
            state == OnlineFlowState.JoiningSession ||
            state == OnlineFlowState.LoadingGameplay;

        bool isMatchFound =
            state == OnlineFlowState.MatchAssigned;

        if (homeVisualRoot != null)
            homeVisualRoot.SetActive(true);

        if (idlePanelRoot != null)
            idlePanelRoot.SetActive(isIdle);

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
            matchmakingLoadingPanel.SetActive(isSearching);

        if (matchFoundPanel != null)
            matchFoundPanel.SetActive(isMatchFound);

        if (isSearching)
            EnsureQueueTimerRunning();
        else
            StopQueueTimer();

        if (isMatchFound)
        {
            HideQueueTimeoutPanel();
            RefreshMatchFoundTexts();
            StartMatchFoundCountdownIfNeeded();
        }
        else
        {
            StopMatchFoundCountdown();
        }

        if (!isSearching && matchmakingProgressText != null)
            matchmakingProgressText.text = string.Empty;
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
                this
            );
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
            RefreshMatchFoundTexts();
            return;
        }

        if (Mathf.Abs(matchFoundCountdownRemaining - configuredDuration) > 0.01f)
        {
            matchFoundCountdownRemaining = configuredDuration;
            RefreshMatchFoundTexts();
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

        matchFoundCountdownRemaining -= Time.unscaledDeltaTime;
        if (matchFoundCountdownRemaining < 0f)
            matchFoundCountdownRemaining = 0f;

        RefreshMatchFoundTexts();

        if (matchFoundCountdownRemaining > 0f)
            return;

        if (hasTriggeredMatchFoundEnd)
            return;

        hasTriggeredMatchFoundEnd = true;
        matchFoundCountdownRunning = false;

        if (globalStatusText != null)
            globalStatusText.text = "MATCH STARTING...";
    }

    private void RefreshMatchFoundTexts()
    {
        ResolveDependencies(false);

        if (onlineFlowController == null || onlineFlowController.RuntimeContext == null)
            return;

        MatchSessionContext session = onlineFlowController.RuntimeContext.currentSession;
        MatchAssignment assignment = onlineFlowController.RuntimeContext.currentAssignment;

        string p1 = "PLAYER 1";
        string p2 = "PLAYER 2";

        if (session != null)
        {
            if (!string.IsNullOrWhiteSpace(session.player1DisplayName))
                p1 = session.player1DisplayName.Trim().ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(session.player2DisplayName))
                p2 = session.player2DisplayName.Trim().ToUpperInvariant();
        }
        else if (assignment != null)
        {
            if (assignment.localIsHost)
            {
                if (assignment.localPlayer != null && !string.IsNullOrWhiteSpace(assignment.localPlayer.displayName))
                    p1 = assignment.localPlayer.displayName.Trim().ToUpperInvariant();

                if (assignment.remotePlayer != null && !string.IsNullOrWhiteSpace(assignment.remotePlayer.displayName))
                    p2 = assignment.remotePlayer.displayName.Trim().ToUpperInvariant();
            }
            else
            {
                if (assignment.remotePlayer != null && !string.IsNullOrWhiteSpace(assignment.remotePlayer.displayName))
                    p1 = assignment.remotePlayer.displayName.Trim().ToUpperInvariant();

                if (assignment.localPlayer != null && !string.IsNullOrWhiteSpace(assignment.localPlayer.displayName))
                    p2 = assignment.localPlayer.displayName.Trim().ToUpperInvariant();
            }
        }

        if (matchFoundPlayerNamesText != null)
            matchFoundPlayerNamesText.text = p1 + "\nVS\n" + p2;

        if (matchFoundCountdownText != null)
            matchFoundCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, matchFoundCountdownRemaining)).ToString();
    }

    private void ShowQueueTimeoutPanel()
    {
        queueTimeoutPanelVisible = true;

        if (queueTimeoutPanel != null)
            queueTimeoutPanel.SetActive(true);

        if (queueTimeoutMessageText != null)
            queueTimeoutMessageText.text = string.IsNullOrWhiteSpace(queueTimeoutMessage)
                ? "NO OPPONENT FOUND."
                : queueTimeoutMessage;

        if (onlineButtonsRoot != null)
            onlineButtonsRoot.SetActive(false);

        if (matchmakingLoadingPanel != null)
            matchmakingLoadingPanel.SetActive(false);

        if (matchFoundPanel != null)
            matchFoundPanel.SetActive(false);
    }

    private void HideQueueTimeoutPanel()
    {
        queueTimeoutPanelVisible = false;

        if (queueTimeoutPanel != null)
            queueTimeoutPanel.SetActive(false);
    }
}