using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BallLauncher1 : MonoBehaviour
{


     private bool isActive = false;
     private bool canMove = true;
    public enum State { Placing, Armed, Launched }

    [Header("References")]
    public BoxCollider launchZone;
    public Collider tableCollider;
    public LayerMask pickMask = ~0;

    [Header("Controls")]
    public Key lockKey = Key.Space;

    [Header("Flick (m/s)")]
    public float deadZoneMeters = 0.03f;
    public float minSpeed = 0.6f;
    public float maxSpeed = 3.8f;

    [Header("Debug")]
    public bool debugLogs = false;
    [SerializeField] private State state = State.Placing;

    private Rigidbody rb;

    private bool tracking;
    private Vector3 startWorld;
    private float startTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        state = State.Placing;
        Debug.Log("È il turno del Player 1");
    }

    private void Update()
    {
      if (TurnManager.Instance.IsPlayer1Turn)
{
    if (!isActive || !canMove)
            return;
        if (Keyboard.current != null && Keyboard.current[lockKey].wasPressedThisFrame)
        {
            if (state == State.Placing) SetArmedState();
            else if (state == State.Armed) SetPlacingState();
            else if (state == State.Launched) SetPlacingState();
        }

        Vector2 screenPos;
        bool down, up, held;

        if (Touchscreen.current != null)
        {
            var t = Touchscreen.current.primaryTouch;
            screenPos = t.position.ReadValue();
            down = t.press.wasPressedThisFrame;
            up = t.press.wasReleasedThisFrame;
            held = t.press.isPressed;
        }
        else
        {
            if (Mouse.current == null) return;
            screenPos = Mouse.current.position.ReadValue();
            down = Mouse.current.leftButton.wasPressedThisFrame;
            up = Mouse.current.leftButton.wasReleasedThisFrame;
            held = Mouse.current.leftButton.isPressed;
        }

        if (down) Begin(screenPos);
        if (held && tracking) Drag(screenPos);
        if (up) End(screenPos);
    }
    }

    public void ResetForNewShot()
    {
        tracking = false;
        state = State.Placing;

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        if (debugLogs) Debug.Log("[BallLauncher] ResetForNewShot -> Placing");
    }

    public void SetActive(bool value)
    {
        isActive = value;
    }

    public void SetPlacingState()
    {
        state = State.Placing;
        StopAndHold();
        if (debugLogs) Debug.Log("[BallLauncher] State = Placing");
    }

    private void SetArmedState()
    {
        state = State.Armed;
        StopAndHold();
        if (debugLogs) Debug.Log("[BallLauncher] State = Armed (locked)");
    }

    private void StopAndHold()
    {
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    public void DisableMovement()
{
    isActive = false;

    Rigidbody rb = GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    Debug.Log(name + " bloccato");
}

    private void Begin(Vector2 screenPos)
    {
        if (state == State.Placing)
        {
            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 3000f, pickMask)) return;
            if (hit.collider == null) return;
            if (hit.collider.transform.root != transform.root) return;
        }

        if (!ScreenToWorldOnTable(screenPos, out startWorld)) return;

        tracking = true;
        startTime = Time.time;
    }

    private void Drag(Vector2 screenPos)
    {
        if (!ScreenToWorldOnTable(screenPos, out var w)) return;

        if (state == State.Placing)
        {
            transform.position = ClampToLaunchZone(w);
        }
    }

    private void End(Vector2 screenPos)
    {
        if (!tracking) return;
        tracking = false;

        if (state != State.Armed) return;

        if (!ScreenToWorldOnTable(screenPos, out var endWorld)) return;

        float dt = Mathf.Max(Time.time - startTime, 0.001f);
        Vector3 delta = endWorld - startWorld;
        float distance = delta.magnitude;

        if (distance < deadZoneMeters) return;

        float speed = Mathf.Clamp(distance / dt, minSpeed, maxSpeed);
        Vector3 dir = delta.normalized;

        rb.isKinematic = false;
        rb.linearVelocity = dir * speed;

        state = State.Launched;
    }

    private bool ScreenToWorldOnTable(Vector2 screenPos, out Vector3 world)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        if (tableCollider != null)
        {
            if (tableCollider.Raycast(ray, out RaycastHit hit, 5000f))
            {
                world = hit.point;
                return true;
            }
            world = Vector3.zero;
            return false;
        }

        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (plane.Raycast(ray, out float enter))
        {
            world = ray.GetPoint(enter);
            return true;
        }

        world = Vector3.zero;
        return false;
    }

    private Vector3 ClampToLaunchZone(Vector3 worldPos)
    {
        Bounds b = launchZone.bounds;

        Vector3 p = worldPos;
        p.x = Mathf.Clamp(p.x, b.min.x, b.max.x);
        p.y = Mathf.Clamp(p.y, b.min.y, b.max.y);
        p.z = Mathf.Clamp(p.z, b.min.z, b.max.z);

        return p;
    }
}