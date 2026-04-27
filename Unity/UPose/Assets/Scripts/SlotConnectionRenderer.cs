using System.Collections.Generic;
using UnityEngine;

public class SlotConnectionRenderer : MonoBehaviour
{
    [Header("Reference")]
    public FragmentController controller;

    [Header("Line Style")]
    public Material lineMaterial;
    public Color lineColor = new Color(1f, 1f, 1f, 0.16f);
    public float lineWidth = 0.012f;

    [Header("Depth / Alignment")]
    [Tooltip("Push line slightly backward or forward on Z if needed.")]
    public float lineZOffset = -0.005f;

    [Header("Optional Center Offsets")]
    [Tooltip("Use this only if your slot root pivot is not visually at the slot center.")]
    public Vector3 soloCenterOffset = Vector3.zero;

    [Tooltip("Use this only if your slot root pivot is not visually at the slot center.")]
    public Vector3 collectiveCenterOffset = Vector3.zero;

    [Header("Connection Reduction")]
    [Tooltip("Recommended: 1. Each collective connects only to the nearest solo.")]
    public int maxLinesPerCollective = 1;

    [Tooltip("Only works when maxLinesPerCollective > 1. Evaluated when connections are rebuilt, not every frame.")]
    [Range(0f, 1f)] public float secondLineChance = 0.0f;

    [Tooltip("Hard cap for total visible lines.")]
    public int maxTotalLines = 6;

    [Tooltip("How often connection pairs are recalculated. Lower = more responsive, higher = less flicker.")]
    public float rebuildInterval = 0.35f;

    [Header("Layer Control")]
    public string lineLayerName = "ConnectionLines";

    [Header("Visibility")]
    public bool hideLinesWhenNoCollective = true;

    private readonly List<LineRenderer> linePool = new List<LineRenderer>();
    private readonly List<ConnectionPair> activePairs = new List<ConnectionPair>();

    private float rebuildTimer = 0f;

    struct ConnectionPair
    {
        public FragmentSlot solo;
        public FragmentSlot collective;

        public ConnectionPair(FragmentSlot soloSlot, FragmentSlot collectiveSlot)
        {
            solo = soloSlot;
            collective = collectiveSlot;
        }
    }

    void Start()
    {
        ApplyLayerToRoot();
        RebuildConnections();
    }

    void LateUpdate()
    {
        if (controller == null || lineMaterial == null)
        {
            DisableAllLines();
            return;
        }

        rebuildTimer += Time.deltaTime;

        if (rebuildTimer >= rebuildInterval || HasInvalidPairs())
        {
            RebuildConnections();
            rebuildTimer = 0f;
        }

        DrawConnections();
    }

    void ApplyLayerToRoot()
    {
        int layer = LayerMask.NameToLayer(lineLayerName);

        if (layer >= 0)
        {
            gameObject.layer = layer;
        }
        else
        {
            Debug.LogWarning("Layer not found: " + lineLayerName + ". Please create this layer in Unity.");
        }
    }

    bool HasInvalidPairs()
    {
        for (int i = 0; i < activePairs.Count; i++)
        {
            ConnectionPair pair = activePairs[i];

            if (pair.solo == null || pair.collective == null)
                return true;

            if (!pair.solo.IsActive() || !pair.collective.IsActive())
                return true;
        }

        return false;
    }

