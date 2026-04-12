using UnityEngine;

public class FragmentSlot : MonoBehaviour
{
    [Header("References")]
    public BoneTrackingCamera trackingCamera;
    public Camera fragmentCamera;
    public Renderer screenRenderer;

    [Header("Default Lifecycle")]
    public float defaultLifeTime = 3f;
    public float defaultFadeInTime = 0.4f;
    public float defaultFadeOutTime = 0.6f;

    [Header("Default Screen Movement")]
    public float defaultMoveSpeed = 1.5f;

    [Header("Normal Screen Shapes")]
    public Vector3[] normalScreenScales = new Vector3[]
    {
        new Vector3(1.2f, 0.675f, 1f),
        new Vector3(1.6f, 0.9f, 1f),
        new Vector3(2.0f, 1.125f, 1f),
        new Vector3(2.4f, 1.35f, 1f)
    };

    [Header("Distorted Screen Shapes")]
    public Vector3[] distortedScreenScales = new Vector3[]
    {
        new Vector3(1.0f, 1.0f, 1f),
        new Vector3(0.9f, 1.5f, 1f),
        new Vector3(2.2f, 0.7f, 1f),
        new Vector3(1.4f, 0.5f, 1f)
    };
    
    [Header("Slot Identity")]
    public int slotIndex = 1;

    private bool isActive = false;
    private float timer = 0f;

    private Vector3 screenStartLocalPos;
    private Vector3 screenTargetLocalPos;

    private float lifeTime;
    private float fadeInTime;
    private float fadeOutTime;
    private float moveSpeed;

    private Material runtimeMaterial;
    private Color baseColor;

    void Start()
    {
        if (screenRenderer != null)
        {
            runtimeMaterial = screenRenderer.material;
            baseColor = runtimeMaterial.color;
        }

        SetVisible(false);
    }

    void Update()
    {
        if (!isActive) return;

        timer += Time.deltaTime;

        if (screenRenderer != null)
        {
            screenRenderer.transform.localPosition = Vector3.Lerp(
                screenRenderer.transform.localPosition,
                screenTargetLocalPos,
                Time.deltaTime * moveSpeed
            );
        }

        float alpha = 1f;

        if (timer < fadeInTime)
        {
            alpha = timer / fadeInTime;
        }
        else if (timer > lifeTime - fadeOutTime)
        {
            alpha = Mathf.Clamp01((lifeTime - timer) / fadeOutTime);
        }

        SetAlpha(alpha);

        if (timer >= lifeTime)
        {
            Deactivate();
        }
    }

    public void Activate(FragmentProfile profile)
    {
        if (profile == null) return;

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

        screenStartLocalPos = profile.startPos;
        screenTargetLocalPos = profile.targetPos;

        lifeTime = profile.lifeTime > 0 ? profile.lifeTime : defaultLifeTime;
        fadeInTime = profile.fadeInTime > 0 ? profile.fadeInTime : defaultFadeInTime;
        fadeOutTime = profile.fadeOutTime > 0 ? profile.fadeOutTime : defaultFadeOutTime;
        moveSpeed = profile.moveSpeed > 0 ? profile.moveSpeed : defaultMoveSpeed;

        if (screenRenderer != null)
        {
            screenRenderer.transform.localPosition = screenStartLocalPos;
            ApplyRandomScreenShape(profile.useDistortion);
        }

        timer = 0f;
        isActive = true;

        SetAlpha(0f);
        SetVisible(true);
    }

    public void Deactivate()
    {
        isActive = false;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (fragmentCamera != null)
        {
            fragmentCamera.gameObject.SetActive(visible);
        }

        if (screenRenderer != null)
        {
            screenRenderer.gameObject.SetActive(visible);
        }
    }

    private void SetAlpha(float alpha)
    {
        if (runtimeMaterial == null) return;

        Color c = baseColor;
        c.a = alpha;
        runtimeMaterial.color = c;
    }

    private void ApplyRandomScreenShape(bool useDistortion)
    {
        if (screenRenderer == null) return;

        Vector3 chosenScale = Vector3.one;

        if (useDistortion)
        {
            if (distortedScreenScales != null && distortedScreenScales.Length > 0)
            {
                chosenScale = distortedScreenScales[Random.Range(0, distortedScreenScales.Length)];
            }
        }
        else
        {
            if (normalScreenScales != null && normalScreenScales.Length > 0)
            {
                chosenScale = normalScreenScales[Random.Range(0, normalScreenScales.Length)];
            }
        }

        screenRenderer.transform.localScale = chosenScale;
    }

    public bool IsActive()
    {
        return isActive;
    }

    
    public Camera GetFragmentCamera()
    {
        return fragmentCamera;
    }

    public BoneTrackingCamera GetTrackingCamera()
    {
        return trackingCamera;
    }

    public Vector3 GetCurrentScreenLocalPosition()
    {
    if (screenRenderer != null)
        return screenRenderer.transform.localPosition;

    return Vector3.zero;
    }
}