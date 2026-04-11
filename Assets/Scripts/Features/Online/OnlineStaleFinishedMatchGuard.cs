using UnityEngine;

public class OnlineStaleFinishedMatchGuard : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController matchController;
    [SerializeField] private OnlineFlowController onlineFlowController;
    [SerializeField] private OnlineMatchPresentationResultStore presentationResultStore;

    [Header("Policy")]
    [SerializeField] private bool autoReturnToMenuIfSceneStartsInsideAlreadyProcessedFinishedMatch = true;
    [SerializeField] private bool logDebug = false;

    private bool handled;
    private string sceneStartMatchId = string.Empty;
    private bool sceneStartedWithProcessedFinishedMatch;

    private void Awake()
    {
        ResolveDependencies();

        sceneStartMatchId = ResolveCurrentMatchId();
        sceneStartedWithProcessedFinishedMatch =
            autoReturnToMenuIfSceneStartsInsideAlreadyProcessedFinishedMatch &&
            matchController != null &&
            matchController.MatchEnded &&
            presentationResultStore != null &&
            !string.IsNullOrWhiteSpace(sceneStartMatchId) &&
            presentationResultStore.HasProcessedMatch(sceneStartMatchId);

        if (logDebug)
        {
            Debug.Log(
                "[OnlineStaleFinishedMatchGuard] Awake -> " +
                "SceneStartMatchId=" + sceneStartMatchId +
                " | MatchEndedAtAwake=" + (matchController != null && matchController.MatchEnded) +
                " | ProcessedAtAwake=" + sceneStartedWithProcessedFinishedMatch,
                this
            );
        }
    }

    private void Update()
    {
        if (handled)
            return;

        if (!sceneStartedWithProcessedFinishedMatch)
            return;

        ResolveDependencies();

        if (matchController == null || !matchController.MatchEnded)
            return;

        string currentMatchId = ResolveCurrentMatchId();
        if (string.IsNullOrWhiteSpace(currentMatchId))
            return;

        if (!string.Equals(currentMatchId, sceneStartMatchId, System.StringComparison.Ordinal))
            return;

        handled = true;

        if (logDebug)
            Debug.LogWarning(
                "[OnlineStaleFinishedMatchGuard] Stale finished match detected from scene start. Returning to menu. MatchId=" + currentMatchId,
                this
            );

        if (onlineFlowController != null)
            onlineFlowController.ReturnToMenuFromMatch(true);
    }

    private string ResolveCurrentMatchId()
    {
        if (onlineFlowController == null ||
            onlineFlowController.RuntimeContext == null ||
            onlineFlowController.RuntimeContext.currentSession == null)
        {
            return string.Empty;
        }

        return onlineFlowController.RuntimeContext.currentSession.matchId ?? string.Empty;
    }

    private void ResolveDependencies()
    {
        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;

        if (presentationResultStore == null)
            presentationResultStore = OnlineMatchPresentationResultStore.Instance;

#if UNITY_2023_1_OR_NEWER
        if (matchController == null)
            matchController = FindFirstObjectByType<FusionOnlineMatchController>();

        if (presentationResultStore == null)
            presentationResultStore = FindFirstObjectByType<OnlineMatchPresentationResultStore>();
#else
        if (matchController == null)
            matchController = FindObjectOfType<FusionOnlineMatchController>();

        if (presentationResultStore == null)
            presentationResultStore = FindObjectOfType<OnlineMatchPresentationResultStore>();
#endif
    }
}
