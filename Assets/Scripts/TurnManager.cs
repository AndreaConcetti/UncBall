using UnityEngine;
using TMPro;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public PlayerController player1;
    public PlayerController player2;

    public TextMeshProUGUI timerText;

    public float turnDuration = 15f;
    private float currentTimer;

    private bool timerRunning = true;

    public bool IsPlayer1Turn => currentPlayer == player1;
    public bool IsPlayer2Turn => currentPlayer == player2;

    private PlayerController currentPlayer;

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

        ScoreManager.Instance.SpawnNewBall(); 

        player1.SetActive(player == player1);
        player2.SetActive(player == player2);

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
}
