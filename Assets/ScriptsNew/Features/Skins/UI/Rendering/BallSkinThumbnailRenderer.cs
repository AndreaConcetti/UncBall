using System.Collections;
using UnityEngine;

public class BallSkinThumbnailRenderer : MonoBehaviour
{
    [Header("Preview Setup")]
    [SerializeField] private BallSkinApplier previewSkinApplier;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture renderTexture;

    [Header("Thumbnail Output")]
    [SerializeField] private int outputWidth = 256;
    [SerializeField] private int outputHeight = 256;

    public IEnumerator RenderThumbnailCoroutine(BallSkinDatabase database, BallSkinData skin, System.Action<Texture2D> onReady)
    {
        if (previewSkinApplier == null || previewCamera == null || renderTexture == null)
        {
            Debug.LogError("[BallSkinThumbnailRenderer] Missing preview references.", this);
            onReady?.Invoke(null);
            yield break;
        }

        bool applied = previewSkinApplier.ApplySkinData(database, skin);
        if (!applied)
        {
            onReady?.Invoke(null);
            yield break;
        }

        yield return null;
        yield return new WaitForEndOfFrame();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;

        previewCamera.Render();

        Texture2D texture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0);
        texture.Apply();

        RenderTexture.active = previous;

        onReady?.Invoke(texture);
    }
}