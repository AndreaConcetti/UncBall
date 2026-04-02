using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigation : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private bool useOnlineFlowAwareReturn = true;

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;

        if (useOnlineFlowAwareReturn && OnlineFlowController.Instance != null)
        {
            OnlineFlowState state = OnlineFlowController.Instance.CurrentState;

            bool onlineSessionRelevant =
                state == OnlineFlowState.Queueing ||
                state == OnlineFlowState.MatchAssigned ||
                state == OnlineFlowState.JoiningSession ||
                state == OnlineFlowState.LoadingGameplay ||
                state == OnlineFlowState.InMatch ||
                state == OnlineFlowState.EndingMatch ||
                state == OnlineFlowState.ReturningToMenu;

            if (onlineSessionRelevant)
            {
                OnlineFlowController.Instance.ReturnToMenuFromMatch(true);
                return;
            }
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}