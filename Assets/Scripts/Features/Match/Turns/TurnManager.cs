using Fusion;
using TMPro;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    [Header("Players")]
    public PlayerController player1;
    public PlayerController player2;
    public PlayerController currentPlayer;

    [Header("References")]
    public ScoreManager scoreManager;
    public BallTurnSpawner ballTurnSpawner;
    public StartEndController startEndController;
    public OnlineGameplayAuthority onlineGameplayAuthority;
    public FusionMatchState fusionMatchState;
    public MatchRuntimeConfig matchRuntimeConfig;
    public PhotonFusionRunnerManager runnerManager;
    public TextMeshProUGUI timerText;

    [Header("Turn UI")]
    public TMP_Text turnOwnerText;
    public string player1TurnLabel = "Player 1";
    public string player2TurnLabel = "Player 2";

    [Header("Turn Settings")]
    public float turnDuration = 15f;

    [Header("Stuck Ball Detection")]
    public bool enableStuckBallCheck = true;
    public float stuckTimeout = 3f;
    public float stuckPositionDeltaThreshold = 0.002f;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private float currentTimer;
    public float CurrentTimer => currentTimer;

    private bool timerRunning = true;
    private bool handlingSuccessfulScore = false;

    private BallPhysics watchedBall;
    private Rigidbody watchedBallRb;
    private bool hasBallBeenLaunched;
    private float stuckTimer;
    private Vector3 lastWatchedBallPosition;

    private bool currentPlayerScoredThisTurn = false;

    public bool IsPlayer1Turn => currentPlayer == player1;
    public bool IsPlayer2Turn => currentPlayer == player2;
    public PlayerID CurrentTurnOwnerId => GetCurrentTurnOwnerId();

    public bool IsLaunchInProgress()
    {
        return watchedBall != null && hasBallBeenLaunched;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ResolveDependencies();
        UpdateTurnText();

        if (!ShouldRunAuthoritativeGameplayFlow())
        {
            PauseTimer();

            if (logDebug)
                Debug.Log("[TurnManager] Online gameplay detected. Local authoritative gameplay flow disabled.", this);

            return;
        }

        StartTurn(player1);
    }

    void OnEnable()
    {
        if (scoreManager == null)
            scoreManager = ScoreManager.Instance;

        if (scoreManager != null)
            scoreManager.onPointsScored.AddListener(OnPointsScored);
    }

    void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.onPointsScored.RemoveListener(OnPointsScored);
    }

    void Update()
    {
        if (!ShouldRunAuthoritativeGameplayFlow())
        {
            UpdateTurnText();
            return;
        }

        UpdateTurnTimer();
        UpdateStuckBallCheck();
    }

    void ResolveDependencies()
    {
        if (scoreManager == null)
            scoreManager = ScoreManager.Instance;

        if (startEndController == null)
            startEndController = StartEndController.InstanceOrFind();

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = MatchRuntimeConfig.Instance;

        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = FindFirstObjectByType<OnlineGameplayAuthority>();

        if (fusionMatchState == null)
            fusionMatchState = FindFirstObjectByType<FusionMatchState>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindFirstObjectByType<MatchRuntimeConfig>();

        if (runnerManager == null)
            runnerManager = FindFirstObjectByType<PhotonFusionRunnerManager>();
#else
        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = FindObjectOfType<OnlineGameplayAuthority>();

        if (fusionMatchState == null)
            fusionMatchState = FindObjectOfType<FusionMatchState>();

        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindObjectOfType<MatchRuntimeConfig>();

        if (runnerManager == null)
            runnerManager = FindObjectOfType<PhotonFusionRunnerManager>();
#endif
    }

    public void ApplyPlayerNames(string player1Name, string player2Name)
    {
        player1TurnLabel = string.IsNullOrWhiteSpace(player1Name) ? "Player 1" : player1Name;
        player2TurnLabel = string.IsNullOrWhiteSpace(player2Name) ? "Player 2" : player2Name;
        UpdateTurnText();
    }

    bool IsRuntimeOnlineMode()
    {
        if (onlineGameplayAuthority != null && onlineGameplayAuthority.IsOnlineSession)
            return true;

        if (matchRuntimeConfig != null && matchRuntimeConfig.IsOnlineMatch)
            return true;

        if (runnerManager != null && runnerManager.IsRunning)
            return true;

        return false;
    }

    bool ShouldRunAuthoritativeGameplayFlow()
    {
        if (IsRuntimeOnlineMode())
            return false;

        return true;
    }

    PlayerID GetCurrentTurnOwnerId()
    {
        if (currentPlayer == player1)
            return PlayerID.Player1;

        if (currentPlayer == player2)
            return PlayerID.Player2;

        if (onlineGameplayAuthority != null)
            return onlineGameplayAuthority.CurrentTurnOwner;

        return PlayerID.None;
    }

    public PlayerID GetRemotePlayerIdFallback()
    {
        if (onlineGameplayAuthority == null)
            return PlayerID.Player2;

        return onlineGameplayAuthority.RemotePlayerId;
    }

    void PublishTurnOwnerRuntime()
    {
        PlayerID owner = GetCurrentTurnOwnerId();

        if (onlineGameplayAuthority != null)
            onlineGameplayAuthority.SetCurrentTurnOwner(owner);

        if (fusionMatchState != null)
            fusionMatchState.PublishTurnOwner(owner);
    }

    void UpdateTurnTimer()
    {
        if (!timerRunning)
            return;

        currentTimer -= Time.deltaTime;

        if (currentTimer < 0f)
            currentTimer = 0f;

        if (timerText != null)
            timerText.text = Mathf.CeilToInt(currentTimer).ToString();

        if (currentTimer <= 0f)
            EndTurn();
    }

    void UpdateStuckBallCheck()
    {
        if (!enableStuckBallCheck || watchedBall == null || !hasBallBeenLaunched)
            return;

        if (watchedBallRb == null)
            watchedBallRb = watchedBall.GetComponent<Rigidbody>();

        if (watchedBallRb != null && watchedBallRb.isKinematic)
            return;

        Vector3 currentPosition = watchedBall.transform.position;
        float positionDelta = Vector3.Distance(currentPosition, lastWatchedBallPosition);
        bool isNearlyStill = positionDelta <= stuckPositionDeltaThreshold;

        if (isNearlyStill)
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer >= stuckTimeout)
            {
                if (currentPlayerScoredThisTurn)
                    HandleSuccessfulTurnResolution();
                else
                    ResolveShotWithoutScore();

                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastWatchedBallPosition = currentPosition;
    }

    void StartTurn(PlayerController player)
    {
        if (!ShouldRunAuthoritativeGameplayFlow())
            return;

        currentPlayer = player;

        ResetTimer();
        ResumeTimer();

        if (player1 != null)
            player1.SetActive(player == player1);

        if (player2 != null)
            player2.SetActive(player == player2);

        PublishTurnOwnerRuntime();
        UpdateTurnText();
        SpawnBallForCurrentTurn();

        if (logDebug)
            Debug.Log("[TurnManager] StartTurn -> " + GetCurrentTurnOwnerId(), this);
    }

    void UpdateTurnText()
    {
        if (turnOwnerText == null)
            return;

        PlayerID owner = GetCurrentTurnOwnerId();

        if (owner == PlayerID.Player1)
            turnOwnerText.text = player1TurnLabel;
        else if (owner == PlayerID.Player2)
            turnOwnerText.text = player2TurnLabel;
        else
            turnOwnerText.text = string.Empty;
    }

    void SpawnBallForCurrentTurn()
    {
        if (!ShouldRunAuthoritativeGameplayFlow())
            return;

        if (ballTurnSpawner == null)
        {
            Debug.LogError("TurnManager: BallTurnSpawner non assegnato.", this);
            return;
        }

        BallPhysics spawnedBall = ballTurnSpawner.PrepareBallForTurn(currentPlayer, player1, player2);
        if (spawnedBall == null)
            return;

        AssignBallToCurrentPlayer(spawnedBall);
        BeginWatchingBall(spawnedBall);
    }

    public void EndTurn()
    {
        if (!ShouldRunAuthoritativeGameplayFlow())
            return;

        DestroyCurrentBallAlways();

        ResetStuckBallWatch();
        ResetTurnScoreState();

        if (currentPlayer == player1)
            StartTurn(player2);
        else
            StartTurn(player1);
    }

    public void BallLost(BallPhysics lostBall)
    {
        if (!ShouldRunAuthoritativeGameplayFlow())
            return;

        if (lostBall == null || lostBall != watchedBall)
            return;

        if (currentPlayerScoredThisTurn)
            HandleSuccessfulTurnResolution();
        else
            ResolveShotWithoutScore();
    }

    public void NotifyBallLaunched(BallPhysics launchedBall)
    {
        if (!ShouldRunAuthoritativeGameplayFlow())
            return;

        if (launchedBall == null)
            return;

        if (watchedBall != launchedBall)
        {
            watchedBall = launchedBall;
            watchedBallRb = launchedBall.GetComponent<Rigidbody>();
        }

        hasBallBeenLaunched = true;
        stuckTimer = 0f;
        lastWatchedBallPosition = launchedBall.transform.position;
    }

    void ResolveShotWithoutScore()
    {
        PauseTimer();

        DestroyCurrentBallAlways();

        ResetStuckBallWatch();
        ResetTurnScoreState();

        if (player1 != null) player1.ball = null;
        if (player2 != null) player2.ball = null;

        if (startEndController != null && startEndController.ShouldDelayProgressionAfterResolvedShot())
        {
            startEndController.HandleResolvedShotTransition();
            return;
        }

        if (currentPlayer == player1)
            StartTurn(player2);
        else
            StartTurn(player1);
    }

    void HandleSuccessfulTurnResolution()
    {
        if (handlingSuccessfulScore)
            return;

        handlingSuccessfulScore = true;

        PauseTimer();

        DestroyCurrentBallAlways();

        ResetStuckBallWatch();
        ResetTurnScoreState();

        if (player1 != null) player1.ball = null;
        if (player2 != null) player2.ball = null;

        if (startEndController != null && startEndController.ShouldDelayProgressionAfterResolvedShot())
        {
            startEndController.HandleResolvedShotTransition();
            handlingSuccessfulScore = false;
            return;
        }

        ResetTimer();
        ResumeTimer();
        SpawnBallForCurrentTurn();

        handlingSuccessfulScore = false;
    }

    public void ResetTimer()
    {
        currentTimer = turnDuration;
    }

    public void PauseTimer()
    {
        timerRunning = false;
    }

    public void ResumeTimer()
    {
        timerRunning = true;
    }

    public void AssignBallToCurrentPlayer(BallPhysics ball)
    {
        if (IsPlayer1Turn && player1 != null)
            player1.ball = ball;
        else if (IsPlayer2Turn && player2 != null)
            player2.ball = ball;
    }

    void OnPointsScored(PlayerID playerWhoScored, int newTotal)
    {
        if (!ShouldRunAuthoritativeGameplayFlow())
            return;

        bool pointBelongsToCurrentTurn =
            (playerWhoScored == PlayerID.Player1 && IsPlayer1Turn) ||
            (playerWhoScored == PlayerID.Player2 && IsPlayer2Turn);

        if (!pointBelongsToCurrentTurn)
            return;

        currentPlayerScoredThisTurn = true;

        if (handlingSuccessfulScore)
            return;

        HandleSuccessfulTurnResolution();
    }

    public void SuspendTurnForHalftime()
    {
        if (!ShouldRunAuthoritativeGameplayFlow())
            return;

        PauseTimer();
        DestroyCurrentBallAlways();
        ResetStuckBallWatch();
        ResetTurnScoreState();

        if (player1 != null)
            player1.ball = null;

        if (player2 != null)
            player2.ball = null;
    }

    public void StartSpecificTurn(PlayerController player)
    {
        if (!ShouldRunAuthoritativeGameplayFlow())
            return;

        ResetStuckBallWatch();
        ResetTurnScoreState();
        StartTurn(player);
    }

    void DestroyCurrentBallAlways()
    {
        BallPhysics ballToDestroy = watchedBall;

        if (ballToDestroy == null)
        {
            if (IsPlayer1Turn && player1 != null)
                ballToDestroy = player1.ball;
            else if (IsPlayer2Turn && player2 != null)
                ballToDestroy = player2.ball;
        }

        if (ballToDestroy != null)
        {
            NetworkObject netObj = ballToDestroy.GetComponent<NetworkObject>();

            if (netObj != null && fusionMatchState != null && fusionMatchState.IsNetworkSpawnReady)
                fusionMatchState.Runner.Despawn(netObj);
            else
                Destroy(ballToDestroy.gameObject);
        }

        if (ballTurnSpawner != null && ballTurnSpawner.launcher != null)
            ballTurnSpawner.launcher.ball = null;

        if (player1 != null)
            player1.ball = null;

        if (player2 != null)
            player2.ball = null;
    }

    void BeginWatchingBall(BallPhysics ball)
    {
        watchedBall = ball;
        watchedBallRb = ball != null ? ball.GetComponent<Rigidbody>() : null;
        hasBallBeenLaunched = false;
        stuckTimer = 0f;
        lastWatchedBallPosition = ball != null ? ball.transform.position : Vector3.zero;
        currentPlayerScoredThisTurn = false;
    }

    void ResetStuckBallWatch()
    {
        watchedBall = null;
        watchedBallRb = null;
        hasBallBeenLaunched = false;
        stuckTimer = 0f;
        lastWatchedBallPosition = Vector3.zero;
    }

    void ResetTurnScoreState()
    {
        currentPlayerScoredThisTurn = false;
    }

    public void ApplyOnlineReplicaState(PlayerID replicatedTurnOwner, float replicatedTimeRemaining)
    {
        if (!IsRuntimeOnlineMode())
            return;

        currentTimer = Mathf.Max(0f, replicatedTimeRemaining);

        if (replicatedTurnOwner == PlayerID.Player1)
            currentPlayer = player1;
        else if (replicatedTurnOwner == PlayerID.Player2)
            currentPlayer = player2;
        else
            currentPlayer = null;

        UpdateTurnText();

        if (timerText != null)
            timerText.text = Mathf.CeilToInt(currentTimer).ToString();
    }
}