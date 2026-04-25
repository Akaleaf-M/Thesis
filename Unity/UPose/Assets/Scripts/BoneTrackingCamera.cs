using UnityEngine;

public class BoneTrackingCamera : MonoBehaviour
{
    public string avatarRootName = "Avatar_Collective";
    public string boneName = "mixamorig:Head";

    public Vector3 localOffset = new Vector3(0f, 0.1f, -1.2f);

    public float targetSmooth = 6f;
    public float positionSmooth = 4f;
    public float lookSmooth = 4f;

    public bool useBoneRotation = false;

    private Transform targetBone;
    private Transform avatarRoot;

    private Vector3 smoothedTargetPosition;
    private bool initialized = false;

    void LateUpdate()
    {
        if (targetBone == null)
        {
            FindBone();
            if (targetBone == null) return;
        }

        if (!initialized)
        {
            smoothedTargetPosition = targetBone.position;
            initialized = true;
        }

        smoothedTargetPosition = Vector3.Lerp(
            smoothedTargetPosition,
            targetBone.position,
            Time.deltaTime * targetSmooth
        );

        Vector3 desiredPosition;

        if (useBoneRotation)
            desiredPosition = smoothedTargetPosition + targetBone.rotation * localOffset;
        else
            desiredPosition = smoothedTargetPosition + localOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            Time.deltaTime * positionSmooth
        );

        Quaternion desiredRotation = Quaternion.LookRotation(smoothedTargetPosition - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            Time.deltaTime * lookSmooth
        );
    }

    public void SetBone(string newBoneName)
    {
        boneName = newBoneName;
        targetBone = null;
        initialized = false;
        FindBone();
    }

    public void ApplyCameraProfile(
        Vector3 offset,
        float fov,
        float newTargetSmooth,
        float newPositionSmooth,
        float newLookSmooth,
        bool newUseBoneRotation
    )
    {
        localOffset = offset;
        targetSmooth = newTargetSmooth;
        positionSmooth = newPositionSmooth;
        lookSmooth = newLookSmooth;
        useBoneRotation = newUseBoneRotation;

        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.fieldOfView = fov;
        }
    }

    public Transform GetTargetBone()
    {
        return targetBone;
    }

    public string GetCurrentBoneName()
    {
        return boneName;
    }

    void FindBone()
    {
        if (avatarRoot == null)
        {
            GameObject avatarObj = GameObject.Find(avatarRootName);
            if (avatarObj != null)
            {
                avatarRoot = avatarObj.transform;
            }
        }

        if (avatarRoot == null) return;

        targetBone = FindChildRecursive(avatarRoot, boneName);
    }

    Transform FindChildRecursive(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName)
                return child;

            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
                return result;
        }
        return null;
    }

    public Transform FindBoneByName(string targetName)
    {
        if (avatarRoot == null)
        {
            GameObject avatarObj = GameObject.Find(avatarRootName);
            if (avatarObj != null)
            {
                avatarRoot = avatarObj.transform;
            }
        }

    if (avatarRoot == null) return null;

    return FindChildRecursive(avatarRoot, targetName);
    }
}