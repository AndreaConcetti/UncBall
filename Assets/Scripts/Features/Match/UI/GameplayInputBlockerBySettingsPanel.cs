using UnityEngine;
using UnityEngine.UI;

public class GameplayInputBlockerBySettingsPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettingsPanelUI settingsPanelUI;
    [SerializeField] private GameObject inputBlockerRoot;
    [SerializeField] private Image inputBlockerImage;

    [Header("Behavior")]
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private void Awake()
    {
        ResolveDependencies();

        if (applyOnAwake)
            ApplyState();
    }

    private void OnEnable()
    {
        ResolveDependencies();

        if (settingsPanelUI != null)
            settingsPanelUI.PanelVisibilityChanged += HandlePanelVisibilityChanged;

        if (applyOnEnable)
            ApplyState();
    }

    private void OnDisable()
    {
        if (settingsPanelUI != null)
            settingsPanelUI.PanelVisibilityChanged -= HandlePanelVisibilityChanged;
    }

    public void ApplyState()
    {
        bool shouldBlock = settingsPanelUI != null && settingsPanelUI.IsPanelOpen;
        SetBlocked(shouldBlock);
    }

    private void HandlePanelVisibilityChanged(bool isOpen)
    {
        SetBlocked(isOpen);
    }

    private void SetBlocked(bool blocked)
    {
        if (inputBlockerRoot != null)
            inputBlockerRoot.SetActive(blocked);

        if (inputBlockerImage != null)
            inputBlockerImage.raycastTarget = blocked;

        if (logDebug)
            Debug.Log("[GameplayInputBlockerBySettingsPanel] SetBlocked -> " + blocked, this);
    }

    private void ResolveDependencies()
    {
#if UNITY_2023_1_OR_NEWER
        if (settingsPanelUI == null)
            settingsPanelUI = FindFirstObjectByType<SettingsPanelUI>(FindObjectsInactive.Include);
#else
        if (settingsPanelUI == null)
            settingsPanelUI = FindObjectOfType<SettingsPanelUI>();
#endif
    }
}