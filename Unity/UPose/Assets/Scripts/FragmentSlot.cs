using UnityEngine;

public class FragmentSlot : MonoBehaviour
{
    [Header("References")]
    public BoneTrackingCamera trackingCamera;
    public Camera fragmentCamera;
    public Renderer screenRenderer;

    [Header("Lifecycle")]
    public float lifeTime = 3f;
    public float fadeInTime = 0.4f;
    public float fadeOutTime = 0.6f;

    [Header("Screen Movement")]
    public float moveSpeed = 1.5f;

    private bool isActive = false;
    private float timer = 0f;

    private Vector3 screenStartLocalPos;
    private Vector3 screenTargetLocalPos;

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

        // 只让 screen 自己漂移，不移动整个 slot
        if (screenRenderer != null)
        {
            screenRenderer.transform.localPosition = Vector3.Lerp(
                screenRenderer.transform.localPosition,
                screenTargetLocalPos,
                Time.deltaTime * moveSpeed
            );
        }

        // alpha 生命周期
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

    public void Activate(string boneName, Vector3 localStartPos, Vector3 localTargetPos)
    {
        Debug.Log("FragmentSlot Activate called: " + boneName);

        if (trackingCamera != null)
        {
            trackingCamera.SetBone(boneName);
        }

        screenStartLocalPos = localStartPos;
        screenTargetLocalPos = localTargetPos;

        if (screenRenderer != null)
        {
            screenRenderer.transform.localPosition = screenStartLocalPos;
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

    public bool IsActive()
    {
        return isActive;
    }
}