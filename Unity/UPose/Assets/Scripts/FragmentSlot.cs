using TMPro;
using UnityEngine;

public enum FragmentSlotKind
{
    Solo,
    Collective
}

public class FragmentSlot : MonoBehaviour
{
    [Header("Slot Identity")]
    public int slotIndex = 1;

    [Header("Core References")]
    public Camera fragmentCamera;
    public BoneTrackingCamera trackingCamera;
    public Renderer screenRenderer;
    public Transform overlayRoot;

    [Header("Render Texture")]
    public int renderTextureWidth = 512;
    public int renderTextureHeight = 512;
    public int renderTextureDepth = 16;
    public RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGB32;
    public string screenTextureProperty = "_BaseMap";

    [Header("Texture Crop")]
    public bool preserveTextureAspectWithCrop = false;

    [Header("Overlay Text")]
    public bool compensateOverlayTextScale = true;

    [Header("Annotation")]
    public bool enableAnnotationSystem = true;
    public FragmentSlotKind slotKind = FragmentSlotKind.Collective;
    public bool showSoloCoordinates = false;
    public bool showCollectiveCoordinates = true;
    public bool showCollectiveBoneLabel = true;
    public bool showCollectiveStatusLabel = true;
    public bool collectiveLabelOutside = true;
    public Vector2 labelOffset = new Vector2(0.03f, 0.03f);
    public Vector2 collectiveLabelOffset = new Vector2(0.03f, 0.08f);
    public string soloLabelPrefix = "SOLO";
    public string collectiveLabelPrefix = "COLLECTIVE";
    public string collectiveSourceLabel = "AGGREGATED_BODY";
    public string collectiveStatusLabel = "LIVE";
    public string annotationTextHexColor = "#FFFFFF";
    public string annotationAccentHexColor = "#00FF00";
    public bool showAnnotationMarker = true;
    public string annotationMarkerSymbol = "\u25A0";
    public string annotationMarkerHexColor = "#00FFFF";

    [Header("Screen Shapes")]
    public Vector3[] normalScreenScales = new Vector3[]
    {
        new Vector3(1f, 1f, 1f),
        new Vector3(2f, 2f, 1f),
        new Vector3(3f, 3f, 1f)
    };

    public Vector3[] distortedScreenScales = new Vector3[0];

    [Header("Fade")]
    public float currentAlpha = 0f;

    [Header("Runtime State")]
    [SerializeField] private bool isActive = false;

    private FragmentProfile currentProfile;
    private Material runtimeMaterial;
    private RenderTexture runtimeRT;
    private string activeTextureProperty;
    private Vector2 baseTextureScale = Vector2.one;
    private Vector2 baseTextureOffset = Vector2.zero;
    private Vector3 baseScreenLocalScale = Vector3.one;
    private TextMeshPro[] overlayTexts;
    private Vector3[] overlayTextBaseScales;
    private FeedLabelController feedLabelController;
    private TextMeshPro feedLabelText;
    private Transform feedMarker;
    private TrackLabelController trackLabelController;
    private TextMeshPro trackLabelText;
    private Transform borderTop;
    private Transform borderLeft;
    private Transform borderRight;

    private Vector3 startPos;
    private Vector3 targetPos;
    private float timer;

    void Awake()
    {
        AutoAssignReferences();
        CacheAnnotationReferences();
        CacheOverlayTextScales();
        InitializeRuntimeResources();
        SetVisible(false);
        SetAlpha(0f);
    }

    void OnDestroy()
    {
        CleanupRuntimeResources();
    }

    void Update()
    {
        if (!isActive || currentProfile == null) return;

        timer += Time.deltaTime;

        // move
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            Time.deltaTime * currentProfile.moveSpeed
        );

        // fade
        float alpha = 1f;

        if (timer < currentProfile.fadeInTime)
        {
            alpha = Mathf.Clamp01(timer / currentProfile.fadeInTime);
        }
        else if (timer > currentProfile.lifeTime - currentProfile.fadeOutTime)
        {
            float t = (timer - (currentProfile.lifeTime - currentProfile.fadeOutTime)) / currentProfile.fadeOutTime;
            alpha = Mathf.Clamp01(1f - t);
        }

        SetAlpha(alpha);

        if (timer >= currentProfile.lifeTime)
        {
            Deactivate();
        }

