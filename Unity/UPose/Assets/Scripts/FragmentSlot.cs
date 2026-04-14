using UnityEngine;

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

    private Vector3 startPos;
    private Vector3 targetPos;
    private float timer;

    void Awake()
    {
        AutoAssignReferences();
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
            if (overlay != null) overlayRoot = overlay;
        }
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
        }
        else if (runtimeMaterial.HasProperty("_MainTex"))
        {
            runtimeMaterial.SetTexture("_MainTex", runtimeRT);
        }

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

        ApplyRandomScreenShape(profile.useDistortion);

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
    }

    public void Deactivate()
    {
        isActive = false;
        currentProfile = null;
        timer = 0f;
        SetVisible(false);
        SetAlpha(0f);
    }

    void ApplyRandomScreenShape(bool useDistortion)
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

        screenRenderer.transform.localScale = chosen;
    }

    public void SetVisible(bool visible)
    {
        if (screenRenderer != null)
            screenRenderer.enabled = visible;

        if (fragmentCamera != null)
            fragmentCamera.enabled = visible;

        if (overlayRoot != null)
            overlayRoot.gameObject.SetActive(visible);
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

    public Camera GetFragmentCamera()
    {
        return fragmentCamera;
    }

    public BoneTrackingCamera GetTrackingCamera()
    {
        return trackingCamera;
    }
}