using UnityEngine;
using TMPro;

/// <summary>
/// Gestisce lo stato generale del match:
/// - avvio partita
/// - pausa / resume
/// - fine partita
/// - modalità a punti oppure a tempo
/// - timer globale del match
///
/// IMPORTANTE:
/// Questo script NON gestisce il timer del singolo turno.
/// Il timer del turno resta nel TurnManager.
/// Qui gestiamo solo il timer "lungo" della partita.
/// </summary>
public class StartEndController : MonoBehaviour
{
    public enum MatchMode
    {
        ScoreTarget,
        TimeLimit
    }

    [Header("References")]
    [Tooltip("Score manager principale del match")]
    public ScoreManagerNew scoreManager;

    [Tooltip("Script che aggiorna il punteggio nel pannello post game")]
    public PostGameScoreDisplay postGameScoreDisplay;

    [Header("UI Panels")]
    [Tooltip("UI principale di gioco")]
    public GameObject gameUIPanel;

    [Tooltip("Pannello pausa")]
    public GameObject pausePanel;

    [Tooltip("Pannello fine partita")]
    public GameObject postGamePanel;

    [Header("Match Mode")]
    [Tooltip("Modalità di partita: a punteggio oppure a tempo")]
    public MatchMode matchMode = MatchMode.ScoreTarget;

    [Header("Score Target Mode")]
    [Tooltip("Punteggio necessario per terminare la partita in modalità ScoreTarget")]
    public int targetScore = 16;

    [Header("Time Limit Mode")]
    [Tooltip("Durata totale della partita in secondi. 180 = 3 minuti")]
    public float matchDuration = 180f;

    [Tooltip("Testo UI opzionale per mostrare il timer globale della partita")]
    public TMP_Text matchTimerText;

    [Tooltip("Formato mm:ss per il timer match. Se disattivo mostra solo i secondi interi")]
    public bool useMinutesSecondsFormat = true;

    [Header("Start Options")]
    [Tooltip("Se attivo, il match parte automaticamente allo Start della scena")]
    public bool startMatchOnStart = true;

    [Header("Time Options")]
    [Tooltip("Se attivo, in pausa Time.timeScale va a 0")]
    public bool stopTimeOnPause = true;

    [Tooltip("Se attivo, a fine partita Time.timeScale va a 0")]
    public bool stopTimeOnEnd = true;

    // Timer runtime del match globale
    private float currentMatchTimer;

    // Stato runtime del match
    private bool matchStarted = false;
    private bool matchEnded = false;
    private bool isPaused = false;

    /// <summary>
    /// Tempo match rimanente leggibile da altri script.
    /// </summary>
    public float CurrentMatchTimer => currentMatchTimer;

    void Start()
    {
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (postGamePanel != null)
            postGamePanel.SetActive(false);

        if (gameUIPanel != null)
            gameUIPanel.SetActive(true);

        matchStarted = false;
        matchEnded = false;
        isPaused = false;

        // Inizializza il timer globale del match
        currentMatchTimer = matchDuration;
        UpdateMatchTimerUI();

        if (startMatchOnStart)
            StartMatch();
    }

    void Update()
    {
        if (!matchStarted || matchEnded || scoreManager == null)
            return;

        // Modalità a tempo:
        // il timer globale del match scende continuamente fino a 0
        if (matchMode == MatchMode.TimeLimit)
        {
            currentMatchTimer -= Time.deltaTime;

            if (currentMatchTimer < 0f)
                currentMatchTimer = 0f;

            UpdateMatchTimerUI();

            if (currentMatchTimer <= 0f)
            {
                EndMatch();
                return;
            }
        }

        // Modalità a punti:
        // la partita termina appena uno dei due raggiunge il targetScore
        if (matchMode == MatchMode.ScoreTarget)
        {
            if (scoreManager.ScoreP1 >= targetScore || scoreManager.ScoreP2 >= targetScore)
            {
                EndMatch();
                return;
            }
        }
    }

    /// <summary>
    /// Avvia un nuovo match.
    /// Questo:
    /// - resetta timer globale match
    /// - resetta stato pausa/fine partita
    /// - riapre la game UI
    /// - chiama StartMatch() sullo ScoreManagerNew
    /// </summary>
    public void StartMatch()
    {
        if (scoreManager == null)
            scoreManager = ScoreManagerNew.Instance;

        if (scoreManager == null)
        {
            Debug.LogError("[StartEndController] ScoreManagerNew non trovato.", this);
            return;
        }

        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (postGamePanel != null)
            postGamePanel.SetActive(false);

        if (gameUIPanel != null)
            gameUIPanel.SetActive(true);

        isPaused = false;
        matchEnded = false;
        matchStarted = true;

        // Reset del timer globale partita
        currentMatchTimer = matchDuration;
        UpdateMatchTimerUI();

        // Avvia il match lato score manager
        scoreManager.StartMatch();
    }

    /// <summary>
    /// Mette in pausa il match.
    /// Se stopTimeOnPause è attivo, blocca il tempo globale di Unity.
    /// </summary>
    public void PauseMatch()
    {
        if (!matchStarted || matchEnded || isPaused)
            return;

        isPaused = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (stopTimeOnPause)
            Time.timeScale = 0f;
    }

    /// <summary>
    /// Riprende il match dalla pausa.
    /// </summary>
    public void ResumeMatch()
    {
        if (!matchStarted || matchEnded || !isPaused)
            return;

        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;
    }

    /// <summary>
    /// Alterna automaticamente tra pausa e resume.
    /// </summary>
    public void TogglePause()
    {
        if (isPaused)
            ResumeMatch();
        else
            PauseMatch();
    }

    /// <summary>
    /// Termina il match.
    /// Mostra il pannello post game, aggiorna i punteggi finali
    /// e opzionalmente blocca il tempo di gioco.
    /// </summary>
    public void EndMatch()
    {
        if (matchEnded)
            return;

        matchEnded = true;
        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (gameUIPanel != null)
            gameUIPanel.SetActive(false);

        if (postGamePanel != null)
            postGamePanel.SetActive(true);

        if (postGameScoreDisplay != null)
            postGameScoreDisplay.UpdateScores();

        if (stopTimeOnEnd)
            Time.timeScale = 0f;
    }

    /// <summary>
    /// Resetta solo lo stato UI e del tempo globale Unity.
    /// Non riavvia automaticamente il match.
    /// </summary>
    public void ResetUIState()
    {
        matchStarted = false;
        matchEnded = false;
        isPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (postGamePanel != null)
            postGamePanel.SetActive(false);

        if (gameUIPanel != null)
            gameUIPanel.SetActive(true);

        currentMatchTimer = matchDuration;
        UpdateMatchTimerUI();

        Time.timeScale = 1f;
    }

    /// <summary>
    /// Riporta il Time.timeScale a 1.
    /// Utile da collegare a bottoni UI.
    /// </summary>
    public void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Aggiorna il testo UI del timer globale partita.
    /// </summary>
    void UpdateMatchTimerUI()
    {
        if (matchTimerText == null)
            return;

        if (useMinutesSecondsFormat)
        {
            int totalSeconds = Mathf.CeilToInt(currentMatchTimer);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            matchTimerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }
        else
        {
            matchTimerText.text = Mathf.CeilToInt(currentMatchTimer).ToString();
        }
    }

    public bool IsMatchStarted()
    {
        return matchStarted;
    }

    public bool IsMatchEnded()
    {
        return matchEnded;
    }

    public bool IsPaused()
    {
        return isPaused;
    }
}