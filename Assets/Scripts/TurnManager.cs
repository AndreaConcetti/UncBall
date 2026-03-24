using UnityEngine;
using TMPro;

/// <summary>
/// Gestisce il flusso dei turni:
/// - player attivo
/// - timer turno
/// - cambio turno
/// - stuck ball check
/// - stesso turno dopo punto
///
/// Regola implementata:
/// se durante il turno corrente entra almeno UNA pallina del player attivo,
/// il turno rimane allo stesso player.
/// Inoltre, se cade una pallina vecchia in DeathZone, non deve influenzare il turno.
/// Solo la watchedBall (pallina del turno corrente) può chiudere il tiro.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    [Header("Players")]
    public PlayerController player1;
    public PlayerController player2;
    public PlayerController currentPlayer;

    [Header("References")]
    public ScoreManagerNew scoreManager;
    public BallTurnSpawner ballTurnSpawner;
    public StartEndController startEndController;
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

    private float currentTimer;
    public float CurrentTimer => currentTimer;

    private bool timerRunning = true;
    private bool handlingSuccessfulScore = false;

    private BallPhysics watchedBall;
    private Rigidbody watchedBallRb;
    private bool hasBallBeenLaunched;
    private float stuckTimer;
    private Vector3 lastWatchedBallPosition;

    // True se durante questo turno il player corrente ha fatto almeno un punto con una sua pallina
    private bool currentPlayerScoredThisTurn = false;

    public bool IsPlayer1Turn => currentPlayer == player1;
    public bool IsPlayer2Turn => currentPlayer == player2;

    /// <summary>
    /// True solo se esiste ancora la ball corrente ed è già stata lanciata.
    /// </summary>
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
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

#if UNITY_2023_1_OR_NEWER
        if (startEndController == null)
            startEndController = FindFirstObjectByType<StartEndController>();
#else
        if (startEndController == null)
            startEndController = FindObjectOfType<StartEndController>();
