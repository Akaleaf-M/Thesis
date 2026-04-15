using UnityEngine;

public class FaceCameraBillboard : MonoBehaviour
{
    [Header("Reference")]
    public Camera targetCamera;

    [Header("Behavior")]
    public bool reverseForward = true;

    void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 dir = transform.position - targetCamera.transform.position;

        if (dir.sqrMagnitude < 0.0001f) return;

        if (reverseForward)
            transform.forward = dir.normalized;
        else
            transform.forward = -dir.normalized;
    }
}