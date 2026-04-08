using UnityEngine;

[System.Serializable]
public class FragmentProfile
{
    public string boneName;

    public Vector3 startPos;
    public Vector3 targetPos;

    public float lifeTime;
    public float fadeInTime;
    public float fadeOutTime;
    public float moveSpeed;

    public bool useDistortion;

    public Vector3 cameraOffset;
    public float cameraFOV;
    public float targetSmooth;
    public float positionSmooth;
    public float lookSmooth;
    public bool useBoneRotation;
}