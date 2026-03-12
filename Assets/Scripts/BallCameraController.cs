using UnityEngine;

/// <summary>
/// Muove una singola camera tra:
/// - overview strategica
/// - aim Player 1
/// - aim Player 2
///
/// Il BallLauncher chiede solo di entrare o uscire dalla modalità aim.
/// Questo script decide quale anchor usare in base al turno corrente.
/// </summary>
public class BallCameraController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;

    [Header("Camera Positions")]
    [Tooltip("Posizione/rotazione overview strategica")]
    public Transform overviewAnchor;

    [Tooltip("Posizione/rotazione aim per Player 1")]
    public Transform aimAnchorPlayer1;

    [Tooltip("Posizione/rotazione aim per Player 2")]
    public Transform aimAnchorPlayer2;

    [Header("Transition")]
    [Range(1f, 20f)]
    public float transitionSpeed = 5f;

    private Transform targetAnchor;

    void Awake()
    {
        targetAnchor = overviewAnchor;
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
    /// aiming = true  -> usa l'anchor aim del player attivo
    /// aiming = false -> torna in overview
    /// </summary>
    public void SetAiming(bool aiming)
    {
        if (!aiming)
        {
            targetAnchor = overviewAnchor;
            return;
        }

        if (TurnManager.Instance == null)
        {
            targetAnchor = overviewAnchor;
            return;
        }

        if (TurnManager.Instance.IsPlayer1Turn)
            targetAnchor = aimAnchorPlayer1 != null ? aimAnchorPlayer1 : overviewAnchor;
        else if (TurnManager.Instance.IsPlayer2Turn)
            targetAnchor = aimAnchorPlayer2 != null ? aimAnchorPlayer2 : overviewAnchor;
        else
            targetAnchor = overviewAnchor;
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

        if (aimAnchorPlayer1 != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(aimAnchorPlayer1.position, 0.2f);
            Gizmos.DrawRay(aimAnchorPlayer1.position, aimAnchorPlayer1.forward * 1.5f);
        }

        if (aimAnchorPlayer2 != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(aimAnchorPlayer2.position, 0.2f);
            Gizmos.DrawRay(aimAnchorPlayer2.position, aimAnchorPlayer2.forward * 1.5f);
        }
    }
#endif
}