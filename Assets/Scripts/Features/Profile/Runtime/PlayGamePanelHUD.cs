using TMPro;
using UnityEngine;

public class PlayGamePanelHUD : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Main HUD Texts")]
    [SerializeField] private TMP_Text currentNameText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text softCurrencyText;
    [SerializeField] private TMP_Text premiumCurrencyText;

    [Header("Formatting")]
    [SerializeField] private string emptyNameFallback = "PLAYER";
    [SerializeField] private string rankPrefix = "RANK ";
    [SerializeField] private string rankSuffix = " LP";
    [SerializeField] private string softPrefix = "";
    [SerializeField] private string softSuffix = "";
    [SerializeField] private string premiumPrefix = "";
    [SerializeField] private string premiumSuffix = "";

    [Header("Behavior")]
    [SerializeField] private bool logDebug = true;

    private bool subscribed;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
        RefreshUi();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        ResolveDependencies();
        RefreshUi();
    }

    public void RefreshUi()
    {
        ResolveDependencies();

        string currentName = emptyNameFallback;
        int rankedLp = 0;
        int softCurrency = 0;
        int premiumCurrency = 0;

        if (profileManager != null)
        {
            if (!string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName))
                currentName = profileManager.ActiveDisplayName.Trim();

            rankedLp = Mathf.Max(0, profileManager.ActiveRankedLp);

            if (profileManager.ActiveProfile != null)
            {
                softCurrency = Mathf.Max(0, profileManager.ActiveProfile.softCurrency);
                premiumCurrency = Mathf.Max(0, profileManager.ActiveProfile.premiumCurrency);
            }
        }

        if (currentNameText != null)
            currentNameText.text = currentName.ToUpperInvariant();

        if (rankText != null)
            rankText.text = rankPrefix + rankedLp + rankSuffix;

        if (softCurrencyText != null)
            softCurrencyText.text = softPrefix + softCurrency + softSuffix;

        if (premiumCurrencyText != null)
            premiumCurrencyText.text = premiumPrefix + premiumCurrency + premiumSuffix;

        if (logDebug)
        {
            Debug.Log(
                "[PlayGamePanelHUD] RefreshUi -> " +
                "Name=" + currentName +
                " | RankedLp=" + rankedLp +
                " | Soft=" + softCurrency +
                " | Premium=" + premiumCurrency,
                this
            );
        }
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        RefreshUi();
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;
    }

    private void Subscribe()
    {
        if (subscribed || profileManager == null)
            return;

        profileManager.OnActiveProfileChanged += HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged += HandleProfileChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || profileManager == null)
            return;

        profileManager.OnActiveProfileChanged -= HandleProfileChanged;
        profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
        subscribed = false;
    }
}