#endif

        StartTurn(player1);
    }

    void OnEnable()
    {
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

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
        UpdateTurnTimer();
        UpdateStuckBallCheck();
    }

    public void ApplyPlayerNames(string player1Name, string player2Name)
    {
        player1TurnLabel = string.IsNullOrWhiteSpace(player1Name) ? "Player 1" : player1Name;
        player2TurnLabel = string.IsNullOrWhiteSpace(player2Name) ? "Player 2" : player2Name;

        UpdateTurnText();
    }

    void UpdateTurnTimer()
    {
        if (!timerRunning)
            return;

        currentTimer -= Time.deltaTime;

        if (timerText != null)
            timerText.text = Mathf.CeilToInt(currentTimer).ToString();

        if (currentTimer <= 0f)
        {
            Debug.Log("Tempo scaduto!");
            EndTurn();
        }
    }

    /// <summary>
    /// Controllo stuck basato SOLO sulla posizione.
    /// Vale solo per la watchedBall del turno corrente.
    /// Se la watchedBall si blocca:
    /// - se il player corrente aveva già segnato in questo turno, rigioca
    /// - altrimenti perde il turno
    /// </summary>
    void UpdateStuckBallCheck()
    {
        if (!enableStuckBallCheck)
            return;

        if (watchedBall == null)
            return;

        if (!hasBallBeenLaunched)
            return;

        if (ballTurnSpawner != null && ballTurnSpawner.IsBallInsideCurrentLaunchArea(watchedBall, currentPlayer, player1, player2))
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
                Debug.Log("Palla del turno bloccata troppo a lungo.");

                if (currentPlayerScoredThisTurn)
                {
                    HandleSuccessfulTurnResolution();
                }
                else
                {
                    ResolveShotWithoutScore();
                }

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
        currentPlayer = player;

        ResetTimer();
        ResumeTimer();

        if (player1 != null)
            player1.SetActive(player == player1);

        if (player2 != null)
            player2.SetActive(player == player2);

        UpdateTurnText();
        SpawnBallForCurrentTurn();

       // Debug.Log("Turno di: " + currentPlayer.name);
    }

    void UpdateTurnText()
    {
        if (turnOwnerText == null)
            return;

        if (currentPlayer == player1)
            turnOwnerText.text = player1TurnLabel;
        else if (currentPlayer == player2)
            turnOwnerText.text = player2TurnLabel;
        else
            turnOwnerText.text = string.Empty;
    }

    void SpawnBallForCurrentTurn()
    {
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
        DestroyCurrentBallIfNotLaunched();

        ResetStuckBallWatch();
        ResetTurnScoreState();

        if (currentPlayer == player1)
            StartTurn(player2);
        else
            StartTurn(player1);
    }

    /// <summary>
    /// Chiamato dalla DeathZone.
    /// Solo la watchedBall del turno corrente può davvero chiudere il tiro.
    /// Se cade una pallina vecchia, viene ignorata ai fini del turno.
    /// </summary>
    public void BallLost(BallPhysics lostBall)
    {
        if (lostBall == null)
            return;

        if (lostBall != watchedBall)
        {
            Debug.Log("DeathZone: è caduta una ball non tracciata dal turno corrente, ignorata per il cambio turno.");
            return;
        }

       // Debug.Log("DeathZone: è caduta la watchedBall del turno corrente.");

        if (currentPlayerScoredThisTurn)
        {
            HandleSuccessfulTurnResolution();
        }
        else
        {
            ResolveShotWithoutScore();
        }
    }

    /// <summary>
    /// Chiamato dal BallLauncher quando il lancio avviene davvero.
    /// Da questo momento il controllo stuck può partire.
    /// </summary>
    public void NotifyBallLaunched(BallPhysics launchedBall)
    {
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

    /// <summary>
    /// Tiro fallito: nessun punto utile del player corrente.
    /// </summary>
    void ResolveShotWithoutScore()
    {
        PauseTimer();
        ResetStuckBallWatch();
        ResetTurnScoreState();

        if (player1 != null) player1.ball = null;
        if (player2 != null) player2.ball = null;

        if (startEndController != null && startEndController.ShouldDelayProgressionAfterResolvedShot())
        {
            startEndController.HandleResolvedShotTransition();
            return;
        }

        EndTurn();
    }

    /// <summary>
    /// Tiro riuscito: durante questo turno il player corrente ha segnato almeno una volta.
    /// Quindi rigioca.
    /// </summary>
    void HandleSuccessfulTurnResolution()
    {
        if (handlingSuccessfulScore)
            return;

        handlingSuccessfulScore = true;

        PauseTimer();
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

    /// <summary>
    /// Se durante il turno corrente segna almeno una pallina del player attivo,
    /// il turno diventa "riuscito".
    /// La prima volta che accade, rigioca subito lo stesso player con nuova ball.
    /// </summary>
    void OnPointsScored(PlayerID playerWhoScored, int newTotal)
    {
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
        PauseTimer();
        ResetStuckBallWatch();
        ResetTurnScoreState();

        if (player1 != null)
            player1.ball = null;

        if (player2 != null)
            player2.ball = null;
    }

    public void StartSpecificTurn(PlayerController player)
    {
        ResetStuckBallWatch();
        ResetTurnScoreState();
        StartTurn(player);
    }

    void DestroyCurrentBallIfNotLaunched()
    {
        if (hasBallBeenLaunched)
            return;

        BallPhysics ballToDestroy = watchedBall;

        if (ballToDestroy == null)
        {
            if (IsPlayer1Turn && player1 != null)
                ballToDestroy = player1.ball;
            else if (IsPlayer2Turn && player2 != null)
                ballToDestroy = player2.ball;
        }

        if (ballToDestroy != null)
            Destroy(ballToDestroy.gameObject);

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
}