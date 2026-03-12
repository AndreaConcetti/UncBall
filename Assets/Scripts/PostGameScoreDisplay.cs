using UnityEngine;
using TMPro;

public class PostGameScoreDisplay : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text player1FinalScoreText;
    public TMP_Text player2FinalScoreText;

    [Header("Optional Score Source")]
    public ScoreManagerNew scoreManager;

    private void Awake()
    {
        UpdateScores();
    }

    public void UpdateScores()
    {
        if (scoreManager == null)
        {
            scoreManager = ScoreManagerNew.Instance;
        }

        if (scoreManager == null)
        {
            Debug.LogWarning("PostGameScoreDisplay: ScoreManagerNew non trovato.");
            return;
        }

        player1FinalScoreText.text = scoreManager.ScoreP1.ToString();
        player2FinalScoreText.text = scoreManager.ScoreP2.ToString();
    }

    public void SetScores(int player1Score, int player2Score)
    {
        player1FinalScoreText.text = player1Score.ToString();
        player2FinalScoreText.text = player2Score.ToString();
    }
}