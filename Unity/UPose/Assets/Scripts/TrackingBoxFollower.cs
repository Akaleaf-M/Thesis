using System.Collections.Generic;
using UnityEngine;

public class TrackingBoxFollower : MonoBehaviour
{
    [Header("References")]
    public FragmentSlot fragmentSlot;
    public Transform overlayRoot;

    [Header("Tracking Box Parts")]
    public Transform topLine;
    public Transform bottomLine;
    public Transform leftLine;
    public Transform rightLine;

    [Header("Viewport Mapping")]
    public Vector2 viewportMin = new Vector2(-0.5f, -0.5f);
    public Vector2 viewportMax = new Vector2(0.5f, 0.5f);

    [Header("Follow")]
    public float followSmooth = 8f;
    public float sizeSmooth = 8f;

    [Header("Box Size Limits")]
    public Vector2 minBoxSize = new Vector2(0.12f, 0.12f);
    public Vector2 maxBoxSize = new Vector2(0.65f, 0.65f);

    [Header("Frame Thickness")]
    public float lineThickness = 0.01f;

    [Header("Padding")]
    public Vector2 boxPadding = new Vector2(0.03f, 0.03f);

    [Header("Behavior")]
    public bool hideWhenBehindCamera = true;
    public bool addMicroJitter = true;
    public float jitterAmount = 0.005f;
    public float jitterSpeed = 2f;

    [Header("Default State")]
    public Vector2 defaultBoxSize = new Vector2(0.25f, 0.25f);
    public Vector3 defaultLocalPosition = Vector3.zero;

    private Vector3 smoothedLocalPos;
    private Vector2 smoothedBoxSize;

