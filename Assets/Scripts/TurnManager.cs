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
/// Se StartEndController richiede una transizione di match
/// (halftime o end match), il TurnManager non avanza al turno successivo
/// finché la transizione non viene eseguita.
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
    /// Parte solo dopo il lancio reale e non vale se la ball è ancora nella launch area.
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
                Debug.Log("Palla bloccata troppo a lungo: fine risoluzione del tiro.");
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
        currentPlayer = player;

        ResetTimer();
        ResumeTimer();

        if (player1 != null)
            player1.SetActive(player == player1);

        if (player2 != null)
            player2.SetActive(player == player2);

        UpdateTurnText();
        SpawnBallForCurrentTurn();

        Debug.Log("Turno di: " + currentPlayer.name);
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

        if (currentPlayer == player1)
            StartTurn(player2);
        else
            StartTurn(player1);
    }

    public void BallLost()
    {
        Debug.Log("Palla persa!");
        ResolveShotWithoutScore();
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
    /// Chiamato quando il tiro si conclude senza segnare:
    /// death zone, ball bloccata, ecc.
    /// Se il match deve entrare in halftime/end, non avanza al turno successivo.
    /// </summary>
    void ResolveShotWithoutScore()
    {
        PauseTimer();
        ResetStuckBallWatch();

        if (player1 != null) player1.ball = null;
        if (player2 != null) player2.ball = null;

        if (startEndController != null && startEndController.ShouldDelayProgressionAfterResolvedShot())
        {
            startEndController.HandleResolvedShotTransition();
            return;
        }

        EndTurn();
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
    /// Dopo un punto il turno normalmente NON cambia:
    /// stesso player, reset timer, nuova ball.
    ///
    /// Ma se il match deve entrare in halftime o finire,
    /// non spawniamo la nuova ball e lasciamo la transizione allo StartEndController.
    /// </summary>
    void OnPointsScored(PlayerID playerWhoScored, int newTotal)
    {
        if (handlingSuccessfulScore)
            return;

        bool pointBelongsToCurrentTurn =
            (playerWhoScored == PlayerID.Player1 && IsPlayer1Turn) ||
            (playerWhoScored == PlayerID.Player2 && IsPlayer2Turn);

        if (!pointBelongsToCurrentTurn)
            return;

        handlingSuccessfulScore = true;

        PauseTimer();
        ResetStuckBallWatch();

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

    /// <summary>
    /// Chiamato a halftime: ferma il turno corrente e pulisce i riferimenti runtime.
    /// Non cambia il player corrente.
    /// </summary>
    public void SuspendTurnForHalftime()
    {
        PauseTimer();
        ResetStuckBallWatch();

        if (player1 != null)
            player1.ball = null;

        if (player2 != null)
            player2.ball = null;
    }

    /// <summary>
    /// Riparte il secondo tempo col player specificato.
    /// Serve per far iniziare P2 nel secondo tempo.
    /// </summary>
    public void StartSpecificTurn(PlayerController player)
    {
        ResetStuckBallWatch();
        StartTurn(player);
    }

    /// <summary>
    /// Se il turno finisce prima del lancio, elimina la ball corrente e pulisce i riferimenti.
    /// Se invece la ball è già stata lanciata, non la tocca.
    /// </summary>
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
    }

    void ResetStuckBallWatch()
    {
        watchedBall = null;
        watchedBallRb = null;
        hasBallBeenLaunched = false;
        stuckTimer = 0f;
        lastWatchedBallPosition = Vector3.zero;
    }
}