using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChestShopPanel : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;
    [SerializeField] private PlayerChestSlotInventory chestInventory;
    [SerializeField] private ChestShopFeedbackPanel feedbackPanel;

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Wallet UI - Soft")]
    [SerializeField] private List<TMP_Text> softCurrencyTexts = new List<TMP_Text>();
    [SerializeField] private string softPrefix = "";
    [SerializeField] private string softSuffix = "";

    [Header("Wallet UI - Premium")]
    [SerializeField] private List<TMP_Text> premiumCurrencyTexts = new List<TMP_Text>();
    [SerializeField] private string premiumPrefix = "";
    [SerializeField] private string premiumSuffix = "";

    [Header("Soft Purchase Buttons")]
    [SerializeField] private Button buyCommonSoftButton;
    [SerializeField] private Button buyRareSoftButton;
    [SerializeField] private Button buyEpicSoftButton;
    [SerializeField] private Button buyRandomSoftButton;

    [Header("Premium Purchase Buttons")]
    [SerializeField] private Button buyRarePremiumButton;
    [SerializeField] private Button buyEpicPremiumButton;
    [SerializeField] private Button buyLegendaryPremiumButton;

    [Header("Price Labels")]
    [SerializeField] private TMP_Text buyCommonSoftPriceText;
    [SerializeField] private TMP_Text buyRareSoftPriceText;
    [SerializeField] private TMP_Text buyEpicSoftPriceText;
    [SerializeField] private TMP_Text buyRandomSoftPriceText;
    [SerializeField] private TMP_Text buyRarePremiumPriceText;
    [SerializeField] private TMP_Text buyEpicPremiumPriceText;
    [SerializeField] private TMP_Text buyLegendaryPremiumPriceText;

    [Header("Fallback Feedback Text")]
    [SerializeField] private TMP_Text feedbackText;

    [Header("Feedback Messages")]
    [SerializeField] private string purchaseSuccessMessage = "PURCHASE COMPLETED";
    [SerializeField] private string notEnoughSoftMessage = "NOT ENOUGH SOFT CURRENCY";
    [SerializeField] private string notEnoughPremiumMessage = "NOT ENOUGH PREMIUM CURRENCY";
    [SerializeField] private string purchaseFailedMessage = "PURCHASE FAILED";
    [SerializeField] private string queuedChestMessage = "CHEST ADDED TO QUEUE";

    [Header("Soft Prices")]
    [SerializeField] private int commonChestSoftPrice = 100;
    [SerializeField] private int rareChestSoftPrice = 250;
    [SerializeField] private int epicChestSoftPrice = 600;
    [SerializeField] private int randomChestSoftPrice = 1200;

    [Header("Premium Prices")]
    [SerializeField] private int rareChestPremiumPrice = 40;
    [SerializeField] private int epicChestPremiumPrice = 90;
    [SerializeField] private int legendaryChestPremiumPrice = 160;

    [Header("Behavior")]
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool autoWireButtons = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private bool subscribed;

    private void Awake()
    {
        ResolveDependencies();
        WireButtons();
        RefreshStaticPriceTexts();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();
        WireButtons();
        RefreshStaticPriceTexts();

        if (refreshOnEnable)
            RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnwireButtons();
    }

    public void OpenPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        RefreshUI();
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void BuyCommonWithSoft()
    {
        TryBuyChestWithSoft(ChestType.GuaranteedCommon, commonChestSoftPrice);
    }

    public void BuyRareWithSoft()
    {
        TryBuyChestWithSoft(ChestType.GuaranteedRare, rareChestSoftPrice);
    }

    public void BuyEpicWithSoft()
    {
        TryBuyChestWithSoft(ChestType.GuaranteedEpic, epicChestSoftPrice);
    }

    public void BuyRandomWithSoft()
    {
        TryBuyChestWithSoft(ChestType.Random, randomChestSoftPrice);
    }

    public void BuyRareWithPremium()
    {
        TryBuyChestWithPremium(ChestType.GuaranteedRare, rareChestPremiumPrice);
    }

    public void BuyEpicWithPremium()
    {
        TryBuyChestWithPremium(ChestType.GuaranteedEpic, epicChestPremiumPrice);
    }

    public void BuyLegendaryWithPremium()
    {
        TryBuyChestWithPremium(ChestType.GuaranteedLegendary, legendaryChestPremiumPrice);
    }

    public void RefreshUI()
    {
        ResolveDependencies();

        int soft = GetSoftCurrency();
        int premium = GetPremiumCurrency();

        RefreshWalletTexts(softCurrencyTexts, softPrefix, soft, softSuffix);
        RefreshWalletTexts(premiumCurrencyTexts, premiumPrefix, premium, premiumSuffix);

        if (buyCommonSoftButton != null)
            buyCommonSoftButton.interactable = soft >= Mathf.Max(0, commonChestSoftPrice);

        if (buyRareSoftButton != null)
            buyRareSoftButton.interactable = soft >= Mathf.Max(0, rareChestSoftPrice);

        if (buyEpicSoftButton != null)
            buyEpicSoftButton.interactable = soft >= Mathf.Max(0, epicChestSoftPrice);

        if (buyRandomSoftButton != null)
            buyRandomSoftButton.interactable = soft >= Mathf.Max(0, randomChestSoftPrice);

        if (buyRarePremiumButton != null)
            buyRarePremiumButton.interactable = premium >= Mathf.Max(0, rareChestPremiumPrice);

        if (buyEpicPremiumButton != null)
            buyEpicPremiumButton.interactable = premium >= Mathf.Max(0, epicChestPremiumPrice);

        if (buyLegendaryPremiumButton != null)
            buyLegendaryPremiumButton.interactable = premium >= Mathf.Max(0, legendaryChestPremiumPrice);

        if (logDebug)
        {
            Debug.Log(
                "[ChestShopPanel] RefreshUI -> " +
                "Soft=" + soft +
                " | Premium=" + premium +
                " | OccupiedSlots=" + (chestInventory != null ? chestInventory.GetOccupiedSlotCount() : -1) +
                " | Queue=" + (chestInventory != null ? chestInventory.GetQueuedChestCount() : -1),
                this
            );
        }
    }

    private void RefreshWalletTexts(List<TMP_Text> targets, string prefix, int value, string suffix)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                targets[i].text = prefix + value + suffix;
        }
    }

    private void RefreshStaticPriceTexts()
    {
        SetPriceText(buyCommonSoftPriceText, commonChestSoftPrice, "SOFT");
        SetPriceText(buyRareSoftPriceText, rareChestSoftPrice, "SOFT");
        SetPriceText(buyEpicSoftPriceText, epicChestSoftPrice, "SOFT");
        SetPriceText(buyRandomSoftPriceText, randomChestSoftPrice, "SOFT");
        SetPriceText(buyRarePremiumPriceText, rareChestPremiumPrice, "PREMIUM");
        SetPriceText(buyEpicPremiumPriceText, epicChestPremiumPrice, "PREMIUM");
        SetPriceText(buyLegendaryPremiumPriceText, legendaryChestPremiumPrice, "PREMIUM");
    }

    private void SetPriceText(TMP_Text target, int value, string currencyLabel)
    {
        if (target != null)
            target.text = value + " " + currencyLabel;
    }

    private void TryBuyChestWithSoft(ChestType chestType, int cost)
    {
        ResolveDependencies();

        if (profileManager == null || chestInventory == null)
        {
            ShowFeedback(purchaseFailedMessage);
            return;
        }

        int safeCost = Mathf.Max(0, cost);
        if (GetSoftCurrency() < safeCost)
        {
            ShowFeedback(notEnoughSoftMessage);
            RefreshUI();
            return;
        }

        bool willBeQueued = chestInventory.GetFirstFreeSlotIndex() < 0;

        profileManager.AddSoftCurrency(-safeCost);

        bool awarded = chestInventory.AwardChest(chestType);
        if (!awarded)
        {
            profileManager.AddSoftCurrency(safeCost);
            ShowFeedback(purchaseFailedMessage);
            RefreshUI();
            return;
        }

        ShowFeedback(willBeQueued ? queuedChestMessage : purchaseSuccessMessage);
        RefreshUI();

        if (logDebug)
        {
            Debug.Log(
                "[ChestShopPanel] Buy with soft -> " +
                "Type=" + chestType +
                " | Cost=" + safeCost +
                " | NewSoft=" + GetSoftCurrency() +
                " | Queued=" + willBeQueued,
                this
            );
        }
    }

    private void TryBuyChestWithPremium(ChestType chestType, int cost)
    {
        ResolveDependencies();

        if (profileManager == null || chestInventory == null)
        {
            ShowFeedback(purchaseFailedMessage);
            return;
        }

        int safeCost = Mathf.Max(0, cost);
        if (GetPremiumCurrency() < safeCost)
        {
            ShowFeedback(notEnoughPremiumMessage);
            RefreshUI();
            return;
        }

        bool willBeQueued = chestInventory.GetFirstFreeSlotIndex() < 0;

        profileManager.AddPremiumCurrency(-safeCost);

        bool awarded = chestInventory.AwardChest(chestType);
        if (!awarded)
        {
            profileManager.AddPremiumCurrency(safeCost);
            ShowFeedback(purchaseFailedMessage);
            RefreshUI();
            return;
        }

        ShowFeedback(willBeQueued ? queuedChestMessage : purchaseSuccessMessage);
        RefreshUI();

        if (logDebug)
        {
            Debug.Log(
                "[ChestShopPanel] Buy with premium -> " +
                "Type=" + chestType +
                " | Cost=" + safeCost +
                " | NewPremium=" + GetPremiumCurrency() +
                " | Queued=" + willBeQueued,
                this
            );
        }
    }

    private int GetSoftCurrency()
    {
        if (profileManager == null || profileManager.ActiveProfile == null)
            return 0;

        return Mathf.Max(0, profileManager.ActiveProfile.softCurrency);
    }

    private int GetPremiumCurrency()
    {
        if (profileManager == null || profileManager.ActiveProfile == null)
            return 0;

        return Mathf.Max(0, profileManager.ActiveProfile.premiumCurrency);
    }

    private void ShowFeedback(string message)
    {
        if (feedbackPanel != null)
        {
            feedbackPanel.Show(message);
            return;
        }

        if (feedbackText != null)
            feedbackText.text = message;
    }

    private void HandleProfileChanged(PlayerProfileRuntimeData _)
    {
        RefreshUI();
    }

    private void HandleChestInventoryChanged()
    {
        RefreshUI();
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;

        if (chestInventory == null)
            chestInventory = PlayerChestSlotInventory.Instance;
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (profileManager != null)
        {
            profileManager.OnActiveProfileChanged -= HandleProfileChanged;
            profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
            profileManager.OnActiveProfileChanged += HandleProfileChanged;
            profileManager.OnActiveProfileDataChanged += HandleProfileChanged;
        }

        if (chestInventory != null)
        {
            chestInventory.OnChestInventoryChanged -= HandleChestInventoryChanged;
            chestInventory.OnChestInventoryChanged += HandleChestInventoryChanged;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (profileManager != null)
        {
            profileManager.OnActiveProfileChanged -= HandleProfileChanged;
            profileManager.OnActiveProfileDataChanged -= HandleProfileChanged;
        }

        if (chestInventory != null)
            chestInventory.OnChestInventoryChanged -= HandleChestInventoryChanged;

        subscribed = false;
    }

    private void WireButtons()
    {
        if (!autoWireButtons)
            return;

        AddButtonListener(buyCommonSoftButton, BuyCommonWithSoft);
        AddButtonListener(buyRareSoftButton, BuyRareWithSoft);
        AddButtonListener(buyEpicSoftButton, BuyEpicWithSoft);
        AddButtonListener(buyRandomSoftButton, BuyRandomWithSoft);
        AddButtonListener(buyRarePremiumButton, BuyRareWithPremium);
        AddButtonListener(buyEpicPremiumButton, BuyEpicWithPremium);
        AddButtonListener(buyLegendaryPremiumButton, BuyLegendaryWithPremium);
    }

    private void UnwireButtons()
    {
        if (!autoWireButtons)
            return;

        RemoveButtonListener(buyCommonSoftButton, BuyCommonWithSoft);
        RemoveButtonListener(buyRareSoftButton, BuyRareWithSoft);
        RemoveButtonListener(buyEpicSoftButton, BuyEpicWithSoft);
        RemoveButtonListener(buyRandomSoftButton, BuyRandomWithSoft);
        RemoveButtonListener(buyRarePremiumButton, BuyRareWithPremium);
        RemoveButtonListener(buyEpicPremiumButton, BuyEpicWithPremium);
        RemoveButtonListener(buyLegendaryPremiumButton, BuyLegendaryWithPremium);
    }

    private void AddButtonListener(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    private void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(callback);
    }
}
