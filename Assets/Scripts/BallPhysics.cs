using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Custom 2D physics simulation for a pinball-style ball on a flat 3D table viewed from above.
/// Uses X/Z as the play surface; Y is managed separately (always flat).
/// Unity's built-in physics is NOT used — disable Rigidbody or set it to kinematic.
/// </summary>
public class BallPhysics : MonoBehaviour
{
    [Header("Feel")]
    public float gravityStrength = 9.8f;
    public Vector3 gravityDirection = Vector3.back; // –Z = toward drain
    public float linearDrag = 0.02f;
    public float maxSpeed = 25f;
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false; // we drive gravity ourselves
        _rb.constraints = RigidbodyConstraints.FreezePositionY
                        | RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void FixedUpdate()
    {
        // Simulated gravity on the play surface
        _rb.AddForce(gravityDirection.normalized * gravityStrength, ForceMode.Acceleration);

        // Drag
        _rb.linearVelocity *= Mathf.Clamp01(1f - linearDrag * Time.fixedDeltaTime);

        // Speed cap
        if (_rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
    }

    public void Launch(Vector3 impulse) => _rb.AddForce(impulse, ForceMode.Impulse);
    public void AddForce(Vector3 force) => _rb.AddForce(force, ForceMode.Impulse);
    public void Deactivate() { _rb.linearVelocity = Vector3.zero; _rb.isKinematic = true; }
    public void Activate()   { _rb.isKinematic = false; }
}