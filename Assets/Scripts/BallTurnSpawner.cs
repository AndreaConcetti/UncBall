using UnityEngine;

/// <summary>
/// Gestisce la creazione e la preparazione della ball del turno corrente.
/// Tiene anche traccia dei lati di gioco:
/// - quale player è a sinistra
/// - quale player è a destra
/// </summary>
public class BallTurnSpawner : MonoBehaviour
{
    [Header("References")]
    public BallLauncher launcher;

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

    [Header("Optional Material Override")]
    public bool overrideBallMaterial = false;
    public Material player1Material;
    public Material player2Material;

    [Header("Runtime Sides")]
    [Tooltip("True se Player1 occupa attualmente il lato sinistro del tavolo")]
    [SerializeField] private bool player1IsOnLeft = true;

    public BallPhysics PrepareBallForTurn(PlayerController player, PlayerController player1, PlayerController player2)
    {
        if (player == null)
        {
            Debug.LogError("BallTurnSpawner: player nullo.", this);
            return null;
        }

        Transform spawnPoint = GetSpawnPointForPlayer(player, player1, player2);
        BoxCollider placementArea = GetPlacementAreaForPlayer(player, player1, player2);
        GameObject prefabToSpawn = GetBallPrefabForPlayer(player, player1, player2);

        if (spawnPoint == null)
        {
            Debug.LogError("BallTurnSpawner: spawn point non assegnato.", this);
            return null;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError("BallTurnSpawner: prefab ball non assegnato.", this);
            return null;
        }

        GameObject ballObject = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);

        if (overrideBallMaterial)
        {
            MeshRenderer renderer = ballObject.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.material = player == player1 ? player1Material : player2Material;
        }

        BallPhysics ballPhysics = ballObject.GetComponent<BallPhysics>();
        if (ballPhysics == null)
        {
            Debug.LogError("BallTurnSpawner: il prefab spawnato non contiene BallPhysics.", ballObject);
            return null;
        }

        BallOwnership ownership = ballObject.GetComponent<BallOwnership>();
        if (ownership != null)
            ownership.Owner = player == player1 ? PlayerID.Player1 : PlayerID.Player2;

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

        Debug.Log("[BallTurnSpawner] Player sides swapped.");
    }

    public void ClearAllBallsInScene()
    {
#if UNITY_2023_1_OR_NEWER
        BallPhysics[] balls = FindObjectsByType<BallPhysics>(FindObjectsSortMode.None);
#else
        BallPhysics[] balls = FindObjectsOfType<BallPhysics>();
#endif

        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i] != null)
                Destroy(balls[i].gameObject);
        }

        if (launcher != null)
            launcher.ball = null;

        Debug.Log("[BallTurnSpawner] All balls cleared.");
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

    Transform GetSpawnPointForPlayer(PlayerController player, PlayerController player1, PlayerController player2)
    {
        if (player == player1)
            return player1SpawnPoint;

        if (player == player2)
            return player2SpawnPoint;

        return null;
    }

    BoxCollider GetPlacementAreaForPlayer(PlayerController player, PlayerController player1, PlayerController player2)
    {
        if (player == player1)
            return player1PlacementArea;

        if (player == player2)
            return player2PlacementArea;

        return null;
    }

    GameObject GetBallPrefabForPlayer(PlayerController player, PlayerController player1, PlayerController player2)
    {
        if (player == player1)
            return player1BallPrefab != null ? player1BallPrefab : fallbackBallPrefab;

        if (player == player2)
            return player2BallPrefab != null ? player2BallPrefab : fallbackBallPrefab;

        return fallbackBallPrefab;
    }
}
