using TMPro;
using UnityEngine;

public class TrackLabelController : MonoBehaviour
{
    [Header("References")]
    public FragmentSlot fragmentSlot;
    public TextMeshPro labelText;

    [Header("Tracking Box Reference")]
    public Transform topLine;

    [Header("Content")]
    public string labelPrefix = "TRACK";
    public bool showBoneName = false;

    [Header("Layout")]
    public float leftPadding = 0.00f;
    public float topPadding = 0.03f;
    public float zOffset = 0.001f;

    void Start()
    {
        ForceLeftAlignment();
        UpdateLabel();
        UpdatePosition();
    }

    void LateUpdate()
    {
        UpdateLabel();
        UpdatePosition();
    }

    void ForceLeftAlignment()
    {
        if (labelText != null)
        {
            labelText.alignment = TextAlignmentOptions.Left;
        }
    }

    void UpdatePosition()
    {
        if (topLine == null || labelText == null) return;

        // 先强制刷新文本几何信息
        labelText.ForceMeshUpdate();

        float topWidth = topLine.localScale.x;
        float leftEdgeX = topLine.localPosition.x - (topWidth * 0.5f);

        // 关键：拿到当前文本的可视宽度，并补 half width
        float textWidth = labelText.textBounds.size.x;

        transform.localPosition = new Vector3(
            leftEdgeX + leftPadding + (textWidth * 0.5f),
            topLine.localPosition.y + topPadding,
            zOffset
        );
    }

    void UpdateLabel()
    {
        if (labelText == null) return;

        if (!showBoneName || fragmentSlot == null)
        {
            labelText.text = labelPrefix;
            return;
        }

        BoneTrackingCamera btc = fragmentSlot.GetTrackingCamera();
        if (btc == null)
        {
            labelText.text = labelPrefix;
            return;
        }

        string boneName = btc.GetCurrentBoneName();
        string shortBone = SimplifyBoneName(boneName);

        labelText.text = $"{labelPrefix}_{shortBone}";
    }

    string SimplifyBoneName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "UNK";

        if (fullName.Contains("Head")) return "HEAD";
        if (fullName.Contains("Spine2")) return "TORSO";
        if (fullName.Contains("Spine")) return "TORSO";
        if (fullName.Contains("Hips")) return "CORE";
        if (fullName.Contains("LeftArm")) return "L_ARM";
        if (fullName.Contains("RightArm")) return "R_ARM";
        if (fullName.Contains("LeftForeArm")) return "L_FORE";
        if (fullName.Contains("RightForeArm")) return "R_FORE";
        if (fullName.Contains("LeftHand")) return "L_HAND";
        if (fullName.Contains("RightHand")) return "R_HAND";

        return "SEG";
    }
}