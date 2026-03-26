using UnityEngine;

public class BoneTrackingCamera : MonoBehaviour
{
    public string avatarRootName = "Avatar1";
    public string boneName = "mixamorig:Head";

    public Vector3 localOffset = new Vector3(0f, 0.1f, -1.2f);

    public float targetSmooth = 6f;     // 骨骼目标点的平滑
    public float positionSmooth = 4f;   // 相机位置的平滑
    public float lookSmooth = 4f;       // 相机朝向的平滑

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

        // 先平滑骨骼目标点
        smoothedTargetPosition = Vector3.Lerp(
            smoothedTargetPosition,
            targetBone.position,
            Time.deltaTime * targetSmooth
        );

        // 再根据平滑后的目标点 + 骨骼朝向计算机位
        Vector3 desiredPosition = smoothedTargetPosition + targetBone.rotation * localOffset;

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
}