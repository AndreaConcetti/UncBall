using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerProfileManager profileManager;

    [Header("Scene References")]
    [SerializeField] private string multiplayerSceneName = "MultiplayerMenu";

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    private void Start()
    {
        ResolveDependencies();
    }

    private void ResolveDependencies()
    {
        if (profileManager == null)
            profileManager = PlayerProfileManager.Instance;
    }

    public void OpenMultiplayerMode()
    {
        ResolveDependencies();

        if (logDebug)
        {
            Debug.Log(
                "[MainMenu] OpenMultiplayerMode -> " +
                "ProfileId=" + (profileManager != null ? profileManager.ActiveProfileId : "NULL") +
                " | DisplayName=" + (profileManager != null ? profileManager.ActiveDisplayName : "NULL"),
                this
            );
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(multiplayerSceneName);
    }
}