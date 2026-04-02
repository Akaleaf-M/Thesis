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

    [Header("Normal Screen Shapes")]
    public Vector3[] normalScreenScales = new Vector3[]
    {
        new Vector3(1.6f, 0.9f, 1f),
        new Vector3(2.0f, 1.125f, 1f),
        new Vector3(1.2f, 0.675f, 1f)
    };

    [Header("Distorted Screen Shapes")]
    public Vector3[] distortedScreenScales = new Vector3[]
    {
        new Vector3(1.0f, 1.0f, 1f),
        new Vector3(0.8f, 1.4f, 1f),
        new Vector3(2.2f, 0.7f, 1f)
    };

    [Range(0f, 1f)]
    public float distortionChance = 0.2f;

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
            ApplyRandomScreenShape();
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

    private void ApplyRandomScreenShape()
    {
        if (screenRenderer == null) return;

        bool useDistorted = Random.value < distortionChance;
        Vector3 chosenScale = Vector3.one;

        if (useDistorted)
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
}