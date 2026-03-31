using UnityEngine;

public class FusionOnlineMatchUIActions : MonoBehaviour
{
    public void ResumeAfterHalftime()
    {
#if UNITY_2023_1_OR_NEWER
        FusionOnlineMatchController controller = FindFirstObjectByType<FusionOnlineMatchController>();
#else
        FusionOnlineMatchController controller = FindObjectOfType<FusionOnlineMatchController>();
#endif
        if (controller != null)
            controller.RequestResumeAfterHalftime();
    }
}