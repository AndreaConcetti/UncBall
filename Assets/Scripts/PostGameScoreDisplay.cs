using UnityEngine;
using TMPro;

public class PostGameScoreDisplay : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text player1FinalScoreText;
    public TMP_Text player2FinalScoreText;

    [Header("Optional Score Source")]
    public ScoreManager scoreManager;

    private void Awake()
    {
        UpdateScores();
    }

    public void UpdateScores()
    {
        if (scoreManager == null)
        {
            Debug.LogWarning("PostGameScoreDisplay: ScoreManager non assegnato.");
            return;
        }

        player1FinalScoreText.text = scoreManager.player1Score.ToString();
        player2FinalScoreText.text = scoreManager.player2Score.ToString();
    }

    public void SetScores(int player1Score, int player2Score)
    {
        player1FinalScoreText.text = player1Score.ToString();
        player2FinalScoreText.text = player2Score.ToString();
    }
}