using System.Collections;
using Fusion;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class FusionGameplayBootstrap : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private MatchRuntimeConfig matchRuntimeConfig;
    [SerializeField] private PhotonFusionRunnerManager runnerManager;
    [SerializeField] private OnlineGameplayAuthority onlineGameplayAuthority;
    [SerializeField] private OnlineMatchSession onlineMatchSession;

    [Header("Legacy Components To Disable In Online")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private StartEndController startEndController;

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

        DisableLegacyAuthoritativeFlow();

        if (!bootstrapStarted)
        {
            bootstrapStarted = true;
            StartCoroutine(BootstrapOnlineRoutine());
        }
    }

    private void ResolveDependencies()
    {
        if (matchRuntimeConfig == null)
            matchRuntimeConfig = MatchRuntimeConfig.Instance;

        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;

        if (onlineMatchSession == null)
            onlineMatchSession = OnlineMatchSession.Instance;

        if (turnManager == null)
            turnManager = TurnManager.Instance;

        if (startEndController == null)
            startEndController = StartEndController.InstanceOrFind();

#if UNITY_2023_1_OR_NEWER
        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindFirstObjectByType<MatchRuntimeConfig>();

        if (runnerManager == null)
            runnerManager = FindFirstObjectByType<PhotonFusionRunnerManager>();

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = FindFirstObjectByType<OnlineGameplayAuthority>();

        if (onlineMatchSession == null)
            onlineMatchSession = FindFirstObjectByType<OnlineMatchSession>();

        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        if (startEndController == null)
            startEndController = FindFirstObjectByType<StartEndController>();
#else
        if (matchRuntimeConfig == null)
            matchRuntimeConfig = FindObjectOfType<MatchRuntimeConfig>();

        if (runnerManager == null)
            runnerManager = FindObjectOfType<PhotonFusionRunnerManager>();

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = FindObjectOfType<OnlineGameplayAuthority>();

        if (onlineMatchSession == null)
            onlineMatchSession = FindObjectOfType<OnlineMatchSession>();

        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        if (startEndController == null)
            startEndController = FindObjectOfType<StartEndController>();
#endif
    }

    private bool ShouldRunOnlineBootstrap()
    {
        if (runnerManager != null && runnerManager.IsRunning)
            return true;

        if (matchRuntimeConfig != null && matchRuntimeConfig.IsOnlineMatch)
            return true;

        if (onlineMatchSession != null && onlineMatchSession.HasPreparedSession)
            return true;

        return false;
    }

    private void DisableLegacyAuthoritativeFlow()
    {
        if (turnManager != null)
            turnManager.enabled = false;

        if (startEndController != null)
            startEndController.enabled = false;
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

        if (onlineMatchSession != null && onlineMatchSession.HasPreparedSession)
        {
            p1Name = onlineMatchSession.CurrentSession.hostDisplayName;
            p2Name = onlineMatchSession.CurrentSession.joinDisplayName;
        }
        else if (matchRuntimeConfig != null)
        {
            p1Name = matchRuntimeConfig.SelectedPlayer1Name;
            p2Name = matchRuntimeConfig.SelectedPlayer2Name;
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
        {
            yield return StartCoroutine(EnsureOnlineControllerSpawned(runner));
        }

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