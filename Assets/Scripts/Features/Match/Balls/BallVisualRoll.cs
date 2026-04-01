using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallVisualRoll : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private Transform visualTransform;

    [Header("Ball Geometry")]
    [SerializeField] private float ballRadius = 0.5f;

    [Header("Plane")]
    [SerializeField] private Vector3 planeNormal = Vector3.up;

    [Header("Motion Filter")]
    [SerializeField] private float minSpeedToRotate = 0.02f;
    [SerializeField] private bool projectVelocityOnPlane = true;

    [Header("Visual Tuning")]
    [SerializeField] private float visualRotationMultiplier = 1f;

    [Header("Smoothing")]
    [SerializeField] private bool smoothRotationAxis = true;
    [SerializeField] private float axisSmoothingSpeed = 18f;

    [Header("Reset")]
    [SerializeField] private Vector3 initialLocalEulerAngles = Vector3.zero;

    [Header("Remote Fallback")]
    [SerializeField] private bool useTransformDeltaWhenKinematic = true;

    private Vector3 currentSmoothedAxis = Vector3.right;
    private Vector3 lastTrackedPosition;
    private bool hasLastTrackedPosition;

    private void Awake()
    {
        ResolveReferences();

        if (planeNormal.sqrMagnitude < 0.0001f)
            planeNormal = Vector3.up;

        planeNormal.Normalize();

        ResetVisualRotation();
        CacheCurrentPosition();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (targetRigidbody == null || visualTransform == null)
            return;

        float deltaTime = Mathf.Max(0.0001f, Time.deltaTime);

        Vector3 velocity = GetEffectiveVelocity(deltaTime);

        if (projectVelocityOnPlane)
            velocity = Vector3.ProjectOnPlane(velocity, planeNormal);

        float speed = velocity.magnitude;
        if (speed < minSpeedToRotate)
        {
            CacheCurrentPosition();
            return;
        }

        Vector3 moveDirection = velocity / speed;
        Vector3 rotationAxis = Vector3.Cross(planeNormal, moveDirection);
        float axisMagnitude = rotationAxis.magnitude;

        if (axisMagnitude < 0.0001f)
        {
            CacheCurrentPosition();
            return;
        }

        rotationAxis /= axisMagnitude;

        if (smoothRotationAxis)
        {
            currentSmoothedAxis = Vector3.Slerp(
                currentSmoothedAxis,
                rotationAxis,
                axisSmoothingSpeed * deltaTime
            ).normalized;

            rotationAxis = currentSmoothedAxis;
        }
        else
        {
            currentSmoothedAxis = rotationAxis;
        }

        float safeRadius = Mathf.Max(0.0001f, ballRadius);
        float angularSpeedRad = speed / safeRadius;
        float angleDegThisFrame = angularSpeedRad * Mathf.Rad2Deg * visualRotationMultiplier * deltaTime;

        visualTransform.Rotate(rotationAxis, angleDegThisFrame, Space.World);
        CacheCurrentPosition();
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

    private void ResolveReferences()
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
    }

    private Vector3 GetEffectiveVelocity(float deltaTime)
    {
        if (targetRigidbody == null)
            return Vector3.zero;

        if (!targetRigidbody.isKinematic)
            return targetRigidbody.linearVelocity;

        if (!useTransformDeltaWhenKinematic)
            return Vector3.zero;

        Vector3 currentPosition = transform.position;

        if (!hasLastTrackedPosition)
        {
            lastTrackedPosition = currentPosition;
            hasLastTrackedPosition = true;
            return Vector3.zero;
        }

        return (currentPosition - lastTrackedPosition) / deltaTime;
    }

    private void CacheCurrentPosition()
    {
        lastTrackedPosition = transform.position;
        hasLastTrackedPosition = true;
    }
}