    void LateUpdate()
    {
        if (fragmentSlot == null || overlayRoot == null) return;

        Camera cam = fragmentSlot.GetFragmentCamera();
        BoneTrackingCamera btc = fragmentSlot.GetTrackingCamera();

        if (cam == null || btc == null) return;

        string currentBone = btc.GetCurrentBoneName();
        if (string.IsNullOrEmpty(currentBone)) return;

        List<Transform> refs = GetReferenceBones(currentBone, btc);
        if (refs.Count == 0) return;

        List<Vector3> validViewportPoints = new List<Vector3>();

        foreach (Transform t in refs)
        {
            if (t == null) continue;

            Vector3 vp = cam.WorldToViewportPoint(t.position);
            if (vp.z > 0f)
            {
                validViewportPoints.Add(vp);
            }
        }

        if (validViewportPoints.Count == 0)
        {
            if (hideWhenBehindCamera)
                gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        float minX = validViewportPoints[0].x;
        float maxX = validViewportPoints[0].x;
        float minY = validViewportPoints[0].y;
        float maxY = validViewportPoints[0].y;

        foreach (Vector3 vp in validViewportPoints)
        {
            if (vp.x < minX) minX = vp.x;
            if (vp.x > maxX) maxX = vp.x;
            if (vp.y < minY) minY = vp.y;
            if (vp.y > maxY) maxY = vp.y;
        }

        minX -= boxPadding.x;
        maxX += boxPadding.x;
        minY -= boxPadding.y;
        maxY += boxPadding.y;

        Vector2 centerViewport = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        Vector2 sizeViewport = new Vector2(maxX - minX, maxY - minY);

        float localX = Mathf.Lerp(viewportMin.x, viewportMax.x, centerViewport.x);
        float localY = Mathf.Lerp(viewportMin.y, viewportMax.y, centerViewport.y);

        Vector2 targetSize = new Vector2(
            Mathf.Lerp(0f, viewportMax.x - viewportMin.x, sizeViewport.x),
            Mathf.Lerp(0f, viewportMax.y - viewportMin.y, sizeViewport.y)
        );

        targetSize.x = Mathf.Clamp(targetSize.x, minBoxSize.x, maxBoxSize.x);
        targetSize.y = Mathf.Clamp(targetSize.y, minBoxSize.y, maxBoxSize.y);

        Vector3 targetLocalPos = new Vector3(localX, localY, transform.localPosition.z);

        if (addMicroJitter)
        {
            float jx = Mathf.PerlinNoise(Time.time * jitterSpeed, 0f) - 0.5f;
            float jy = Mathf.PerlinNoise(0f, Time.time * jitterSpeed) - 0.5f;
            targetLocalPos.x += jx * jitterAmount;
            targetLocalPos.y += jy * jitterAmount;
        }

        smoothedLocalPos = Vector3.Lerp(
            transform.localPosition,
            targetLocalPos,
            Time.deltaTime * followSmooth
        );

        smoothedBoxSize = Vector2.Lerp(
            smoothedBoxSize,
            targetSize,
            Time.deltaTime * sizeSmooth
        );

        transform.localPosition = smoothedLocalPos;

        UpdateFrame(smoothedBoxSize);
    }

    void UpdateFrame(Vector2 size)
    {
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        if (topLine != null)
        {
            topLine.localPosition = new Vector3(0f, halfH, 0f);
            topLine.localScale = new Vector3(size.x, lineThickness, 1f);
        }

        if (bottomLine != null)
        {
            bottomLine.localPosition = new Vector3(0f, -halfH, 0f);
            bottomLine.localScale = new Vector3(size.x, lineThickness, 1f);
        }

        if (leftLine != null)
        {
            leftLine.localPosition = new Vector3(-halfW, 0f, 0f);
            leftLine.localScale = new Vector3(lineThickness, size.y, 1f);
        }

        if (rightLine != null)
        {
            rightLine.localPosition = new Vector3(halfW, 0f, 0f);
            rightLine.localScale = new Vector3(lineThickness, size.y, 1f);
        }
    }

    List<Transform> GetReferenceBones(string currentBone, BoneTrackingCamera btc)
    {
        List<Transform> refs = new List<Transform>();

        void Add(string boneName)
        {
            Transform t = btc.FindBoneByName(boneName);
            if (t != null) refs.Add(t);
        }

        if (currentBone == "mixamorig:Hips")
        {
            Add("mixamorig:Hips");
            Add("mixamorig:LeftUpLeg");
            Add("mixamorig:RightUpLeg");
            Add("mixamorig:Spine");
        }
        else if (currentBone == "mixamorig:Spine" || currentBone == "mixamorig:Spine2")
        {
            Add("mixamorig:Spine");
            Add("mixamorig:Spine2");
            Add("mixamorig:LeftShoulder");
            Add("mixamorig:RightShoulder");
            Add("mixamorig:Hips");
        }
        else if (currentBone == "mixamorig:Head")
        {
            Add("mixamorig:Head");
            Add("mixamorig:Neck");
            Add("mixamorig:Spine2");
        }
        else if (currentBone == "mixamorig:LeftArm")
        {
            Add("mixamorig:LeftShoulder");
            Add("mixamorig:LeftArm");
            Add("mixamorig:LeftForeArm");
        }
        else if (currentBone == "mixamorig:RightArm")
        {
            Add("mixamorig:RightShoulder");
            Add("mixamorig:RightArm");
            Add("mixamorig:RightForeArm");
        }
        else if (currentBone == "mixamorig:LeftForeArm" || currentBone == "mixamorig:LeftHand")
        {
            Add("mixamorig:LeftForeArm");
            Add("mixamorig:LeftHand");
        }
        else if (currentBone == "mixamorig:RightForeArm" || currentBone == "mixamorig:RightHand")
        {
            Add("mixamorig:RightForeArm");
            Add("mixamorig:RightHand");
        }
        else
        {
            Transform t = btc.GetTargetBone();
            if (t != null) refs.Add(t);
        }

        return refs;
    }

    void Start()
    {
        smoothedLocalPos = defaultLocalPosition;
        smoothedBoxSize = defaultBoxSize;
        transform.localPosition = smoothedLocalPos;
        UpdateFrame(smoothedBoxSize);
    }
}