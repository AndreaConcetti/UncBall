using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerMenuPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlineFlowController onlineFlowController;
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("UI")]
    [SerializeField] private TMP_Text globalStatusText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private GameObject searchingPanel;
    [SerializeField] private GameObject idlePanel;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private void OnEnable()
    {
        ResolveDependencies();

        if (onlineFlowController != null)
            onlineFlowController.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (onlineFlowController != null)
            onlineFlowController.OnStateChanged -= HandleStateChanged;
    }

    private void Start()
    {
        ResolveDependencies();
        RefreshStaticUi();
        RefreshStateUi();
    }

    public void QueueNormal()
    {
        ResolveDependencies();

        if (onlineFlowController == null)
            return;

        onlineFlowController.EnterQueue(QueueType.Normal);
    }

    public void QueueRanked()
    {
        ResolveDependencies();

        if (onlineFlowController == null)
            return;

        onlineFlowController.EnterQueue(QueueType.Ranked);
    }

    public void CancelQueue()
    {
        ResolveDependencies();

        if (onlineFlowController == null)
            return;

        onlineFlowController.CancelQueue();
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ResolveDependencies()
    {
        if (onlineFlowController == null)
            onlineFlowController = OnlineFlowController.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;
    }

    private void HandleStateChanged(OnlineRuntimeContext context)
    {
        RefreshStateUi();

        if (logDebug && context != null)
            Debug.Log("[MultiplayerMenuPresenter] State -> " + context.state + " | " + context.statusMessage, this);
    }

    private void RefreshStaticUi()
    {
        if (playerNameText != null)
        {
            playerNameText.text =
                profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName)
                ? profileManager.ActiveDisplayName.Trim().ToUpperInvariant()
                : "PLAYER";
        }
    }

    private void RefreshStateUi()
    {
        OnlineRuntimeContext context = onlineFlowController != null ? onlineFlowController.RuntimeContext : null;

        string status = context != null && !string.IsNullOrWhiteSpace(context.statusMessage)
            ? context.statusMessage
            : "Idle";

        if (globalStatusText != null)
            globalStatusText.text = status.ToUpperInvariant();

        bool searching =
            context != null &&
            (context.state == OnlineFlowState.Queueing ||
             context.state == OnlineFlowState.MatchAssigned ||
             context.state == OnlineFlowState.JoiningSession ||
             context.state == OnlineFlowState.LoadingGameplay);

        if (searchingPanel != null)
            searchingPanel.SetActive(searching);

        if (idlePanel != null)
            idlePanel.SetActive(!searching);
    }
}