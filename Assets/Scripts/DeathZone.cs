using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        BallPhysics ball = other.GetComponent<BallPhysics>();

        if (ball != null)
        {
            TurnManager.Instance.BallLost();

            Destroy(other.gameObject);
        }
    }
}