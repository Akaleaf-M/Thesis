using UnityEngine;

public enum FragmentSourceType
{
    Collective,
    Solo
}

[System.Serializable]
public class FragmentProfile
{
    [Header("Source")]
    public FragmentSourceType sourceType = FragmentSourceType.Collective;
    public int soloIndex = -1;   // 1=P1, 2=P2, 3=P3, 4=P4

    [Header("Tracking")]
    public string boneName = "mixamorig:Spine2";
    public bool useBoneRotation = false;

    [Header("Screen Motion")]
    public Vector3 startPos = Vector3.zero;
    public Vector3 targetPos = Vector3.zero;
    public float moveSpeed = 1.0f;

    [Header("Lifetime")]
    public float lifeTime = 4.0f;
    public float fadeInTime = 0.3f;
    public float fadeOutTime = 0.5f;

    [Header("Camera")]
    public Vector3 cameraOffset = new Vector3(0f, 0f, -2f);
    public float cameraFOV = 35f;
    public float targetSmooth = 6f;
    public float positionSmooth = 4f;
    public float lookSmooth = 4f;

    [Header("Visual")]
    public bool useDistortion = false;
}