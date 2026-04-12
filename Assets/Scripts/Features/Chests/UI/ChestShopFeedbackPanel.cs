using TMPro;
using UnityEngine;

public class ChestShopFeedbackPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text messageText;

    [Header("Behavior")]
    [SerializeField] private float visibleDurationSeconds = 1.75f;
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private float remainingTime;
    private bool isShowing;

    private void Awake()
    {
        if (hideOnAwake)
            HideImmediate();
    }

    private void Update()
    {
        if (!isShowing)
            return;

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        remainingTime -= delta;

        if (remainingTime <= 0f)
            HideImmediate();
    }

    public void Show(string message)
    {
        if (messageText != null)
            messageText.text = message;

        if (root != null)
            root.SetActive(true);
        else
            gameObject.SetActive(true);

        remainingTime = Mathf.Max(0.1f, visibleDurationSeconds);
        isShowing = true;

        if (logDebug)
            Debug.Log("[ChestShopFeedbackPanel] Show -> " + message, this);
    }

    public void HideImmediate()
    {
        isShowing = false;
        remainingTime = 0f;

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }
}
