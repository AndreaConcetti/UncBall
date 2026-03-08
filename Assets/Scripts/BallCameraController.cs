using UnityEngine;

/// <summary>
/// Transitions the camera between two static positions:
///   OVERVIEW  — top-down table view, default gameplay camera
///   AIM       — fixed low angle while the player charges a shot
///
/// Both positions are defined as world-space transforms so you can place
/// them freely in the scene with full position + rotation control.
/// BallLauncher calls SetAiming() automatically if this component is assigned.
/// </summary>
public class BallCameraController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;

    [Header("Camera Positions")]
    [Tooltip("Transform marking the overview camera position/rotation (create an empty GameObject)")]
    public Transform overviewAnchor;

    [Tooltip("Transform marking the aim camera position/rotation (create an empty GameObject)")]
    public Transform aimAnchor;

    [Header("Transition")]
    [Tooltip("How fast the camera lerps between positions (higher = snappier)")]
    [Range(1f, 20f)]
    public float transitionSpeed = 5f;

    // ── Runtime State ──────────────────────────────────────────

    private Transform _target;

    // ── Unity Lifecycle ────────────────────────────────────────

    private void Awake()
    {
        _target = overviewAnchor;
    }

    private void LateUpdate()
    {
        if (cam == null || _target == null) return;

        float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
        cam.transform.position = Vector3.Lerp(cam.transform.position, _target.position, t);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, _target.rotation, t);
    }

    // ── Public API ─────────────────────────────────────────────

    /// <summary>
    /// Call with true when charging begins, false when shot is released or cancelled.
    /// </summary>
    public void SetAiming(bool aiming)
    {
        _target = aiming ? aimAnchor : overviewAnchor;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (overviewAnchor != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(overviewAnchor.position, 0.2f);
            Gizmos.DrawRay(overviewAnchor.position, overviewAnchor.forward * 1.5f);
        }
        if (aimAnchor != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(aimAnchor.position, 0.2f);
            Gizmos.DrawRay(aimAnchor.position, aimAnchor.forward * 1.5f);
        }
    }
#endif
}