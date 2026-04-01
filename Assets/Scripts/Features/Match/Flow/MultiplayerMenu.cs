using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class MatchmakingQueuePreset
{
    public string queueId = "normal";
    public MatchMode matchMode = MatchMode.ScoreTarget;
    public int pointsToWin = 16;
    public float matchDuration = 180f;
    public bool isRanked = false;
    public bool allowChestRewards = false;
    public bool allowXpRewards = false;
    public bool allowStatsProgression = false;
}

public class MultiplayerMenu : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlineMatchSession onlineMatchSession;
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private PhotonFusionSessionController fusionSessionController;
    [SerializeField] private PhotonFusionRunnerManager fusionRunnerManager;
    [SerializeField] private FusionLobbyService fusionLobbyService;

    [Header("Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Inputs")]
    [SerializeField] private TMP_InputField privateHostPlayerNameInputField;
    [SerializeField] private TMP_InputField privateJoinPlayerNameInputField;
    [SerializeField] private TMP_InputField matchmakingPlayerNameInputField;
    [SerializeField] private TMP_InputField hostPointsToWinInputField;
    [SerializeField] private TMP_InputField hostMatchDurationInputField;
    [SerializeField] private TMP_InputField joinRoomCodeInputField;

    [Header("Host Match Mode UI")]
    [SerializeField] private TMP_Text hostMatchModeText;
    [SerializeField] private GameObject hostTimeModeSelectedObject;
    [SerializeField] private GameObject hostScoreModeSelectedObject;

    [Header("Main Generic Status")]
    [SerializeField] private TMP_Text statusText;

    [Header("Matchmaking Loading UI")]
    [SerializeField] private GameObject matchmakingLoadingPanel;
    [SerializeField] private TMP_Text matchmakingLoadingMessageText;
    [SerializeField] private TMP_Text matchmakingElapsedTimerText;

    [Header("Match Found UI")]
    [SerializeField] private GameObject matchFoundPanel;
    [SerializeField] private TMP_Text matchFoundTitleText;
    [SerializeField] private TMP_Text matchFoundPlayer1Text;
    [SerializeField] private TMP_Text matchFoundPlayer2Text;
    [SerializeField] private TMP_Text matchFoundCountdownText;
    [SerializeField] private float matchFoundCountdownDuration = 5f;

    [Header("Private Host Lobby UI")]
    [SerializeField] private GameObject waitingOpponentPanelHost;
    [SerializeField] private TMP_Text generatedRoomCodeText;
    [SerializeField] private TMP_Text joinedPlayerText;
    [SerializeField] private GameObject joinedPlayerTextRoot;
    [SerializeField] private TMP_Text lobbyStatusText;
    [SerializeField] private Button hostStartMatchButton;

    [Header("Private Join Lobby UI")]
    [SerializeField] private GameObject waitingOpponentPanelJoin;
    [SerializeField] private TMP_Text joinLobbyMessageText;
    [SerializeField] private TMP_Text joinLobbyStatusText;

    [Header("Optional Generic Private Loading")]
    [SerializeField] private GameObject privateLobbyLoadingPanel;
    [SerializeField] private TMP_Text privateLobbyLoadingText;

    [Header("Defaults")]
    [SerializeField] private int defaultPointsToWin = 16;
    [SerializeField] private float defaultMatchDuration = 180f;
    [SerializeField] private bool useLiveMultiplayerServices = true;

    [Header("Host Mode Runtime")]
    [SerializeField] private MatchMode privateHostMatchMode = MatchMode.ScoreTarget;
    [SerializeField] private MatchMode privateJoinMatchMode = MatchMode.ScoreTarget;

    [Header("Presets")]
    [SerializeField]
    private MatchmakingQueuePreset normalMatchmakingPreset = new MatchmakingQueuePreset
    {
        queueId = "normal",
        matchMode = MatchMode.ScoreTarget,
        pointsToWin = 16,
        matchDuration = 180f,
        isRanked = false,
        allowChestRewards = false,
        allowXpRewards = false,
        allowStatsProgression = false
    };

    [SerializeField]
    private MatchmakingQueuePreset rankedMatchmakingPreset = new MatchmakingQueuePreset
    {
        queueId = "ranked",
        matchMode = MatchMode.TimeLimit,
        pointsToWin = 16,
        matchDuration = 180f,
        isRanked = true,
        allowChestRewards = false,
        allowXpRewards = false,
        allowStatsProgression = false
    };

    [Header("Display Strings")]
    [SerializeField] private string defaultSearchingText = "SEARCHING FOR OPPONENT...";
    [SerializeField] private string defaultWaitingHostText = "WAITING FOR OPPONENT";
    [SerializeField] private string defaultJoinWaitingText = "WAITING FOR HOST";
    [SerializeField] private string defaultLobbyReadyText = "LOBBY READY";
    [SerializeField] private string defaultStartingText = "STARTING MATCH...";
    [SerializeField] private string defaultMatchFoundTitle = "MATCH FOUND!";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool matchmakingTimerRunning;
    private float matchmakingElapsedSeconds;

    private bool matchFoundCountdownRunning;
    private float matchFoundCountdownRemaining;
    private bool autoStartMatchmakingRequested;

    private void OnEnable()
    {
        ResolveDependencies();

        if (onlineMatchSession != null)
            onlineMatchSession.OnSessionUpdated += HandleSessionUpdated;

        if (fusionSessionController != null)
            fusionSessionController.OnOperationCompleted += HandleFusionOperationCompleted;
    }

    private void OnDisable()
    {
        if (onlineMatchSession != null)
            onlineMatchSession.OnSessionUpdated -= HandleSessionUpdated;

        if (fusionSessionController != null)
            fusionSessionController.OnOperationCompleted -= HandleFusionOperationCompleted;
    }

    private void Start()
    {
        ResolveDependencies();
        SanitizePersistentRuntime();

        HideAllPanels();
        StopAndResetMatchmakingTimer();
        StopMatchFoundCountdown();
        RefreshHostMatchModeUI();
        RefreshAllUI();
        SetGlobalStatus("READY");
    }

    private void Update()
    {
        UpdateMatchmakingElapsedTimer();
        UpdateMatchFoundCountdown();
    }

    public void ToggleHostMatchMode()
    {
        privateHostMatchMode = privateHostMatchMode == MatchMode.TimeLimit
            ? MatchMode.ScoreTarget
            : MatchMode.TimeLimit;

        RefreshHostMatchModeUI();
    }

    public void SetHostMatchModeTime()
    {
        privateHostMatchMode = MatchMode.TimeLimit;
        RefreshHostMatchModeUI();
    }

    public void SetHostMatchModePoints()
    {
        privateHostMatchMode = MatchMode.ScoreTarget;
        RefreshHostMatchModeUI();
    }

    public void CreateHostLobby()
    {
        ResolveDependencies();
        SanitizePersistentRuntime();

        onlineMatchSession.PreparePrivateHostSession(
            GetResolvedLocalProfileId(),
            ReadInput(privateHostPlayerNameInputField, GetResolvedDefaultLocalDisplayName()),
            "Player 2",
            privateHostMatchMode,
            ReadHostPointsToWin(),
            ReadHostMatchDuration(),
            false,
            useLiveMultiplayerServices,
            false,
            false,
            false
        );

        ShowHostWaiting();
        StopAndResetMatchmakingTimer();
        StopMatchFoundCountdown();
        RefreshAllUI();

        if (logDebug)
            Debug.Log("[MultiplayerMenu] CreateHostLobby", this);
    }

    public void JoinPrivateLobby()
    {
        ResolveDependencies();
        SanitizePersistentRuntime();

        onlineMatchSession.PreparePrivateJoinSession(
            ReadJoinRoomCode(),
            GetResolvedLocalProfileId(),
            ReadInput(privateJoinPlayerNameInputField, GetResolvedDefaultLocalDisplayName()),
            "HostPlayer",
            privateJoinMatchMode,
            defaultPointsToWin,
            defaultMatchDuration,
            false,
            useLiveMultiplayerServices,
            false,
            false,
            false
        );

        ShowJoinWaiting();
        StopAndResetMatchmakingTimer();
        StopMatchFoundCountdown();
        RefreshAllUI();

        if (logDebug)
            Debug.Log("[MultiplayerMenu] JoinPrivateLobby", this);
    }

    public void QueueNormalMatchmaking()
    {
        StartMatchmaking(normalMatchmakingPreset);
    }

    public void QueueRankedMatchmaking()
    {
        StartMatchmaking(rankedMatchmakingPreset);
    }

    public void CancelMatchmakingSearch()
    {
        ResolveDependencies();

        if (onlineMatchSession != null)
            onlineMatchSession.CancelMatchmakingSearch();

        StopAndResetMatchmakingTimer();
        StopMatchFoundCountdown();
        HideAllPanels();
        RefreshAllUI();
        SetGlobalStatus("MATCHMAKING CANCELLED");

        if (logDebug)
            Debug.Log("[MultiplayerMenu] CancelMatchmakingSearch", this);
    }

    public void LeaveCurrentLobby()
    {
        ResolveDependencies();

        if (onlineMatchSession != null)
            onlineMatchSession.LeaveCurrentLobbyOrMatchmaking();

        StopAndResetMatchmakingTimer();
        StopMatchFoundCountdown();
        HideAllPanels();
        RefreshAllUI();
        SetGlobalStatus("LOBBY CLOSED");

        if (logDebug)
            Debug.Log("[MultiplayerMenu] LeaveCurrentLobby", this);
    }

    public void TryHostStartPreparedMatch()
    {
        ResolveDependencies();

        if (onlineMatchSession == null || !onlineMatchSession.CanHostStartMatch())
        {
            ApplyLobbyStatusText("LOBBY NOT READY");
            SetGlobalStatus("LOBBY NOT READY");
            return;
        }

        if (fusionSessionController == null)
        {
            ApplyLobbyStatusText("FUSION CONTROLLER MISSING");
            SetGlobalStatus("FUSION CONTROLLER MISSING");
            return;
        }

        fusionSessionController.HostPreparedSession();
        ApplyLobbyStatusText(defaultStartingText);
        SetGlobalStatus(defaultStartingText);

        if (logDebug)
            Debug.Log("[MultiplayerMenu] TryHostStartPreparedMatch", this);
    }

    public void ReturnToMainMenu()
    {
        StopAndResetMatchmakingTimer();
        StopMatchFoundCountdown();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void StartMatchmaking(MatchmakingQueuePreset preset)
    {
        ResolveDependencies();
        SanitizePersistentRuntime();

        onlineMatchSession.PrepareMatchmakingSession(
            preset.queueId,
            GetResolvedLocalProfileId(),
            ReadInput(matchmakingPlayerNameInputField, GetResolvedDefaultLocalDisplayName()),
            preset.matchMode,
            preset.pointsToWin,
            preset.matchDuration,
            preset.isRanked,
            useLiveMultiplayerServices,
            preset.allowChestRewards,
            preset.allowXpRewards,
            preset.allowStatsProgression
        );

        HideAllPanels();

        if (matchmakingLoadingPanel != null)
            matchmakingLoadingPanel.SetActive(true);

        RestartMatchmakingTimer();
        StopMatchFoundCountdown();
        ApplyMatchmakingStatusText(defaultSearchingText);
        SetGlobalStatus(defaultSearchingText);

        if (logDebug)
        {
            Debug.Log(
                "[MultiplayerMenu] StartMatchmaking -> " +
                "Queue=" + preset.queueId +
                " | Mode=" + preset.matchMode +
                " | Ranked=" + preset.isRanked,
                this
            );
        }
    }

    private void SanitizePersistentRuntime()
    {
        ResolveDependencies();

        fusionSessionController?.ForceResetLocalState("MultiplayerMenu sanitize");
        fusionRunnerManager?.ForceCleanupStaleRunnerIfNeeded();
        fusionLobbyService?.ForceResetRuntimeState("Menu sanitize");
    }

    private void HandleSessionUpdated()
    {
        RefreshAllUI();
    }

    private void HandleFusionOperationCompleted(bool success, string message)
    {
        SetGlobalStatus(message);

        if (onlineMatchSession != null && onlineMatchSession.CurrentSession != null)
            onlineMatchSession.CurrentSession.lobbyStatusText = message;

        RefreshAllUI();

        if (logDebug)
            Debug.Log("[MultiplayerMenu] Fusion operation -> " + message, this);
    }

    private void RefreshAllUI()
    {
        RefreshButtonsState();
        RefreshHostMatchModeUI();
        RefreshMatchmakingUI();
        RefreshPrivateHostUI();
        RefreshPrivateJoinUI();
        RefreshPrivateLoadingUI();
    }

    private void RefreshHostMatchModeUI()
    {
        if (hostMatchModeText != null)
            hostMatchModeText.text = privateHostMatchMode == MatchMode.TimeLimit ? "TIME" : "POINTS";

        if (hostTimeModeSelectedObject != null)
            hostTimeModeSelectedObject.SetActive(privateHostMatchMode == MatchMode.TimeLimit);

        if (hostScoreModeSelectedObject != null)
            hostScoreModeSelectedObject.SetActive(privateHostMatchMode == MatchMode.ScoreTarget);
    }

    private void RefreshMatchmakingUI()
    {
        if (onlineMatchSession == null || onlineMatchSession.CurrentSession == null)
        {
            StopAndResetMatchmakingTimer();
            StopMatchFoundCountdown();
            SetMatchFoundPanel(false);
            return;
        }

        OnlineMatchSessionData session = onlineMatchSession.CurrentSession;
        bool isSearching = session.isMatchmakingSearch;

        if (!isSearching)
        {
            StopAndResetMatchmakingTimer();
            StopMatchFoundCountdown();
            SetMatchFoundPanel(false);

            if (matchmakingLoadingPanel != null)
                matchmakingLoadingPanel.SetActive(false);

            return;
        }

        bool hasMatchFound = session.hasRemoteJoiner;

        if (hasMatchFound)
        {
            StopAndResetMatchmakingTimer();

            if (matchmakingLoadingPanel != null)
                matchmakingLoadingPanel.SetActive(false);

            SetMatchFoundPanel(true);
            RefreshMatchFoundPanel(session);

            if (!matchFoundCountdownRunning)
                StartMatchFoundCountdown();

            SetGlobalStatus(defaultMatchFoundTitle);
            return;
        }

        StopMatchFoundCountdown();
        SetMatchFoundPanel(false);

        if (matchmakingLoadingPanel != null)
            matchmakingLoadingPanel.SetActive(true);

        EnsureMatchmakingTimerRunning();

        string message = defaultSearchingText;
        ApplyMatchmakingStatusText(message);
        SetGlobalStatus(message);
    }

    private void RefreshMatchFoundPanel(OnlineMatchSessionData session)
    {
        if (session == null)
            return;

        if (matchFoundTitleText != null)
            matchFoundTitleText.text = defaultMatchFoundTitle;

        string p1 = string.IsNullOrWhiteSpace(session.hostDisplayName) ? "PLAYER 1" : session.hostDisplayName.ToUpperInvariant();
        string p2 = string.IsNullOrWhiteSpace(session.joinDisplayName) ? "PLAYER 2" : session.joinDisplayName.ToUpperInvariant();

        if (matchFoundPlayer1Text != null)
            matchFoundPlayer1Text.text = p1;

        if (matchFoundPlayer2Text != null)
            matchFoundPlayer2Text.text = p2;

        if (matchFoundCountdownText != null)
            matchFoundCountdownText.text = Mathf.CeilToInt(Mathf.Max(0f, matchFoundCountdownRemaining)).ToString();
    }

    private void RefreshPrivateHostUI()
    {
        if (onlineMatchSession == null || onlineMatchSession.CurrentSession == null)
            return;

        OnlineMatchSessionData session = onlineMatchSession.CurrentSession;
        bool shouldShowHostPanel =
            session.hasPreparedSession &&
            session.sessionType == MatchRuntimeConfig.MatchSessionType.OnlinePrivate &&
            session.isHost &&
            !session.isMatchmakingSearch;

        if (waitingOpponentPanelHost != null)
            waitingOpponentPanelHost.SetActive(shouldShowHostPanel);

        if (!shouldShowHostPanel)
            return;

        string resolvedRoomCode = !string.IsNullOrWhiteSpace(session.roomCode)
            ? session.roomCode
            : (!string.IsNullOrWhiteSpace(session.sessionId) ? session.sessionId : "-");

        if (generatedRoomCodeText != null)
            generatedRoomCodeText.text = resolvedRoomCode;

        bool hasJoiner = session.hasRemoteJoiner && !string.IsNullOrWhiteSpace(session.joinDisplayName);

        if (joinedPlayerTextRoot != null)
            joinedPlayerTextRoot.SetActive(hasJoiner);
        else if (joinedPlayerText != null)
            joinedPlayerText.gameObject.SetActive(hasJoiner);

        if (joinedPlayerText != null)
        {
            if (hasJoiner)
                joinedPlayerText.text = $"'{session.joinDisplayName.ToUpperInvariant()}' JOINED THE LOBBY";
            else
                joinedPlayerText.text = string.Empty;
        }

        string lobbyStatus = ResolvePrivateHostStatus(session);
        ApplyLobbyStatusText(lobbyStatus);
        SetGlobalStatus(lobbyStatus);
    }

    private void RefreshPrivateJoinUI()
    {
        if (onlineMatchSession == null || onlineMatchSession.CurrentSession == null)
            return;

        OnlineMatchSessionData session = onlineMatchSession.CurrentSession;
        bool shouldShowJoinPanel =
            session.hasPreparedSession &&
            session.sessionType == MatchRuntimeConfig.MatchSessionType.OnlinePrivate &&
            !session.isHost &&
            !session.isMatchmakingSearch;

        if (waitingOpponentPanelJoin != null)
            waitingOpponentPanelJoin.SetActive(shouldShowJoinPanel);

        if (!shouldShowJoinPanel)
            return;

        string hostName = string.IsNullOrWhiteSpace(session.hostDisplayName)
            ? "HOST"
            : session.hostDisplayName.ToUpperInvariant();

        if (joinLobbyMessageText != null)
            joinLobbyMessageText.text = $"YOU JOINED '{hostName}' LOBBY";

        string joinStatus = ResolveDisplayStatus(session, defaultJoinWaitingText);

        if (joinLobbyStatusText != null)
            joinLobbyStatusText.text = joinStatus;

        SetGlobalStatus(joinStatus);
    }

    private void RefreshPrivateLoadingUI()
    {
        if (privateLobbyLoadingPanel == null)
            return;

        bool isVisible =
            (waitingOpponentPanelHost != null && waitingOpponentPanelHost.activeSelf) ||
            (waitingOpponentPanelJoin != null && waitingOpponentPanelJoin.activeSelf);

        privateLobbyLoadingPanel.SetActive(false);

        if (!isVisible || privateLobbyLoadingText == null || onlineMatchSession == null || onlineMatchSession.CurrentSession == null)
            return;

        privateLobbyLoadingText.text = ResolveDisplayStatus(onlineMatchSession.CurrentSession, "LOBBY");
    }

    private void RefreshButtonsState()
    {
        if (hostStartMatchButton == null)
            return;

        hostStartMatchButton.interactable =
            onlineMatchSession != null &&
            onlineMatchSession.CanHostStartMatch();
    }

    private string ResolvePrivateHostStatus(OnlineMatchSessionData session)
    {
        if (session == null)
            return defaultWaitingHostText;

        if (session.matchStarted || session.startRequested)
            return defaultStartingText;

        if (session.hasRemoteJoiner)
            return defaultLobbyReadyText;

        return ResolveDisplayStatus(session, defaultWaitingHostText);
    }

    private string ResolveDisplayStatus(OnlineMatchSessionData session, string fallback)
    {
        if (session == null)
            return fallback;

        if (!string.IsNullOrWhiteSpace(session.lobbyStatusText))
            return session.lobbyStatusText.Trim().ToUpperInvariant();

        return fallback;
    }

    private void ApplyMatchmakingStatusText(string value)
    {
        if (matchmakingLoadingMessageText != null)
            matchmakingLoadingMessageText.text = value;
    }

    private void ApplyLobbyStatusText(string value)
    {
        if (lobbyStatusText != null)
            lobbyStatusText.text = value;

        if (joinLobbyStatusText != null && waitingOpponentPanelJoin != null && waitingOpponentPanelJoin.activeSelf)
            joinLobbyStatusText.text = value;

        if (privateLobbyLoadingText != null && privateLobbyLoadingPanel != null && privateLobbyLoadingPanel.activeSelf)
            privateLobbyLoadingText.text = value;
    }

    private void SetGlobalStatus(string value)
    {
        if (statusText != null)
            statusText.text = value;

        if (logDebug)
            Debug.Log("[MultiplayerMenu] STATUS -> " + value, this);
    }

    private void HideAllPanels()
    {
        if (waitingOpponentPanelHost != null)
            waitingOpponentPanelHost.SetActive(false);

        if (waitingOpponentPanelJoin != null)
            waitingOpponentPanelJoin.SetActive(false);

        if (privateLobbyLoadingPanel != null)
            privateLobbyLoadingPanel.SetActive(false);

        if (matchmakingLoadingPanel != null)
            matchmakingLoadingPanel.SetActive(false);

        SetMatchFoundPanel(false);
    }

    private void ShowHostWaiting()
    {
        HideAllPanels();

        if (waitingOpponentPanelHost != null)
            waitingOpponentPanelHost.SetActive(true);
    }

    private void ShowJoinWaiting()
    {
        HideAllPanels();

        if (waitingOpponentPanelJoin != null)
            waitingOpponentPanelJoin.SetActive(true);
    }

    private void SetMatchFoundPanel(bool visible)
    {
        if (matchFoundPanel != null)
            matchFoundPanel.SetActive(visible);
    }

    private void StartMatchFoundCountdown()
    {
        matchFoundCountdownRunning = true;
        matchFoundCountdownRemaining = matchFoundCountdownDuration;
        autoStartMatchmakingRequested = false;
    }

    private void StopMatchFoundCountdown()
    {
        matchFoundCountdownRunning = false;
        matchFoundCountdownRemaining = 0f;
        autoStartMatchmakingRequested = false;

        if (matchFoundCountdownText != null)
            matchFoundCountdownText.text = string.Empty;
    }

    private void UpdateMatchFoundCountdown()
    {
        if (!matchFoundCountdownRunning)
            return;

        if (onlineMatchSession == null || onlineMatchSession.CurrentSession == null)
        {
            StopMatchFoundCountdown();
            return;
        }

        OnlineMatchSessionData session = onlineMatchSession.CurrentSession;
        if (!session.isMatchmakingSearch || !session.hasRemoteJoiner)
        {
            StopMatchFoundCountdown();
            return;
        }

        matchFoundCountdownRemaining -= Time.unscaledDeltaTime;
        if (matchFoundCountdownRemaining < 0f)
            matchFoundCountdownRemaining = 0f;

        RefreshMatchFoundPanel(session);

        if (matchFoundCountdownRemaining > 0f)
            return;

        if (autoStartMatchmakingRequested)
            return;

        autoStartMatchmakingRequested = true;
        matchFoundCountdownRunning = false;

        if (session.isHost)
        {
            if (fusionSessionController != null)
            {
                fusionSessionController.HostPreparedSession();
                SetGlobalStatus(defaultStartingText);
            }
        }
    }

    private void RestartMatchmakingTimer()
    {
        matchmakingElapsedSeconds = 0f;
        matchmakingTimerRunning = true;
        RefreshMatchmakingTimerText();
    }

    private void EnsureMatchmakingTimerRunning()
    {
        if (matchmakingTimerRunning)
            return;

        matchmakingTimerRunning = true;
        RefreshMatchmakingTimerText();
    }

    private void StopAndResetMatchmakingTimer()
    {
        matchmakingTimerRunning = false;
        matchmakingElapsedSeconds = 0f;
        RefreshMatchmakingTimerText();
    }

    private void UpdateMatchmakingElapsedTimer()
    {
        if (!matchmakingTimerRunning)
            return;

        matchmakingElapsedSeconds += Time.unscaledDeltaTime;
        RefreshMatchmakingTimerText();
    }

    private void RefreshMatchmakingTimerText()
    {
        if (matchmakingElapsedTimerText == null)
            return;

        if (!matchmakingTimerRunning && matchmakingElapsedSeconds <= 0f)
        {
            matchmakingElapsedTimerText.text = "00:00";
            return;
        }

        int totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, matchmakingElapsedSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        matchmakingElapsedTimerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    private void ResolveDependencies()
    {
        if (onlineMatchSession == null)
            onlineMatchSession = OnlineMatchSession.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (fusionSessionController == null)
            fusionSessionController = PhotonFusionSessionController.Instance;

        if (fusionRunnerManager == null)
            fusionRunnerManager = PhotonFusionRunnerManager.Instance;

        if (fusionLobbyService == null)
            fusionLobbyService = FusionLobbyService.Instance;
    }

    private string GetResolvedLocalProfileId()
    {
        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
            return profileManager.ActiveProfileId;

        return "local_player_1";
    }

    private string GetResolvedDefaultLocalDisplayName()
    {
        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName))
            return profileManager.ActiveDisplayName;

        return "Player 1";
    }

    private string ReadInput(TMP_InputField field, string fallback)
    {
        if (field == null || string.IsNullOrWhiteSpace(field.text))
            return fallback;

        return field.text.Trim();
    }

    private int ReadHostPointsToWin()
    {
        if (hostPointsToWinInputField == null)
            return defaultPointsToWin;

        if (int.TryParse(hostPointsToWinInputField.text, out int value))
            return Mathf.Max(1, value);

        return defaultPointsToWin;
    }

    private float ReadHostMatchDuration()
    {
        if (hostMatchDurationInputField == null)
            return defaultMatchDuration;

        if (float.TryParse(hostMatchDurationInputField.text, out float value))
            return Mathf.Max(1f, value);

        return defaultMatchDuration;
    }

    private string ReadJoinRoomCode()
    {
        if (joinRoomCodeInputField == null || string.IsNullOrWhiteSpace(joinRoomCodeInputField.text))
            return "ROOM01";

        return joinRoomCodeInputField.text.Trim().ToUpperInvariant();
    }
}