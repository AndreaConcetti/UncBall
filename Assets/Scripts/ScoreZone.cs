using UnityEngine;

public class ScoreZone : MonoBehaviour
{
    public int points = 10;

    private void OnTriggerEnter(Collider other)
    {
        BallPhysics ball = other.GetComponent<BallPhysics>();

        if (ball != null)
        {
            // assegna punti
            ScoreManager.Instance.AddScore(points);



            // blocca la pallina
            Rigidbody rb = other.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            Destroy(gameObject);
        }
    }
}
