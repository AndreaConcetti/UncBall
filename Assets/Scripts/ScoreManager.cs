using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public GameObject ballPrefab;

    public Transform launchZone1;
    public Transform launchZone2;

    public int player1Score;
    public int player2Score;

    void Awake()
    {
        Instance = this;
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

        SpawnNewBall();
    }

    void SpawnNewBall()
    {
        Transform spawnPoint;

        if (TurnManager.Instance.IsPlayer1Turn)
            spawnPoint = launchZone1;
        else
            spawnPoint = launchZone2;

        Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
    }
}
