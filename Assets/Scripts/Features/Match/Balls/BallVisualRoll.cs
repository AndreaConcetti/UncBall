using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallVisualRoll : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private Transform visualTransform;

    [Header("Ball Geometry")]
    [Tooltip("Raggio visivo della pallina in unità Unity")]
    [SerializeField] private float ballRadius = 0.5f;

    [Header("Plane")]
    [Tooltip("Normale del piano su cui la ball rotola. Nel tuo caso resta normalmente Vector3.up")]
    [SerializeField] private Vector3 planeNormal = Vector3.up;

    [Header("Motion Filter")]
    [Tooltip("Velocità minima sotto la quale non aggiorna la rotazione")]
    [SerializeField] private float minSpeedToRotate = 0.02f;

    [Tooltip("Ignora la componente di velocità lungo la normale del piano")]
    [SerializeField] private bool projectVelocityOnPlane = true;

    [Header("Visual Tuning")]
    [Tooltip("Moltiplicatore finale della velocità di rotazione visiva")]
    [SerializeField] private float visualRotationMultiplier = 1f;

    [Header("Smoothing")]
    [Tooltip("Smussa il cambio di asse di rotazione per evitare jitter")]
    [SerializeField] private bool smoothRotationAxis = true;

    [SerializeField] private float axisSmoothingSpeed = 18f;

    [Header("Reset")]
    [Tooltip("Rotazione locale iniziale del visual")]
    [SerializeField] private Vector3 initialLocalEulerAngles = Vector3.zero;

    private Vector3 currentSmoothedAxis = Vector3.right;

    private void Awake()
    {
        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>();

        if (visualTransform == null)
        {
            if (transform.childCount > 0)
                visualTransform = transform.GetChild(0);
            else
                visualTransform = transform;
        }

        if (planeNormal.sqrMagnitude < 0.0001f)
            planeNormal = Vector3.up;

        planeNormal.Normalize();

        ResetVisualRotation();
    }

    private void LateUpdate()
    {
        if (targetRigidbody == null || visualTransform == null)
            return;

        if (targetRigidbody.isKinematic)
            return;

        Vector3 velocity = targetRigidbody.linearVelocity;

        if (projectVelocityOnPlane)
            velocity = Vector3.ProjectOnPlane(velocity, planeNormal);

        float speed = velocity.magnitude;
        if (speed < minSpeedToRotate)
            return;

        Vector3 moveDirection = velocity / speed;

        Vector3 rotationAxis = Vector3.Cross(planeNormal, moveDirection);
        float axisMagnitude = rotationAxis.magnitude;

        if (axisMagnitude < 0.0001f)
            return;

        rotationAxis /= axisMagnitude;

        if (smoothRotationAxis)
        {
            currentSmoothedAxis = Vector3.Slerp(
                currentSmoothedAxis,
                rotationAxis,
                axisSmoothingSpeed * Time.deltaTime
            ).normalized;

            rotationAxis = currentSmoothedAxis;
        }
        else
        {
            currentSmoothedAxis = rotationAxis;
        }

        float safeRadius = Mathf.Max(0.0001f, ballRadius);

        // velocità angolare teorica di rotolamento puro: omega = v / r
        float angularSpeedRad = speed / safeRadius;
        float angleDegThisFrame = angularSpeedRad * Mathf.Rad2Deg * visualRotationMultiplier * Time.deltaTime;

        visualTransform.Rotate(rotationAxis, angleDegThisFrame, Space.World);
    }

    public void ResetVisualRotation()
    {
        if (visualTransform == null)
            return;

        visualTransform.localRotation = Quaternion.Euler(initialLocalEulerAngles);
        currentSmoothedAxis = Vector3.right;
    }

    public void SetVisualTransform(Transform newVisualTransform)
    {
        visualTransform = newVisualTransform;
    }

    public void SetPlaneNormal(Vector3 newPlaneNormal)
    {
        if (newPlaneNormal.sqrMagnitude < 0.0001f)
            return;

        planeNormal = newPlaneNormal.normalized;
    }

    public Transform GetVisualTransform()
    {
        return visualTransform;
    }
}