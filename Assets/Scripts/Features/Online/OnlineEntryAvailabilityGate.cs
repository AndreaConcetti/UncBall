using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OnlineEntryAvailabilityGate : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button onlineButton;
    [SerializeField] private GameObject offlinePopupRoot;
    [SerializeField] private TMP_Text offlinePopupText;

    [Header("Optional Visuals")]
    [SerializeField] private CanvasGroup onlineButtonCanvasGroup;
    [SerializeField] private float disabledAlpha = 0.45f;
    [SerializeField] private float enabledAlpha = 1f;

    [Header("Popup")]
    [SerializeField] private bool showPopupWhenPressedWithoutInternet = true;
    [SerializeField] private string noInternetMessage = "NO INTERNET CONNECTION";
    [SerializeField] private float popupDuration = 2f;

    [Header("Behaviour")]
    [SerializeField] private bool checkContinuously = true;
    [SerializeField] private bool disableButtonInteractable = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool lastInternetAvailable;
    private float popupHideTimer;
    private bool popupVisible;

    private void Awake()
    {
        ResolveReferences();
        HidePopupImmediate();
        RefreshAvailability(forceLog: false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshAvailability(forceLog: false);

        if (onlineButton != null)
        {
            onlineButton.onClick.RemoveListener(HandleOnlineButtonClicked);
            onlineButton.onClick.AddListener(HandleOnlineButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (onlineButton != null)
            onlineButton.onClick.RemoveListener(HandleOnlineButtonClicked);
    }

    private void Update()
    {
        if (checkContinuously)
            RefreshAvailability(forceLog: false);

        if (!popupVisible)
            return;

        popupHideTimer -= Time.unscaledDeltaTime;
        if (popupHideTimer <= 0f)
            HidePopupImmediate();
    }

    private void ResolveReferences()
    {
        if (onlineButton == null)
            onlineButton = GetComponent<Button>();

        if (onlineButtonCanvasGroup == null && onlineButton != null)
            onlineButtonCanvasGroup = onlineButton.GetComponent<CanvasGroup>();
    }

    private void HandleOnlineButtonClicked()
    {
        bool internetAvailable = HasInternetConnection();

        if (internetAvailable)
            return;

        if (logDebug)
            Debug.Log("[OnlineEntryAvailabilityGate] Online button pressed without internet.", this);

        if (showPopupWhenPressedWithoutInternet)
            ShowOfflinePopup();
    }

    private void RefreshAvailability(bool forceLog)
    {
        bool internetAvailable = HasInternetConnection();

        if (disableButtonInteractable && onlineButton != null)
            onlineButton.interactable = internetAvailable;

        if (onlineButtonCanvasGroup != null)
            onlineButtonCanvasGroup.alpha = internetAvailable ? enabledAlpha : disabledAlpha;

        if (internetAvailable && popupVisible)
            HidePopupImmediate();

        if (forceLog || internetAvailable != lastInternetAvailable)
        {
            if (logDebug)
            {
                Debug.Log(
                    "[OnlineEntryAvailabilityGate] RefreshAvailability -> InternetAvailable=" + internetAvailable,
                    this
                );
            }

            lastInternetAvailable = internetAvailable;
        }
    }

    private bool HasInternetConnection()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
    }

    private void ShowOfflinePopup()
    {
        if (offlinePopupRoot != null)
            offlinePopupRoot.SetActive(true);

        if (offlinePopupText != null)
            offlinePopupText.text = noInternetMessage;

        popupVisible = true;
        popupHideTimer = Mathf.Max(0.1f, popupDuration);
    }

    private void HidePopupImmediate()
    {
        if (offlinePopupRoot != null)
            offlinePopupRoot.SetActive(false);

        popupVisible = false;
        popupHideTimer = 0f;
    }
}