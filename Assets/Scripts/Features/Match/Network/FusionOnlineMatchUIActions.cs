using UnityEngine;

public class FusionOnlineMatchUIActions : MonoBehaviour
{
    private FusionOnlineMatchController cachedOnlineController;
    private OfflineBotMatchController cachedOfflineBotController;

    private FusionOnlineMatchController GetOnlineController()
    {
        if (cachedOnlineController != null)
            return cachedOnlineController;

#if UNITY_2023_1_OR_NEWER
        cachedOnlineController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
        cachedOnlineController = FindObjectOfType<FusionOnlineMatchController>();
#endif
        return cachedOnlineController;
    }

    private OfflineBotMatchController GetOfflineBotController()
    {
        if (cachedOfflineBotController != null)
            return cachedOfflineBotController;

#if UNITY_2023_1_OR_NEWER
        cachedOfflineBotController = FindFirstObjectByType<OfflineBotMatchController>();
#else
        cachedOfflineBotController = FindObjectOfType<OfflineBotMatchController>();
#endif
        return cachedOfflineBotController;
    }

    public void ResumeAfterHalftime()
    {
        FusionOnlineMatchController onlineController = GetOnlineController();
        if (onlineController != null)
        {
            onlineController.RequestResumeAfterHalftime();
            return;
        }

        OfflineBotMatchController offlineController = GetOfflineBotController();
        if (offlineController != null)
            offlineController.RequestResumeAfterHalftime();
    }

    public void RequestSurrender()
    {
        FusionOnlineMatchController onlineController = GetOnlineController();
        if (onlineController != null)
        {
            onlineController.RequestLocalSurrender();
            return;
        }

        OfflineBotMatchController offlineController = GetOfflineBotController();
        if (offlineController != null)
            offlineController.RequestLocalSurrender();
    }
}