using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private MatchRuntimeConfig matchRuntimeConfig;
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Scene References")]
    [SerializeField] private string multiplayerSceneName = "MultiplayerMenu";

    [Header("Multiplayer Defaults")]
    [SerializeField] private string defaultRemotePlayerName = "Remote Player";
    [SerializeField] private bool defaultMultiplayerIsRanked = false;
    [SerializeField] private int defaultMultiplayerPointsToWin = 16;
    [SerializeField] private float defaultMultiplayerMatchDuration = 180f;
    [SerializeField] private MatchMode defaultMultiplayerMatchMode = MatchMode.ScoreTarget;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private void Start()
    {
        ResolveDependencies();
    }

    private void ResolveDependencies()
    {
        if (matchRuntimeConfig == null)
            matchRuntimeConfig = MatchRuntimeConfig.Instance;

        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;
    }

    public void OpenMultiplayerMode()
    {
        ResolveDependencies();

        if (matchRuntimeConfig != null)
        {
            matchRuntimeConfig.ConfigureMultiplayerMode(
                defaultMultiplayerMatchMode,
                defaultMultiplayerPointsToWin,
                defaultMultiplayerMatchDuration,
                GetResolvedLocalProfileId(),
                GetResolvedDefaultLocalDisplayName(),
                defaultRemotePlayerName,
                defaultMultiplayerIsRanked,
                MatchRuntimeConfig.MatchSessionType.OnlinePrivate,
                MatchRuntimeConfig.MatchAuthorityType.HostClient,
                MatchRuntimeConfig.LocalParticipantSlot.Player1,
                false,
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false,
                false
            );
        }

        if (logDebug)
            Debug.Log("[MainMenu] OpenMultiplayerMode", this);

        Time.timeScale = 1f;
        SceneManager.LoadScene(multiplayerSceneName);
    }

    private string GetResolvedLocalProfileId()
    {
        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveProfileId))
            return profileManager.ActiveProfileId;

        return "local_player_1";
    }

    private string GetResolvedDefaultLocalDisplayName()
    {
        if (profileManager != null && !string.IsNullOrWhiteSpace(profileManager.ActiveDisplayName))
            return profileManager.ActiveDisplayName;

        return "Player 1";
    }
}