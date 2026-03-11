using UnityEngine;
using TMPro;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public PlayerController player1;
    public PlayerController player2;

    public Material Player1Material;

    public Material Player2Material;

     public BallLauncher launcher;
    public GameObject ballPrefab;

    public Transform launchZone1;
    public Transform launchZone2;

    public TextMeshProUGUI timerText;

    public float turnDuration = 15f;
    private float currentTimer;
    public float CurrentTimer => currentTimer;


    private bool timerRunning = true;

    public bool IsPlayer1Turn => currentPlayer == player1;
    public bool IsPlayer2Turn => currentPlayer == player2;

    public PlayerController currentPlayer;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        StartTurn(player1);
    }

    void Update()
    {

        if (!timerRunning)
            return;


        currentTimer -= Time.deltaTime;
        int seconds = Mathf.CeilToInt(currentTimer);
        timerText.text = seconds.ToString();

        if (currentTimer <= 0)
        {
            Debug.Log("Tempo scaduto!");
            EndTurn();
        }
    }

    void StartTurn(PlayerController player)
    {
        currentPlayer = player;

        ResetTimer();
        ResumeTimer();

        // reset timer
        currentTimer = turnDuration;

        SpawnNewBall(player);

        Debug.Log("Turno di: " + currentPlayer.name);


    }

    public void EndTurn()
    {
        if (currentPlayer == player1)
            StartTurn(player2);
        else
            StartTurn(player1);
    }

    // chiamato quando la palla cade nella deathzone
    public void BallLost()
    {
        Debug.Log("Palla persa!");
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
    if (IsPlayer1Turn)
    {
        player1.ball = ball;
    }
    else
    {
        player2.ball = ball;
    }
}

public void SpawnNewBall(PlayerController player)
{
    Transform spawnPoint;

    if (IsPlayer1Turn)
        spawnPoint = launchZone1;
    else
        spawnPoint = launchZone2;


    player1.SetActive(player == player1);
    player2.SetActive(player == player2);


    GameObject ballObject = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);

    ballObject.GetComponent<MeshRenderer>().material = IsPlayer1Turn ? Player1Material : Player2Material;

    BallPhysics ballPhysics = ballObject.GetComponent<BallPhysics>();

    Rigidbody rb = ballObject.GetComponent<Rigidbody>();

    // blocca la pallina
    rb.constraints = RigidbodyConstraints.FreezeAll;

    // assegna al launcher
    launcher.ball = ballPhysics;

    //riattiva il launcher
    launcher.ResetLaunch();

    // assegna al player corrente
    AssignBallToCurrentPlayer(ballPhysics);
}
}
