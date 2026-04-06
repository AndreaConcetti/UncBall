using UnityEngine;

public class FusionOnlineMatchUIActions : MonoBehaviour
{
    private FusionOnlineMatchController cachedController;

    private FusionOnlineMatchController GetController()
    {
        if (cachedController != null)
            return cachedController;

#if UNITY_2023_1_OR_NEWER
        cachedController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
        cachedController = FindObjectOfType<FusionOnlineMatchController>();
#endif
        return cachedController;
    }

    public void ResumeAfterHalftime()
    {
        FusionOnlineMatchController controller = GetController();
        if (controller != null)
            controller.RequestResumeAfterHalftime();
    }

    public void RequestSurrender()
    {
        FusionOnlineMatchController controller = GetController();
        if (controller != null)
            controller.RequestLocalSurrender();
    }
}