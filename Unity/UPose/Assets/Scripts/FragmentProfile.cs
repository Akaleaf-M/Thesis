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
}