using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class BallResetter2 : MonoBehaviour
{
    [Header("References")]
    public BoxCollider launchZone;
    public BallLauncher2 launcher;

    [Header("Reset")]
    public Key resetKey = Key.R;
    public Vector3 worldOffset = new Vector3(0f, 0.03f, 0f);
    public bool resetOnStart = true;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (resetOnStart)
            ResetToZoneCenter();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[resetKey].wasPressedThisFrame)
            ResetToZoneCenter();
    }

    public void ResetToZoneCenter()
    {
        if (launchZone == null)
        {
            Debug.LogError("[BallResetter] launchZone non assegnata.");
            return;
        }

        Vector3 centerWorld = launchZone.transform.TransformPoint(launchZone.center);

        // Azzeramento fisica (supportato)
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Teletrasporto al centro della LaunchZone
        transform.position = centerWorld + worldOffset;

        // Resta ferma (non rotola) ma deve essere riposizionabile
        rb.isKinematic = true;

        // Stato: subito Placing (senza dover premere Space)
        if (launcher != null)
            launcher.ResetForNewShot();
    }
}