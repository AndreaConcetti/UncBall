using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionOnlineMatchConnectionWatcher : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PhotonFusionRunnerManager runnerManager;
    [SerializeField] private FusionOnlineMatchController matchController;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        Unsubscribe();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

        if (matchController == null)
        {
#if UNITY_2023_1_OR_NEWER
            matchController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
            matchController = FindObjectOfType<FusionOnlineMatchController>();
#endif
        }
    }

    private void Subscribe()
    {
        if (runnerManager == null)
            return;

        runnerManager.OnPlayerLeftEvent -= HandlePlayerLeft;
        runnerManager.OnDisconnectedFromServerEvent -= HandleDisconnectedFromServer;
        runnerManager.OnShutdownEvent -= HandleShutdown;
        runnerManager.OnSceneLoadDoneEvent -= HandleSceneLoadDone;

        runnerManager.OnPlayerLeftEvent += HandlePlayerLeft;
        runnerManager.OnDisconnectedFromServerEvent += HandleDisconnectedFromServer;
        runnerManager.OnShutdownEvent += HandleShutdown;
        runnerManager.OnSceneLoadDoneEvent += HandleSceneLoadDone;
    }

    private void Unsubscribe()
    {
        if (runnerManager == null)
            return;

        runnerManager.OnPlayerLeftEvent -= HandlePlayerLeft;
        runnerManager.OnDisconnectedFromServerEvent -= HandleDisconnectedFromServer;
        runnerManager.OnShutdownEvent -= HandleShutdown;
        runnerManager.OnSceneLoadDoneEvent -= HandleSceneLoadDone;
    }

    private void HandleSceneLoadDone()
    {
        ResolveReferences();
    }

    private void HandlePlayerLeft(PlayerRef player)
    {
        ResolveReferences();

        if (matchController == null)
            return;

        if (logDebug)
            Debug.Log("[FusionOnlineMatchConnectionWatcher] HandlePlayerLeft -> " + player, this);

        matchController.NotifyRunnerPlayerLeft(player);
    }

    private void HandleDisconnectedFromServer()
    {
        ResolveReferences();

        if (matchController == null)
            return;

        if (logDebug)
            Debug.LogWarning("[FusionOnlineMatchConnectionWatcher] HandleDisconnectedFromServer", this);

        matchController.NotifyLocalDisconnectedFromSession();
    }

    private void HandleShutdown(ShutdownReason reason)
    {
        ResolveReferences();

        if (matchController == null)
            return;

        if (logDebug)
            Debug.LogWarning("[FusionOnlineMatchConnectionWatcher] HandleShutdown -> " + reason, this);

        matchController.NotifyRunnerShutdown(reason);
    }
}