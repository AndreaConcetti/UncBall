using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MultiplayerMenu : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlineMatchSession onlineMatchSession;
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "Gameplay";

    [Header("Optional Status UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text generatedRoomCodeText;
    [SerializeField] private TMP_Text lobbyStatusHostText;
    [SerializeField] private TMP_Text lobbyStatusJoinText;

    [Header("Host Mode UI")]
    [SerializeField] private TMP_Text hostModeLabelText;

    [Header("Private Lobby - Host")]
    [SerializeField] private TMP_InputField hostRemotePlayerNameInputField;
    [SerializeField] private TMP_InputField hostPointsToWinInputField;
    [SerializeField] private TMP_InputField hostMatchDurationInputField;

    [SerializeField] private Selectable hostPointsToWinSelectable;
    [SerializeField] private Selectable hostMatchDurationSelectable;

    [SerializeField] private bool hostUsesTimeMode = true;

    [Header("Private Lobby - Join")]
    [SerializeField] private TMP_InputField joinRoomCodeInputField;
    [SerializeField] private TMP_InputField joinRemotePlayerNameInputField;

    [Header("Defaults")]
    [SerializeField] private string defaultRemotePlayerName = "Remote Player";
    [SerializeField] private int defaultPointsToWin = 16;
    [SerializeField] private float defaultMatchDuration = 180f;
    [SerializeField] private bool useLiveMultiplayerServices = false;

    [Header("Progression Safety For Placeholder")]
    [SerializeField] private bool disableChestRewardsForPlaceholderSessions = true;
    [SerializeField] private bool disableXpRewardsForPlaceholderSessions = true;
    [SerializeField] private bool disableStatsProgressionForPlaceholderSessions = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool hasPreparedSession = false;

    private void Start()
    {
        ResolveDependencies();
        InitializeDefaultInputs();
        RefreshHostModeUI();
        RefreshLobbyTexts();

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
        {
            Debug.Log(
                "[MultiplayerMenu] RefreshHostModeUI -> HostUsesTimeMode=" + hostUsesTimeMode,
                this
            );
        }
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

        onlineMatchSession.PreparePrivateHostSession(
            GetResolvedLocalProfileId(),
            GetResolvedLocalDisplayName(),
            ReadHostRemotePlayerName(),
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

        hasPreparedSession = true;
        RefreshLobbyTexts();

        SetStatus(
            "Private host lobby prepared (" +
            (hostUsesTimeMode ? "Time Based" : "Points Based") +
            ")"
        );

        if (logDebug)
        {
            Debug.Log(
                "[MultiplayerMenu] Host lobby prepared. " +
                "MatchMode=" + matchMode +
                " | PointsToWin=" + resolvedPointsToWin +
                " | Duration=" + resolvedMatchDuration,
                this
            );
        }
    }

    public void JoinPrivateLobby()
    {
        ResolveDependencies();

        if (onlineMatchSession == null)
        {
            Debug.LogError("[MultiplayerMenu] OnlineMatchSession missing.", this);
            return;
        }

        onlineMatchSession.PreparePrivateJoinSession(
            ReadJoinRoomCode(),
            GetResolvedLocalProfileId(),
            GetResolvedLocalDisplayName(),
            ReadJoinRemotePlayerName(),
            StartEndController.MatchMode.TimeLimit,
            defaultPointsToWin,
            defaultMatchDuration,
            isRanked: false,
            useLiveMultiplayerServices: useLiveMultiplayerServices,
            allowChestRewards: !disableChestRewardsForPlaceholderSessions,
            allowXpRewards: !disableXpRewardsForPlaceholderSessions,
            allowStatsProgression: !disableStatsProgressionForPlaceholderSessions
        );

        hasPreparedSession = true;
        RefreshLobbyTexts();
        SetStatus("Private join session prepared");

        if (logDebug)
        {
            Debug.Log(
                "[MultiplayerMenu] Join lobby prepared. " +
                "RoomCode=" + ReadJoinRoomCode(),
                this
            );
        }
    }

    public void TryHostStartPreparedMatch()
    {
        ResolveDependencies();

        if (onlineMatchSession == null)
        {
            Debug.LogError("[MultiplayerMenu] OnlineMatchSession missing.", this);
            return;
        }

        if (!onlineMatchSession.HasPreparedSession)
        {
            SetStatus("Cannot start: no prepared host session");
            return;
        }

        if (!onlineMatchSession.CurrentSession.isHost)
        {
            SetStatus("Cannot start: current user is not host");
            return;
        }

        if (!onlineMatchSession.CanHostStartMatch())
        {
            SetStatus("Cannot start: waiting for joined player");
            RefreshLobbyTexts();
            return;
        }

        bool requested = onlineMatchSession.RequestHostStartMatch();

        if (!requested)
        {
            SetStatus("Cannot start: host start request failed");
            return;
        }

        StartPreparedGameplay();
    }

    public void PrepareNormalMatchmaking()
    {
        PrepareOnlineMatchmakingInternal(false);
    }

    public void PrepareRankedMatchmaking()
    {
        PrepareOnlineMatchmakingInternal(true);
    }

    public void PrepareAndStartNormalMatchmaking()
    {
        PrepareNormalMatchmaking();
        StartPreparedGameplay();
    }

    public void PrepareAndStartRankedMatchmaking()
    {
        PrepareRankedMatchmaking();
        StartPreparedGameplay();
    }

    public void StartPreparedGameplay()
    {
        ResolveDependencies();

        if (onlineMatchSession == null)
        {
            Debug.LogError("[MultiplayerMenu] OnlineMatchSession missing.", this);
            return;
        }

        bool loaded = onlineMatchSession.LoadPreparedMatchScene(gameplaySceneName);

        if (loaded)
            SetStatus("Loading prepared gameplay...");

        if (logDebug)
        {
            Debug.Log(
                "[MultiplayerMenu] StartPreparedGameplay -> " +
                "Loaded=" + loaded +
                " | Scene=" + gameplaySceneName +
                " | HasPreparedSession=" + hasPreparedSession,
                this
            );
        }
    }

    public void RefreshLobbyTexts()
    {
        string roomCode = "-";
        string hostStatus = "Lobby empty / waiting opponent";
        string joinStatus = "Lobby not found / not joined";

        if (onlineMatchSession != null &&
            onlineMatchSession.CurrentSession != null &&
            onlineMatchSession.CurrentSession.hasPreparedSession)
        {
            roomCode = string.IsNullOrWhiteSpace(onlineMatchSession.CurrentSession.roomCode)
                ? "-"
                : onlineMatchSession.CurrentSession.roomCode;

            if (onlineMatchSession.CurrentSession.isHost)
            {
                hostStatus = onlineMatchSession.CurrentSession.hasRemoteJoiner
                    ? "Opponent joined / lobby ready"
                    : "Waiting for opponent";
            }
            else
            {
                joinStatus = "Joined lobby";
            }
        }

        if (generatedRoomCodeText != null)
            generatedRoomCodeText.text = roomCode;

        if (lobbyStatusHostText != null)
            lobbyStatusHostText.text = hostStatus;

        if (lobbyStatusJoinText != null)
            lobbyStatusJoinText.text = joinStatus;
    }

    public void ClearPreparedSession()
    {
        ResolveDependencies();

        if (onlineMatchSession != null)
            onlineMatchSession.ClearPreparedSession();

        hasPreparedSession = false;
        RefreshLobbyTexts();
        SetStatus("No prepared session");
    }

    private void PrepareOnlineMatchmakingInternal(bool isRanked)
    {
        ResolveDependencies();

        if (onlineMatchSession == null)
        {
            Debug.LogError("[MultiplayerMenu] OnlineMatchSession missing.", this);
            return;
        }

        onlineMatchSession.PrepareMatchmakingSession(
            GetResolvedLocalProfileId(),
            GetResolvedLocalDisplayName(),
            defaultRemotePlayerName,
            StartEndController.MatchMode.TimeLimit,
            defaultPointsToWin,
            defaultMatchDuration,
            isRanked,
            useLiveMultiplayerServices,
            allowChestRewards: !disableChestRewardsForPlaceholderSessions,
            allowXpRewards: !disableXpRewardsForPlaceholderSessions,
            allowStatsProgression: !disableStatsProgressionForPlaceholderSessions
        );

        hasPreparedSession = true;
        RefreshLobbyTexts();

        SetStatus(isRanked
            ? "Ranked matchmaking prepared"
            : "Normal matchmaking prepared");

        if (logDebug)
        {
            Debug.Log(
                "[MultiplayerMenu] Matchmaking prepared. Ranked=" + isRanked,
                this
            );
        }
    }

    private void InitializeDefaultInputs()
    {
        if (hostRemotePlayerNameInputField != null && string.IsNullOrWhiteSpace(hostRemotePlayerNameInputField.text))
            hostRemotePlayerNameInputField.text = defaultRemotePlayerName;

        if (joinRemotePlayerNameInputField != null && string.IsNullOrWhiteSpace(joinRemotePlayerNameInputField.text))
            joinRemotePlayerNameInputField.text = defaultRemotePlayerName;

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

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        if (logDebug)
            Debug.Log("[MultiplayerMenu] " + message, this);
    }

    private void ResolveDependencies()
    {
        if (onlineMatchSession == null)
            onlineMatchSession = OnlineMatchSession.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (onlineMatchSession == null)
            onlineMatchSession = FindFirstObjectByType<OnlineMatchSession>();

        if (profileManager == null)
            profileManager = FindFirstObjectByType<PlayerProfileManager>();
#else
        if (onlineMatchSession == null)
            onlineMatchSession = FindObjectOfType<OnlineMatchSession>();

        if (profileManager == null)
            profileManager = FindObjectOfType<PlayerProfileManager>();
#endif
    }

    private string GetResolvedLocalProfileId()
    {
        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
            return profileManager.ActiveProfileId;

        return "local_player_1";
    }

    private string GetResolvedLocalDisplayName()
    {
        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName))
            return profileManager.ActiveDisplayName;

        return "Player 1";
    }

    private string ReadHostRemotePlayerName()
    {
        if (hostRemotePlayerNameInputField == null)
            return defaultRemotePlayerName;

        string value = hostRemotePlayerNameInputField.text.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            hostRemotePlayerNameInputField.text = defaultRemotePlayerName;
            return defaultRemotePlayerName;
        }

        return value;
    }

    private string ReadJoinRemotePlayerName()
    {
        if (joinRemotePlayerNameInputField == null)
            return defaultRemotePlayerName;

        string value = joinRemotePlayerNameInputField.text.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            joinRemotePlayerNameInputField.text = defaultRemotePlayerName;
            return defaultRemotePlayerName;
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
}