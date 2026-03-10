using UnityEngine;

public class MatchEndController : MonoBehaviour
{
    [Header("References")]
    public ScoreManager scoreManager;

    [Header("UI Panels")]
    public GameObject gameUIPanel;
    public GameObject postGamePanel;

    [Header("Win Settings")]
    public int targetScore = 16;

    [Header("Options")]
    public bool stopTimeOnEnd = true;

    private bool matchEnded = false;

    void Start()
    {
        if (postGamePanel != null)
            postGamePanel.SetActive(false);
    }

    void Update()
    {
        if (matchEnded || scoreManager == null)
            return;

        if (scoreManager.player1Score >= targetScore || scoreManager.player2Score >= targetScore)
        {
            EndMatch();
        }
    }

    void EndMatch()
    {
        matchEnded = true;

        if (gameUIPanel != null)
            gameUIPanel.SetActive(false);

        if (postGamePanel != null)
            postGamePanel.SetActive(true);

        if (stopTimeOnEnd)
          Time.timeScale = 0f;
    }

    public void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }
}