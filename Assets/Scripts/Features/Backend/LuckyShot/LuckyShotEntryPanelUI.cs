using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LuckyShotEntryPanelUI : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject panelRoot;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text tokenCountText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button debugGrantTokenButton;

    [Header("Options")]
    [SerializeField] private int debugGrantAmount = 1;
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool verboseLogs = true;

    private LuckyShotEntryService entryService;

    private void Awake()
    {
        ResolveEntryService();

        if (playButton != null)
        {
            playButton.onClick.RemoveListener(OnPressPlay);
            playButton.onClick.AddListener(OnPressPlay);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshNow);
            refreshButton.onClick.AddListener(RefreshNow);
        }

        if (debugGrantTokenButton != null)
        {
            debugGrantTokenButton.onClick.RemoveListener(OnPressDebugGrant);
            debugGrantTokenButton.onClick.AddListener(OnPressDebugGrant);
        }

        if (titleText != null)
            titleText.text = "LUCKY SHOT";

        if (descriptionText != null)
            descriptionText.gameObject.SetActive(false);

        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotEntryPanelUI] Awake -> " +
                "EntryService=" + (entryService != null) +
                " | PanelRoot=" + GetName(panelRoot) +
                " | PlayButton=" + GetName(playButton) +
                " | CloseButton=" + GetName(closeButton) +
                " | RefreshButton=" + GetName(refreshButton) +
                " | DebugGrantButton=" + GetName(debugGrantTokenButton),
                this);
        }
    }

    private void OnEnable()
    {
        ResolveEntryService();
        Subscribe();

        if (refreshOnEnable)
            RefreshNow();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void OpenPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        RefreshNow();
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void RefreshNow()
    {
        ResolveEntryService();

        if (entryService == null)
        {
            ApplyTokens(0);
            SetPlayInteractable(false);

            if (verboseLogs)
                Debug.LogWarning("[LuckyShotEntryPanelUI] RefreshNow -> LuckyShotEntryService missing.", this);

            return;
        }

        entryService.RefreshTokens();
    }

    private async void OnPressPlay()
    {
        ResolveEntryService();

        if (entryService == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotEntryPanelUI] OnPressPlay -> LuckyShotEntryService missing.", this);

            SetPlayInteractable(false);
            return;
        }

        bool ok = await entryService.TryEnterLuckyShotAsync();

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotEntryPanelUI] OnPressPlay -> Result=" + ok +
                " | CachedTokens=" + entryService.CachedTokens,
                this);
        }
    }

    private async void OnPressDebugGrant()
    {
        ResolveEntryService();

        if (entryService == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotEntryPanelUI] OnPressDebugGrant -> LuckyShotEntryService missing.", this);

            return;
        }

        await entryService.DebugGrantTokensAsync(Mathf.Max(1, debugGrantAmount));
    }

    private void Subscribe()
    {
        if (entryService == null)
            return;

        entryService.TokensChanged -= HandleTokensChanged;
        entryService.TokensChanged += HandleTokensChanged;

        entryService.BusyStateChanged -= HandleBusyStateChanged;
        entryService.BusyStateChanged += HandleBusyStateChanged;

        entryService.FeedbackRaised -= HandleFeedbackRaised;
        entryService.FeedbackRaised += HandleFeedbackRaised;

        entryService.EntryConsumedAndGameplayStarted -= HandleEntryStarted;
        entryService.EntryConsumedAndGameplayStarted += HandleEntryStarted;
    }

    private void Unsubscribe()
    {
        if (entryService == null)
            return;

        entryService.TokensChanged -= HandleTokensChanged;
        entryService.BusyStateChanged -= HandleBusyStateChanged;
        entryService.FeedbackRaised -= HandleFeedbackRaised;
        entryService.EntryConsumedAndGameplayStarted -= HandleEntryStarted;
    }

    private void HandleTokensChanged(int tokenCount)
    {
        ApplyTokens(tokenCount);
    }

    private void HandleBusyStateChanged(bool isBusy)
    {
        int currentTokens = entryService != null ? entryService.CachedTokens : 0;
        SetPlayInteractable(!isBusy && currentTokens > 0);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotEntryPanelUI] HandleBusyStateChanged -> Busy=" + isBusy +
                " | Tokens=" + currentTokens,
                this);
        }
    }

    private void HandleFeedbackRaised(string message)
    {
        if (verboseLogs && !string.IsNullOrWhiteSpace(message))
            Debug.Log("[LuckyShotEntryPanelUI] Feedback -> " + message, this);
    }

    private void HandleEntryStarted()
    {
        if (verboseLogs)
            Debug.Log("[LuckyShotEntryPanelUI] HandleEntryStarted -> Lucky Shot start acknowledged.", this);
    }

    private void ApplyTokens(int tokenCount)
    {
        int safeTokens = Mathf.Max(0, tokenCount);

        if (tokenCountText != null)
            tokenCountText.text = "AVAILABLE TOKEN: " + safeTokens;

        bool canPlay = entryService != null && !entryService.IsBusy && safeTokens > 0;
        SetPlayInteractable(canPlay);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotEntryPanelUI] ApplyTokens -> Tokens=" + safeTokens +
                " | CanPlay=" + canPlay,
                this);
        }
    }

    private void SetPlayInteractable(bool value)
    {
        if (playButton != null)
            playButton.interactable = value;
    }

    private void ResolveEntryService()
    {
        if (entryService != null)
            return;

        entryService = LuckyShotEntryService.Instance;

#if UNITY_2023_1_OR_NEWER
        if (entryService == null)
            entryService = FindFirstObjectByType<LuckyShotEntryService>();
#else
        if (entryService == null)
            entryService = FindObjectOfType<LuckyShotEntryService>();
#endif
    }

    private string GetName(UnityEngine.Object target)
    {
        return target == null ? "<null>" : target.name;
    }
}