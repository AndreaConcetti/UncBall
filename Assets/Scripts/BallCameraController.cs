using UnityEngine;

/// <summary>
/// Muove una singola camera tra:
/// - overview strategica
/// - aim lato sinistro
/// - aim lato destro
///
/// La scelta del lato non dipende dall'identità fissa Player1/Player2,
/// ma dalla posizione reale corrente del player sul tavolo.
/// Questo evita errori dopo lo swap di halftime.
/// </summary>
public class BallCameraController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public TurnManager turnManager;
    public BallTurnSpawner ballTurnSpawner;

    [Header("Camera Positions")]
    [Tooltip("Posizione/rotazione overview strategica")]
    public Transform overviewAnchor;

    [Tooltip("Posizione/rotazione aim per il lato sinistro del tavolo")]
    public Transform leftSideAimAnchor;

    [Tooltip("Posizione/rotazione aim per il lato destro del tavolo")]
    public Transform rightSideAimAnchor;

    [Header("Transition")]
    [Range(1f, 20f)]
    public float transitionSpeed = 5f;

    private Transform targetAnchor;

    void Awake()
    {
        targetAnchor = overviewAnchor;
    }

    void Start()
    {
        if (turnManager == null)
            turnManager = TurnManager.Instance;

#if UNITY_2023_1_OR_NEWER
        if (ballTurnSpawner == null)
            ballTurnSpawner = FindFirstObjectByType<BallTurnSpawner>();
#else
        if (ballTurnSpawner == null)
            ballTurnSpawner = FindObjectOfType<BallTurnSpawner>();
#endif
    }

    void LateUpdate()
    {
        if (cam == null || targetAnchor == null)
            return;

        float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

        cam.transform.position = Vector3.Lerp(cam.transform.position, targetAnchor.position, t);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetAnchor.rotation, t);
    }

    /// <summary>
    /// aiming = true  -> usa l'anchor aim del lato occupato dal player di turno
    /// aiming = false -> torna in overview
    /// </summary>
    public void SetAiming(bool aiming)
    {
        if (!aiming)
        {
            targetAnchor = overviewAnchor;
            return;
        }

        if (turnManager == null)
            turnManager = TurnManager.Instance;

        if (turnManager == null || ballTurnSpawner == null || turnManager.currentPlayer == null)
        {
            targetAnchor = overviewAnchor;
            return;
        }

        bool playerIsOnLeft = ballTurnSpawner.IsPlayerOnLeft(
            turnManager.currentPlayer,
            turnManager.player1,
            turnManager.player2
        );

        if (playerIsOnLeft)
            targetAnchor = leftSideAimAnchor != null ? leftSideAimAnchor : overviewAnchor;
        else
            targetAnchor = rightSideAimAnchor != null ? rightSideAimAnchor : overviewAnchor;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (overviewAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(overviewAnchor.position, 0.2f);
            Gizmos.DrawRay(overviewAnchor.position, overviewAnchor.forward * 1.5f);
        }

        if (leftSideAimAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(leftSideAimAnchor.position, 0.2f);
            Gizmos.DrawRay(leftSideAimAnchor.position, leftSideAimAnchor.forward * 1.5f);
        }

        if (rightSideAimAnchor != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(rightSideAimAnchor.position, 0.2f);
            Gizmos.DrawRay(rightSideAimAnchor.position, rightSideAimAnchor.forward * 1.5f);
        }
    }
#endif
}