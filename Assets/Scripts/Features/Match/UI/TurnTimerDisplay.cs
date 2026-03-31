using TMPro;
using UnityEngine;

public class TurnTimerDisplay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text targetText;

    [Header("Dependencies")]
    [SerializeField] private FusionOnlineMatchController onlineMatchController;
    [SerializeField] private OnlineGameplayAuthority onlineGameplayAuthority;

    [Header("Formatting")]
    [SerializeField] private bool useCeil = true;
    [SerializeField] private string fallbackText = "0";

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void Update()
    {
        ResolveDependencies();

        if (targetText == null)
            return;

        if (onlineMatchController == null)
        {
            targetText.text = fallbackText;
            return;
        }

        float value = Mathf.Max(0f, onlineMatchController.CurrentTurnTimeRemaining);
        int shown = useCeil ? Mathf.CeilToInt(value) : Mathf.RoundToInt(value);
        targetText.text = shown.ToString();

        if (logDebug)
        {
            Debug.Log(
                "[TurnTimerDisplay] Updated -> " +
                "TurnTime=" + value +
                " | Shown=" + shown,
                this
            );
        }
    }

    private void ResolveDependencies()
    {
        if (onlineGameplayAuthority == null)
            onlineGameplayAuthority = OnlineGameplayAuthority.Instance;

        if (onlineMatchController == null && onlineGameplayAuthority != null)
            onlineMatchController = onlineGameplayAuthority.OnlineMatchController;

#if UNITY_2023_1_OR_NEWER
        if (onlineMatchController == null)
            onlineMatchController = FindFirstObjectByType<FusionOnlineMatchController>();
#else
        if (onlineMatchController == null)
            onlineMatchController = FindObjectOfType<FusionOnlineMatchController>();
#endif
    }
}