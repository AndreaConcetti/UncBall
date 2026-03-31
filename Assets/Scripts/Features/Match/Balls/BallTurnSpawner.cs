using Fusion;
using UnityEngine;

public class BallTurnSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BallLauncher launcher;
    [SerializeField] private PhotonFusionRunnerManager runnerManager;
    [SerializeField] private OnlineGameplayAuthority onlineGameplayAuthority;

    [Header("Ball Prefabs")]
    [SerializeField] private GameObject player1BallPrefab;
    [SerializeField] private GameObject player2BallPrefab;
    [SerializeField] private GameObject fallbackBallPrefab;

    [Header("Physical Side Spawn Points")]
    [SerializeField] private Transform leftSpawnPoint;
    [SerializeField] private Transform rightSpawnPoint;

    [Header("Physical Side Placement Areas")]
    [SerializeField] private BoxCollider leftPlacementArea;
    [SerializeField] private BoxCollider rightPlacementArea;

    [Header("Runtime Sides")]
    [SerializeField] private bool player1IsOnLeft = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BallPhysics lastBoundLocalBall;
    private PlayerID lastBoundLocalOwner = PlayerID.None;

    public BallLauncher Launcher => launcher;
    public bool Player1IsOnLeft => player1IsOnLeft;
    public bool Player2IsOnLeft => !player1IsOnLeft;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void ResolveDependencies()
    {
        if (runnerManager == null)
            runnerManager = PhotonFusionRunnerManager.Instance;

        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;

#if UNITY_2023_1_OR_NEWER
        if (launcher == null)
            launcher = FindFirstObjectByType<BallLauncher>();
#else
        if (launcher == null)
            launcher = FindObjectOfType<BallLauncher>();
#endif
    }

    public void SetPlayer1Side(bool shouldPlayer1BeOnLeft)
    {
        if (player1IsOnLeft == shouldPlayer1BeOnLeft)
            return;

        player1IsOnLeft = shouldPlayer1BeOnLeft;

        if (logDebug)
        {
            Debug.Log(
                "[BallTurnSpawner] SetPlayer1Side -> Player1IsOnLeft=" + player1IsOnLeft,
                this
            );
        }
    }

    public void SwapPlayerSides()
    {
        player1IsOnLeft = !player1IsOnLeft;

        if (logDebug)
        {
            Debug.Log(
                "[BallTurnSpawner] SwapPlayerSides -> Player1IsOnLeft=" + player1IsOnLeft,
                this
            );
        }
    }

    public BallPhysics SpawnOnlineBallForOwner(PlayerID ownerId, string overrideSkinUniqueId = "")
    {
        ResolveDependencies();

        if (runnerManager == null || !runnerManager.IsRunning || runnerManager.ActiveRunner == null)
        {
            Debug.LogError("[BallTurnSpawner] Runner not ready for online spawn.", this);
            return null;
        }

        Transform spawnPoint = GetSpawnPointForOwner(ownerId);
        GameObject prefabToSpawn = GetPrefabForOwner(ownerId);

        if (spawnPoint == null || prefabToSpawn == null)
        {
            Debug.LogError("[BallTurnSpawner] Missing spawn point or prefab for online spawn.", this);
            return null;
        }

        string skinUniqueId = string.IsNullOrWhiteSpace(overrideSkinUniqueId)
            ? string.Empty
            : overrideSkinUniqueId.Trim();

        NetworkObject prefabNetObj = prefabToSpawn.GetComponent<NetworkObject>();
        if (prefabNetObj == null)
        {
            Debug.LogError("[BallTurnSpawner] Ball prefab missing NetworkObject.", this);
            return null;
        }

        NetworkObject spawnedObject = runnerManager.ActiveRunner.Spawn(
            prefabNetObj,
            spawnPoint.position,
            spawnPoint.rotation,
            inputAuthority: null,
            onBeforeSpawned: (runner, obj) =>
            {
                BallOwnership ownership = obj.GetComponent<BallOwnership>();
                if (ownership != null)
                    ownership.Owner = ownerId;

                FusionNetworkBall fusionBall = obj.GetComponent<FusionNetworkBall>();
                if (fusionBall != null)
                    fusionBall.SetOwnerAndSkin(ownerId, skinUniqueId);
            }
        );

        if (spawnedObject == null)
        {
            Debug.LogError("[BallTurnSpawner] Runner.Spawn returned null.", this);
            return null;
        }

        BallPhysics physics = spawnedObject.GetComponent<BallPhysics>();
        if (physics == null)
        {
            Debug.LogError("[BallTurnSpawner] Spawned object missing BallPhysics.", this);
            return null;
        }

        Rigidbody rb = spawnedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.isKinematic = true;
        }

        if (logDebug)
        {
            Debug.Log(
                "[BallTurnSpawner] SpawnOnlineBallForOwner -> " +
                "Owner=" + ownerId +
                " | SpawnSide=" + (IsPlayerIdOnLeft(ownerId) ? "LEFT" : "RIGHT") +
                " | SkinId=" + skinUniqueId,
                this
            );
        }

        return physics;
    }

    public void RegisterNetworkSpawnedBall(FusionNetworkBall networkBall)
    {
        if (networkBall == null)
            return;

        TryBindCurrentOnlineBallForLocalControl(networkBall.BallPhysics, networkBall.OwnerPlayerId);
    }

    public void TryBindCurrentOnlineBallForLocalControl(BallPhysics currentBall, PlayerID owner)
    {
        ResolveDependencies();

        if (launcher == null || currentBall == null)
            return;

        PlayerID localPlayer = ResolveEffectiveLocalPlayerId();
        if (owner != localPlayer)
            return;

        if (lastBoundLocalBall == currentBall && lastBoundLocalOwner == owner)
            return;

        launcher.ball = currentBall;
        launcher.SetActivePlacementArea(GetPlacementAreaForOwner(owner));
        launcher.ResetLaunch();

        lastBoundLocalBall = currentBall;
        lastBoundLocalOwner = owner;

        if (logDebug)
        {
            Debug.Log(
                "[BallTurnSpawner] Bound local online ball -> " +
                "Local=" + localPlayer +
                " | Owner=" + owner +
                " | Side=" + (IsPlayerIdOnLeft(owner) ? "LEFT" : "RIGHT"),
                this
            );
        }
    }

    public void ClearOnlineLauncherBindingIfNeeded(PlayerID currentTurnOwner)
    {
        ResolveDependencies();

        if (launcher == null)
            return;

        PlayerID localPlayer = ResolveEffectiveLocalPlayerId();
        if (currentTurnOwner == localPlayer)
            return;

        launcher.ball = null;
        launcher.SetActivePlacementArea(null);

        lastBoundLocalBall = null;
        lastBoundLocalOwner = PlayerID.None;
    }

    public void ClearAllBallsInScene()
    {
#if UNITY_2023_1_OR_NEWER
        BallPhysics[] balls = FindObjectsByType<BallPhysics>(FindObjectsSortMode.None);
#else
        BallPhysics[] balls = FindObjectsOfType<BallPhysics>();
#endif

        bool canDespawnNetworkObjects =
            runnerManager != null &&
            runnerManager.IsRunning &&
            runnerManager.ActiveRunner != null &&
            runnerManager.ActiveRunner.IsServer;

        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i] == null)
                continue;

            NetworkObject netObj = balls[i].GetComponent<NetworkObject>();

            if (netObj != null && canDespawnNetworkObjects)
                runnerManager.ActiveRunner.Despawn(netObj);
            else
                Destroy(balls[i].gameObject);
        }

        if (launcher != null)
        {
            launcher.ball = null;
            launcher.SetActivePlacementArea(null);
        }

        lastBoundLocalBall = null;
        lastBoundLocalOwner = PlayerID.None;
    }

    public bool IsPlayerIdOnLeft(PlayerID playerId)
    {
        if (playerId == PlayerID.Player1)
            return player1IsOnLeft;

        if (playerId == PlayerID.Player2)
            return !player1IsOnLeft;

        return true;
    }

    public bool IsPlayerIdOnRight(PlayerID playerId)
    {
        return !IsPlayerIdOnLeft(playerId);
    }

    public Transform GetSpawnPointForOwner(PlayerID owner)
    {
        bool ownerIsOnLeft = IsPlayerIdOnLeft(owner);
        return ownerIsOnLeft ? leftSpawnPoint : rightSpawnPoint;
    }

    public BoxCollider GetPlacementAreaForOwner(PlayerID owner)
    {
        bool ownerIsOnLeft = IsPlayerIdOnLeft(owner);
        return ownerIsOnLeft ? leftPlacementArea : rightPlacementArea;
    }

    private GameObject GetPrefabForOwner(PlayerID owner)
    {
        if (owner == PlayerID.Player1)
            return player1BallPrefab != null ? player1BallPrefab : fallbackBallPrefab;

        if (owner == PlayerID.Player2)
            return player2BallPrefab != null ? player2BallPrefab : fallbackBallPrefab;

        return fallbackBallPrefab;
    }

    private PlayerID ResolveEffectiveLocalPlayerId()
    {
        ResolveDependencies();

        if (onlineGameplayAuthority != null)
            return onlineGameplayAuthority.LocalPlayerId;

        if (runnerManager != null && runnerManager.IsRunning && runnerManager.ActiveRunner != null)
            return runnerManager.ActiveRunner.IsServer ? PlayerID.Player1 : PlayerID.Player2;

        return PlayerID.Player1;
    }
}