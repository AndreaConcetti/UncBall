using UnityEngine;
using TMPro;

/// <summary>
/// Gestisce il flusso dei turni dei due player:
/// - decide di chi è il turno corrente
/// - gestisce il timer del turno
/// - spawna la nuova pallina nella launch zone corretta
/// - cambia turno quando la palla viene persa o quando il tempo scade
/// - dopo un punto NON cambia turno: resetta il timer e spawna una nuova palla
/// - se una palla lanciata resta ferma troppo a lungo in una posizione "morta",
///   termina il turno automaticamente e passa all'altro player
///
/// Questo script NON calcola i punti.
/// I punti vengono calcolati da SlotScorer / StarPlate / ScoreManagerNew.
/// Qui ci occupiamo solo della logica di turno e spawn.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    [Header("Players")]
    [Tooltip("Controller del Player 1")]
    public PlayerController player1;

    [Tooltip("Controller del Player 2")]
    public PlayerController player2;

    [Tooltip("Player attualmente attivo")]
    public PlayerController currentPlayer;

    [Header("References")]
    [Tooltip("Launcher che gestisce il posizionamento e il lancio della pallina")]
    public BallLauncher launcher;

    [Tooltip("ScoreManager nuovo, usato solo per ascoltare l'evento di punto")]
    public ScoreManagerNew scoreManager;

    [Tooltip("Testo UI che mostra il timer del turno")]
    public TextMeshProUGUI timerText;

    [Header("Ball Prefabs")]
    [Tooltip("Prefab da spawnare quando è il turno del Player 1")]
    public GameObject player1BallPrefab;

    [Tooltip("Prefab da spawnare quando è il turno del Player 2")]
    public GameObject player2BallPrefab;

    [Tooltip("Prefab di fallback, usato solo se manca quello specifico del player")]
    public GameObject fallbackBallPrefab;

    [Header("Spawn Points")]
    [Tooltip("Punto di spawn della pallina del Player 1")]
    public Transform launchZone1;

    [Tooltip("Punto di spawn della pallina del Player 2")]
    public Transform launchZone2;

    [Header("Optional Material Override")]
    [Tooltip("Se attivo, forza il materiale della pallina in base al turno")]
    public bool overrideBallMaterial = false;

    [Tooltip("Materiale da applicare alla pallina del Player 1 se overrideBallMaterial è attivo")]
    public Material player1Material;

    [Tooltip("Materiale da applicare alla pallina del Player 2 se overrideBallMaterial è attivo")]
    public Material player2Material;

    [Header("Turn Settings")]
    [Tooltip("Durata del turno in secondi")]
    public float turnDuration = 15f;

    [Header("Stuck Ball Detection")]
    [Tooltip("Se attivo, una ball lanciata che resta ferma troppo a lungo fa terminare il turno")]
    public bool enableStuckBallCheck = true;

    [Tooltip("Numero di secondi per cui la palla deve rimanere praticamente ferma prima di terminare il turno")]
    public float stuckTimeout = 1.25f;

    [Tooltip("Spostamento minimo tra due frame per considerare la ball ancora in movimento")]
    public float stuckPositionDeltaThreshold = 0.002f;

    // Timer runtime del turno corrente
    private float currentTimer;
    public float CurrentTimer => currentTimer;

    // Se false, il timer del turno non scala
    private bool timerRunning = true;

    // Protezione contro doppi trigger mentre stiamo già gestendo un punto
    private bool handlingSuccessfulScore = false;

    // Stato del controllo "ball bloccata"
    private BallPhysics watchedBall;
    private Rigidbody watchedBallRb;
    private bool hasBallBeenLaunched;
    private float stuckTimer;
    private Vector3 lastWatchedBallPosition;

    public bool IsPlayer1Turn => currentPlayer == player1;
    public bool IsPlayer2Turn => currentPlayer == player2;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

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

    /// <summary>
    /// Aggiorna il timer del turno.
    /// Se arriva a zero, il turno passa all'altro player.
    /// </summary>
    void UpdateTurnTimer()
    {
        if (!timerRunning)
            return;

        currentTimer -= Time.deltaTime;

        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(currentTimer);
            timerText.text = seconds.ToString();
        }

        if (currentTimer <= 0f)
        {
            Debug.Log("Tempo scaduto!");
            EndTurn();
        }
    }

    /// <summary>
    /// Controlla se la ball attuale è stata lanciata e poi è rimasta
    /// praticamente ferma nello spazio per troppo tempo.
    ///
    /// IMPORTANTE:
    /// il controllo è basato SOLO sulla posizione della transform.
    /// La rotazione non viene considerata:
    /// se la ball gira su se stessa ma resta nello stesso punto,
    /// viene considerata ferma.
    /// </summary>
    void UpdateStuckBallCheck()
    {
        if (!enableStuckBallCheck)
            return;

        if (watchedBall == null)
            return;

        if (watchedBallRb == null)
            watchedBallRb = watchedBall.GetComponent<Rigidbody>();

        if (watchedBallRb != null && watchedBallRb.isKinematic)
            return;

        Vector3 currentPosition = watchedBall.transform.position;
        float positionDelta = Vector3.Distance(currentPosition, lastWatchedBallPosition);

        // Prima aspettiamo che la ball sia stata davvero lanciata,
        // cioè che abbia iniziato a muoversi nello spazio almeno una volta.
        if (!hasBallBeenLaunched)
        {
            bool ballStartedMoving = positionDelta > stuckPositionDeltaThreshold;

            if (ballStartedMoving)
                hasBallBeenLaunched = true;

            lastWatchedBallPosition = currentPosition;
            return;
        }

        // Da qui in poi guardiamo solo se la posizione cambia oppure no.
        bool isNearlyStill = positionDelta <= stuckPositionDeltaThreshold;

        if (isNearlyStill)
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer >= stuckTimeout)
            {
                Debug.Log("Palla bloccata troppo a lungo: fine turno.");
                EndTurn();
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastWatchedBallPosition = currentPosition;
    }

    /// <summary>
    /// Avvia un turno per il player indicato.
    /// Questo metodo:
    /// - imposta currentPlayer
    /// - resetta e riattiva il timer
    /// - attiva il player corretto e disattiva l'altro
    /// - spawna una nuova pallina nella launch zone corretta
    /// </summary>
    void StartTurn(PlayerController player)
    {
        currentPlayer = player;

        ResetTimer();
        ResumeTimer();

        if (player1 != null)
            player1.SetActive(player == player1);

        if (player2 != null)
            player2.SetActive(player == player2);

        SpawnNewBall(player);

        Debug.Log("Turno di: " + currentPlayer.name);
    }

    /// <summary>
    /// Termina il turno corrente e passa all'altro player.
    /// Usato SOLO quando:
    /// - il timer scade
    /// - la palla viene persa nella death zone
    /// - la palla resta bloccata troppo a lungo
    ///
    /// NON va usato quando si fa punto.
    /// </summary>
    public void EndTurn()
    {
        ResetStuckBallWatch();

        if (currentPlayer == player1)
            StartTurn(player2);
        else
            StartTurn(player1);
    }

    /// <summary>
    /// Chiamato da DeathZone quando una pallina viene persa.
    /// In questo caso il turno passa all'avversario.
    /// </summary>
    public void BallLost()
    {
        Debug.Log("Palla persa!");
        EndTurn();
    }

    /// <summary>
    /// Riporta il timer alla durata piena del turno.
    /// </summary>
    public void ResetTimer()
    {
        currentTimer = turnDuration;
    }

    /// <summary>
    /// Ferma il countdown del timer.
    /// </summary>
    public void PauseTimer()
    {
        timerRunning = false;
    }

    /// <summary>
    /// Riattiva il countdown del timer.
    /// </summary>
    public void ResumeTimer()
    {
        timerRunning = true;
    }

    /// <summary>
    /// Salva il riferimento della ball appena spawnata dentro il PlayerController attivo.
    /// Questo permette agli altri sistemi di sapere qual è la ball attualmente in mano al player.
    /// </summary>
    public void AssignBallToCurrentPlayer(BallPhysics ball)
    {
        if (IsPlayer1Turn)
            player1.ball = ball;
        else
            player2.ball = ball;
    }

    /// <summary>
    /// Spawna una nuova pallina per il player indicato.
    ///
    /// Regole:
    /// - se è Player 1 usa launchZone1 e il prefab del Player 1
    /// - se è Player 2 usa launchZone2 e il prefab del Player 2
    /// - se manca il prefab specifico usa fallbackBallPrefab
    ///
    /// Inoltre:
    /// - opzionalmente applica il materiale del player
    /// - congela la rigidbody all'inizio
    /// - assegna la ball al launcher
    /// - resetta il launcher
    /// - imposta il BallOwnership se presente
    /// - inizializza il controllo stuck-ball
    /// </summary>
    public void SpawnNewBall(PlayerController player)
    {
        Transform spawnPoint = player == player1 ? launchZone1 : launchZone2;
        GameObject prefabToSpawn = GetBallPrefabForPlayer(player);

        if (spawnPoint == null)
        {
            Debug.LogError("TurnManager: spawn point non assegnato per il player corrente.", this);
            return;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError("TurnManager: nessun prefab pallina assegnato per il player corrente.", this);
            return;
        }

        GameObject ballObject = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);

        if (overrideBallMaterial)
        {
            MeshRenderer renderer = ballObject.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.material = player == player1 ? player1Material : player2Material;
        }

        BallPhysics ballPhysics = ballObject.GetComponent<BallPhysics>();
        if (ballPhysics == null)
        {
            Debug.LogError("TurnManager: il prefab spawnato non contiene BallPhysics.", ballObject);
            return;
        }

        BallOwnership ownership = ballObject.GetComponent<BallOwnership>();
        if (ownership != null)
            ownership.Owner = player == player1 ? PlayerID.Player1 : PlayerID.Player2;

        Rigidbody rb = ballObject.GetComponent<Rigidbody>();
        if (rb != null)
            rb.constraints = RigidbodyConstraints.FreezeAll;

        if (launcher != null)
        {
            launcher.ball = ballPhysics;
            launcher.ResetLaunch();
        }

        AssignBallToCurrentPlayer(ballPhysics);
        BeginWatchingBall(ballPhysics);
    }

    /// <summary>
    /// Restituisce il prefab corretto in base al player.
    /// Se manca il prefab specifico, usa il fallback se presente.
    /// </summary>
    GameObject GetBallPrefabForPlayer(PlayerController player)
    {
        if (player == player1)
            return player1BallPrefab != null ? player1BallPrefab : fallbackBallPrefab;

        if (player == player2)
            return player2BallPrefab != null ? player2BallPrefab : fallbackBallPrefab;

        return fallbackBallPrefab;
    }

    /// <summary>
    /// Viene chiamato automaticamente da ScoreManagerNew quando un player fa punto.
    ///
    /// IMPORTANTE:
    /// - qui NON cambiamo turno
    /// - il player che ha segnato continua a giocare
    /// - resettiamo il timer
    /// - riattiviamo il timer
    /// - spawniamo una nuova pallina per il currentPlayer
    ///
    /// Questo replica il comportamento del sistema vecchio:
    /// "segni -> stessa mano continua".
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

        ResetTimer();
        ResumeTimer();
        SpawnNewBall(currentPlayer);

        handlingSuccessfulScore = false;
    }

    /// <summary>
    /// Inizia a monitorare la ball attuale per capire se, una volta lanciata,
    /// resta bloccata troppo a lungo.
    /// </summary>
    void BeginWatchingBall(BallPhysics ball)
    {
        watchedBall = ball;
        watchedBallRb = ball != null ? ball.GetComponent<Rigidbody>() : null;
        hasBallBeenLaunched = false;
        stuckTimer = 0f;
        lastWatchedBallPosition = ball != null ? ball.transform.position : Vector3.zero;
    }

    /// <summary>
    /// Azzera completamente il controllo sulla ball attuale.
    /// Da usare quando il turno finisce o cambia player.
    /// </summary>
    void ResetStuckBallWatch()
    {
        watchedBall = null;
        watchedBallRb = null;
        hasBallBeenLaunched = false;
        stuckTimer = 0f;
        lastWatchedBallPosition = Vector3.zero;
    }
}