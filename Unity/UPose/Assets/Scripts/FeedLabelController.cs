using TMPro;
using UnityEngine;

public class FeedLabelController : MonoBehaviour
{
    [Header("References")]
    public FragmentSlot fragmentSlot;
    public TextMeshPro labelText;

    [Header("Format")]
    public string labelPrefix = "FEED";
    public bool showCoordinates = true;
    public int decimalPlaces = 2;

    [Header("Marker Style")]
    public string markerHexColor = "#00FF00";
    public string markerSymbol = "■";

    [Header("Text Style")]
    public string textHexColor = "#FFFFFF";

    [Header("Layout")]
    public Vector3 anchorOffset = new Vector3(-0.42f, 0.40f, 0.001f);

    [Header("Behavior")]
    public bool updateLayoutEveryFrame = false;
    public bool autoFindFragmentSlot = true;
    public bool autoFindLabelText = true;

    void Awake()
    {
        ResolveReferences();
    }

    void Start()
    {
        ResolveReferences();
        ApplyLayout();
        UpdateLabel();
    }

    void LateUpdate()
    {
        if (autoFindFragmentSlot && (fragmentSlot == null || !fragmentSlot.gameObject.scene.IsValid()))
        {
            ResolveReferences();
        }

        if (updateLayoutEveryFrame)
        {
            ApplyLayout();
        }

        UpdateLabel();
    }

    void ResolveReferences()
    {
        if (autoFindFragmentSlot)
        {
            fragmentSlot = GetComponentInParent<FragmentSlot>();
        }

        if (autoFindLabelText && labelText == null)
        {
            labelText = GetComponentInChildren<TextMeshPro>(true);
        }
    }

    void ApplyLayout()
    {
        transform.localPosition = anchorOffset;
    }

    void UpdateLabel()
    {
        if (fragmentSlot == null || labelText == null) return;

        int index = fragmentSlot.slotIndex;
        Vector3 pos = fragmentSlot.transform.localPosition;

        string marker = $"<color={markerHexColor}>{markerSymbol}</color>";
        string head = $"<color={textHexColor}>{labelPrefix}_{index:00}</color>";

        if (!showCoordinates)
        {
            labelText.text = $"{marker} {head}";
            return;
        }

        string format = "F" + decimalPlaces;
        string coords = $"<color={textHexColor}>X:{pos.x.ToString(format)} Y:{pos.y.ToString(format)}</color>";

        labelText.text = $"{marker} {head}\n{coords}";
    }
}