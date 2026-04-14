using TMPro;
using UnityEngine;

public class StreamToken : MonoBehaviour
{
    [Header("References")]
    public TextMeshPro textMesh;

    [Header("Motion")]
    public Vector3 moveDirection = Vector3.left;
    public float moveSpeed = 6f;

    [Header("Lifetime / Bounds")]
    public float destroyXMin = -12f;
    public float destroyXMax = 12f;

    public void Initialize(
        string content,
        Color color,
        Vector3 direction,
        float speed,
        float xMin,
        float xMax
    )
    {
        if (textMesh == null)
            textMesh = GetComponent<TextMeshPro>();

        if (textMesh != null)
        {
            textMesh.text = content;
            textMesh.color = color;
        }

        moveDirection = direction.normalized;
        moveSpeed = speed;
        destroyXMin = xMin;
        destroyXMax = xMax;
    }

    void Update()
    {
        transform.localPosition += moveDirection * moveSpeed * Time.deltaTime;

        float x = transform.localPosition.x;
        if (x < destroyXMin || x > destroyXMax)
        {
            Destroy(gameObject);
        }
    }
}