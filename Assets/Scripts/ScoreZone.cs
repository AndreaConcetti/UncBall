using UnityEngine;

public class ScoreZone : MonoBehaviour
{
    public int points = 10;
    public GameObject ballPrefab;
    public Transform spawnPoint;

    private void OnTriggerEnter(Collider other)
    {
        BallLauncher1 player = other.GetComponent<BallLauncher1>();

        if (player != null)
        {
            // Aggiunge punti
            ScoreManager.Instance.AddScore(player, points);

            // Blocca la pallina
            player.DisableMovement();

            // Genera nuova pallina
            Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
        }
    }
}
