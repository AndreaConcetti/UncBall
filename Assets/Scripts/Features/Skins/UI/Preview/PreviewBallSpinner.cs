using UnityEngine;

public class PreviewBallSpinner : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private Vector3 rotationAxis = new Vector3(0f, 1f, 0f);
    [SerializeField] private float rotationSpeed = 35f;
    [SerializeField] private bool useUnscaledTime = true;

    private void Update()
    {
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        transform.Rotate(rotationAxis.normalized, rotationSpeed * delta, Space.World);
    }
}