using UnityEngine;

public class BallSkinApplier : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Material Source")]
    [SerializeField] private Material runtimeMaterialTemplate;

    [Header("Shader Property Names")]
    [SerializeField] private string baseColorProperty = "_BaseColor";
    [SerializeField] private string patternTextureProperty = "_PatternTex";
    [SerializeField] private string patternColorProperty = "_PatternColor";
    [SerializeField] private string patternIntensityProperty = "_PatternIntensity";
    [SerializeField] private string patternScaleProperty = "_PatternScale";

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    private Material runtimeMaterialInstance;

    private void Awake()
    {
        AutoAssignRendererIfNeeded();
        EnsureRuntimeMaterialInstance();
    }

    public bool ApplySkinData(BallSkinDatabase database, BallSkinData data)
    {
        if (!BallSkinResolver.TryResolve(
                database,
                data,
                out Color baseColor,
                out Texture2D patternTexture,
                out Color patternColor,
                out float patternIntensity,
                out float patternScale))
        {
            Debug.LogError("[BallSkinApplier] Impossibile risolvere la skin.", this);
            return false;
        }

        ApplyDirect(baseColor, patternTexture, patternColor, patternIntensity, patternScale);

        if (logDebug)
            Debug.Log("[BallSkinApplier] Skin applicata: " + data.skinUniqueId, this);

        return true;
    }

    public void ApplyDirect(Color baseColor, Texture2D patternTexture, Color patternColor, float patternIntensity = 1f, float patternScale = 1f)
    {
        AutoAssignRendererIfNeeded();
        EnsureRuntimeMaterialInstance();

        if (targetRenderer == null || runtimeMaterialInstance == null)
        {
            Debug.LogError("[BallSkinApplier] Renderer o materiale runtime mancanti.", this);
            return;
        }

        if (runtimeMaterialInstance.HasProperty(baseColorProperty))
            runtimeMaterialInstance.SetColor(baseColorProperty, baseColor);

        if (runtimeMaterialInstance.HasProperty(patternTextureProperty))
            runtimeMaterialInstance.SetTexture(patternTextureProperty, patternTexture);

        if (runtimeMaterialInstance.HasProperty(patternColorProperty))
            runtimeMaterialInstance.SetColor(patternColorProperty, patternColor);

        if (runtimeMaterialInstance.HasProperty(patternIntensityProperty))
            runtimeMaterialInstance.SetFloat(patternIntensityProperty, patternIntensity);

        if (runtimeMaterialInstance.HasProperty(patternScaleProperty))
            runtimeMaterialInstance.SetFloat(patternScaleProperty, patternScale);
    }

    private void AutoAssignRendererIfNeeded()
    {
        if (targetRenderer != null)
            return;

        targetRenderer = GetComponentInChildren<MeshRenderer>(true);
    }

    private void EnsureRuntimeMaterialInstance()
    {
        if (targetRenderer == null)
            return;

        if (runtimeMaterialInstance != null)
            return;

        Material source = runtimeMaterialTemplate != null ? runtimeMaterialTemplate : targetRenderer.sharedMaterial;

        if (source == null)
        {
            Debug.LogError("[BallSkinApplier] Nessun materiale sorgente disponibile.", this);
            return;
        }

        runtimeMaterialInstance = new Material(source);
        runtimeMaterialInstance.name = source.name + "_RuntimeInstance";
        targetRenderer.material = runtimeMaterialInstance;
    }
}