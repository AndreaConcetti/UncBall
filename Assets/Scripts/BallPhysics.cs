using UnityEngine;

/// <summary>
/// Fisica custom della ball sul piano X/Z.
/// Durante il placement il Rigidbody resta kinematic.
/// Durante il lancio torna dinamico.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BallPhysics : MonoBehaviour
{
    [Header("Feel")]
    public float gravityStrength = 9.8f;
    public Vector3 gravityDirection = Vector3.back;
    public float linearDrag = 0.02f;
    public float maxSpeed = 25f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void FixedUpdate()
    {
        if (_rb == null || _rb.isKinematic)
            return;

        _rb.AddForce(gravityDirection.normalized * gravityStrength, ForceMode.Acceleration);

        _rb.linearVelocity *= Mathf.Clamp01(1f - linearDrag * Time.fixedDeltaTime);

        if (_rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
    }

    public void Launch(Vector3 impulse)
    {
        if (_rb == null)
            return;

        _rb.AddForce(impulse, ForceMode.Impulse);
    }

    public void AddForce(Vector3 force)
    {
        if (_rb == null)
            return;

        _rb.AddForce(force, ForceMode.Impulse);
    }

    public void Deactivate()
    {
        if (_rb == null)
            return;

        _rb.isKinematic = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.constraints = RigidbodyConstraints.FreezeAll;
        _rb.isKinematic = true;
    }

    public void Activate()
    {
        if (_rb == null)
            return;

        _rb.isKinematic = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }
}
