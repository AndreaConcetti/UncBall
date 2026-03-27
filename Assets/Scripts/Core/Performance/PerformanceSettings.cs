using UnityEngine;

public class PerformanceSettings : MonoBehaviour
{
    [Header("Target Framerate")]
    [SerializeField] private int targetFps = 60;

    [Header("VSync")]
    [SerializeField] private bool disableVSync = true;

    private void Awake()
    {
        if (disableVSync)
            QualitySettings.vSyncCount = 0;

        Application.targetFrameRate = targetFps;
    }
}