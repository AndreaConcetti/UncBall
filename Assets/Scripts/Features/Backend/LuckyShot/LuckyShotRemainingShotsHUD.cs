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

    private int currentRemainingShots = 3;

    private void Awake()
    {
        ResolveReferences();
        ApplyTitle();
        ApplyRemainingShots(maxShots);
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        RefreshFromCurrentSession();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (sessionRuntime == null)
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
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionLoaded += HandleSessionLoaded;

        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionPreviewChanged += HandleSessionPreviewChanged;

        sessionRuntime.SessionResolved -= HandleSessionResolved;
        sessionRuntime.SessionResolved += HandleSessionResolved;
    }

    private void Unsubscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionResolved -= HandleSessionResolved;
    }

    private void RefreshFromCurrentSession()
    {
        if (sessionRuntime == null)
        {
            ApplyRemainingShots(maxShots);
            return;
        }

        LuckyShotActiveSession session = sessionRuntime.CurrentSession;

        if (!session.IsValid() || !session.hasActiveSession)
        {
            ApplyRemainingShots(maxShots);
            return;
        }

        ApplyRemainingShots(session.remainingShots);
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        if (!session.IsValid() || !session.hasActiveSession)
        {
            ApplyRemainingShots(maxShots);
            return;
        }

        ApplyRemainingShots(session.remainingShots);
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (!preview.hasActiveSession)
        {
            ApplyRemainingShots(maxShots);
            return;
        }

        ApplyRemainingShots(preview.remainingShots);
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        if (!result.success)
            return;

        ApplyRemainingShots(result.remainingShotsAfterResolve);
    }

    private void ApplyTitle()
    {
        if (titleText != null)
            titleText.text = titleLabel;
    }

    private void ApplyRemainingShots(int remainingShots)
    {
        currentRemainingShots = Mathf.Clamp(remainingShots, 0, maxShots);

        if (remainingShotsText != null)
            remainingShotsText.text = currentRemainingShots + "/" + maxShots;

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotRemainingShotsHUD] ApplyRemainingShots -> " +
                currentRemainingShots + "/" + maxShots,
                this);
        }
    }
}