using Fusion;
using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        BallPhysics ball = other.GetComponent<BallPhysics>();
        if (ball == null)
            return;

#if UNITY_2023_1_OR_NEWER
        FusionOnlineMatchController onlineController = FindFirstObjectByType<FusionOnlineMatchController>();
        OfflineBotMatchController offlineController = FindFirstObjectByType<OfflineBotMatchController>();
#else
        FusionOnlineMatchController onlineController = FindObjectOfType<FusionOnlineMatchController>();
        OfflineBotMatchController offlineController = FindObjectOfType<OfflineBotMatchController>();
#endif

        NetworkObject netObj = other.GetComponent<NetworkObject>();

        // Online path stays unchanged.
        if (onlineController != null && netObj != null && netObj.enabled)
        {
            if (onlineController.HasStateAuthority)
                onlineController.NotifyAuthoritativeBallLost(ball);

            return;
        }

        // Offline bot path.
        if (offlineController != null && offlineController.IsOfflineBotSessionActive)
        {
            offlineController.NotifyOfflineBallLost(ball);

            if (ball != null)
                Destroy(ball.gameObject);

            return;
        }

        if (LuckyShotGameplayController.Instance != null)
        {
            LuckyShotGameplayController.Instance.NotifyBallLost(ball);
        }

        // Fallback local cleanup.
        if (ball != null)
            Destroy(ball.gameObject);
    }
}