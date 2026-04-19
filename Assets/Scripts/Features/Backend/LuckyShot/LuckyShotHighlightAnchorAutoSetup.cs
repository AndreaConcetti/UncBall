using UnityEngine;

public sealed class LuckyShotHighlightAnchorAutoSetup : MonoBehaviour
{
    [SerializeField] private string anchorName = "HighlightAnchor";
    [SerializeField] private bool createIfMissing = true;
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    [ContextMenu("Create / Refresh Highlight Anchor")]
    private void CreateOrRefreshAnchor()
    {
        Transform existing = transform.Find(anchorName);
        if (existing == null && createIfMissing)
        {
            GameObject go = new GameObject(anchorName);
            existing = go.transform;
            existing.SetParent(transform, false);
        }

        if (existing != null)
        {
            existing.localPosition = localOffset;
            existing.localRotation = Quaternion.identity;
            existing.localScale = Vector3.one;
        }
    }
}
