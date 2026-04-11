using UnityEngine;

public class OnlinePostMatchPanelsController : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject postGameRoot;
    [SerializeField] private GameObject rewardsObtainedRoot;

    [Header("Optional")]
    [SerializeField] private OnlineRewardsObtainedPresenter rewardsPresenter;
    [SerializeField] private bool hideRewardsOnAwake = true;
    [SerializeField] private bool logDebug = true;

    private void Awake()
    {
        ResolveDependencies();

        if (hideRewardsOnAwake && rewardsObtainedRoot != null)
            rewardsObtainedRoot.SetActive(false);

        if (logDebug)
        {
            Debug.Log(
                "[OnlinePostMatchPanelsController] Awake -> " +
                "PostGameRoot=" + SafeName(postGameRoot) +
                " | RewardsRoot=" + SafeName(rewardsObtainedRoot) +
                " | RewardsPresenter=" + SafeName(rewardsPresenter),
                this
            );
        }
    }

    public void OpenRewardsObtained()
    {
        ResolveDependencies();

        if (logDebug)
            Debug.Log("[OnlinePostMatchPanelsController] OpenRewardsObtained invoked.", this);

        if (postGameRoot != null)
            postGameRoot.SetActive(false);

        if (rewardsObtainedRoot != null)
            rewardsObtainedRoot.SetActive(true);

        if (rewardsPresenter != null)
            rewardsPresenter.ShowLatest();
        else
            Debug.LogWarning("[OnlinePostMatchPanelsController] Rewards presenter is NULL.", this);
    }

    public void ReturnToPostGame()
    {
        if (logDebug)
            Debug.Log("[OnlinePostMatchPanelsController] ReturnToPostGame invoked.", this);

        if (rewardsObtainedRoot != null)
            rewardsObtainedRoot.SetActive(false);

        if (postGameRoot != null)
            postGameRoot.SetActive(true);
    }

    private void ResolveDependencies()
    {
        if (rewardsPresenter == null && rewardsObtainedRoot != null)
            rewardsPresenter = rewardsObtainedRoot.GetComponentInChildren<OnlineRewardsObtainedPresenter>(true);

#if UNITY_2023_1_OR_NEWER
        if (rewardsPresenter == null)
            rewardsPresenter = FindFirstObjectByType<OnlineRewardsObtainedPresenter>(FindObjectsInactive.Include);
#else
        if (rewardsPresenter == null)
            rewardsPresenter = FindObjectOfType<OnlineRewardsObtainedPresenter>(true);
#endif
    }

    private string SafeName(Object obj)
    {
        return obj == null ? "<null>" : obj.name;
    }
}
