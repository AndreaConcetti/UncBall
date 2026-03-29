using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class MatchmakingQueuePreset
{
    public string queueId = "normal";
    public StartEndController.MatchMode matchMode = StartEndController.MatchMode.ScoreTarget;
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

    [Header("Local Player Input")]
    [SerializeField] private TMP_InputField localPlayerNameInputField;
    [SerializeField] private string defaultHostLocalPlayerName = "HostPlayer";
    [SerializeField] private string defaultJoinLocalPlayerName = "JoinPlayer";

    [Header("Optional Status UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text generatedRoomCodeText;
    [SerializeField] private TMP_Text lobbyStatusHostText;
    [SerializeField] private TMP_Text lobbyStatusJoinText;

    [Header("Waiting Panels")]
    [SerializeField] private GameObject waitingOpponentPanelHost;
    [SerializeField] private GameObject waitingOpponentPanelJoin;

    [Header("Waiting Panel Host Texts")]
    [SerializeField] private TMP_Text waitingHostRoomCodeText;
    [SerializeField] private TMP_Text waitingHostLobbyStatusText;
    [SerializeField] private TMP_Text waitingHostPlayerEnteredText;

    [Header("Waiting Panel Join Texts")]
    [SerializeField] private TMP_Text waitingJoinLobbyStatusText;
    [SerializeField] private TMP_Text waitingJoinPlayerEnteredText;
    [SerializeField] private TMP_Text waitingJoinRoomCodeText;

    [Header("Waiting Buttons")]
    [SerializeField] private Button hostStartMatchButton;

    [Header("Private Lobby Loading UI")]
    [SerializeField] private GameObject privateLobbyLoadingPanel;
    [SerializeField] private TMP_Text privateLobbyLoadingText;
    [SerializeField][TextArea] private string createLobbyLoadingMessage = "Creating lobby...\nIt could take about 30 seconds.";
    [SerializeField][TextArea] private string joinLobbyLoadingMessage = "Joining lobby...\nIt could take about 30 seconds.";

    [Header("Matchmaking Loading UI")]
    [SerializeField] private GameObject matchmakingLoadingPanel;
    [SerializeField] private TMP_Text matchmakingLoadingText;
    [SerializeField][TextArea] private string normalMatchmakingLoadingMessage = "SEARCHING NORMAL MATCHMAKING...";
    [SerializeField][TextArea] private string rankedMatchmakingLoadingMessage = "SEARCHING RANKED MATCHMAKING...";
    [SerializeField][TextArea] private string defaultSearchingStatusText = "WAITING FOR OPPONENT";

    [Header("Match Found Panel")]
    [SerializeField] private GameObject matchFoundPanel;
    [SerializeField] private TMP_Text matchFoundTitleText;
    [SerializeField] private TMP_Text matchFoundLocalPlayerNameText;
    [SerializeField] private TMP_Text matchFoundOpponentPlayerNameText;
    [SerializeField] private TMP_Text matchFoundCountdownText;
    [SerializeField] private float matchFoundCountdownSeconds = 3f;
    [SerializeField] private bool autoStartMatchmakingMatch = true;

    [Header("Host Mode UI")]
    [SerializeField] private TMP_Text hostModeLabelText;

    [Header("Private Lobby - Host")]
    [SerializeField] private TMP_InputField hostPointsToWinInputField;
    [SerializeField] private TMP_InputField hostMatchDurationInputField;
    [SerializeField] private Selectable hostPointsToWinSelectable;
    [SerializeField] private Selectable hostMatchDurationSelectable;
    [SerializeField] private bool hostUsesTimeMode = true;

    [Header("Private Lobby - Join")]
    [SerializeField] private TMP_InputField joinRoomCodeInputField;

    [Header("Matchmaking Defaults")]
    [SerializeField]
    private MatchmakingQueuePreset normalMatchmakingPreset = new MatchmakingQueuePreset
    {
        queueId = "normal",
        matchMode = StartEndController.MatchMode.ScoreTarget,
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
        matchMode = StartEndController.MatchMode.ScoreTarget,
        pointsToWin = 21,
        matchDuration = 180f,
        isRanked = true,
        allowChestRewards = false,
        allowXpRewards = false,
        allowStatsProgression = false
    };

    [Header("Defaults")]
    [SerializeField] private int defaultPointsToWin = 16;
    [SerializeField] private float defaultMatchDuration = 180f;
    [SerializeField] private bool useLiveMultiplayerServices = true;

    [Header("Progression Safety For Placeholder")]
    [SerializeField] private bool disableChestRewardsForPlaceholderSessions = true;
    [SerializeField] private bool disableXpRewardsForPlaceholderSessions = true;
    [SerializeField] private bool disableStatsProgressionForPlaceholderSessions = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool isCreatingLobby;
    private bool isJoiningLobby;
    private bool isSearchingMatchmaking;
    private bool isShowingMatchFound;
    private Coroutine matchFoundCountdownCoroutine;
    private string currentMatchmakingLoadingBaseMessage = string.Empty;

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
        InitializeDefaultInputs();
        RefreshHostModeUI();
        HideWaitingPanels();
        HideAllLoading();
        HideMatchFoundPanel();
        RefreshLobbyTexts();
        RefreshButtonsState();

        if (generatedRoomCodeText != null && string.IsNullOrWhiteSpace(generatedRoomCodeText.text))
            generatedRoomCodeText.text = "-";

        SetStatus("No prepared session");
    }

    public void SetHostModeTime()
    {
        hostUsesTimeMode = true;
        RefreshHostModeUI();
        SetStatus("Host mode set to Time Based");
    }

    public void SetHostModeScore()
    {
        hostUsesTimeMode = false;
        RefreshHostModeUI();
        SetStatus("Host mode set to Points Based");
    }

    public void ToggleHostMode()
    {
        hostUsesTimeMode = !hostUsesTimeMode;
        RefreshHostModeUI();
        SetStatus(hostUsesTimeMode ? "Host mode set to Time Based" : "Host mode set to Points Based");
    }

    public void RefreshHostModeUI()
    {
        bool useTimeMode = hostUsesTimeMode;

        if (hostModeLabelText != null)
            hostModeLabelText.text = useTimeMode ? "TIME BASED" : "POINTS BASED";

        SetSelectableInteractable(hostMatchDurationSelectable, useTimeMode);
        SetSelectableInteractable(hostPointsToWinSelectable, !useTimeMode);

        if (hostMatchDurationInputField != null)
        {
            hostMatchDurationInputField.readOnly = !useTimeMode;

            if (hostMatchDurationInputField.textComponent != null)
                hostMatchDurationInputField.textComponent.alpha = useTimeMode ? 1f : 0.5f;
        }

        if (hostPointsToWinInputField != null)
        {
            hostPointsToWinInputField.readOnly = useTimeMode;

            if (hostPointsToWinInputField.textComponent != null)
                hostPointsToWinInputField.textComponent.alpha = useTimeMode ? 0.5f : 1f;
        }

        if (logDebug)
            Debug.Log("[MultiplayerMenu] RefreshHostModeUI -> HostUsesTimeMode=" + hostUsesTimeMode, this);
    }

    public void CreateHostLobby()
    {
        ResolveDependencies();

        if (onlineMatchSession == null)
        {
            Debug.LogError("[MultiplayerMenu] OnlineMatchSession missing.", this);
            return;
        }

        StartEndController.MatchMode matchMode =
            hostUsesTimeMode
                ? StartEndController.MatchMode.TimeLimit
                : StartEndController.MatchMode.ScoreTarget;

        int resolvedPointsToWin = hostUsesTimeMode
            ? Mathf.Max(1, defaultPointsToWin)
            : ReadHostPointsToWin();

        float resolvedMatchDuration = hostUsesTimeMode
            ? ReadHostMatchDuration()
            : Mathf.Max(1f, defaultMatchDuration);

        string resolvedLocalName = ReadResolvedLocalPlayerName(isHostFlow: true);

        isCreatingLobby = true;
        isJoiningLobby = false;
        isSearchingMatchmaking = false;
        currentMatchmakingLoadingBaseMessage = string.Empty;

        HideWaitingPanels();
        HideMatchFoundPanel();
        HideAllLoading();
        ShowPrivateLobbyLoading(createLobbyLoadingMessage);

        onlineMatchSession.PreparePrivateHostSession(
            GetResolvedLocalProfileId(),
            resolvedLocalName,
            "Player 2",
            matchMode,
            resolvedPointsToWin,
            resolvedMatchDuration,
            isRanked: false,
            useLiveMultiplayerServices: useLiveMultiplayerServices,
            allowChestRewards: !disableChestRewardsForPlaceholderSessions,
            allowXpRewards: !disableXpRewardsForPlaceholderSessions,
            allowStatsProgression: !disableStatsProgressionForPlaceholderSessions,
            customRoomCode: null
        );

        RefreshLobbyTexts();
        RefreshButtonsState();
        SetStatus("Creating online lobby...");
    }

    public void JoinPrivateLobby()
    {
        ResolveDependencies();

        if (onlineMatchSession == null)
        {
            Debug.LogError("[MultiplayerMenu] OnlineMatchSession missing.", this);
            return;
        }

        string resolvedLocalName = ReadResolvedLocalPlayerName(isHostFlow: false);

        isCreatingLobby = false;
        isJoiningLobby = true;
        isSearchingMatchmaking = false;
        currentMatchmakingLoadingBaseMessage = string.Empty;

        HideWaitingPanels();
        HideMatchFoundPanel();
        HideAllLoading();
        ShowPrivateLobbyLoading(joinLobbyLoadingMessage);

        onlineMatchSession.PreparePrivateJoinSession(
            ReadJoinRoomCode(),
            GetResolvedLocalProfileId(),
            resolvedLocalName,
            "HostPlayer",
            StartEndController.MatchMode.TimeLimit,
            defaultPointsToWin,
            defaultMatchDuration,
            isRanked: false,
            useLiveMultiplayerServices: useLiveMultiplayerServices,
            allowChestRewards: !disableChestRewardsForPlaceholderSessions,
            allowXpRewards: !disableXpRewardsForPlaceholderSessions,
            allowStatsProgression: !disableStatsProgressionForPlaceholderSessions
        );

        RefreshLobbyTexts();
        RefreshButtonsState();
        SetStatus("Joining online lobby...");
    }

    public void QueueNormalMatchmaking()
    {
        StartMatchmaking(normalMatchmakingPreset, normalMatchmakingLoadingMessage);
    }

    public void QueueRankedMatchmaking()
    {
        StartMatchmaking(rankedMatchmakingPreset, rankedMatchmakingLoadingMessage);
    }

    public void CancelMatchmakingSearch()
    {
        ResolveDependencies();

        if (onlineMatchSession == null)
        {
            Debug.LogError("[MultiplayerMenu] OnlineMatchSession missing.", this);
            return;
        }

        StopMatchFoundCountdown();
        HideMatchFoundPanel();

        onlineMatchSession.CancelMatchmakingSearch();

        isCreatingLobby = false;
        isJoiningLobby = false;
        isSearchingMatchmaking = false;
        currentMatchmakingLoadingBaseMessage = string.Empty;

        HideAllLoading();
        HideWaitingPanels();
        RefreshLobbyTexts();
        RefreshButtonsState();
        SetStatus("Matchmaking cancelled.");
    }

    private void StartMatchmaking(MatchmakingQueuePreset preset, string loadingMessage)
    {
        ResolveDependencies();

        if (onlineMatchSession == null)
        {
            Debug.LogError("[MultiplayerMenu] OnlineMatchSession missing.", this);
            return;
        }

        string resolvedLocalName = ReadResolvedLocalPlayerName(isHostFlow: false);

        isCreatingLobby = false;
        isJoiningLobby = false;
        isSearchingMatchmaking = true;
        currentMatchmakingLoadingBaseMessage = string.IsNullOrWhiteSpace(loadingMessage)
            ? (preset.isRanked ? "SEARCHING RANKED MATCHMAKING..." : "SEARCHING NORMAL MATCHMAKING...")
            : loadingMessage.Trim();

        HideWaitingPanels();
        HideMatchFoundPanel();
        HideAllLoading();
        ShowMatchmakingLoading(currentMatchmakingLoadingBaseMessage);

        onlineMatchSession.PrepareMatchmakingSession(
            preset.queueId,
            GetResolvedLocalProfileId(),
            resolvedLocalName,
            preset.matchMode,
            preset.pointsToWin,
            preset.matchDuration,
            preset.isRanked,
            useLiveMultiplayerServices,
            preset.allowChestRewards,
            preset.allowXpRewards,
            preset.allowStatsProgression
        );

        RefreshLobbyTexts();
        RefreshButtonsState();
        SetStatus("Searching matchmaking...");
    }

    public void TryHostStartPreparedMatch()
    {
        ResolveDependencies();

        if (onlineMatchSession == null)
        {
            Debug.LogError("[MultiplayerMenu] OnlineMatchSession missing.", this);
            return;
        }

        if (!onlineMatchSession.CanHostStartMatch())
        {
            SetStatus("Cannot start: lobby not ready");
            RefreshLobbyTexts();
            RefreshButtonsState();
            return;
        }

        if (fusionSessionController == null)
        {
            SetStatus("Cannot start: Fusion controller missing");
            return;
        }

        fusionSessionController.HostPreparedSession();
        SetStatus("Starting match...");
    }

    public void CloseHostWaitingPanel()
    {
        if (waitingOpponentPanelHost != null)
            waitingOpponentPanelHost.SetActive(false);
    }

    public void CloseJoinWaitingPanel()
    {
        if (waitingOpponentPanelJoin != null)
            waitingOpponentPanelJoin.SetActive(false);
    }

    public void RefreshLobbyTexts()
    {
        string roomCode = "-";

        string hostSummaryStatus = "Lobby not ready";
        string joinSummaryStatus = "Room not joined";

        string hostWaitingStatus = "LOBBY NOT READY";
        string joinWaitingStatus = "JOINING LOBBY";

        string hostPlayerEnteredText = "WAITING FOR PLAYER";
        string joinPlayerEnteredText = "WAITING FOR HOST";

        if (onlineMatchSession != null &&
            onlineMatchSession.CurrentSession != null &&
            onlineMatchSession.CurrentSession.hasPreparedSession)
        {
            OnlineMatchSessionData session = onlineMatchSession.CurrentSession;

            roomCode = string.IsNullOrWhiteSpace(session.roomCode)
                ? "-"
                : session.roomCode;

            if (session.sessionType == MatchRuntimeConfig.MatchSessionType.OnlineMatchmaking)
            {
                hostSummaryStatus = session.isRanked ? "Ranked queue" : "Normal queue";
                joinSummaryStatus = session.lobbyStatusText;
                hostWaitingStatus = session.isRanked ? "RANKED MATCHMAKING" : "NORMAL MATCHMAKING";
                joinWaitingStatus = string.IsNullOrWhiteSpace(session.lobbyStatusText)
                    ? "SEARCHING MATCHMAKING"
                    : session.lobbyStatusText.ToUpperInvariant();

                hostPlayerEnteredText = "SEARCHING FOR OPPONENT";
                joinPlayerEnteredText = "YOU ARE " + session.localDisplayName.ToUpperInvariant();
            }
            else if (session.isHost)
            {
                hostSummaryStatus = session.hasRemoteJoiner ? "Lobby ready" : "Lobby not ready";
                hostWaitingStatus = session.hasRemoteJoiner ? "LOBBY READY" : "LOBBY NOT READY";

                if (session.hasRemoteJoiner)
                    hostPlayerEnteredText = BuildHostJoinedMessage(session.remoteDisplayName);
                else
                    hostPlayerEnteredText = "WAITING FOR PLAYER";

                joinSummaryStatus = "Share this room code";
            }
            else
            {
                joinSummaryStatus = session.hasRemoteJoiner ? "Joined lobby / ready" : "Joining lobby";
                joinWaitingStatus = session.hasRemoteJoiner ? "JOINED LOBBY" : "JOINING LOBBY";
                joinPlayerEnteredText = BuildJoinLobbyMessage(session.remoteDisplayName);

                hostSummaryStatus = "Host side";
            }
        }

        if (generatedRoomCodeText != null)
            generatedRoomCodeText.text = roomCode;

        if (lobbyStatusHostText != null)
            lobbyStatusHostText.text = hostSummaryStatus;

        if (lobbyStatusJoinText != null)
            lobbyStatusJoinText.text = joinSummaryStatus;

        if (waitingHostRoomCodeText != null)
            waitingHostRoomCodeText.text = roomCode;

        if (waitingJoinRoomCodeText != null)
            waitingJoinRoomCodeText.text = roomCode;

        if (waitingHostLobbyStatusText != null)
            waitingHostLobbyStatusText.text = hostWaitingStatus;

        if (waitingJoinLobbyStatusText != null)
            waitingJoinLobbyStatusText.text = joinWaitingStatus;

        if (waitingHostPlayerEnteredText != null)
            waitingHostPlayerEnteredText.text = hostPlayerEnteredText;

        if (waitingJoinPlayerEnteredText != null)
            waitingJoinPlayerEnteredText.text = joinPlayerEnteredText;
    }

    private void RefreshButtonsState()
    {
        if (hostStartMatchButton != null)
        {
            bool canStart = false;

            if (onlineMatchSession != null &&
                onlineMatchSession.CurrentSession != null &&
                onlineMatchSession.CurrentSession.hasPreparedSession &&
                onlineMatchSession.CurrentSession.isHost)
            {
                canStart = onlineMatchSession.CanHostStartMatch();
            }

            hostStartMatchButton.interactable = canStart;
        }
    }

    private void InitializeDefaultInputs()
    {
        if (localPlayerNameInputField != null && string.IsNullOrWhiteSpace(localPlayerNameInputField.text))
            localPlayerNameInputField.text = defaultHostLocalPlayerName;

        if (hostPointsToWinInputField != null && string.IsNullOrWhiteSpace(hostPointsToWinInputField.text))
            hostPointsToWinInputField.text = defaultPointsToWin.ToString();

        if (hostMatchDurationInputField != null && string.IsNullOrWhiteSpace(hostMatchDurationInputField.text))
            hostMatchDurationInputField.text = Mathf.RoundToInt(defaultMatchDuration).ToString();
    }

    private void SetSelectableInteractable(Selectable selectable, bool interactable)
    {
        if (selectable == null)
            return;

        selectable.interactable = interactable;

        Graphic graphic = selectable.targetGraphic;
        if (graphic != null)
        {
            Color c = graphic.color;
            c.a = interactable ? 1f : 0.5f;
            graphic.color = c;
        }
    }

    private void HandleSessionUpdated()
    {
        if (onlineMatchSession == null ||
            onlineMatchSession.CurrentSession == null ||
            !onlineMatchSession.CurrentSession.hasPreparedSession)
        {
            RefreshLobbyTexts();
            RefreshButtonsState();
            return;
        }

        OnlineMatchSessionData session = onlineMatchSession.CurrentSession;

        bool hostCreationFinished =
            isCreatingLobby &&
            session.isHost &&
            !string.IsNullOrWhiteSpace(session.roomCode);

        bool joinFinished =
            isJoiningLobby &&
            !session.isHost &&
            !string.IsNullOrWhiteSpace(session.roomCode);

        if (hostCreationFinished)
        {
            HideAllLoading();
            ShowHostWaitingPanel();
            isCreatingLobby = false;
        }

        if (joinFinished)
        {
            HideAllLoading();
            ShowJoinWaitingPanel();
            isJoiningLobby = false;
        }

        if (session.sessionType == MatchRuntimeConfig.MatchSessionType.OnlineMatchmaking)
        {
            if (session.hasRemoteJoiner)
            {
                HideAllLoading();

                if (!isShowingMatchFound)
                    ShowMatchFoundPanel(session);

                isSearchingMatchmaking = false;
            }
            else
            {
                if (isSearchingMatchmaking)
                    RefreshMatchmakingLoadingText(session);
            }
        }

        RefreshLobbyTexts();
        RefreshButtonsState();
    }

    private void HandleFusionOperationCompleted(bool success, string message)
    {
        HideAllLoading();
        isCreatingLobby = false;
        isJoiningLobby = false;
        isSearchingMatchmaking = false;
        currentMatchmakingLoadingBaseMessage = string.Empty;

        RefreshLobbyTexts();
        RefreshButtonsState();
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        if (logDebug)
            Debug.Log("[MultiplayerMenu] " + message, this);
    }

    private void ShowPrivateLobbyLoading(string message)
    {
        if (privateLobbyLoadingPanel != null)
            privateLobbyLoadingPanel.SetActive(true);

        if (privateLobbyLoadingText != null)
            privateLobbyLoadingText.text = message;
    }

    private void ShowMatchmakingLoading(string message)
    {
        if (matchmakingLoadingPanel != null)
            matchmakingLoadingPanel.SetActive(true);

        if (matchmakingLoadingText != null)
            matchmakingLoadingText.text = message;
    }

    private void RefreshMatchmakingLoadingText(OnlineMatchSessionData session)
    {
        if (matchmakingLoadingText == null || session == null)
            return;

        string title = string.IsNullOrWhiteSpace(currentMatchmakingLoadingBaseMessage)
            ? (session.isRanked ? "SEARCHING RANKED MATCHMAKING..." : "SEARCHING NORMAL MATCHMAKING...")
            : currentMatchmakingLoadingBaseMessage;

        string normalizedStatus = NormalizeMatchmakingStatus(session.lobbyStatusText);

        matchmakingLoadingText.text = title + "\n" + normalizedStatus;
    }

    private string NormalizeMatchmakingStatus(string rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
            return defaultSearchingStatusText;

        string normalized = rawStatus.Trim();

        if (string.Equals(normalized, "Searching matchmaking", StringComparison.OrdinalIgnoreCase))
            return "WAITING FOR OPPONENT";

        if (string.Equals(normalized, "Waiting for opponent", StringComparison.OrdinalIgnoreCase))
            return "WAITING FOR OPPONENT";

        if (string.Equals(normalized, "Joined matchmaking lobby", StringComparison.OrdinalIgnoreCase))
            return "MATCH FOUND";

        if (string.Equals(normalized, "Lobby full / ready", StringComparison.OrdinalIgnoreCase))
            return "MATCH FOUND";

        return normalized.ToUpperInvariant();
    }

    private void HideAllLoading()
    {
        if (privateLobbyLoadingPanel != null)
            privateLobbyLoadingPanel.SetActive(false);

        if (matchmakingLoadingPanel != null)
            matchmakingLoadingPanel.SetActive(false);
    }

    private void ShowMatchFoundPanel(OnlineMatchSessionData session)
    {
        if (session == null)
            return;

        isShowingMatchFound = true;

        if (matchFoundPanel != null)
            matchFoundPanel.SetActive(true);

        if (matchFoundTitleText != null)
            matchFoundTitleText.text = "MATCH FOUND";

        if (matchFoundLocalPlayerNameText != null)
            matchFoundLocalPlayerNameText.text = BuildDisplayName(session.localDisplayName, "PLAYER 1");

        if (matchFoundOpponentPlayerNameText != null)
            matchFoundOpponentPlayerNameText.text = BuildDisplayName(session.remoteDisplayName, "PLAYER 2");

        if (matchFoundCountdownText != null)
            matchFoundCountdownText.text = Mathf.CeilToInt(matchFoundCountdownSeconds).ToString();

        StopMatchFoundCountdown();
        matchFoundCountdownCoroutine = StartCoroutine(MatchFoundCountdownRoutine());
    }

    private void HideMatchFoundPanel()
    {
        if (matchFoundPanel != null)
            matchFoundPanel.SetActive(false);

        isShowingMatchFound = false;
    }

    private void StopMatchFoundCountdown()
    {
        if (matchFoundCountdownCoroutine != null)
        {
            StopCoroutine(matchFoundCountdownCoroutine);
            matchFoundCountdownCoroutine = null;
        }
    }

    private IEnumerator MatchFoundCountdownRoutine()
    {
        float remaining = Mathf.Max(1f, matchFoundCountdownSeconds);

        while (remaining > 0f)
        {
            if (matchFoundCountdownText != null)
                matchFoundCountdownText.text = Mathf.CeilToInt(remaining).ToString();

            yield return new WaitForSecondsRealtime(1f);
            remaining -= 1f;
        }

        if (matchFoundCountdownText != null)
            matchFoundCountdownText.text = "0";

        if (autoStartMatchmakingMatch &&
            onlineMatchSession != null &&
            onlineMatchSession.CurrentSession != null &&
            onlineMatchSession.CurrentSession.sessionType == MatchRuntimeConfig.MatchSessionType.OnlineMatchmaking &&
            onlineMatchSession.CurrentSession.hasRemoteJoiner &&
            onlineMatchSession.CurrentSession.isHost)
        {
            TryHostStartPreparedMatch();
        }

        matchFoundCountdownCoroutine = null;
    }

    private void ResolveDependencies()
    {
        if (onlineMatchSession == null)
            onlineMatchSession = OnlineMatchSession.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (fusionSessionController == null)
            fusionSessionController = PhotonFusionSessionController.Instance;

#if UNITY_2023_1_OR_NEWER
        if (onlineMatchSession == null)
            onlineMatchSession = FindFirstObjectByType<OnlineMatchSession>();

        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();

        if (fusionSessionController == null)
            fusionSessionController = FindFirstObjectByType<PhotonFusionSessionController>();
#else
        if (onlineMatchSession == null)
            onlineMatchSession = FindObjectOfType<OnlineMatchSession>();

        if (profileManager == null)
            profileManager = FindObjectOfType<PlayerProfileManager>();

        if (fusionSessionController == null)
            fusionSessionController = FindObjectOfType<PhotonFusionSessionController>();
#endif
    }

    private string GetResolvedLocalProfileId()
    {
        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
            return profileManager.ActiveProfileId;

        return "local_player_1";
    }

    private string ReadResolvedLocalPlayerName(bool isHostFlow)
    {
        string fallback = isHostFlow ? defaultHostLocalPlayerName : defaultJoinLocalPlayerName;

        if (localPlayerNameInputField == null)
            return fallback;

        string value = localPlayerNameInputField.text.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            localPlayerNameInputField.text = fallback;
            return fallback;
        }

        return value;
    }

    private int ReadHostPointsToWin()
    {
        if (hostPointsToWinInputField == null)
            return Mathf.Max(1, defaultPointsToWin);

        if (int.TryParse(hostPointsToWinInputField.text, out int parsed))
        {
            parsed = Mathf.Max(1, parsed);
            hostPointsToWinInputField.text = parsed.ToString();
            return parsed;
        }

        hostPointsToWinInputField.text = defaultPointsToWin.ToString();
        return Mathf.Max(1, defaultPointsToWin);
    }

    private float ReadHostMatchDuration()
    {
        if (hostMatchDurationInputField == null)
            return Mathf.Max(1f, defaultMatchDuration);

        if (float.TryParse(hostMatchDurationInputField.text, out float parsed))
        {
            parsed = Mathf.Max(1f, parsed);
            hostMatchDurationInputField.text = Mathf.RoundToInt(parsed).ToString();
            return parsed;
        }

        hostMatchDurationInputField.text = Mathf.RoundToInt(defaultMatchDuration).ToString();
        return Mathf.Max(1f, defaultMatchDuration);
    }

    private string ReadJoinRoomCode()
    {
        if (joinRoomCodeInputField == null)
            return "ROOM01";

        string value = joinRoomCodeInputField.text.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(value))
        {
            joinRoomCodeInputField.text = "ROOM01";
            return "ROOM01";
        }

        return value;
    }

    private string BuildJoinLobbyMessage(string hostName)
    {
        string resolvedHostName = string.IsNullOrWhiteSpace(hostName) ? "HOSTPLAYER" : hostName.Trim().ToUpperInvariant();
        return "YOU JOINED " + resolvedHostName + " LOBBY";
    }

    private string BuildHostJoinedMessage(string joinName)
    {
        string resolvedJoinName = string.IsNullOrWhiteSpace(joinName) ? "PLAYER" : joinName.Trim().ToUpperInvariant();
        return resolvedJoinName + " JOINED THE LOBBY";
    }

    private string BuildDisplayName(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim().ToUpperInvariant();
    }

    private void ShowHostWaitingPanel()
    {
        if (waitingOpponentPanelHost != null)
            waitingOpponentPanelHost.SetActive(true);

        if (waitingOpponentPanelJoin != null)
            waitingOpponentPanelJoin.SetActive(false);
    }

    private void ShowJoinWaitingPanel()
    {
        if (waitingOpponentPanelHost != null)
            waitingOpponentPanelHost.SetActive(false);

        if (waitingOpponentPanelJoin != null)
            waitingOpponentPanelJoin.SetActive(true);
    }

    private void HideWaitingPanels()
    {
        if (waitingOpponentPanelHost != null)
            waitingOpponentPanelHost.SetActive(false);

        if (waitingOpponentPanelJoin != null)
            waitingOpponentPanelJoin.SetActive(false);
    }
}