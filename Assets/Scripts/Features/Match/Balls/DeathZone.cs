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
        FusionOnlineMatchController controller = FindFirstObjectByType<FusionOnlineMatchController>();
#else
        FusionOnlineMatchController controller = FindObjectOfType<FusionOnlineMatchController>();
#endif

        NetworkObject netObj = other.GetComponent<NetworkObject>();

        if (controller != null && netObj != null)
        {
            if (controller.HasStateAuthority)
                controller.NotifyAuthoritativeBallLost(ball);

            return;
        }
    }
}