using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public PlayerController player1;
    public PlayerController player2;

    public float turnDuration = 15f;
    private float currentTimer;
    public float CurrentTimer => currentTimer;


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
        currentTimer -= Time.deltaTime;

        if (currentTimer <= 0)
        {
            Debug.Log("Tempo scaduto!");
            EndTurn();
        }
    }

    void StartTurn(PlayerController player)
    {
        currentPlayer = player;

        // reset timer
        currentTimer = turnDuration;

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
}
