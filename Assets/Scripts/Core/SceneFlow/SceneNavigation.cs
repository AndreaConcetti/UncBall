using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigation : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private bool useFusionAwareShutdown = true;

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;

        if (useFusionAwareShutdown && MatchRuntimeConfig.Instance != null && MatchRuntimeConfig.Instance.IsOnlineMatch)
        {
            if (PhotonFusionSessionController.Instance != null)
            {
                PhotonFusionSessionController.Instance.ShutdownSessionAndReturnToMenu(true);
                return;
            }
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}