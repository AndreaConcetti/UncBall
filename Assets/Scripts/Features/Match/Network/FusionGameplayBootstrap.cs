using System.Collections;
using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class FusionGameplayBootstrap : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PhotonFusionRunnerManager runnerManager;
    [SerializeField] private OnlineGameplayAuthority onlineGameplayAuthority;
    [SerializeField] private OnlineFlowController onlineFlowController;

    [Header("Online Controller Prefab")]
    [SerializeField] private FusionOnlineMatchController onlineMatchControllerPrefab;

    [Header("Runtime")]
    [SerializeField] private bool bootstrapStarted = false;
    [SerializeField] private bool bootstrapCompleted = false;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private void Awake()
    {
        ResolveDependencies();

        if (!ShouldRunOnlineBootstrap())
            return;

        if (onlineGameplayAuthority != null)
            onlineGameplayAuthority.ForceOfflineDisabledState();

        if (!bootstrapStarted)
        {
            bootstrapStarted = true;
            StartCoroutine(BootstrapOnlineRoutine());
        }
    }

    private void ResolveDependencies()
    {
        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;

        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;
    }

    private bool ShouldRunOnlineBootstrap()
    {
        if (runnerManager != null && runnerManager.IsRunning)
            return true;

        if (onlineFlowController != null &&
            onlineFlowController.RuntimeContext != null &&
            onlineFlowController.RuntimeContext.currentSession != null)
        {
            return true;
        }

        return false;
    }

    private IEnumerator BootstrapOnlineRoutine()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while ((runnerManager == null || !runnerManager.IsRunning || runnerManager.ActiveRunner == null) && elapsed < timeout)
        {
            ResolveDependencies();
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (runnerManager == null || !runnerManager.IsRunning || runnerManager.ActiveRunner == null)
        {
            Debug.LogError("[FusionGameplayBootstrap] Runner not ready in Gameplay.", this);
            yield break;
        }

        NetworkRunner runner = runnerManager.ActiveRunner;
        bool isServer = runner.IsServer;

        PlayerID localPlayer = isServer ? PlayerID.Player1 : PlayerID.Player2;
        PlayerID remotePlayer = isServer ? PlayerID.Player2 : PlayerID.Player1;

        string p1Name = "Player 1";
        string p2Name = "Player 2";

        if (onlineFlowController != null &&
            onlineFlowController.RuntimeContext != null &&
            onlineFlowController.RuntimeContext.currentSession != null)
        {
            MatchSessionContext session = onlineFlowController.RuntimeContext.currentSession;
            p1Name = string.IsNullOrWhiteSpace(session.player1DisplayName) ? "Player 1" : session.player1DisplayName.Trim();
            p2Name = string.IsNullOrWhiteSpace(session.player2DisplayName) ? "Player 2" : session.player2DisplayName.Trim();
        }

        string localName = localPlayer == PlayerID.Player1 ? p1Name : p2Name;
        string remoteName = localPlayer == PlayerID.Player1 ? p2Name : p1Name;

        if (onlineGameplayAuthority != null)
        {
            onlineGameplayAuthority.ConfigureOnline(
                localPlayer,
                remotePlayer,
                isServer,
                localName,
                remoteName,
                PlayerID.Player1
            );
        }

        if (isServer)
            yield return StartCoroutine(EnsureOnlineControllerSpawned(runner));

        if (onlineFlowController != null)
            onlineFlowController.NotifyMatchStarted();

        bootstrapCompleted = true;

        if (logDebug)
        {
            Debug.Log(
                "[FusionGameplayBootstrap] Online bootstrap completed. " +
                "IsServer=" + isServer +
                " | LocalPlayer=" + localPlayer +
                " | RemotePlayer=" + remotePlayer,
                this
            );
        }
    }

    private IEnumerator EnsureOnlineControllerSpawned(NetworkRunner runner)
    {
        float searchTimeout = 2f;
        float searchElapsed = 0f;

        while (FindExistingOnlineController() == null && searchElapsed < searchTimeout)
        {
            searchElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (FindExistingOnlineController() != null)
            yield break;

        if (onlineMatchControllerPrefab == null)
        {
            Debug.LogError("[FusionGameplayBootstrap] OnlineMatchController prefab missing.", this);
            yield break;
        }

        NetworkObject prefabNetObj = onlineMatchControllerPrefab.GetComponent<NetworkObject>();
        if (prefabNetObj == null)
        {
            Debug.LogError("[FusionGameplayBootstrap] OnlineMatchController prefab needs NetworkObject.", this);
            yield break;
        }

        runner.Spawn(prefabNetObj, Vector3.zero, Quaternion.identity);

        if (logDebug)
            Debug.Log("[FusionGameplayBootstrap] Spawned FusionOnlineMatchController.", this);
    }

    private FusionOnlineMatchController FindExistingOnlineController()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<FusionOnlineMatchController>();
#else
        return FindObjectOfType<FusionOnlineMatchController>();
#endif
    }
}