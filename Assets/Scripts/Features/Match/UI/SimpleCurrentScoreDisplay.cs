using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SimpleCurrentScoreDisplay : MonoBehaviour
{
    [Header("UI References")]
    public List<TMP_Text> player1ScoreTexts = new List<TMP_Text>();
    public List<TMP_Text> player2ScoreTexts = new List<TMP_Text>();

    [Header("Optional Score Source")]
    public ScoreManager scoreManager;

    void OnEnable()
    {
        RefreshScores();
    }

    public void RefreshScores()
    {
        if (scoreManager == null)
            scoreManager = ScoreManager.Instance;

        if (scoreManager == null)
        {
            Debug.LogWarning("SimpleCurrentScoreDisplay: ScoreManager non trovato.");
            return;
        }

        UpdateTexts(player1ScoreTexts, scoreManager.ScoreP1);
        UpdateTexts(player2ScoreTexts, scoreManager.ScoreP2);
    }

    public void SetScores(int player1Score, int player2Score)
    {
        UpdateTexts(player1ScoreTexts, player1Score);
        UpdateTexts(player2ScoreTexts, player2Score);
    }

    void UpdateTexts(List<TMP_Text> texts, int value)
    {
        string valueString = value.ToString();

        for (int i = 0; i < texts.Count; i++)
        {
            if (texts[i] != null)
                texts[i].text = valueString;
        }
    }
}