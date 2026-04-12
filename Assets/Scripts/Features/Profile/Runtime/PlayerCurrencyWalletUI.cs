using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerCurrencyWalletUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Soft Currency UI")]
    [SerializeField] private List<TMP_Text> softCurrencyTexts = new List<TMP_Text>();
    [SerializeField] private string softPrefix = "";
    [SerializeField] private string softSuffix = "";

    [Header("Premium Currency UI")]
    [SerializeField] private List<TMP_Text> premiumCurrencyTexts = new List<TMP_Text>();
    [SerializeField] private string premiumPrefix = "";
    [SerializeField] private string premiumSuffix = "";

    [Header("Behavior")]
    [SerializeField] private bool refreshOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private bool subscribed;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();

        if (refreshOnEnable)
            RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void RefreshUI()
    {
        ResolveDependencies();

        int soft = 0;
        int premium = 0;

        if (profileManager != null && profileManager.ActiveProfile != null)
        {
            soft = Mathf.Max(0, profileManager.ActiveProfile.softCurrency);
            premium = Mathf.Max(0, profileManager.ActiveProfile.premiumCurrency);
        }

        RefreshTexts(softCurrencyTexts, softPrefix, soft, softSuffix);
        RefreshTexts(premiumCurrencyTexts, premiumPrefix, premium, premiumSuffix);

        if (logDebug)
        {
            Debug.Log(
                "[PlayerCurrencyWalletUI] RefreshUI -> Soft=" + soft + " | Premium=" + premium,
                this
            );
        }
    }

    private void RefreshTexts(List<TMP_Text> targets, string prefix, int value, string suffix)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                targets[i].text = prefix + value + suffix;
        }
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        RefreshUI();
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
