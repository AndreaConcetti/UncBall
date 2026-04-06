using UnityEngine;
using UnityEngine.UI;

public class TableThemeController : MonoBehaviour
{
    [Header("3D Renderers")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private Material lightTableMaterial;
    [SerializeField] private Material darkTableMaterial;
    [SerializeField] private bool instantiateMaterialsAtRuntime = true;

    [Header("UI Images (Optional)")]
    [SerializeField] private Image[] targetImages;
    [SerializeField] private Sprite lightTableSprite;
    [SerializeField] private Sprite darkTableSprite;

    [Header("Debug")]
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField] private bool logDebug = false;

    private Material runtimeLightMaterial;
    private Material runtimeDarkMaterial;

    private void Awake()
    {
        PrepareRuntimeMaterialsIfNeeded();
    }

    private void OnEnable()
    {
        if (applyOnEnable)
            RefreshTheme();
    }

    public void RefreshTheme()
    {
        bool useDarkMode = LocalGameSettings.TableDarkModeEnabled;

        ApplyRendererTheme(useDarkMode);
        ApplyImageTheme(useDarkMode);

        if (logDebug)
            Debug.Log("[TableThemeController] RefreshTheme -> DarkMode=" + useDarkMode, this);
    }

    private void PrepareRuntimeMaterialsIfNeeded()
    {
        if (!instantiateMaterialsAtRuntime)
            return;

        if (lightTableMaterial != null && runtimeLightMaterial == null)
            runtimeLightMaterial = new Material(lightTableMaterial);

        if (darkTableMaterial != null && runtimeDarkMaterial == null)
            runtimeDarkMaterial = new Material(darkTableMaterial);
    }

    private void ApplyRendererTheme(bool useDarkMode)
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            return;

        Material selectedMaterial = ResolveMaterialForMode(useDarkMode);
        if (selectedMaterial == null)
            return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            renderer.sharedMaterial = selectedMaterial;
        }
    }

    private void ApplyImageTheme(bool useDarkMode)
    {
        if (targetImages == null || targetImages.Length == 0)
            return;

        Sprite selectedSprite = useDarkMode ? darkTableSprite : lightTableSprite;
        if (selectedSprite == null)
            return;

        for (int i = 0; i < targetImages.Length; i++)
        {
            Image image = targetImages[i];
            if (image == null)
                continue;

            image.sprite = selectedSprite;
        }
    }

    private Material ResolveMaterialForMode(bool useDarkMode)
    {
        if (instantiateMaterialsAtRuntime)
            return useDarkMode ? runtimeDarkMaterial : runtimeLightMaterial;

        return useDarkMode ? darkTableMaterial : lightTableMaterial;
    }
}