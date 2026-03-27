using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        BallPhysics ball = other.GetComponent<BallPhysics>();

        if (ball == null)
            return;

        if (TurnManager.Instance != null)
            TurnManager.Instance.BallLost(ball);

        Destroy(other.gameObject);
    }
}