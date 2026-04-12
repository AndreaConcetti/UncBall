using System;
using UnityEngine;

[Serializable]
public sealed class BotOfflineMatchRequest
{
    [Header("Identity")]
    [SerializeField] private string requestId;
    [SerializeField] private BotDifficulty difficulty;
    [SerializeField] private string localDisplayName;
    [SerializeField] private string botDisplayName;
    [SerializeField] private string localProfileId;
    [SerializeField] private string botProfileId;

    [Header("Rules")]
    [SerializeField] private MatchMode matchMode;
    [SerializeField] private int pointsToWin;
    [SerializeField] private float matchDurationSeconds;
    [SerializeField] private float turnDurationSeconds;

    [Header("Sides / Turn")]
    [SerializeField] private bool localPlayerIsPlayer1;
    [SerializeField] private bool player1StartsOnLeft;
    [SerializeField] private PlayerID initialTurnOwner;

    [Header("Skins")]
    [SerializeField] private string player1SkinUniqueId;
    [SerializeField] private string player2SkinUniqueId;

    [Header("Flags")]
    [SerializeField] private bool useDisguisedBotIdentity;
    [SerializeField] private bool createdOfflineWithoutInternet;
    [SerializeField] private int randomSeed;

    public string RequestId => requestId;
    public BotDifficulty Difficulty => difficulty;
    public string LocalDisplayName => localDisplayName;
    public string BotDisplayName => botDisplayName;
    public string LocalProfileId => localProfileId;
    public string BotProfileId => botProfileId;

    public MatchMode MatchMode => matchMode;
    public int PointsToWin => pointsToWin;
    public float MatchDurationSeconds => matchDurationSeconds;
    public float TurnDurationSeconds => turnDurationSeconds;

    public bool LocalPlayerIsPlayer1 => localPlayerIsPlayer1;
    public bool Player1StartsOnLeft => player1StartsOnLeft;
    public PlayerID InitialTurnOwner => initialTurnOwner;

    public string Player1SkinUniqueId => player1SkinUniqueId;
    public string Player2SkinUniqueId => player2SkinUniqueId;

    public bool UseDisguisedBotIdentity => useDisguisedBotIdentity;
    public bool CreatedOfflineWithoutInternet => createdOfflineWithoutInternet;
    public int RandomSeed => randomSeed;

    public BotOfflineMatchRequest(
        string requestId,
        BotDifficulty difficulty,
        string localDisplayName,
        string botDisplayName,
        string localProfileId,
        string botProfileId,
        MatchMode matchMode,
        int pointsToWin,
        float matchDurationSeconds,
        float turnDurationSeconds,
        bool localPlayerIsPlayer1,
        bool player1StartsOnLeft,
        PlayerID initialTurnOwner,
        string player1SkinUniqueId,
        string player2SkinUniqueId,
        bool useDisguisedBotIdentity,
        bool createdOfflineWithoutInternet,
        int randomSeed)
    {
        this.requestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId.Trim();
        this.difficulty = difficulty;
        this.localDisplayName = string.IsNullOrWhiteSpace(localDisplayName) ? "PLAYER" : localDisplayName.Trim();
        this.botDisplayName = string.IsNullOrWhiteSpace(botDisplayName) ? "BOT" : botDisplayName.Trim();
        this.localProfileId = string.IsNullOrWhiteSpace(localProfileId) ? "local_player" : localProfileId.Trim();
        this.botProfileId = string.IsNullOrWhiteSpace(botProfileId) ? "offline_bot" : botProfileId.Trim();

        this.matchMode = matchMode;
        this.pointsToWin = Mathf.Max(1, pointsToWin);
        this.matchDurationSeconds = Mathf.Max(1f, matchDurationSeconds);
        this.turnDurationSeconds = Mathf.Max(1f, turnDurationSeconds);

        this.localPlayerIsPlayer1 = localPlayerIsPlayer1;
        this.player1StartsOnLeft = player1StartsOnLeft;
        this.initialTurnOwner = initialTurnOwner == PlayerID.None ? PlayerID.Player1 : initialTurnOwner;

        this.player1SkinUniqueId = string.IsNullOrWhiteSpace(player1SkinUniqueId) ? string.Empty : player1SkinUniqueId.Trim();
        this.player2SkinUniqueId = string.IsNullOrWhiteSpace(player2SkinUniqueId) ? string.Empty : player2SkinUniqueId.Trim();

        this.useDisguisedBotIdentity = useDisguisedBotIdentity;
        this.createdOfflineWithoutInternet = createdOfflineWithoutInternet;
        this.randomSeed = randomSeed;
    }

    public string GetPlayer1DisplayName()
    {
        return localPlayerIsPlayer1 ? localDisplayName : botDisplayName;
    }

    public string GetPlayer2DisplayName()
    {
        return localPlayerIsPlayer1 ? botDisplayName : localDisplayName;
    }

    public string GetLocalAssignedDisplayName()
    {
        return localDisplayName;
    }

    public string GetBotAssignedDisplayName()
    {
        return botDisplayName;
    }

    public string GetLocalAssignedSkinId()
    {
        return localPlayerIsPlayer1 ? player1SkinUniqueId : player2SkinUniqueId;
    }

    public string GetBotAssignedSkinId()
    {
        return localPlayerIsPlayer1 ? player2SkinUniqueId : player1SkinUniqueId;
    }

    public override string ToString()
    {
        return $"BotOfflineMatchRequest | " +
               $"RequestId={requestId} | Difficulty={difficulty} | Mode={matchMode} | " +
               $"PointsToWin={pointsToWin} | MatchDuration={matchDurationSeconds} | TurnDuration={turnDurationSeconds} | " +
               $"LocalIsP1={localPlayerIsPlayer1} | P1Left={player1StartsOnLeft} | InitialTurn={initialTurnOwner} | " +
               $"P1Name={GetPlayer1DisplayName()} | P2Name={GetPlayer2DisplayName()} | " +
               $"P1Skin={player1SkinUniqueId} | P2Skin={player2SkinUniqueId} | Seed={randomSeed}";
    }
}
