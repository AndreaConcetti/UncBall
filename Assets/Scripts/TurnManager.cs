using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance;

    public PlayerController player1;
    public PlayerController player2;

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

    void StartTurn(PlayerController player)
    {
        currentPlayer = player;

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
}
