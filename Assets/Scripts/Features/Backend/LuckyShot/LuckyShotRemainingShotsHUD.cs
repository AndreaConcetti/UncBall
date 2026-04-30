using TMPro;
using UnityEngine;

public sealed class LuckyShotRemainingShotsHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LuckyShotSessionRuntime sessionRuntime;

    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text remainingShotsText;

    [Header("Settings")]
    [SerializeField] private string titleLabel = "REMAINING SHOTS";
    [SerializeField] private int maxShots = 3;
    [SerializeField] private bool verboseLogs;

    private int displayedRemainingShots;
    private bool subscribed;

    private void Awake()
    {
        ResolveReferences();
        ApplyTitle();
        ApplyRemainingShots(maxShots, "Awake");
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        RefreshFromLoadedSessionOnly();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (sessionRuntime != null)
            return;

        sessionRuntime = LuckyShotSessionRuntime.Instance;

#if UNITY_2023_1_OR_NEWER
        if (sessionRuntime == null)
            sessionRuntime = FindFirstObjectByType<LuckyShotSessionRuntime>();
#else
        if (sessionRuntime == null)
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();
#endif
    }

    private void Subscribe()
    {
        if (sessionRuntime == null || subscribed)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionLoaded += HandleSessionLoaded;

        sessionRuntime.SessionResolved -= HandleSessionResolved;
        sessionRuntime.SessionResolved += HandleSessionResolved;

        /*
         * IMPORTANTISSIMO:
         * Non ascoltiamo SessionPreviewChanged per aggiornare il testo.
         *
         * SessionPreviewChanged viene emesso anche quando parte il tiro,
         * perché MarkShotConsumedAsync scala subito RemainingShots nel backend.
         *
         * Il comportamento richiesto invece è:
         * - il valore resta invariato mentre la ball è in movimento;
         * - il valore scala solo quando il tiro si conclude.
         */

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (sessionRuntime == null || !subscribed)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionResolved -= HandleSessionResolved;

        subscribed = false;
    }

    private void RefreshFromLoadedSessionOnly()
    {
        if (sessionRuntime == null)
        {
            ApplyRemainingShots(maxShots, "NoRuntime");
            return;
        }

        LuckyShotActiveSession session = sessionRuntime.CurrentSession;

        if (!session.IsValid() || !session.hasActiveSession)
        {
            ApplyRemainingShots(maxShots, "NoActiveSession");
            return;
        }

        ApplyRemainingShots(session.remainingShots, "RefreshFromLoadedSessionOnly");
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        if (!session.IsValid() || !session.hasActiveSession)
        {
            ApplyRemainingShots(maxShots, "SessionLoaded_NoActiveSession");
            return;
        }

        ApplyRemainingShots(session.remainingShots, "SessionLoaded");
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        if (!result.success)
            return;

        /*
         * Questo evento arriva quando il tiro è stato risolto da uno slot.
         * Quindi qui il numero può aggiornarsi.
         */
        ApplyRemainingShots(result.remainingShotsAfterResolve, "SessionResolved");
    }

    private void ApplyTitle()
    {
        if (titleText != null)
            titleText.text = titleLabel;
    }

    private void ApplyRemainingShots(int remainingShots, string reason)
    {
        displayedRemainingShots = Mathf.Clamp(remainingShots, 0, maxShots);

        if (remainingShotsText != null)
            remainingShotsText.text = displayedRemainingShots + "/" + maxShots;

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotRemainingShotsHUD] ApplyRemainingShots -> " +
                displayedRemainingShots + "/" + maxShots +
                " | Reason=" + reason,
                this);
        }
    }
}