        UpdateAnnotation();
    }

    void AutoAssignReferences()
    {
        if (fragmentCamera == null)
            fragmentCamera = GetComponentInChildren<Camera>(true);

        if (trackingCamera == null)
            trackingCamera = GetComponentInChildren<BoneTrackingCamera>(true);

        if (screenRenderer == null)
        {
            Transform screen = transform.Find("Screen");
            if (screen != null) screenRenderer = screen.GetComponent<Renderer>();
            if (screenRenderer == null) screenRenderer = GetComponentInChildren<Renderer>(true);
        }

        if (overlayRoot == null)
        {
            Transform overlay = transform.Find("Overlay");
            if (overlay == null)
            {
                Transform screen = transform.Find("Screen");
                if (screen != null) overlay = screen.Find("Overlay");
            }

            if (overlay != null) overlayRoot = overlay;
        }

        if (screenRenderer != null)
            baseScreenLocalScale = screenRenderer.transform.localScale;
    }

    void InitializeRuntimeResources()
    {
        if (fragmentCamera == null || screenRenderer == null) return;

        CleanupRuntimeResources();

        runtimeRT = new RenderTexture(
            renderTextureWidth,
            renderTextureHeight,
            renderTextureDepth,
            renderTextureFormat
        );

        runtimeRT.name = $"RT_Slot_{slotIndex:00}";
        runtimeRT.Create();

        fragmentCamera.targetTexture = runtimeRT;

        runtimeMaterial = new Material(screenRenderer.sharedMaterial);
        runtimeMaterial.name = $"MAT_Slot_{slotIndex:00}";

        if (runtimeMaterial.HasProperty(screenTextureProperty))
        {
            runtimeMaterial.SetTexture(screenTextureProperty, runtimeRT);
            activeTextureProperty = screenTextureProperty;
        }
        else if (runtimeMaterial.HasProperty("_MainTex"))
        {
            runtimeMaterial.SetTexture("_MainTex", runtimeRT);
            activeTextureProperty = "_MainTex";
        }

        CacheBaseTextureTransform();
        ApplyTextureCropForCurrentScreenScale();

        screenRenderer.material = runtimeMaterial;
    }

    void CleanupRuntimeResources()
    {
        if (fragmentCamera != null && fragmentCamera.targetTexture == runtimeRT)
        {
            fragmentCamera.targetTexture = null;
        }

        if (runtimeRT != null)
        {
            if (runtimeRT.IsCreated()) runtimeRT.Release();
            Destroy(runtimeRT);
            runtimeRT = null;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        activeTextureProperty = null;
    }

    public void RefreshSlotResources()
    {
        InitializeRuntimeResources();
    }

    public void Activate(FragmentProfile profile)
    {
        if (profile == null) return;

        currentProfile = profile;
        isActive = true;
        timer = 0f;

        startPos = profile.startPos;
        targetPos = profile.targetPos;
        transform.localPosition = startPos;

        ApplyProfileScreenScale(profile);

        if (trackingCamera != null)
        {
            trackingCamera.SetBone(profile.boneName);
            trackingCamera.ApplyCameraProfile(
                profile.cameraOffset,
                profile.cameraFOV,
                profile.targetSmooth,
                profile.positionSmooth,
                profile.lookSmooth,
                profile.useBoneRotation
            );
        }

        SetVisible(true);
        SetAlpha(0f);
        RefreshAnnotationMode();
        UpdateAnnotation();
    }

    public void Deactivate()
    {
        isActive = false;
        currentProfile = null;
        timer = 0f;
        SetVisible(false);
        SetAlpha(0f);
    }

    public void SetSlotKind(FragmentSlotKind kind)
    {
        slotKind = kind;
        RefreshAnnotationMode();
        UpdateAnnotation();
    }

    void ApplyProfileScreenScale(FragmentProfile profile)
    {
        if (profile == null || screenRenderer == null) return;

        if (profile.overrideScreenScale)
        {
            ApplyScreenScale(profile.screenScale);
            return;
        }

        if (profile.randomizeScreenScale)
            ApplyRandomScreenShape(profile.useDistortion);
    }

    public void ApplyScreenScale(Vector3 scale)
    {
        if (screenRenderer == null) return;

        scale.x = Mathf.Max(0.01f, scale.x);
        scale.y = Mathf.Max(0.01f, scale.y);
        scale.z = Mathf.Max(0.01f, scale.z);

        screenRenderer.transform.localScale = scale;
        ApplyTextureCropForCurrentScreenScale();
        ApplyOverlayTextScaleCompensation();
    }

    public void SetScreenScale(Vector3 scale)
    {
        ApplyScreenScale(scale);
    }

    public Vector3 GetScreenScale()
    {
        return GetCurrentScreenLocalScale();
    }

    public void SetTextureAspectCropEnabled(bool enabled)
    {
        preserveTextureAspectWithCrop = enabled;
        ApplyTextureCropForCurrentScreenScale();
    }

    void CacheOverlayTextScales()
    {
        if (overlayRoot == null) return;

        overlayTexts = overlayRoot.GetComponentsInChildren<TextMeshPro>(true);
        overlayTextBaseScales = new Vector3[overlayTexts.Length];

        for (int i = 0; i < overlayTexts.Length; i++)
        {
            overlayTextBaseScales[i] = overlayTexts[i] != null ? overlayTexts[i].transform.localScale : Vector3.one;
        }
    }

    void ApplyOverlayTextScaleCompensation()
    {
        if (overlayTexts == null || overlayTextBaseScales == null) return;
        if (screenRenderer == null) return;

        if (!compensateOverlayTextScale)
        {
            RestoreOverlayTextBaseScales();
            return;
        }

        Vector3 currentScale = screenRenderer.transform.localScale;
        Vector3 compensation = new Vector3(
            SafeScaleRatio(baseScreenLocalScale.x, currentScale.x),
            SafeScaleRatio(baseScreenLocalScale.y, currentScale.y),
            SafeScaleRatio(baseScreenLocalScale.z, currentScale.z)
        );

        for (int i = 0; i < overlayTexts.Length && i < overlayTextBaseScales.Length; i++)
        {
            if (overlayTexts[i] == null) continue;
            overlayTexts[i].transform.localScale = Vector3.Scale(overlayTextBaseScales[i], compensation);
        }
    }

    void RestoreOverlayTextBaseScales()
    {
        for (int i = 0; i < overlayTexts.Length && i < overlayTextBaseScales.Length; i++)
        {
            if (overlayTexts[i] == null) continue;
            overlayTexts[i].transform.localScale = overlayTextBaseScales[i];
        }
    }

    float SafeScaleRatio(float baseValue, float currentValue)
    {
        if (Mathf.Abs(currentValue) < 0.0001f) return 1f;
        return baseValue / currentValue;
    }

    void CacheBaseTextureTransform()
    {
        if (runtimeMaterial == null || string.IsNullOrEmpty(activeTextureProperty)) return;

        baseTextureScale = runtimeMaterial.GetTextureScale(activeTextureProperty);
        baseTextureOffset = runtimeMaterial.GetTextureOffset(activeTextureProperty);
    }

    void ApplyTextureCropForCurrentScreenScale()
    {
        if (runtimeMaterial == null || runtimeRT == null || string.IsNullOrEmpty(activeTextureProperty)) return;

        if (!preserveTextureAspectWithCrop)
        {
            runtimeMaterial.SetTextureScale(activeTextureProperty, baseTextureScale);
            runtimeMaterial.SetTextureOffset(activeTextureProperty, baseTextureOffset);
            return;
        }

        Vector3 screenScale = GetCurrentScreenLocalScale();
        float screenAspect = Mathf.Abs(screenScale.x) / Mathf.Max(0.01f, Mathf.Abs(screenScale.y));
        float textureAspect = runtimeRT.width / Mathf.Max(1f, (float)runtimeRT.height);
        float uvAspect = screenAspect / Mathf.Max(0.01f, textureAspect);

        Vector2 cropScale = Vector2.one;

        if (uvAspect > 1f)
        {
            cropScale.y = 1f / uvAspect;
        }
        else
        {
            cropScale.x = uvAspect;
        }

        Vector2 cropOffset = new Vector2(
            (1f - cropScale.x) * 0.5f,
            (1f - cropScale.y) * 0.5f
        );

        runtimeMaterial.SetTextureScale(activeTextureProperty, Vector2.Scale(baseTextureScale, cropScale));
        runtimeMaterial.SetTextureOffset(activeTextureProperty, baseTextureOffset + Vector2.Scale(baseTextureScale, cropOffset));
    }

    public void ApplyRandomScreenShape(bool useDistortion)
    {
        if (screenRenderer == null) return;

        Vector3 chosen = Vector3.one;

        if (useDistortion && distortedScreenScales != null && distortedScreenScales.Length > 0)
        {
            chosen = distortedScreenScales[Random.Range(0, distortedScreenScales.Length)];
        }
        else if (normalScreenScales != null && normalScreenScales.Length > 0)
        {
            chosen = normalScreenScales[Random.Range(0, normalScreenScales.Length)];
        }

        ApplyScreenScale(chosen);
    }

    public void SetVisible(bool visible)
    {
        if (screenRenderer != null)
            screenRenderer.enabled = visible;

        if (fragmentCamera != null)
            fragmentCamera.enabled = visible;

        if (overlayRoot != null)
        {
            overlayRoot.gameObject.SetActive(visible);

            if (visible)
            {
                RestoreTrackingBoxFrame();
                RefreshAnnotationMode();
                UpdateAnnotation();
            }
        }
    }

    void RestoreTrackingBoxFrame()
    {
        Transform trackingBoxFrame = overlayRoot.Find("TrackingBox_Frame");
        if (trackingBoxFrame == null) return;

        trackingBoxFrame.gameObject.SetActive(true);

        SetChildActive(trackingBoxFrame, "TB_Top");
        SetChildActive(trackingBoxFrame, "TB_Bottom");
        SetChildActive(trackingBoxFrame, "TB_Left");
        SetChildActive(trackingBoxFrame, "TB_Right");
        SetChildActive(trackingBoxFrame, "TrackLabel");
    }

    void SetChildActive(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            child.gameObject.SetActive(true);
    }

    public void SetAlpha(float alpha)
    {
        currentAlpha = alpha;

        if (runtimeMaterial != null)
        {
            if (runtimeMaterial.HasProperty("_BaseColor"))
            {
                Color c = runtimeMaterial.GetColor("_BaseColor");
                c.a = alpha;
                runtimeMaterial.SetColor("_BaseColor", c);
            }
            else if (runtimeMaterial.HasProperty("_Color"))
            {
                Color c = runtimeMaterial.GetColor("_Color");
                c.a = alpha;
                runtimeMaterial.SetColor("_Color", c);
            }
        }

        if (overlayRoot != null)
        {
            CanvasGroup cg = overlayRoot.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = alpha;
        }
    }

    public bool IsActive()
    {
        return isActive;
    }

    public Vector3 GetCurrentScreenLocalPosition()
    {
        if (screenRenderer != null)
            return screenRenderer.transform.localPosition;

        return Vector3.zero;
    }

    public Vector3 GetCurrentScreenLocalScale()
    {
        if (screenRenderer != null)
            return screenRenderer.transform.localScale;

        return Vector3.one;
    }

    public Camera GetFragmentCamera()
    {
        return fragmentCamera;
    }

    public BoneTrackingCamera GetTrackingCamera()
    {
        return trackingCamera;
    }

    void CacheAnnotationReferences()
    {
        if (overlayRoot == null) return;

        if (feedLabelController == null)
            feedLabelController = overlayRoot.GetComponentInChildren<FeedLabelController>(true);

        if (feedLabelController != null)
        {
            feedLabelController.enabled = false;
            feedMarker = feedLabelController.transform;

            if (feedLabelText == null)
                feedLabelText = feedLabelController.labelText;
        }

        if (feedLabelText == null)
        {
            Transform feedLabel = overlayRoot.Find("FeedMarker/FeedLabel");
            if (feedLabel != null)
                feedLabelText = feedLabel.GetComponent<TextMeshPro>();
        }

        if (feedMarker == null)
        {
            Transform marker = overlayRoot.Find("FeedMarker");
            if (marker != null)
                feedMarker = marker;
            else if (feedLabelText != null)
                feedMarker = feedLabelText.transform;
        }

        if (trackLabelController == null)
            trackLabelController = overlayRoot.GetComponentInChildren<TrackLabelController>(true);

        if (trackLabelController != null && trackLabelText == null)
            trackLabelText = trackLabelController.labelText;

        if (trackLabelText == null)
        {
            Transform trackLabel = overlayRoot.Find("TrackingBox_Frame/TrackLabel");
            if (trackLabel != null)
                trackLabelText = trackLabel.GetComponent<TextMeshPro>();
        }

        if (borderTop == null)
            borderTop = overlayRoot.Find("Border_Top");

        if (borderLeft == null)
            borderLeft = overlayRoot.Find("Border_Left");

        if (borderRight == null)
            borderRight = overlayRoot.Find("Border_Right");
    }

    void RefreshAnnotationMode()
    {
        if (!enableAnnotationSystem) return;

        CacheAnnotationReferences();

        if (feedLabelText != null)
        {
            feedLabelText.alignment = TextAlignmentOptions.TopLeft;
        }

        bool showTrackLabel = slotKind == FragmentSlotKind.Collective && showCollectiveBoneLabel;

        if (trackLabelController != null)
        {
            trackLabelController.showBoneName = showTrackLabel;
            trackLabelController.labelPrefix = "TRACK";
        }

        if (trackLabelText != null)
            trackLabelText.gameObject.SetActive(showTrackLabel);
    }

    void UpdateAnnotation()
    {
        if (!enableAnnotationSystem) return;

        CacheAnnotationReferences();
        UpdateFeedLabelContent();
        UpdateFeedLabelPosition();
        RefreshAnnotationMode();
    }

    void UpdateFeedLabelContent()
    {
        if (feedLabelText == null) return;

        Vector3 pos = transform.localPosition;
        string indexText = slotIndex.ToString("00");

        if (slotKind == FragmentSlotKind.Solo)
        {
            string label = $"{soloLabelPrefix}_{indexText}";

            if (showSoloCoordinates)
                label += $"\nX:{pos.x:F2} Y:{pos.y:F2}";

            feedLabelText.text = FormatAnnotationWithMarker(label);
            return;
        }

        string collectiveLabel = $"{collectiveLabelPrefix}_{indexText}";
        string bodyRegion = SimplifyBoneName(GetCurrentBoneName());

        if (showCollectiveBoneLabel)
            collectiveLabel += $"\n<color={annotationAccentHexColor}>TRACK_{bodyRegion}</color>";

        if (showCollectiveCoordinates)
            collectiveLabel += $"\nX:{pos.x:F2} Y:{pos.y:F2}";

        if (showCollectiveStatusLabel)
            collectiveLabel += $"\n{collectiveSourceLabel} <color={annotationAccentHexColor}>{collectiveStatusLabel}</color>";

        feedLabelText.text = FormatAnnotationWithMarker(collectiveLabel);
    }

    void UpdateFeedLabelPosition()
    {
        if (feedMarker == null || feedLabelText == null) return;

        Vector3 anchor = GetAnnotationAnchor();
        feedMarker.localPosition = anchor;

        feedLabelText.alignment = TextAlignmentOptions.TopLeft;
        feedLabelText.ForceMeshUpdate();

        Bounds bounds = feedLabelText.textBounds;
        Vector3 textScale = feedLabelText.transform.localScale;
        Vector3 textLocal = feedLabelText.transform.localPosition;
        textLocal.x = -bounds.min.x * textScale.x;
        textLocal.y = -bounds.max.y * textScale.y;
        feedLabelText.transform.localPosition = textLocal;
    }

    Vector3 GetAnnotationAnchor()
    {
        if (borderTop == null || borderLeft == null)
            return new Vector3(labelOffset.x, -labelOffset.y, 0.001f);

        bool outside = slotKind == FragmentSlotKind.Collective && collectiveLabelOutside;
        Vector2 offset = outside ? collectiveLabelOffset : labelOffset;
        Vector2 compensatedOffset = CompensateAnnotationOffset(offset);

        if (outside && borderRight != null)
        {
            return new Vector3(
                borderRight.localPosition.x + compensatedOffset.x,
                borderTop.localPosition.y - compensatedOffset.y,
                0.001f
            );
        }

        float y = outside
            ? borderTop.localPosition.y + compensatedOffset.y
            : borderTop.localPosition.y - compensatedOffset.y;

        return new Vector3(
            borderLeft.localPosition.x + compensatedOffset.x,
            y,
            0.001f
        );
    }

    Vector2 CompensateAnnotationOffset(Vector2 offset)
    {
        if (screenRenderer == null)
            return offset;

        Vector3 currentScale = screenRenderer.transform.localScale;
        return new Vector2(
            offset.x * SafeScaleRatio(baseScreenLocalScale.x, currentScale.x),
            offset.y * SafeScaleRatio(baseScreenLocalScale.y, currentScale.y)
        );
    }

    string FormatAnnotationWithMarker(string body)
    {
        string text = $"<color={annotationTextHexColor}>{body}</color>";

        if (!showAnnotationMarker)
            return text;

        string marker = $"<color={annotationMarkerHexColor}>{annotationMarkerSymbol}</color>";
        return $"{marker} {text}";
    }

    string GetCurrentBoneName()
    {
        if (trackingCamera == null) return string.Empty;
        return trackingCamera.GetCurrentBoneName();
    }

    string SimplifyBoneName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "UNK";

        if (fullName.Contains("Head")) return "HEAD";
        if (fullName.Contains("Spine2")) return "TORSO";
        if (fullName.Contains("Spine")) return "TORSO";
        if (fullName.Contains("Hips")) return "CORE";
        if (fullName.Contains("LeftArm")) return "ARM";
        if (fullName.Contains("RightArm")) return "ARM";
        if (fullName.Contains("LeftForeArm")) return "FOREARM";
        if (fullName.Contains("RightForeArm")) return "FOREARM";
        if (fullName.Contains("LeftHand")) return "HAND";
        if (fullName.Contains("RightHand")) return "HAND";

        return "SEG";
    }
}
