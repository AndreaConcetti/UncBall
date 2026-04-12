using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class OnlineFinishedMatchSessionRetirer : MonoBehaviour
{
    private const string MatchStatePropertyKey = "match_state";
    private const int MatchStateEnded = 1;

    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController matchController;
    [SerializeField] private PhotonFusionRunnerManager runnerManager;

    [Header("Behavior")]
    [SerializeField] private bool retireSessionAsSoonAsMatchEnds = true;
    [SerializeField] private bool onlyServerMayRetireSession = true;
    [SerializeField] private bool logDebug = true;

    private bool retired;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void Update()
    {
        if (!retireSessionAsSoonAsMatchEnds)
            return;

        if (retired)
            return;

        ResolveDependencies();

        if (matchController == null || !matchController.MatchEnded)
            return;

        if (runnerManager == null || !runnerManager.IsRunning)
            return;

        if (onlyServerMayRetireSession && !runnerManager.IsCurrentRunnerServer())
            return;

        Dictionary<string, SessionProperty> props = new Dictionary<string, SessionProperty>
        {
            { MatchStatePropertyKey, MatchStateEnded }
        };

        bool ok = runnerManager.TryUpdateSessionProperties(props);
        retired = ok;

        if (logDebug)
        {
            Debug.LogWarning(
                "[OnlineFinishedMatchSessionRetirer] Retire finished session -> " +
                "Ok=" + ok +
                " | Session=" + runnerManager.GetCurrentSessionName(),
                this);
        }
    }

    private void ResolveDependencies()
    {
        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (matchController == null)
            matchController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
        if (matchController == null)
            matchController = FindObjectOfType<FusionOnlineMatchController>();
#endif
    }
}
