using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    private int player1Score = 0;
    private int player2Score = 0;

    private void Awake()
    {
        Instance = this;
    }

    public void AddScore(PlayerController player, int amount)
    {
        if (player == TurnManager.Instance.player1)
        {
            player1Score += amount;
            Debug.Log("Player 1 Score: " + player1Score);
        }
        else if (player == TurnManager.Instance.player2)
        {
            player2Score += amount;
            Debug.Log("Player 2 Score: " + player2Score);
        }
    }
}
