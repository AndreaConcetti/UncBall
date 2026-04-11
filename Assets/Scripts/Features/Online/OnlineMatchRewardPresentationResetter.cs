using UnityEngine;

public class OnlineMatchRewardPresentationResetter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private OnlineMatchPresentationResultStore resultStore;
    [SerializeField] private OnlineRewardsObtainedPresenter rewardsPresenter;

    [Header("Roots To Reset")]
    [SerializeField] private GameObject rewardsObtainedRoot;
    [SerializeField] private GameObject levelUpOverlayRoot;
    [SerializeField] private GameObject postGamePanelRoot;

    [Header("Options")]
    [SerializeField] private bool clearPresentationOnlyOnAwake = true;
    [SerializeField] private bool hideRewardsRootOnAwake = true;
    [SerializeField] private bool hideLevelUpOverlayOnAwake = true;
    [SerializeField] private bool showPostGamePanelRootOnAwake = true;
    [SerializeField] private bool logDebug = false;

    private void Awake()
    {
        ResolveDependencies();

        if (clearPresentationOnlyOnAwake && resultStore != null)
            resultStore.ClearPresentationOnly();

        if (rewardsPresenter != null)
            rewardsPresenter.Hide();

        if (hideRewardsRootOnAwake && rewardsObtainedRoot != null)
            rewardsObtainedRoot.SetActive(false);

        if (hideLevelUpOverlayOnAwake && levelUpOverlayRoot != null)
            levelUpOverlayRoot.SetActive(false);

        if (showPostGamePanelRootOnAwake && postGamePanelRoot != null)
            postGamePanelRoot.SetActive(true);

        if (logDebug)
        {
            Debug.Log(
                "[OnlineMatchRewardPresentationResetter] Awake -> " +
                "ClearedPresentationOnly=" + (clearPresentationOnlyOnAwake && resultStore != null) +
                " | RewardsRootHidden=" + (hideRewardsRootOnAwake && rewardsObtainedRoot != null) +
                " | OverlayHidden=" + (hideLevelUpOverlayOnAwake && levelUpOverlayRoot != null) +
                " | PostGameShown=" + (showPostGamePanelRootOnAwake && postGamePanelRoot != null),
                this
            );
        }
    }

    private void ResolveDependencies()
    {
        if (resultStore == null)
            resultStore = OnlineMatchPresentationResultStore.Instance;

#if UNITY_2023_1_OR_NEWER
        if (rewardsPresenter == null)
            rewardsPresenter = FindFirstObjectByType<OnlineRewardsObtainedPresenter>(FindObjectsInactive.Include);

        if (resultStore == null)
            resultStore = FindFirstObjectByType<OnlineMatchPresentationResultStore>(FindObjectsInactive.Include);
#else
        if (rewardsPresenter == null)
            rewardsPresenter = FindObjectOfType<OnlineRewardsObtainedPresenter>(true);

        if (resultStore == null)
            resultStore = FindObjectOfType<OnlineMatchPresentationResultStore>(true);
#endif
    }
}
