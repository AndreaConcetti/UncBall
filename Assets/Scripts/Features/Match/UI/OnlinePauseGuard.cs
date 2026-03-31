using UnityEngine;
using UnityEngine.UI;

public class OnlinePauseGuard : MonoBehaviour
{
    [Header("Optional References")]
    [SerializeField] private OnlineGameplayAuthority onlineGameplayAuthority;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Selectable[] pauseSelectablesToDisable;
    [SerializeField] private Behaviour[] pauseBehavioursToDisable;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool applied;

    private void Awake()
    {
        ResolveDependencies();
        ApplyIfOnline();
    }

    private void Start()
    {
        ResolveDependencies();
        ApplyIfOnline();
    }

    private void Update()
    {
        if (!IsOnline())
            return;

        if (Time.timeScale != 1f)
            Time.timeScale = 1f;

        if (pausePanel != null && pausePanel.activeSelf)
            pausePanel.SetActive(false);

        if (!applied)
            ApplyIfOnline();
    }

    private void ApplyIfOnline()
    {
        if (!IsOnline())
            return;

        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (pauseSelectablesToDisable != null)
        {
            for (int i = 0; i < pauseSelectablesToDisable.Length; i++)
            {
                if (pauseSelectablesToDisable[i] != null)
                    pauseSelectablesToDisable[i].interactable = false;
            }
        }

        if (pauseBehavioursToDisable != null)
        {
            for (int i = 0; i < pauseBehavioursToDisable.Length; i++)
            {
                if (pauseBehavioursToDisable[i] != null)
                    pauseBehavioursToDisable[i].enabled = false;
            }
        }

        applied = true;

        if (logDebug)
            Debug.Log("[OnlinePauseGuard] Online pause disabled.", this);
    }

    private bool IsOnline()
    {
        ResolveDependencies();
        return onlineGameplayAuthority != null && onlineGameplayAuthority.IsOnlineSession;
    }

    private void ResolveDependencies()
    {
        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;
    }
}