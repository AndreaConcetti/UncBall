using UnityEngine;

public class OnlineGameplayAuthority : MonoBehaviour
{
    public static OnlineGameplayAuthority Instance { get; private set; }

    [Header("Runtime")]
    [SerializeField] private bool isOnlineSession = false;
    [SerializeField] private bool hasStateAuthority = false;
    [SerializeField] private PlayerID localPlayerId = PlayerID.Player1;
    [SerializeField] private PlayerID remotePlayerId = PlayerID.Player2;
    [SerializeField] private PlayerID currentTurnOwner = PlayerID.Player1;
    [SerializeField] private string localPlayerName = "Player 1";
    [SerializeField] private string remotePlayerName = "Player 2";

    [Header("Cached Online Controller")]
    [SerializeField] private FusionOnlineMatchController onlineMatchController;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    public bool IsOnlineSession => isOnlineSession;
    public bool HasStateAuthority => hasStateAuthority;
    public PlayerID LocalPlayerId => localPlayerId;
    public PlayerID RemotePlayerId => remotePlayerId;
    public PlayerID CurrentTurnOwner => currentTurnOwner;
    public string LocalPlayerName => localPlayerName;
    public string RemotePlayerName => remotePlayerName;
    public FusionOnlineMatchController OnlineMatchController => onlineMatchController;

    private void Awake()
    {
        Instance = this;
    }

    public void ConfigureOnline(
        PlayerID resolvedLocalPlayerId,
        PlayerID resolvedRemotePlayerId,
        bool resolvedHasStateAuthority,
        string resolvedLocalName,
        string resolvedRemoteName,
        PlayerID initialTurnOwner)
    {
        isOnlineSession = true;
        hasStateAuthority = resolvedHasStateAuthority;
        localPlayerId = resolvedLocalPlayerId;
        remotePlayerId = resolvedRemotePlayerId;
        currentTurnOwner = initialTurnOwner;
        localPlayerName = string.IsNullOrWhiteSpace(resolvedLocalName) ? "Player 1" : resolvedLocalName;
        remotePlayerName = string.IsNullOrWhiteSpace(resolvedRemoteName) ? "Player 2" : resolvedRemoteName;

        if (logDebug)
        {
            Debug.Log(
                "[OnlineGameplayAuthority] ConfigureOnline -> " +
                "Local=" + localPlayerId +
                " | Remote=" + remotePlayerId +
                " | HasAuthority=" + hasStateAuthority +
                " | TurnOwner=" + currentTurnOwner +
                " | LocalName=" + localPlayerName +
                " | RemoteName=" + remotePlayerName,
                this
            );
        }
    }

    public void ForceOnlinePlaceholderState()
    {
        isOnlineSession = true;
        hasStateAuthority = false;
        localPlayerId = PlayerID.Player1;
        remotePlayerId = PlayerID.Player2;
        currentTurnOwner = PlayerID.Player1;
        localPlayerName = "Player 1";
        remotePlayerName = "Player 2";
        onlineMatchController = null;

        if (logDebug)
            Debug.Log("[OnlineGameplayAuthority] ForceOnlinePlaceholderState", this);
    }

    public void ForceOfflineDisabledState()
    {
        ForceOnlinePlaceholderState();
    }

    public void SetOnlineMatchController(FusionOnlineMatchController controller)
    {
        onlineMatchController = controller;
    }

    public void SetStateAuthority(bool value)
    {
        hasStateAuthority = value;
    }

    public void SetCurrentTurnOwner(PlayerID owner)
    {
        currentTurnOwner = owner;
    }

    public void SetResolvedLocalPlayer(PlayerID local, PlayerID remote, string localName, string remoteName)
    {
        localPlayerId = local;
        remotePlayerId = remote;

        if (!string.IsNullOrWhiteSpace(localName))
            this.localPlayerName = localName;

        if (!string.IsNullOrWhiteSpace(remoteName))
            this.remotePlayerName = remoteName;

        if (logDebug)
        {
            Debug.Log(
                "[OnlineGameplayAuthority] SetResolvedLocalPlayer -> " +
                "Local=" + localPlayerId +
                " | Remote=" + remotePlayerId +
                " | LocalName=" + this.localPlayerName +
                " | RemoteName=" + this.remotePlayerName,
                this
            );
        }
    }

    public bool CanLocalControlCurrentTurn()
    {
        if (!isOnlineSession)
            return false;

        return localPlayerId == currentTurnOwner;
    }

    public bool CanLocalControlPlayer(PlayerID owner)
    {
        if (!isOnlineSession)
            return false;

        return localPlayerId == owner;
    }
}