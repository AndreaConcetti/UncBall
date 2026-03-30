using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using UnityEngine;

public class BallTurnSpawner : MonoBehaviour
{
    [Header("References")]
    public BallLauncher launcher;
    public PhotonFusionRunnerManager runnerManager;
    public OnlineGameplayAuthority onlineGameplayAuthority;

    [Header("Ball Prefabs")]
    public GameObject player1BallPrefab;
    public GameObject player2BallPrefab;
    public GameObject fallbackBallPrefab;

    [Header("Spawn Points")]
    public Transform player1SpawnPoint;
    public Transform player2SpawnPoint;

    [Header("Placement Areas")]
    public BoxCollider player1PlacementArea;
    public BoxCollider player2PlacementArea;

    [Header("Skin System")]
    public bool applyEquippedSkins = true;

    [Header("Runtime Sides")]
    [SerializeField] private bool player1IsOnLeft = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private BallPhysics lastBoundOnlineBall;
    private PlayerID lastBoundOnlineOwner = PlayerID.None;

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
    }

    private PlayerID ResolveEffectiveLocalPlayerId()
    {
        ResolveDependencies();

        if (onlineGameplayAuthority != null && onlineGameplayAuthority.OnlineMatchController != null)
            return onlineGameplayAuthority.OnlineMatchController.EffectiveLocalPlayerId;

        if (runnerManager != null && runnerManager.IsRunning && runnerManager.ActiveRunner != null)
            return runnerManager.ActiveRunner.IsServer ? PlayerID.Player1 : PlayerID.Player2;

        if (onlineGameplayAuthority != null)
            return onlineGameplayAuthority.LocalPlayerId;

        return PlayerID.Player1;
    }

    public BallPhysics SpawnOnlineBallForOwner(PlayerID ownerId)
    {
        ResolveDependencies();

        if (runnerManager == null || !runnerManager.IsRunning || runnerManager.ActiveRunner == null)
        {
            Debug.LogError("[BallTurnSpawner] Runner not ready for online spawn.", this);
            return null;
        }

        Transform spawnPoint = ownerId == PlayerID.Player1 ? player1SpawnPoint : player2SpawnPoint;
        GameObject prefabToSpawn = ownerId == PlayerID.Player1
            ? (player1BallPrefab != null ? player1BallPrefab : fallbackBallPrefab)
            : (player2BallPrefab != null ? player2BallPrefab : fallbackBallPrefab);

        if (spawnPoint == null || prefabToSpawn == null)
        {
            Debug.LogError("[BallTurnSpawner] Missing spawn point or prefab for online spawn.", this);
            return null;
        }

        string skinUniqueId = ResolveEquippedSkinUniqueIdForOwner(ownerId);

        NetworkObject networkPrefab = prefabToSpawn.GetComponent<NetworkObject>();
        if (networkPrefab == null)
        {
            Debug.LogError("[BallTurnSpawner] Ball prefab missing NetworkObject.", this);
            return null;
        }

        NetworkObject spawnedObject = runnerManager.ActiveRunner.Spawn(
            networkPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            inputAuthority: null,
            onBeforeSpawned: (runner, obj) =>
            {
                FusionNetworkBall fusionBall = obj.GetComponent<FusionNetworkBall>();
                if (fusionBall != null)
                    fusionBall.SetOwnerAndSkin(ownerId, skinUniqueId);

                BallOwnership ownership = obj.GetComponent<BallOwnership>();
                if (ownership != null)
                    ownership.Owner = ownerId;
            }
        );

        if (spawnedObject == null)
        {
            Debug.LogError("[BallTurnSpawner] Runner.Spawn returned null.", this);
            return null;
        }

        BallPhysics ballPhysics = spawnedObject.GetComponent<BallPhysics>();
        if (ballPhysics == null)
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
            Debug.Log("[BallTurnSpawner] SpawnOnlineBallForOwner -> " + ownerId, this);

        return ballPhysics;
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

        if (lastBoundOnlineBall == currentBall && lastBoundOnlineOwner == owner)
            return;

        BoxCollider area = owner == PlayerID.Player1 ? player1PlacementArea : player2PlacementArea;

        launcher.ball = currentBall;
        launcher.SetActivePlacementArea(area);
        launcher.ResetLaunch();

        lastBoundOnlineBall = currentBall;
        lastBoundOnlineOwner = owner;

        if (logDebug)
        {
            Debug.Log(
                "[BallTurnSpawner] TryBindCurrentOnlineBallForLocalControl -> " +
                "LocalPlayer=" + localPlayer +
                " | Owner=" + owner,
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
        lastBoundOnlineBall = null;
        lastBoundOnlineOwner = PlayerID.None;
    }

    // Compatibilità
    public BallPhysics PrepareBallForTurn(PlayerController player, PlayerController player1, PlayerController player2)
    {
        ResolveDependencies();

        bool isOnline = onlineGameplayAuthority != null && onlineGameplayAuthority.IsOnlineSession;
        if (isOnline)
            return null;

        if (player == null)
            return null;

        Transform spawnPoint = GetSpawnPointForPlayer(player, player1, player2);
        BoxCollider placementArea = GetPlacementAreaForPlayer(player, player1, player2);
        GameObject prefabToSpawn = GetBallPrefabForPlayer(player, player1, player2);

        if (spawnPoint == null || prefabToSpawn == null)
            return null;

        PlayerID ownerId = player == player1 ? PlayerID.Player1 : PlayerID.Player2;
        string skinUniqueId = ResolveEquippedSkinUniqueIdForOwner(ownerId);

        GameObject ballObject = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);

        BallPhysics ballPhysics = ballObject.GetComponent<BallPhysics>();
        if (ballPhysics == null)
            return null;

        BallOwnership ownership = ballObject.GetComponent<BallOwnership>();
        if (ownership != null)
            ownership.Owner = ownerId;

        ApplyOfflineSkin(ballObject, skinUniqueId);

        Rigidbody rb = ballObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.isKinematic = true;
        }

        if (launcher != null)
        {
            launcher.ball = ballPhysics;
            launcher.SetActivePlacementArea(placementArea);
            launcher.ResetLaunch();
        }

        return ballPhysics;
    }

    public void SwapPlayerSides()
    {
        Transform tempSpawn = player1SpawnPoint;
        player1SpawnPoint = player2SpawnPoint;
        player2SpawnPoint = tempSpawn;

        BoxCollider tempArea = player1PlacementArea;
        player1PlacementArea = player2PlacementArea;
        player2PlacementArea = tempArea;

        player1IsOnLeft = !player1IsOnLeft;
    }

    public void ClearAllBallsInScene()
    {
#if UNITY_2023_1_OR_NEWER
        BallPhysics[] balls = FindObjectsByType<BallPhysics>(FindObjectsSortMode.None);
#else
        BallPhysics[] balls = FindObjectsOfType<BallPhysics>();
#endif

        bool isOnline =
            onlineGameplayAuthority != null &&
            onlineGameplayAuthority.IsOnlineSession &&
            runnerManager != null &&
            runnerManager.IsRunning &&
            runnerManager.ActiveRunner != null;

        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i] == null)
                continue;

            NetworkObject netObj = balls[i].GetComponent<NetworkObject>();

            if (isOnline && netObj != null && runnerManager.ActiveRunner.IsServer)
                runnerManager.ActiveRunner.Despawn(netObj);
            else if (!isOnline)
                Destroy(balls[i].gameObject);
        }

        if (launcher != null)
            launcher.ball = null;

        lastBoundOnlineBall = null;
        lastBoundOnlineOwner = PlayerID.None;
    }

    public bool IsBallInsideCurrentLaunchArea(BallPhysics ball, PlayerController currentPlayer, PlayerController player1, PlayerController player2)
    {
        if (ball == null || currentPlayer == null)
            return false;

        BoxCollider area = GetPlacementAreaForPlayer(currentPlayer, player1, player2);
        if (area == null)
            return false;

        Vector3 local = area.transform.InverseTransformPoint(ball.transform.position);
        Vector3 half = area.size * 0.5f;
        Vector3 center = area.center;

        bool insideX = local.x >= center.x - half.x && local.x <= center.x + half.x;
        bool insideY = local.y >= center.y - half.y && local.y <= center.y + half.y;
        bool insideZ = local.z >= center.z - half.z && local.z <= center.z + half.z;

        return insideX && insideY && insideZ;
    }

    public bool IsPlayerOnLeft(PlayerController player, PlayerController player1, PlayerController player2)
    {
        if (player == null)
            return false;

        if (player == player1)
            return player1IsOnLeft;

        if (player == player2)
            return !player1IsOnLeft;

        return false;
    }

    public bool IsPlayerOnRight(PlayerController player, PlayerController player1, PlayerController player2)
    {
        return !IsPlayerOnLeft(player, player1, player2);
    }

    private Transform GetSpawnPointForPlayer(PlayerController player, PlayerController player1, PlayerController player2)
    {
        if (player == player1)
            return player1SpawnPoint;

        if (player == player2)
            return player2SpawnPoint;

        return null;
    }

    private BoxCollider GetPlacementAreaForPlayer(PlayerController player, PlayerController player1, PlayerController player2)
    {
        if (player == player1)
            return player1PlacementArea;

        if (player == player2)
            return player2PlacementArea;

        return null;
    }

    private GameObject GetBallPrefabForPlayer(PlayerController player, PlayerController player1, PlayerController player2)
    {
        if (player == player1)
            return player1BallPrefab != null ? player1BallPrefab : fallbackBallPrefab;

        if (player == player2)
            return player2BallPrefab != null ? player2BallPrefab : fallbackBallPrefab;

        return fallbackBallPrefab;
    }

    private void ApplyOfflineSkin(GameObject ballObject, string skinUniqueId)
    {
        if (!applyEquippedSkins || string.IsNullOrWhiteSpace(skinUniqueId))
            return;

        BallSkinApplier skinApplier = ballObject.GetComponent<BallSkinApplier>();
        if (skinApplier == null || PlayerSkinLoadout.Instance == null || PlayerSkinLoadout.Instance.Database == null)
            return;

        BallSkinData skin = ResolveSkinDataByUniqueId(PlayerSkinLoadout.Instance.Database, skinUniqueId);
        if (skin != null)
            skinApplier.ApplySkinData(PlayerSkinLoadout.Instance.Database, skin);
    }

    private string ResolveEquippedSkinUniqueIdForOwner(PlayerID ownerId)
    {
        if (!applyEquippedSkins || PlayerSkinLoadout.Instance == null)
            return string.Empty;

        BallSkinData equippedSkin = null;

        if (ownerId == PlayerID.Player1)
            equippedSkin = PlayerSkinLoadout.Instance.GetEquippedSkinForPlayer1();
        else if (ownerId == PlayerID.Player2)
            equippedSkin = PlayerSkinLoadout.Instance.GetEquippedSkinForPlayer2();

        if (equippedSkin == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(equippedSkin.skinUniqueId) ? string.Empty : equippedSkin.skinUniqueId.Trim();
    }

    private BallSkinData ResolveSkinDataByUniqueId(BallSkinDatabase database, string skinUniqueId)
    {
        if (database == null || string.IsNullOrWhiteSpace(skinUniqueId))
            return null;

        Type dbType = database.GetType();

        FieldInfo[] fields = dbType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            object value = fields[i].GetValue(database);
            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item is BallSkinData candidate &&
                        candidate != null &&
                        string.Equals(candidate.skinUniqueId, skinUniqueId, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }
}