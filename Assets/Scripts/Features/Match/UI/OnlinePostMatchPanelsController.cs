using UnityEngine;

public class OnlinePostMatchPanelsController : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject postGameRoot;
    [SerializeField] private GameObject rewardsObtainedRoot;

    [Header("Optional CanvasGroups")]
    [SerializeField] private CanvasGroup postGameCanvasGroup;
    [SerializeField] private CanvasGroup rewardsCanvasGroup;

    [Header("Optional Gameplay UI")]
    [SerializeField] private GameObject inGameUiRoot;
    [SerializeField] private bool keepInGameUiActiveAtMatchEnd = true;

    [Header("Behaviour")]
    [SerializeField] private bool hideRewardsOnAwake = true;
    [SerializeField] private bool hidePostGameWhenOpeningRewards = true;
    [SerializeField] private bool disablePostGameRaycastsWhenRewardsOpen = true;
    [SerializeField] private bool restorePostGameOnReturn = true;

    [Header("Optional")]
    [SerializeField] private OnlineRewardsObtainedPresenter rewardsPresenter;
    [SerializeField] private bool logDebug = true;

    private void Awake()
    {
        ResolveDependencies();

        ApplyInitialState();
        ApplyGameplayUiPolicy();

        if (logDebug)
        {
            Debug.Log(
                "[OnlinePostMatchPanelsController] Awake -> " +
                "PostGameRoot=" + SafeName(postGameRoot) +
                " | RewardsRoot=" + SafeName(rewardsObtainedRoot) +
                " | RewardsPresenter=" + SafeName(rewardsPresenter) +
                " | InGameUiRoot=" + SafeName(inGameUiRoot) +
                " | KeepInGameUiActiveAtMatchEnd=" + keepInGameUiActiveAtMatchEnd,
                this
            );
        }
    }

    public void OpenRewardsObtained()
    {
        ResolveDependencies();

        if (logDebug)
        {
            Debug.Log(
                "[OnlinePostMatchPanelsController] OpenRewardsObtained invoked. " +
                "PostGameActive=" + IsRootActive(postGameRoot) +
                " | RewardsActive=" + IsRootActive(rewardsObtainedRoot),
                this
            );
        }

        if (hidePostGameWhenOpeningRewards)
        {
            SetRootVisible(postGameRoot, postGameCanvasGroup, false, !disablePostGameRaycastsWhenRewardsOpen);
        }
        else if (disablePostGameRaycastsWhenRewardsOpen)
        {
            SetCanvasGroupInteraction(postGameCanvasGroup, false, false);
        }

        SetRootVisible(rewardsObtainedRoot, rewardsCanvasGroup, true, true);
        ApplyGameplayUiPolicy();

        if (rewardsPresenter != null)
        {
            rewardsPresenter.ForceReplayLatest();
        }
        else
        {
            Debug.LogWarning("[OnlinePostMatchPanelsController] Rewards presenter is NULL.", this);
        }
    }

    public void ReturnToPostGame()
    {
        ResolveDependencies();

        if (logDebug)
        {
            Debug.Log(
                "[OnlinePostMatchPanelsController] ReturnToPostGame invoked. " +
                "RestorePostGame=" + restorePostGameOnReturn,
                this
            );
        }

        SetRootVisible(rewardsObtainedRoot, rewardsCanvasGroup, false, false);

        if (restorePostGameOnReturn)
            SetRootVisible(postGameRoot, postGameCanvasGroup, true, true);
        else
            SetCanvasGroupInteraction(postGameCanvasGroup, true, true);

        ApplyGameplayUiPolicy();
    }

    public void ApplyGameplayUiPolicy()
    {
        if (inGameUiRoot == null)
            return;

        inGameUiRoot.SetActive(keepInGameUiActiveAtMatchEnd);

        if (logDebug)
        {
            Debug.Log(
                "[OnlinePostMatchPanelsController] ApplyGameplayUiPolicy -> " +
                "InGameUiRoot=" + SafeName(inGameUiRoot) +
                " | Active=" + inGameUiRoot.activeSelf,
                this
            );
        }
    }

    private void ApplyInitialState()
    {
        if (hideRewardsOnAwake)
            SetRootVisible(rewardsObtainedRoot, rewardsCanvasGroup, false, false);
        else
            SetRootVisible(rewardsObtainedRoot, rewardsCanvasGroup, true, true);

        if (postGameRoot != null)
        {
            bool shouldBeVisible = postGameRoot.activeSelf;
            SetRootVisible(postGameRoot, postGameCanvasGroup, shouldBeVisible, shouldBeVisible);
        }
    }

    private void SetRootVisible(GameObject root, CanvasGroup canvasGroup, bool visible, bool allowRaycasts)
    {
        if (root != null)
            root.SetActive(visible);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = allowRaycasts && visible;
        }

        if (logDebug)
        {
            Debug.Log(
                "[OnlinePostMatchPanelsController] SetRootVisible -> " +
                "Root=" + SafeName(root) +
                " | Visible=" + visible +
                " | AllowRaycasts=" + allowRaycasts +
                " | CanvasGroup=" + SafeName(canvasGroup),
                this
            );
        }
    }

    private void SetCanvasGroupInteraction(CanvasGroup canvasGroup, bool interactable, bool blocksRaycasts)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = blocksRaycasts;

        if (logDebug)
        {
            Debug.Log(
                "[OnlinePostMatchPanelsController] SetCanvasGroupInteraction -> " +
                "CanvasGroup=" + SafeName(canvasGroup) +
                " | Interactable=" + interactable +
                " | BlocksRaycasts=" + blocksRaycasts,
                this
            );
        }
    }

    private bool IsRootActive(GameObject root)
    {
        return root != null && root.activeInHierarchy;
    }

    private void ResolveDependencies()
    {
        if (rewardsPresenter == null && rewardsObtainedRoot != null)
            rewardsPresenter = rewardsObtainedRoot.GetComponentInChildren<OnlineRewardsObtainedPresenter>(true);

        if (postGameCanvasGroup == null && postGameRoot != null)
            postGameCanvasGroup = postGameRoot.GetComponent<CanvasGroup>();

        if (rewardsCanvasGroup == null && rewardsObtainedRoot != null)
            rewardsCanvasGroup = rewardsObtainedRoot.GetComponent<CanvasGroup>();

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