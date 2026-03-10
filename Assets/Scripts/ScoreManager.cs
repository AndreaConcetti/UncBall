using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int player1Score;
    public int player2Score;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
    }

    public void AddScore(int points)
    {
        if (TurnManager.Instance.IsPlayer1Turn)
        {
            player1Score += points;
        }
        else
        {
            player2Score += points;
        }

        Debug.Log("P1: " + player1Score + " | P2: " + player2Score);

        TurnManager.Instance.ResetTimer();
        TurnManager.Instance.ResumeTimer();

        TurnManager.Instance.SpawnNewBall(TurnManager.Instance.currentPlayer);
    }
}
