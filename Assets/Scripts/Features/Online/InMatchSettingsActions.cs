using UnityEngine;

public class InMatchSettingsActions : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private SettingsPanelUI settingsPanelUI;
    [SerializeField] private FusionOnlineMatchUIActions matchUIActions;

    [Header("Optional Confirm UI")]
    [SerializeField] private GameObject surrenderConfirmPanel;

    [Header("Behavior")]
    [SerializeField] private bool closeSettingsWhenOpeningConfirm = true;
    [SerializeField] private bool closeSettingsAfterConfirm = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private void Awake()
    {
        ResolveReferences();
        SetSurrenderConfirmVisible(false);
    }

    private void ResolveReferences()
    {
        if (settingsPanelUI == null)
        {
#if UNITY_2023_1_OR_NEWER
            settingsPanelUI = FindFirstObjectByType<SettingsPanelUI>();
#else
            settingsPanelUI = FindObjectOfType<SettingsPanelUI>();
#endif
        }

        if (matchUIActions == null)
        {
#if UNITY_2023_1_OR_NEWER
            matchUIActions = FindFirstObjectByType<FusionOnlineMatchUIActions>();
#else
            matchUIActions = FindObjectOfType<FusionOnlineMatchUIActions>();
#endif
        }
    }

    public void OnPressSurrenderButton()
    {
        ResolveReferences();

        if (surrenderConfirmPanel != null)
        {
            SetSurrenderConfirmVisible(true);

            if (closeSettingsWhenOpeningConfirm && settingsPanelUI != null)
                settingsPanelUI.ClosePanel();
        }
        else
        {
            ConfirmSurrender();
        }

        if (logDebug)
            Debug.Log("[InMatchSettingsActions] OnPressSurrenderButton", this);
    }

    public void ConfirmSurrender()
    {
        ResolveReferences();

        if (matchUIActions != null)
            matchUIActions.RequestSurrender();

        SetSurrenderConfirmVisible(false);

        if (closeSettingsAfterConfirm && settingsPanelUI != null)
            settingsPanelUI.ClosePanel();

        if (logDebug)
            Debug.Log("[InMatchSettingsActions] ConfirmSurrender", this);
    }

    public void CancelSurrender()
    {
        SetSurrenderConfirmVisible(false);

        if (logDebug)
            Debug.Log("[InMatchSettingsActions] CancelSurrender", this);
    }

    private void SetSurrenderConfirmVisible(bool visible)
    {
        if (surrenderConfirmPanel != null)
            surrenderConfirmPanel.SetActive(visible);
    }
}