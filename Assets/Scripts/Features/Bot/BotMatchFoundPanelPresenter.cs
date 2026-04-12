using TMPro;
using UnityEngine;

public sealed class BotMatchFoundPanelPresenter : MonoBehaviour
{
    [Header("Runtime")]
    [SerializeField] private BotSessionRuntime botSessionRuntime;

    [Header("Opponent Slot Texts")]
    [SerializeField] private TMP_Text opponentNameText;
    [SerializeField] private TMP_Text opponentWinLoseText;
    [SerializeField] private TMP_Text opponentWinRateText;

    [Header("Options")]
    [SerializeField] private bool uppercaseName = true;
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private bool clearWhenNoBot = false;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private void OnEnable()
    {
        if (refreshOnEnable)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        BotSessionRuntime runtime = ResolveRuntime();
        if (runtime == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[BotMatchFoundPanelPresenter] No BotSessionRuntime found.", this);
            }

            if (clearWhenNoBot)
            {
                ClearTexts();
            }

            return;
        }

        OpponentPresentationProfile profile = runtime.CurrentOpponentPresentation;
        if (profile == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[BotMatchFoundPanelPresenter] No active bot opponent presentation found.", this);
            }

            if (clearWhenNoBot)
            {
                ClearTexts();
            }

            return;
        }

        string displayName = profile.DisplayName;
        if (uppercaseName && !string.IsNullOrWhiteSpace(displayName))
        {
            displayName = displayName.ToUpperInvariant();
        }

        int wins = Mathf.Clamp(profile.TotalWins, 0, profile.TotalMatches);
        int losses = Mathf.Max(0, profile.TotalMatches - wins);

        SetText(opponentNameText, displayName);
        SetText(opponentWinLoseText, $"{wins} W - {losses} L");
        SetText(opponentWinRateText, $"{Mathf.Clamp(profile.WinRatePercent, 0, 100)}%");

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[BotMatchFoundPanelPresenter] Refreshed bot match-found panel -> " +
                $"Name={displayName} | W/L={wins}W-{losses}L | WR={profile.WinRatePercent}%",
                this);
        }
    }

    public void ClearTexts()
    {
        SetText(opponentNameText, string.Empty);
        SetText(opponentWinLoseText, string.Empty);
        SetText(opponentWinRateText, string.Empty);

        if (enableDebugLogs)
        {
            Debug.Log("[BotMatchFoundPanelPresenter] Cleared texts.", this);
        }
    }

    private BotSessionRuntime ResolveRuntime()
    {
        if (botSessionRuntime != null)
        {
            return botSessionRuntime;
        }

        return BotSessionRuntime.Instance;
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target == null)
        {
            return;
        }

        target.text = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }
}
