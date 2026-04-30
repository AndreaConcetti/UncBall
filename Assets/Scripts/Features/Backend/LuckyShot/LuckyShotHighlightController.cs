using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class LuckyShotHighlightController : MonoBehaviour
{
    public enum HighlightPlacementMode
    {
        KeepCurrentPosition = 0,
        SnapToSlotAnchorXZOnly = 1,
        SnapToSlotAnchorXYZ = 2
    }

    public enum HighlightRotationMode
    {
        PreserveEditorRotation = 0,
        LookAtTarget = 1,
        MatchTargetRotation = 2
    }

    [Header("Scene References")]
    [SerializeField] private LuckyShotSlotRegistry slotRegistry;
    [SerializeField] private LuckyShotSessionRuntime sessionRuntime;

    [Header("Winning Highlights")]
    [SerializeField] private Transform board1HighlightRoot;
    [SerializeField] private Transform board2HighlightRoot;
    [SerializeField] private Transform board3HighlightRoot;

    [Header("Highlight Behaviour")]
    [SerializeField] private HighlightPlacementMode placementMode = HighlightPlacementMode.SnapToSlotAnchorXYZ;
    [SerializeField] private HighlightRotationMode rotationMode = HighlightRotationMode.PreserveEditorRotation;
    [SerializeField] private bool forceExactSlotAnchorPosition = true;
    [SerializeField] private bool hideHighlightIfSlotMissing = true;

    [Header("Placement Offsets")]
    [SerializeField] private Vector3 board1LocalOffset = Vector3.zero;
    [SerializeField] private Vector3 board2LocalOffset = Vector3.zero;
    [SerializeField] private Vector3 board3LocalOffset = Vector3.zero;

    [Header("LookAt Settings")]
    [SerializeField] private Vector3 board1WorldAimOffset = Vector3.zero;
    [SerializeField] private Vector3 board2WorldAimOffset = Vector3.zero;
    [SerializeField] private Vector3 board3WorldAimOffset = Vector3.zero;
    [SerializeField] private Vector3 lookAtUp = Vector3.up;

    [Header("Intro Reveal")]
    [SerializeField] private bool keepHighlightsHiddenUntilIntroReveal = true;

    [Tooltip("Ordine di accensione delle board. Esempio: 1,2,3 oppure 3,2,1.")]
    [SerializeField] private int[] introRevealBoardOrder = { 1, 2, 3 };

    [Tooltip("Delay prima della prima luce.")]
    [SerializeField] private float introInitialDelaySeconds = 0.35f;

    [Tooltip("Tempo tra una luce e la successiva.")]
    [SerializeField] private float introDelayBetweenBoardsSeconds = 0.65f;

    [Tooltip("Durata del fade di accensione della singola luce.")]
    [SerializeField] private float introSingleBoardFadeSeconds = 0.25f;

    [Tooltip("Intensità iniziale della luce durante intro.")]
    [SerializeField] private float introOffLightIntensity = 0f;

    [Tooltip("Intensità finale della luce durante intro.")]
    [SerializeField] private float introOnLightIntensity = 1.75f;

    [Tooltip("Se true, all'inizio della reveal spegne tutte le luci target.")]
    [SerializeField] private bool forceLightsOffBeforeIntro = true;

    [Header("Intro Audio")]
    [SerializeField] private AudioSource introAudioSource;
    [SerializeField] private AudioClip introBoardRevealClip;
    [SerializeField] private float introBoardRevealVolume = 1f;

    [Header("Winner Effect")]
    [SerializeField] private int winnerBlinkCount = 5;
    [SerializeField] private float winnerBlinkIntervalSeconds = 0.12f;
    [SerializeField] private float winnerLightIntensity = 2.5f;
    [SerializeField] private float winnerFinalDimIntensity = 0.1f;
    [SerializeField] private bool disableWinnerHighlightAfterEffect = false;

    [Header("Winner Audio")]
    [SerializeField] private AudioSource winnerAudioSource;
    [SerializeField] private AudioClip winnerClip;
    [SerializeField] private float winnerClipVolume = 1f;

    [Header("Optional Side Markers")]
    [SerializeField] private GameObject leftLaunchMarker;
    [SerializeField] private GameObject rightLaunchMarker;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    private Quaternion board1InitialRotation;
    private Quaternion board2InitialRotation;
    private Quaternion board3InitialRotation;

    private bool cachedInitialRotations;
    private bool introRevealCompleted;
    private bool introRevealInProgress;

    private CancellationTokenSource introRevealCancellation;
    private Coroutine winnerEffectCoroutine;

    private void Awake()
    {
        ResolveReferences();
        CacheInitialRotations();

        if (keepHighlightsHiddenUntilIntroReveal)
            SetAllHighlightsVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheInitialRotations();
        Subscribe();

        if (sessionRuntime != null && sessionRuntime.HasActiveSession)
            ApplySession(sessionRuntime.CurrentSession);
    }

    private void OnDisable()
    {
        Unsubscribe();
        CancelIntroReveal();

        if (winnerEffectCoroutine != null)
        {
            StopCoroutine(winnerEffectCoroutine);
            winnerEffectCoroutine = null;
        }
    }

    public void ApplySession(LuckyShotActiveSession session)
    {
        bool preserveCurrentVisibility = introRevealInProgress;
        ApplySessionInternal(session, ShouldAutoShowHighlights(), preserveCurrentVisibility);
    }

    public async Task PlayIntroRevealAsync()
    {
        LuckyShotActiveSession session = sessionRuntime != null ? sessionRuntime.CurrentSession : default;
        await PlayIntroRevealAsync(session);
    }

    public async Task PlayIntroRevealAsync(LuckyShotActiveSession session)
    {
        CancelIntroReveal();

        introRevealCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = introRevealCancellation.Token;

        ResolveReferences();
        CacheInitialRotations();

        introRevealInProgress = true;
        introRevealCompleted = false;

        try
        {
            if (verboseLogs)
            {
                Debug.Log(
                    "[LuckyShotHighlightController] PlayIntroRevealAsync -> started. " +
                    $"SessionValid={session.IsValid()} | Order={BuildOrderDebugString()}",
                    this);
            }

            if (session.IsValid())
                ApplySessionInternal(session, false, false);

            if (forceLightsOffBeforeIntro)
                SetAllHighlightsVisible(false);

            SetAllHighlightLightIntensity(introOffLightIntensity);

            await DelaySecondsAsync(introInitialDelaySeconds, cancellationToken);

            int[] order = GetValidatedIntroOrder();

            for (int i = 0; i < order.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int boardNumber = order[i];
                Transform root = GetBoardHighlightRoot(boardNumber);

                if (root != null)
                {
                    SetHighlightVisible(root, true);
                    PlayOneShot(introAudioSource, introBoardRevealClip, introBoardRevealVolume);

                    await FadeHighlightLightIntensityAsync(
                        root,
                        introOffLightIntensity,
                        introOnLightIntensity,
                        introSingleBoardFadeSeconds,
                        cancellationToken);

                    if (verboseLogs)
                    {
                        Debug.Log(
                            "[LuckyShotHighlightController] PlayIntroRevealAsync -> board revealed. " +
                            $"Board={boardNumber} | Root={root.name}",
                            this);
                    }
                }
                else if (verboseLogs)
                {
                    Debug.LogWarning(
                        $"[LuckyShotHighlightController] PlayIntroRevealAsync -> missing highlight root for board {boardNumber}.",
                        this);
                }

                if (i < order.Length - 1)
                    await DelaySecondsAsync(introDelayBetweenBoardsSeconds, cancellationToken);
            }

            introRevealCompleted = true;
            introRevealInProgress = false;

            if (session.IsValid())
                ApplySessionInternal(session, true, false);

            if (verboseLogs)
                Debug.Log("[LuckyShotHighlightController] PlayIntroRevealAsync -> completed.", this);
        }
        catch (OperationCanceledException)
        {
            introRevealInProgress = false;

            if (verboseLogs)
                Debug.Log("[LuckyShotHighlightController] PlayIntroRevealAsync -> cancelled.", this);
        }
        catch (Exception exception)
        {
            introRevealInProgress = false;

            Debug.LogError(
                "[LuckyShotHighlightController] PlayIntroRevealAsync -> failed.\n" + exception,
                this);
        }
    }

    public void PlayIntroReveal()
    {
        _ = PlayIntroRevealAsync();
    }

    public void PlayHitEffect(int boardNumber, string slotId)
    {
        if (winnerEffectCoroutine != null)
            StopCoroutine(winnerEffectCoroutine);

        winnerEffectCoroutine = StartCoroutine(PlayWinnerEffectRoutine(boardNumber, slotId));
    }

    public void NotifyTargetHit(int boardNumber, string slotId)
    {
        PlayHitEffect(boardNumber, slotId);
    }

    public void ClearHighlights()
    {
        SetAllHighlightsVisible(false);
        SetAllHighlightLightIntensity(introOffLightIntensity);

        if (leftLaunchMarker != null)
            leftLaunchMarker.SetActive(false);

        if (rightLaunchMarker != null)
            rightLaunchMarker.SetActive(false);

        introRevealCompleted = false;
        introRevealInProgress = false;

        if (verboseLogs)
            Debug.Log("[LuckyShotHighlightController] ClearHighlights -> all highlight roots disabled.", this);
    }

    [ContextMenu("Lucky Shot/Preview Intro Reveal")]
    private void InspectorPreviewIntroReveal()
    {
        _ = PlayIntroRevealAsync();
    }

    [ContextMenu("Lucky Shot/Force Hide All Highlights")]
    private void InspectorForceHideAllHighlights()
    {
        CancelIntroReveal();
        introRevealCompleted = false;
        introRevealInProgress = false;
        SetAllHighlightsVisible(false);
        SetAllHighlightLightIntensity(introOffLightIntensity);
    }

    [ContextMenu("Lucky Shot/Force Show All Highlights")]
    private void InspectorForceShowAllHighlights()
    {
        CancelIntroReveal();
        introRevealCompleted = true;
        introRevealInProgress = false;
        SetAllHighlightsVisible(true);
        SetAllHighlightLightIntensity(introOnLightIntensity);
    }

    private void ApplySessionInternal(LuckyShotActiveSession session, bool showHighlights)
    {
        ApplySessionInternal(session, showHighlights, false);
    }

    private void ApplySessionInternal(LuckyShotActiveSession session, bool showHighlights, bool preserveCurrentVisibility)
    {
        ResolveReferences();
        CacheInitialRotations();

        if (slotRegistry == null)
        {
            if (verboseLogs)
                Debug.LogWarning("[LuckyShotHighlightController] ApplySession -> LuckyShotSlotRegistry missing.", this);

            return;
        }

        ApplyBoardHighlight(
            boardNumber: 1,
            slotId: session.board1WinningSlotId,
            highlightRoot: board1HighlightRoot,
            cachedInitialRotation: board1InitialRotation,
            localOffset: board1LocalOffset,
            worldAimOffset: board1WorldAimOffset,
            showHighlight: showHighlights,
            preserveCurrentVisibility: preserveCurrentVisibility);

        ApplyBoardHighlight(
            boardNumber: 2,
            slotId: session.board2WinningSlotId,
            highlightRoot: board2HighlightRoot,
            cachedInitialRotation: board2InitialRotation,
            localOffset: board2LocalOffset,
            worldAimOffset: board2WorldAimOffset,
            showHighlight: showHighlights,
            preserveCurrentVisibility: preserveCurrentVisibility);

        ApplyBoardHighlight(
            boardNumber: 3,
            slotId: session.board3WinningSlotId,
            highlightRoot: board3HighlightRoot,
            cachedInitialRotation: board3InitialRotation,
            localOffset: board3LocalOffset,
            worldAimOffset: board3WorldAimOffset,
            showHighlight: showHighlights,
            preserveCurrentVisibility: preserveCurrentVisibility);

        bool launchLeft = session.launchSide == LuckyShotLaunchSide.Left;

        if (leftLaunchMarker != null)
            leftLaunchMarker.SetActive(launchLeft);

        if (rightLaunchMarker != null)
            rightLaunchMarker.SetActive(!launchLeft);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotHighlightController] ApplySession -> " +
                $"LaunchSide={session.launchSide} | " +
                $"B1={session.board1WinningSlotId} | " +
                $"B2={session.board2WinningSlotId} | " +
                $"B3={session.board3WinningSlotId} | " +
                $"ShowHighlights={showHighlights} | PreserveVisibility={preserveCurrentVisibility}",
                this);
        }
    }

    private void ApplyBoardHighlight(
        int boardNumber,
        string slotId,
        Transform highlightRoot,
        Quaternion cachedInitialRotation,
        Vector3 localOffset,
        Vector3 worldAimOffset,
        bool showHighlight,
        bool preserveCurrentVisibility)
    {
        if (highlightRoot == null)
        {
            if (verboseLogs)
            {
                Debug.LogWarning(
                    $"[LuckyShotHighlightController] ApplyBoardHighlight -> highlight root missing. Board={boardNumber}",
                    this);
            }

            return;
        }

        LuckyShotSlotRegistry.LuckyShotRegisteredSlot slot = slotRegistry.GetSlot(boardNumber, slotId);
        if (slot == null || slot.highlightAnchor == null)
        {
            if (hideHighlightIfSlotMissing && !preserveCurrentVisibility)
                SetHighlightVisible(highlightRoot, false);

            if (verboseLogs)
            {
                Debug.LogWarning(
                    $"[LuckyShotHighlightController] ApplyBoardHighlight -> slot not found. Board={boardNumber} | SlotId={slotId}",
                    this);
            }

            return;
        }

        Transform targetAnchor = slot.highlightAnchor;
        Vector3 targetWorldPosition = targetAnchor.position + targetAnchor.TransformVector(localOffset);

        HighlightPlacementMode effectivePlacementMode = forceExactSlotAnchorPosition
            ? HighlightPlacementMode.SnapToSlotAnchorXYZ
            : placementMode;

        switch (effectivePlacementMode)
        {
            case HighlightPlacementMode.KeepCurrentPosition:
                break;

            case HighlightPlacementMode.SnapToSlotAnchorXZOnly:
                {
                    Vector3 current = highlightRoot.position;
                    highlightRoot.position = new Vector3(
                        targetWorldPosition.x,
                        current.y,
                        targetWorldPosition.z);
                    break;
                }

            case HighlightPlacementMode.SnapToSlotAnchorXYZ:
                highlightRoot.position = targetWorldPosition;
                break;
        }

        switch (rotationMode)
        {
            case HighlightRotationMode.PreserveEditorRotation:
                highlightRoot.rotation = cachedInitialRotation;
                break;

            case HighlightRotationMode.LookAtTarget:
                {
                    Vector3 lookTarget = targetAnchor.position + worldAimOffset;
                    Vector3 lookDirection = lookTarget - highlightRoot.position;

                    if (lookDirection.sqrMagnitude > 0.0001f)
                        highlightRoot.rotation = Quaternion.LookRotation(lookDirection.normalized, lookAtUp);

                    break;
                }

            case HighlightRotationMode.MatchTargetRotation:
                highlightRoot.rotation = targetAnchor.rotation;
                break;
        }

        if (!preserveCurrentVisibility)
            SetHighlightVisible(highlightRoot, showHighlight);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotHighlightController] Highlight applied -> " +
                $"Board={boardNumber} | SlotId={slotId} | Anchor={targetAnchor.name} | " +
                $"WorldPos={highlightRoot.position} | ForceExact={forceExactSlotAnchorPosition} | Visible={showHighlight} | PreserveVisibility={preserveCurrentVisibility}",
                this);
        }
    }

    private System.Collections.IEnumerator PlayWinnerEffectRoutine(int boardNumber, string slotId)
    {
        Transform root = GetBoardHighlightRoot(boardNumber);
        if (root == null)
            yield break;

        SetHighlightVisible(root, true);
        PlayOneShot(winnerAudioSource, winnerClip, winnerClipVolume);

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotHighlightController] PlayWinnerEffectRoutine -> started. " +
                $"Board={boardNumber} | SlotId={slotId}",
                this);
        }

        int blinkCount = Mathf.Max(1, winnerBlinkCount);
        float interval = Mathf.Max(0.01f, winnerBlinkIntervalSeconds);

        for (int i = 0; i < blinkCount; i++)
        {
            SetHighlightLightIntensity(root, winnerLightIntensity);
            yield return new WaitForSeconds(interval);

            SetHighlightLightIntensity(root, introOffLightIntensity);
            yield return new WaitForSeconds(interval);
        }

        SetHighlightLightIntensity(root, winnerFinalDimIntensity);

        if (disableWinnerHighlightAfterEffect)
            SetHighlightVisible(root, false);

        winnerEffectCoroutine = null;

        if (verboseLogs)
        {
            Debug.Log(
                "[LuckyShotHighlightController] PlayWinnerEffectRoutine -> completed. " +
                $"Board={boardNumber} | SlotId={slotId}",
                this);
        }
    }

    private async Task FadeHighlightLightIntensityAsync(
        Transform root,
        float from,
        float to,
        float duration,
        CancellationToken cancellationToken)
    {
        if (root == null)
            return;

        if (duration <= 0f)
        {
            SetHighlightLightIntensity(root, to);
            return;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float intensity = Mathf.Lerp(from, to, t);

            SetHighlightLightIntensity(root, intensity);

            await Task.Yield();
        }

        SetHighlightLightIntensity(root, to);
    }

    private async Task DelaySecondsAsync(float seconds, CancellationToken cancellationToken)
    {
        if (seconds <= 0f)
            return;

        float elapsed = 0f;

        while (elapsed < seconds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }
    }

    private void SetHighlightLightIntensity(Transform root, float intensity)
    {
        if (root == null)
            return;

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].intensity = intensity;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material material = renderer.material;
            if (material == null)
                continue;

            if (material.HasProperty("_EmissionColor"))
            {
                Color emissionColor = Color.white * Mathf.Max(0f, intensity);
                material.SetColor("_EmissionColor", emissionColor);
            }
        }
    }

    private void SetAllHighlightLightIntensity(float intensity)
    {
        SetHighlightLightIntensity(board1HighlightRoot, intensity);
        SetHighlightLightIntensity(board2HighlightRoot, intensity);
        SetHighlightLightIntensity(board3HighlightRoot, intensity);
    }

    private void SetAllHighlightsVisible(bool visible)
    {
        SetHighlightVisible(board1HighlightRoot, visible);
        SetHighlightVisible(board2HighlightRoot, visible);
        SetHighlightVisible(board3HighlightRoot, visible);
    }

    private void SetHighlightVisible(Transform root, bool visible)
    {
        if (root == null)
            return;

        if (root.gameObject.activeSelf != visible)
            root.gameObject.SetActive(visible);
    }

    private bool ShouldAutoShowHighlights()
    {
        if (!keepHighlightsHiddenUntilIntroReveal)
            return true;

        return introRevealCompleted && !introRevealInProgress;
    }

    private Transform GetBoardHighlightRoot(int boardNumber)
    {
        switch (boardNumber)
        {
            case 1:
                return board1HighlightRoot;

            case 2:
                return board2HighlightRoot;

            case 3:
                return board3HighlightRoot;

            default:
                return null;
        }
    }

    private int[] GetValidatedIntroOrder()
    {
        if (introRevealBoardOrder == null || introRevealBoardOrder.Length == 0)
            return new[] { 1, 2, 3 };

        bool has1 = false;
        bool has2 = false;
        bool has3 = false;

        for (int i = 0; i < introRevealBoardOrder.Length; i++)
        {
            if (introRevealBoardOrder[i] == 1)
                has1 = true;
            else if (introRevealBoardOrder[i] == 2)
                has2 = true;
            else if (introRevealBoardOrder[i] == 3)
                has3 = true;
        }

        if (!has1 || !has2 || !has3)
            return new[] { 1, 2, 3 };

        return introRevealBoardOrder;
    }

    private string BuildOrderDebugString()
    {
        int[] order = GetValidatedIntroOrder();

        if (order == null || order.Length == 0)
            return "empty";

        string value = string.Empty;

        for (int i = 0; i < order.Length; i++)
        {
            if (i > 0)
                value += ",";

            value += order[i].ToString();
        }

        return value;
    }

    private void PlayOneShot(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null)
            return;

        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void CancelIntroReveal()
    {
        if (introRevealCancellation != null)
        {
            introRevealCancellation.Cancel();
            introRevealCancellation.Dispose();
            introRevealCancellation = null;
        }

        introRevealInProgress = false;
    }

    private void HandleSessionLoaded(LuckyShotActiveSession session)
    {
        ApplySession(session);
    }

    private void HandleSessionPreviewChanged(LuckyShotSessionPreview preview)
    {
        if (!preview.hasActiveSession)
        {
            ClearHighlights();
            return;
        }

        LuckyShotActiveSession session = sessionRuntime != null ? sessionRuntime.CurrentSession : default;
        if (session.IsValid())
            ApplySession(session);
    }

    private void HandleSessionResolved(LuckyShotResolvedResult result)
    {
        if (!result.success)
            return;

        if (result.isWin)
        {
            PlayHitEffect(result.hitBoardNumber, result.hitSlotId);
            return;
        }

        if (result.sessionAfterResolve.hasActiveSession)
        {
            ApplySession(result.sessionAfterResolve);
            return;
        }

        ClearHighlights();
    }

    private void ResolveReferences()
    {
        if (slotRegistry == null)
        {
#if UNITY_2023_1_OR_NEWER
            slotRegistry = FindFirstObjectByType<LuckyShotSlotRegistry>();
#else
            slotRegistry = FindObjectOfType<LuckyShotSlotRegistry>();
#endif
        }

        if (sessionRuntime == null)
            sessionRuntime = LuckyShotSessionRuntime.Instance;

        if (sessionRuntime == null)
        {
#if UNITY_2023_1_OR_NEWER
            sessionRuntime = FindFirstObjectByType<LuckyShotSessionRuntime>();
#else
            sessionRuntime = FindObjectOfType<LuckyShotSessionRuntime>();
#endif
        }

        if (introAudioSource == null)
            introAudioSource = GetComponent<AudioSource>();

        if (winnerAudioSource == null)
            winnerAudioSource = introAudioSource;
    }

    private void CacheInitialRotations()
    {
        if (cachedInitialRotations)
            return;

        if (board1HighlightRoot != null)
            board1InitialRotation = board1HighlightRoot.rotation;

        if (board2HighlightRoot != null)
            board2InitialRotation = board2HighlightRoot.rotation;

        if (board3HighlightRoot != null)
            board3InitialRotation = board3HighlightRoot.rotation;

        cachedInitialRotations = true;
    }

    private void Subscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionLoaded += HandleSessionLoaded;

        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionPreviewChanged += HandleSessionPreviewChanged;

        sessionRuntime.SessionResolved -= HandleSessionResolved;
        sessionRuntime.SessionResolved += HandleSessionResolved;
    }

    private void Unsubscribe()
    {
        if (sessionRuntime == null)
            return;

        sessionRuntime.SessionLoaded -= HandleSessionLoaded;
        sessionRuntime.SessionPreviewChanged -= HandleSessionPreviewChanged;
        sessionRuntime.SessionResolved -= HandleSessionResolved;
    }
}