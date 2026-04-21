using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LuckyShotEndPanelUI : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject resultsPageRoot;
    [SerializeField] private GameObject rewardsPageRoot;

    [Header("Results Page")]
    [SerializeField] private TMP_Text resultsTitleText;
    [SerializeField] private TMP_Text board1ResultText;
    [SerializeField] private TMP_Text board2ResultText;
    [SerializeField] private TMP_Text board3ResultText;
    [SerializeField] private TMP_Text resultsInfoText;
    [SerializeField] private Button watchAdButton;
    [SerializeField] private TMP_Text watchAdButtonText;
    [SerializeField] private Button nextButton;

    [Header("Rewards Page")]
    [SerializeField] private TMP_Text rewardsTitleText;
    [SerializeField] private TMP_Text rewardsBodyText;
    [SerializeField] private Button backToMenuButton;

    [Header("Optional")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Debug / Dev")]
    [SerializeField] private bool autoGrantExtraShotInEditor = true;
    [SerializeField] private bool verboseLogs = true;

    private LuckyShotSessionRuntime sessionRuntime;
    private LuckyShotGameplayController gameplayController;

    private LuckyShotResolvedResult lastResolvedResult;
    private LuckyShotActiveSession lastResolvedSession;
    private bool hasResolvedResult;
    private bool isBusy;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        CloseImmediately();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void BindButtons()
    {
        if (watchAdButton != null)
        {
            watchAdButton.onClick.RemoveListener(OnPressWatchAd);
            watchAdButton.onClick.AddListener(OnPressWatchAd);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(OnPressNext);
            nextButton.onClick.AddListener(OnPressNext);
        }

        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveListener(OnPressBackToMenu);
            backToMenuButton.onClick.AddListener(OnPressBackToMenu);
        }
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        lastResolvedResult = result;
        lastResolvedSession = result.sessionAfterResolve;
        hasResolvedResult = true;

        bool shouldOpenEndPanel = result.success && (result.isFinalResolution || result.isWin || !result.canRetry);
        if (!shouldOpenEndPanel)
            return;

        OpenResultsPage();

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotEndPanelUI] HandleSessionResolved -> " +
                "Success=" + result.success +
                " | Win=" + result.isWin +
                " | CanRetry=" + result.canRetry +
                " | Final=" + result.isFinalResolution,
                this);
        }
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        if (panelRoot != null && panelRoot.activeSelf)
            RefreshVisiblePage();
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (panelRoot != null && panelRoot.activeSelf)
            RefreshVisiblePage();
    }

    private void HandleFeedbackRaised(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message ?? string.Empty;
    }

    public void OpenResultsPage()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        if (resultsPageRoot != null)
            resultsPageRoot.SetActive(true);

        if (rewardsPageRoot != null)
            rewardsPageRoot.SetActive(false);

        RefreshResultsPage();
    }

    public void OpenRewardsPage()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        if (resultsPageRoot != null)
            resultsPageRoot.SetActive(false);

        if (rewardsPageRoot != null)
            rewardsPageRoot.SetActive(true);

        RefreshRewardsPage();
    }

    public void CloseImmediately()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void RefreshVisiblePage()
    {
        if (resultsPageRoot != null && resultsPageRoot.activeSelf)
            RefreshResultsPage();

        if (rewardsPageRoot != null && rewardsPageRoot.activeSelf)
            RefreshRewardsPage();
    }

    private void RefreshResultsPage()
    {
        LuckyShotActiveSession session = GetBestSessionSnapshot();

        if (resultsTitleText != null)
            resultsTitleText.text = session.rewardGranted ? "RESULTS - TARGET HIT" : "RESULTS";

        bool board1Hit = session.rewardGranted && session.lastHitBoardNumber == 1;
        bool board2Hit = session.rewardGranted && session.lastHitBoardNumber == 2;
        bool board3Hit = session.rewardGranted && session.lastHitBoardNumber == 3;

        if (board1ResultText != null)
            board1ResultText.text = "BOARD 1: " + (board1Hit ? "1/1" : "0/1");

        if (board2ResultText != null)
            board2ResultText.text = "BOARD 2: " + (board2Hit ? "1/1" : "0/1");

        if (board3ResultText != null)
            board3ResultText.text = "BOARD 3: " + (board3Hit ? "1/1" : "0/1");

        if (resultsInfoText != null)
        {
            if (session.rewardGranted)
            {
                resultsInfoText.text =
                    "Winning Board: " + session.lastHitBoardNumber + "\n" +
                    "Winning Slot: " + SafeSlot(session.lastHitSlotId) + "\n" +
                    "Reward Weight: " + Mathf.Max(0, lastResolvedResult.rewardWeight);
            }
            else
            {
                resultsInfoText.text =
                    "No winning target hit.\n" +
                    "Last Slot: " + SafeSlot(session.lastHitSlotId) + "\n" +
                    "Extra Shot Used: " + (session.extraAdShotUsed ? "YES" : "NO");
            }
        }

        bool canOfferExtraShot = CanOfferExtraShot(session);

        if (watchAdButton != null)
            watchAdButton.gameObject.SetActive(canOfferExtraShot);

        if (watchAdButton != null)
            watchAdButton.interactable = canOfferExtraShot && !isBusy;

        if (watchAdButtonText != null)
            watchAdButtonText.text = isBusy ? "LOADING..." : "WATCH AD FOR EXTRA SHOT";

        if (nextButton != null)
            nextButton.interactable = !isBusy;
    }

    private void RefreshRewardsPage()
    {
        LuckyShotActiveSession session = GetBestSessionSnapshot();

        if (rewardsTitleText != null)
            rewardsTitleText.text = session.rewardGranted ? "REWARDS" : "NO REWARDS";

        if (rewardsBodyText != null)
        {
            if (session.rewardGranted)
            {
                rewardsBodyText.text =
                    "Reward Granted\n" +
                    "Board: " + session.lastHitBoardNumber + "\n" +
                    "Slot: " + SafeSlot(session.lastHitSlotId) + "\n" +
                    "Reward Weight: " + Mathf.Max(0, lastResolvedResult.rewardWeight) + "\n" +
                    "Label: " + SafeLabel(lastResolvedResult.rewardLabel);
            }
            else
            {
                rewardsBodyText.text =
                    "No reward obtained in this Lucky Shot session.\n" +
                    "You can return to Main Menu.";
            }
        }

        if (backToMenuButton != null)
            backToMenuButton.interactable = !isBusy;
    }

    private async void OnPressWatchAd()
    {
        if (isBusy)
            return;

        ResolveReferences();

        LuckyShotActiveSession session = GetBestSessionSnapshot();
        if (!CanOfferExtraShot(session))
            return;

        isBusy = true;
        RefreshResultsPage();

        try
        {
            bool adCompleted = await SimulateOrRunRewardedAdAsync();
            if (!adCompleted)
            {
                SetFeedback("Rewarded ad not completed.");
                return;
            }

            bool granted = false;

            if (sessionRuntime != null)
                granted = await sessionRuntime.GrantAdExtraShotAsync(CancellationToken.None);

            if (!granted)
                granted = await ReviveFailedSessionAndGrantExtraShotAsync();

            if (!granted)
            {
                SetFeedback("Unable to grant extra Lucky Shot.");
                return;
            }

            SetFeedback("Extra Lucky Shot granted.");

            CloseImmediately();

            if (gameplayController != null && sessionRuntime != null && sessionRuntime.HasActiveSession)
                gameplayController.EnsurePlayableStateFromSession(sessionRuntime.CurrentSession);

            hasResolvedResult = false;
        }
        finally
        {
            isBusy = false;
            RefreshVisiblePage();
        }
    }

    private void OnPressNext()
    {
        OpenRewardsPage();
    }

    private void OnPressBackToMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning("[LuckyShotEndPanelUI] Main menu scene name is empty.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private bool CanOfferExtraShot(LuckyShotActiveSession session)
    {
        if (session.rewardGranted)
            return false;

        if (session.extraAdShotUsed)
            return false;

        return true;
    }

    private LuckyShotActiveSession GetBestSessionSnapshot()
    {
        if (sessionRuntime != null)
        {
            LuckyShotActiveSession runtimeSession = sessionRuntime.CurrentSession;
            if (runtimeSession.sessionId != null || runtimeSession.lastHitSlotId != null || runtimeSession.remainingShots >= 0)
                return runtimeSession;
        }

        if (hasResolvedResult)
            return lastResolvedSession;

        return default;
    }

    private async Task<bool> SimulateOrRunRewardedAdAsync()
    {
#if UNITY_EDITOR
        if (autoGrantExtraShotInEditor)
        {
            await Task.Delay(150);
            return true;
        }
#endif
        // Qui collegherai il tuo vero Rewarded Ads SDK.
        // Per ora rimane comportamento dev-safe.
        await Task.Delay(150);
        return true;
    }

    private async Task<bool> ReviveFailedSessionAndGrantExtraShotAsync()
    {
        if (sessionRuntime == null)
            return false;

        Type runtimeType = sessionRuntime.GetType();
        FieldInfo currentSessionField = runtimeType.GetField("currentSession", BindingFlags.Instance | BindingFlags.NonPublic);
        if (currentSessionField == null)
            return false;

        object boxedSession = currentSessionField.GetValue(sessionRuntime);
        if (boxedSession == null)
            return false;

        Type sessionType = boxedSession.GetType();

        bool rewardGranted = GetStructBool(sessionType, boxedSession, "rewardGranted");
        bool extraAdShotUsed = GetStructBool(sessionType, boxedSession, "extraAdShotUsed");
        int remainingShots = GetStructInt(sessionType, boxedSession, "remainingShots");

        if (rewardGranted || extraAdShotUsed)
            return false;

        SetStructBool(sessionType, boxedSession, "hasActiveSession", true);
        SetStructBool(sessionType, boxedSession, "extraAdShotUsed", true);
        SetStructBool(sessionType, boxedSession, "shotAlreadyTaken", false);
        SetStructBool(sessionType, boxedSession, "rewardGranted", false);
        SetStructInt(sessionType, boxedSession, "remainingShots", Mathf.Max(1, remainingShots));
        SetStructInt(sessionType, boxedSession, "lastHitBoardNumber", 0);
        SetStructString(sessionType, boxedSession, "lastHitSlotId", string.Empty);

        MethodInfo saveMethod = runtimeType.GetMethod("SaveSessionToBackendAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        if (saveMethod == null)
            return false;

        object invoked = saveMethod.Invoke(sessionRuntime, new object[] { boxedSession, CancellationToken.None });
        bool saveOk = await AwaitBoolResult(invoked);
        if (!saveOk)
            return false;

        currentSessionField.SetValue(sessionRuntime, boxedSession);

        MethodInfo refreshPreviewMethod = runtimeType.GetMethod("RefreshPreview", BindingFlags.Instance | BindingFlags.Public);
        refreshPreviewMethod?.Invoke(sessionRuntime, null);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotEndPanelUI] ReviveFailedSessionAndGrantExtraShotAsync -> session revived for extra shot.",
                this);
        }

        return true;
    }

    private static bool GetStructBool(Type structType, object boxedStruct, string fieldName)
    {
        FieldInfo field = structType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(boxedStruct);
    }

    private static int GetStructInt(Type structType, object boxedStruct, string fieldName)
    {
        FieldInfo field = structType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || field.FieldType != typeof(int))
            return 0;

        return (int)field.GetValue(boxedStruct);
    }

    private static void SetStructBool(Type structType, object boxedStruct, string fieldName, bool value)
    {
        FieldInfo field = structType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(bool))
            field.SetValue(boxedStruct, value);
    }

    private static void SetStructInt(Type structType, object boxedStruct, string fieldName, int value)
    {
        FieldInfo field = structType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(int))
            field.SetValue(boxedStruct, value);
    }

    private static void SetStructString(Type structType, object boxedStruct, string fieldName, string value)
    {
        FieldInfo field = structType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(string))
            field.SetValue(boxedStruct, value ?? string.Empty);
    }

    private static async Task<bool> AwaitBoolResult(object invoked)
    {
        if (invoked is Task<bool> boolTask)
            return await boolTask;

        if (invoked is Task task)
        {
            await task;

            PropertyInfo resultProperty = task.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
            if (resultProperty != null && resultProperty.PropertyType == typeof(bool))
                return (bool)resultProperty.GetValue(task);

            return true;
        }

        return false;
    }

    private void SetFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message ?? string.Empty;

        if (verboseLogs && !string.IsNullOrWhiteSpace(message))
            Debug.Log("[LuckyShotEndPanelUI] Feedback -> " + message, this);
    }

    private string SafeSlot(string slotId)
    {
        return string.IsNullOrWhiteSpace(slotId) ? "-" : slotId;
    }

    private string SafeLabel(string label)
    {
        return string.IsNullOrWhiteSpace(label) ? "-" : label;
    }

    private void ResolveReferences()
    {
        if (sessionRuntime == null)
            sessionRuntime = LuckyShotSessionRuntime.Instance;

        if (gameplayController == null)
            gameplayController = LuckyShotGameplayController.Instance;

#if UNITY_2023_1_OR_NEWER
        if (sessionRuntime == null)
            sessionRuntime = FindFirstObjectByType<LuckyShotSessionRuntime>();

        if (gameplayController == null)
            gameplayController = FindFirstObjectByType<LuckyShotGameplayController>();
#else
        if (sessionRuntime == null)
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();

        if (gameplayController == null)
            gameplayController = FindObjectOfType<LuckyShotGameplayController>();
#endif
    }

    private void Subscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionLoaded += HandleSessionLoaded;

        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionPreviewChanged += HandleSessionPreviewChanged;

        sessionRuntime.SessionResolved -= HandleSessionResolved;
        sessionRuntime.SessionResolved += HandleSessionResolved;

        sessionRuntime.FeedbackRaised -= HandleFeedbackRaised;
        sessionRuntime.FeedbackRaised += HandleFeedbackRaised;
    }

    private void Unsubscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionResolved -= HandleSessionResolved;
        sessionRuntime.FeedbackRaised -= HandleFeedbackRaised;
    }
}