    void RebuildConnections()
    {
        activePairs.Clear();

        if (controller == null) return;

        FragmentSlot[] soloSlots = controller.fixedSoloSlots;
        FragmentSlot[] collectiveSlots = controller.randomCollectiveSlots;

        if (soloSlots == null || collectiveSlots == null) return;

        List<FragmentSlot> activeSolos = GetActiveSlots(soloSlots);
        List<FragmentSlot> activeCollectives = GetActiveSlots(collectiveSlots);

        if (activeSolos.Count == 0 || activeCollectives.Count == 0)
            return;

        for (int c = 0; c < activeCollectives.Count; c++)
        {
            if (activePairs.Count >= maxTotalLines)
                break;

            FragmentSlot collective = activeCollectives[c];

            List<FragmentSlot> sortedSolos = new List<FragmentSlot>(activeSolos);
            sortedSolos.Sort((a, b) =>
            {
                float da = (GetSoloCenter(a) - GetCollectiveCenter(collective)).sqrMagnitude;
                float db = (GetSoloCenter(b) - GetCollectiveCenter(collective)).sqrMagnitude;
                return da.CompareTo(db);
            });

            int lineCountForThisCollective = Mathf.Max(1, maxLinesPerCollective);

            // First line: always connect to nearest solo.
            activePairs.Add(new ConnectionPair(sortedSolos[0], collective));

            // Optional second line: connects to second nearest solo.
            if (lineCountForThisCollective > 1 && sortedSolos.Count > 1 && activePairs.Count < maxTotalLines)
            {
                if (Random.value < secondLineChance)
                {
                    activePairs.Add(new ConnectionPair(sortedSolos[1], collective));
                }
            }
        }
    }

    List<FragmentSlot> GetActiveSlots(FragmentSlot[] slots)
    {
        List<FragmentSlot> result = new List<FragmentSlot>();

        if (slots == null) return result;

        for (int i = 0; i < slots.Length; i++)
        {
            FragmentSlot slot = slots[i];

            if (slot != null && slot.IsActive())
                result.Add(slot);
        }

        return result;
    }

    void DrawConnections()
    {
        int neededCount = activePairs.Count;

        if (hideLinesWhenNoCollective && neededCount == 0)
        {
            DisableAllLines();
            return;
        }

        EnsureLinePoolSize(neededCount);

        for (int i = 0; i < activePairs.Count; i++)
        {
            ConnectionPair pair = activePairs[i];

            if (pair.solo == null || pair.collective == null)
                continue;

            LineRenderer lr = linePool[i];
            lr.enabled = true;

            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;

            lr.positionCount = 2;
            lr.SetPosition(0, GetSoloCenter(pair.solo));
            lr.SetPosition(1, GetCollectiveCenter(pair.collective));
        }

        for (int i = activePairs.Count; i < linePool.Count; i++)
        {
            if (linePool[i] != null)
                linePool[i].enabled = false;
        }
    }

    void EnsureLinePoolSize(int count)
    {
        while (linePool.Count < count)
        {
            linePool.Add(CreateLineRenderer(linePool.Count));
        }
    }

    LineRenderer CreateLineRenderer(int index)
    {
        GameObject go = new GameObject("ConnectionLine_" + index);
        go.transform.SetParent(transform, false);

        int layer = LayerMask.NameToLayer(lineLayerName);

        if (layer >= 0)
        {
            go.layer = layer;
        }
        else
        {
            Debug.LogWarning("Layer not found: " + lineLayerName + ". Please create this layer in Unity.");
        }

        LineRenderer lr = go.AddComponent<LineRenderer>();

        lr.material = lineMaterial;
        lr.useWorldSpace = true;

        lr.positionCount = 2;

        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;

        lr.startColor = lineColor;
        lr.endColor = lineColor;

        lr.alignment = LineAlignment.View;

        lr.numCapVertices = 0;
        lr.numCornerVertices = 0;
        lr.textureMode = LineTextureMode.Stretch;

        lr.enabled = false;

        return lr;
    }

    Vector3 GetSoloCenter(FragmentSlot slot)
    {
        Vector3 p = slot.transform.position + soloCenterOffset;
        p.z += lineZOffset;
        return p;
    }

    Vector3 GetCollectiveCenter(FragmentSlot slot)
    {
        Vector3 p = slot.transform.position + collectiveCenterOffset;
        p.z += lineZOffset;
        return p;
    }

    void DisableAllLines()
    {
        for (int i = 0; i < linePool.Count; i++)
        {
            if (linePool[i] != null)
                linePool[i].enabled = false;
        }
    }
}