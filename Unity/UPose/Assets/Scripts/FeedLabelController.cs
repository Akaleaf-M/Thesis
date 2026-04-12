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
    public string markerHexColor = "#A6FF00";
    public string markerSymbol = "■";

    [Header("Data Source")]
    public bool useScreenLocalPosition = true;

    [Header("Layout")]
    public Vector3 anchorOffset = new Vector3(-0.42f, 0.40f, 0.001f);

    [Header("Behavior")]
    public bool updateLayoutEveryFrame = false;

    void Start()
    {
        ApplyLayout();
        UpdateLabel();
    }

    void LateUpdate()
    {
        if (updateLayoutEveryFrame)
        {
            ApplyLayout();
        }

        UpdateLabel();
    }

    void ApplyLayout()
    {
        transform.localPosition = anchorOffset;
    }

    void UpdateLabel()
    {
        if (fragmentSlot == null || labelText == null) return;

        int index = fragmentSlot.slotIndex;

        string marker = $"<color={markerHexColor}>{markerSymbol}</color>";
        string line1 = $"{marker} {labelPrefix}_{index:00}";

        if (!showCoordinates)
        {
            labelText.text = line1;
            return;
        }

        Vector3 pos = useScreenLocalPosition
            ? fragmentSlot.GetCurrentScreenLocalPosition()
            : fragmentSlot.transform.localPosition;

        string format = "F" + decimalPlaces;
        string line2 = $"X:{pos.x.ToString(format)} Y:{pos.y.ToString(format)}";

        labelText.text = line1 + "\n" + line2;
    }
}