using System.Collections.Generic;
using UnityEngine;

public enum WaterfallPreset
{
    WaterfallA,
    WaterfallB,
    Custom
}

public enum WaterfallVisualMode
{
    TestPatternHorizontal,
    DataWaterfallVertical
}

public class WaterfallController : MonoBehaviour
{
    [Header("Preset")]
    public WaterfallPreset preset = WaterfallPreset.WaterfallA;
    public bool useOutputModeManagerPreset = true;
    public bool applyPresetOnStart = true;
    public bool preserveInspectorLanePaddingOnPreset = true;

    [Header("Visual Mode")]
    public WaterfallVisualMode visualMode = WaterfallVisualMode.TestPatternHorizontal;

    [Header("Layout")]
    public float worldWidth = 12.8f;
    public float worldHeight = 8f;
    public float zOffset = 0f;

    [Header("Global Control")]
    [Range(0f, 3f)] public float globalIntensity = 1f;
    [Range(0f, 4f)] public float speedMultiplier = 1f;
    [Range(0f, 3f)] public float densityMultiplier = 1f;
    [Range(0f, 1f)] public float accentProbability = 0.025f;
    [Range(0f, 1f)] public float glitchProbability = 0.05f;
    [Range(0f, 1f)] public float pulseProbability = 0.035f;
    public Color defaultColor = new Color(0.86f, 0.86f, 0.86f, 1f);
    public Color dimColor = new Color(0.42f, 0.42f, 0.42f, 1f);
    public Color brightColor = new Color(1f, 1f, 1f, 1f);
    public Color accentColor = new Color(0f, 1f, 0f, 1f);
    public Color secondaryAccentColor = new Color(0f, 1f, 1f, 1f);
    [Range(0f, 1f)] public float baseAlpha = 0.72f;

    [Header("Display Structure")]
    public bool showCalibrationFrame = true;
    [Range(0f, 1f)] public float scaffoldAlphaMultiplier = 0.35f;

    [Header("Future VCV Placeholder")]
    [Range(0f, 1f)] public float pulse = 0f;
    [Range(0f, 1f)] public float accentTrigger = 0f;
    public float pulseDecay = 1.8f;
    public float accentDecay = 2.5f;

    [Header("TestPatternHorizontal")]
    public int horizontalUnitCount = 90;
    public int horizontalRowCount = 10;
    public Vector2 horizontalWidthRange = new Vector2(0.35f, 2.4f);
    public Vector2 horizontalShortWidthRange = new Vector2(0.12f, 0.65f);
    public Vector2 horizontalLongWidthRange = new Vector2(2.2f, 5.4f);
    public Vector2 horizontalHeightRange = new Vector2(0.08f, 0.55f);
    public Vector2 horizontalStripeWidthRange = new Vector2(0.025f, 0.12f);
    public Vector2 horizontalStripeHeightRange = new Vector2(0.45f, 1.65f);
    public Vector2 horizontalSpeedRange = new Vector2(0.05f, 0.55f);
    public bool horizontalUseSteppedMotion = false;
    public float horizontalStepInterval = 0.12f;
    public float horizontalGridJitter = 0.06f;
    [Range(0f, 1.5f)]
    public float horizontalLanePadding = 0.55f;
    public bool horizontalBarcodeAlignment = true;
    public int horizontalUnitsPerRow = 96;
    public Vector2 horizontalBarcodeGapRange = new Vector2(0.012f, 0.055f);
    [Range(0f, 1f)] public float horizontalOutlineProbability = 0.22f;
    [Range(0f, 1f)] public float horizontalStripeProbability = 0.72f;
    [Range(0f, 1f)] public float horizontalShortBarProbability = 0.55f;
    [Range(0f, 1f)] public float horizontalLongBarProbability = 0.18f;
    [Range(0f, 1f)] public float horizontalBlinkProbability = 0.04f;
    [Range(0f, 1f)] public float horizontalResetProbability = 0.008f;
    public bool horizontalShowLaneGuides = true;
    public float horizontalFrameInset = 0.08f;

    [Header("DataWaterfallVertical")]
    public int verticalStreamCount = 140;
    public int verticalColumnCount = 96;
    public int segmentsPerStreamMin = 3;
    public int segmentsPerStreamMax = 10;
    public float segmentWidthMin = 0.015f;
    public float segmentWidthMax = 0.075f;
    public float segmentHeightMin = 0.035f;
    public float segmentHeightMax = 0.32f;
    public float segmentGapMin = 0.035f;
    public float segmentGapMax = 0.28f;
    public float fallSpeedMin = 0.7f;
    public float fallSpeedMax = 3.2f;
    public float alphaMin = 0.25f;
    public float alphaMax = 0.82f;
    public float brightnessMin = 0.55f;
    public float brightnessMax = 1f;
    public float jitterAmount = 0.035f;
    public float resetProbability = 0.004f;
    public bool verticalShowColumnGuides = true;
    public int verticalGuideEveryColumns = 12;
    public float verticalFrameInset = 0.08f;

    [Header("Runtime Objects")]
    public string meshRootName = "WaterfallRuntimeMeshes";
    public bool liveApplyInspectorChanges = true;

    private const int DimGroup = 0;
    private const int DefaultGroup = 1;
    private const int BrightGroup = 2;
    private const int AccentGroup = 3;
    private const int SecondaryAccentGroup = 4;
    private const int GroupCount = 5;
    private const float WaterfallBLanePaddingPreset = 0.55f;

    private class MeshLayer
    {
        public Mesh mesh;
        public Material material;
        public readonly List<Vector3> vertices = new List<Vector3>(8192);
        public readonly List<int> indices = new List<int>(8192);
    }

    private class HorizontalUnit
    {
        public float x;
        public float y;
        public float width;
        public float height;
        public float speed;
        public int group;
        public bool outline;
        public bool visible;
        public float stepTimer;
    }

    private class VerticalSegment
    {
        public float offsetY;
        public float offsetX;
        public float width;
        public float height;
        public int group;
        public bool visible;
    }

    private class VerticalStream
    {
        public float x;
        public float y;
        public float speed;
        public float height;
        public float phase;
        public VerticalSegment[] segments;
    }

    private Transform meshRoot;
    private MeshLayer[] lineLayers;
    private MeshLayer[] fillLayers;
    private HorizontalUnit[] horizontalUnits;
    private VerticalStream[] verticalStreams;
    private bool isGlitching;
    private float glitchTimer;
    private float glitchDuration;
    private int lastHorizontalUnitCount;
    private int lastHorizontalRowCount;
    private int lastHorizontalUnitsPerRow;
    private bool lastHorizontalBarcodeAlignment;
    private float lastHorizontalLanePadding;
    private Vector2 lastHorizontalStripeHeightRange;
    private Vector2 lastHorizontalSpeedRange;
    private int lastVerticalStreamCount;

    void Awake()
    {
        ClampControlRanges();

        if (useOutputModeManagerPreset)
            ResolvePresetFromOutputMode();

        if (applyPresetOnStart)
            ApplyPreset();

        InitializeRuntime();
    }

    void OnDestroy()
    {
        CleanupLayers(lineLayers);
        CleanupLayers(fillLayers);
    }

    void Update()
    {
        if (lineLayers == null || fillLayers == null)
            return;

        ClampControlRanges();
        RefreshRuntimeFromInspectorChanges();
        UpdateExternalControlDecay();
        UpdateGlitchState();

        ClearLayers(lineLayers);
        ClearLayers(fillLayers);

        if (visualMode == WaterfallVisualMode.TestPatternHorizontal)
        {
            UpdateTestPatternHorizontal();
            RebuildTestPatternHorizontalMeshes();
        }
        else
        {
            UpdateDataWaterfallVertical();
            RebuildDataWaterfallVerticalMeshes();
        }

        ApplyLayerColors();
        UploadLayers(lineLayers, MeshTopology.Lines);
        UploadLayers(fillLayers, MeshTopology.Triangles);
    }

    public void ApplyPreset()
    {
        if (preset == WaterfallPreset.WaterfallA)
        {
            ApplyWaterfallAPreset();
            return;
        }

        if (preset == WaterfallPreset.WaterfallB)
            ApplyWaterfallBPreset();
    }

    void ApplyWaterfallAPreset()
    {
        visualMode = WaterfallVisualMode.DataWaterfallVertical;
        worldWidth = 12.8f;
        worldHeight = 8f;
        baseAlpha = 0.58f;
        globalIntensity = 0.9f;
        densityMultiplier = 1f;
        speedMultiplier = 1f;
        accentProbability = 0.014f;
        glitchProbability = 0.045f;
        pulseProbability = 0.026f;
        verticalStreamCount = 240;
        verticalColumnCount = 144;
        segmentsPerStreamMin = 4;
        segmentsPerStreamMax = 14;
        segmentWidthMin = 0.01f;
        segmentWidthMax = 0.052f;
        segmentHeightMin = 0.026f;
        segmentHeightMax = 0.24f;
        segmentGapMin = 0.028f;
        segmentGapMax = 0.22f;
        fallSpeedMin = 0.9f;
        fallSpeedMax = 3.8f;
        jitterAmount = 0.022f;
        resetProbability = 0.0035f;
    }

    void ApplyWaterfallBPreset()
    {
        visualMode = WaterfallVisualMode.TestPatternHorizontal;
        worldWidth = 10.24f;
        worldHeight = 7.68f;
        baseAlpha = 0.9f;
        globalIntensity = 0.95f;
        densityMultiplier = 1f;
        speedMultiplier = 1f;
        accentProbability = 0.022f;
        glitchProbability = 0.035f;
        pulseProbability = 0.05f;
        horizontalUnitCount = 540;
        horizontalRowCount = 3;
        horizontalUnitsPerRow = 180;
        horizontalWidthRange = new Vector2(0.35f, 1.4f);
        horizontalShortWidthRange = new Vector2(0.08f, 0.32f);
        horizontalLongWidthRange = new Vector2(1.3f, 3.8f);
        horizontalHeightRange = new Vector2(0.055f, 0.22f);
        horizontalStripeWidthRange = new Vector2(0.01f, 0.065f);
        horizontalStripeHeightRange = new Vector2(0.56f, 0.92f);
        horizontalSpeedRange = new Vector2(1.8f, 5.2f);
        horizontalUseSteppedMotion = false;
        horizontalStepInterval = 0.025f;
        horizontalGridJitter = 0.002f;
        horizontalBarcodeAlignment = true;
        horizontalBarcodeGapRange = new Vector2(0.006f, 0.026f);
        horizontalOutlineProbability = 0.025f;
        horizontalStripeProbability = 0.93f;
        horizontalShortBarProbability = 0.035f;
        horizontalLongBarProbability = 0.028f;
        horizontalBlinkProbability = 0.028f;
        horizontalResetProbability = 0.01f;

        if (!preserveInspectorLanePaddingOnPreset)
            horizontalLanePadding = WaterfallBLanePaddingPreset;
    }

    void ResolvePresetFromOutputMode()
    {
        OutputModeManager manager = FindFirstObjectByType<OutputModeManager>();
        if (manager == null)
            return;

        if (manager.CurrentMode == OutputMode.WaterfallA)
            preset = WaterfallPreset.WaterfallA;

        if (manager.CurrentMode == OutputMode.WaterfallB)
            preset = WaterfallPreset.WaterfallB;
    }

    public void SetIntensity(float value)
    {
        globalIntensity = Mathf.Max(0f, value);
    }

    public void SetSpeedMultiplier(float value)
    {
        speedMultiplier = Mathf.Max(0f, value);
    }

    public void SetDensityMultiplier(float value)
    {
        densityMultiplier = Mathf.Max(0f, value);
    }

    public void SetLanePadding(float value)
    {
        horizontalLanePadding = Mathf.Clamp(value, 0f, 1.5f);
    }

    public void TriggerPulse(float amount)
    {
        pulse = Mathf.Clamp01(Mathf.Max(pulse, amount));
    }

    public void SetGlitchAmount(float value)
    {
        glitchProbability = Mathf.Clamp01(value);
    }

    public void TriggerAccent(float amount)
    {
        accentTrigger = Mathf.Clamp01(Mathf.Max(accentTrigger, amount));
    }

    void OnValidate()
    {
        ClampControlRanges();
    }

    void ClampControlRanges()
    {
        horizontalLanePadding = Mathf.Clamp(horizontalLanePadding, 0f, 1.5f);
    }

    void InitializeRuntime()
    {
        meshRoot = CreateRuntimeRoot(meshRootName);
        lineLayers = CreateLayerSet("Lines");
        fillLayers = CreateLayerSet("Fills");
        InitializeHorizontalUnits();
        InitializeVerticalStreams();
        CacheRuntimeConfig();
    }

    void RefreshRuntimeFromInspectorChanges()
    {
        if (!liveApplyInspectorChanges)
            return;

        bool horizontalNeedsRebuild =
            lastHorizontalUnitCount != horizontalUnitCount ||
            lastHorizontalRowCount != horizontalRowCount ||
            lastHorizontalUnitsPerRow != horizontalUnitsPerRow ||
            lastHorizontalBarcodeAlignment != horizontalBarcodeAlignment ||
            !Mathf.Approximately(lastHorizontalLanePadding, horizontalLanePadding) ||
            lastHorizontalStripeHeightRange != horizontalStripeHeightRange ||
            lastHorizontalSpeedRange != horizontalSpeedRange;

        bool verticalNeedsRebuild = lastVerticalStreamCount != verticalStreamCount;

        if (horizontalNeedsRebuild)
            InitializeHorizontalUnits();

        if (verticalNeedsRebuild)
            InitializeVerticalStreams();

        if (horizontalNeedsRebuild || verticalNeedsRebuild)
            CacheRuntimeConfig();
    }

    void CacheRuntimeConfig()
    {
        lastHorizontalUnitCount = horizontalUnitCount;
        lastHorizontalRowCount = horizontalRowCount;
        lastHorizontalUnitsPerRow = horizontalUnitsPerRow;
        lastHorizontalBarcodeAlignment = horizontalBarcodeAlignment;
        lastHorizontalLanePadding = horizontalLanePadding;
        lastHorizontalStripeHeightRange = horizontalStripeHeightRange;
        lastHorizontalSpeedRange = horizontalSpeedRange;
        lastVerticalStreamCount = verticalStreamCount;
    }

    Transform CreateRuntimeRoot(string rootName)
    {
        GameObject obj = new GameObject(rootName);
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        return obj.transform;
    }

    MeshLayer[] CreateLayerSet(string setName)
    {
        MeshLayer[] layers = new MeshLayer[GroupCount];
        layers[DimGroup] = CreateMeshLayer(setName + "_Dim", dimColor);
        layers[DefaultGroup] = CreateMeshLayer(setName + "_Default", defaultColor);
        layers[BrightGroup] = CreateMeshLayer(setName + "_Bright", brightColor);
        layers[AccentGroup] = CreateMeshLayer(setName + "_Accent", accentColor);
        layers[SecondaryAccentGroup] = CreateMeshLayer(setName + "_SecondaryAccent", secondaryAccentColor);
        return layers;
    }

    MeshLayer CreateMeshLayer(string layerName, Color color)
    {
        GameObject obj = new GameObject("Waterfall_" + layerName);
        obj.transform.SetParent(meshRoot, false);

        MeshFilter filter = obj.AddComponent<MeshFilter>();
        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "Waterfall_" + layerName + "_Mesh";
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;

        Material material = CreateRuntimeMaterial(color);
        renderer.sharedMaterial = material;

        return new MeshLayer
        {
            mesh = mesh,
            material = material
        };
    }

    Material CreateRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.name = "MAT_Runtime_Waterfall";
        ConfigureTransparentMaterial(material, color);
        return material;
    }

    void ConfigureTransparentMaterial(Material material, Color color)
    {
        if (material == null) return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);

        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
    }

    void InitializeHorizontalUnits()
    {
        int barcodeCount = Mathf.Max(1, horizontalRowCount) * Mathf.Max(1, horizontalUnitsPerRow);
        int count = Mathf.Max(1, horizontalBarcodeAlignment ? Mathf.Max(horizontalUnitCount, barcodeCount) : horizontalUnitCount);
        horizontalUnits = new HorizontalUnit[count];

        for (int i = 0; i < horizontalUnits.Length; i++)
        {
            horizontalUnits[i] = new HorizontalUnit();
            ResetHorizontalUnit(horizontalUnits[i], true, i);
        }
    }

    void InitializeVerticalStreams()
    {
        int count = Mathf.Max(1, verticalStreamCount);
        verticalStreams = new VerticalStream[count];

        for (int i = 0; i < verticalStreams.Length; i++)
        {
            verticalStreams[i] = new VerticalStream();
            ResetVerticalStream(verticalStreams[i], true);
        }
    }

    void UpdateExternalControlDecay()
    {
        pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * Mathf.Max(0.01f, pulseDecay));
        accentTrigger = Mathf.MoveTowards(accentTrigger, 0f, Time.deltaTime * Mathf.Max(0.01f, accentDecay));
    }

    void UpdateGlitchState()
    {
        if (isGlitching)
        {
            glitchTimer += Time.deltaTime;
            if (glitchTimer >= glitchDuration)
            {
                isGlitching = false;
                glitchTimer = 0f;
            }
            return;
        }

        float chance = Mathf.Clamp01(glitchProbability + pulse * 0.08f) * Time.deltaTime;
        if (Random.value < chance)
        {
            isGlitching = true;
            glitchDuration = Random.Range(0.04f, 0.16f);
            glitchTimer = 0f;
        }
    }

    void UpdateTestPatternHorizontal()
    {
        if (horizontalUnits == null)
            return;

        int activeCount = Mathf.Clamp(Mathf.RoundToInt(horizontalUnits.Length * Mathf.Max(0f, densityMultiplier)), 0, horizontalUnits.Length);

        for (int i = 0; i < horizontalUnits.Length; i++)
        {
            HorizontalUnit unit = horizontalUnits[i];
            unit.visible = i < activeCount;
            if (!unit.visible)
                continue;

            float delta = Time.deltaTime * Mathf.Max(0f, speedMultiplier) * (1f + pulse * 0.45f);
            if (horizontalUseSteppedMotion)
            {
                unit.stepTimer += delta;
                if (unit.stepTimer >= Mathf.Max(0.005f, horizontalStepInterval))
                {
                    unit.x += unit.speed * unit.stepTimer;
                    unit.stepTimer = 0f;
                }
            }
            else
            {
                unit.x += unit.speed * delta;
            }

            if (isGlitching && Random.value < glitchProbability)
                unit.x += Random.Range(-0.25f, 0.25f);

            if (Random.value < (horizontalResetProbability + pulse * pulseProbability) * Time.deltaTime)
                ResetHorizontalUnit(unit, true, i);

            if (unit.x > worldWidth * 0.5f + unit.width)
                ResetHorizontalUnit(unit, false, i);
        }
    }

    void RebuildTestPatternHorizontalMeshes()
    {
        if (horizontalUnits == null)
            return;

        AddTestPatternScaffold();

        foreach (HorizontalUnit unit in horizontalUnits)
        {
            if (!unit.visible)
                continue;

            if (Random.value < horizontalBlinkProbability * Time.deltaTime * (1f + pulse * 4f))
                continue;

            int group = unit.group;
            if (pulse > 0.01f && Random.value < pulse * 0.02f)
                group = PickSignalGroup(1f);

            if (unit.outline)
                AddRectOutline(lineLayers[group], unit.x, unit.y, unit.width, unit.height);
            else
                AddRectFill(fillLayers[group], unit.x, unit.y, unit.width, unit.height);
        }
    }

    void ResetHorizontalUnit(HorizontalUnit unit, bool randomizeX, int unitIndex)
    {
        int rows = Mathf.Max(1, horizontalRowCount);
        int row = horizontalBarcodeAlignment ? unitIndex % rows : Random.Range(0, rows);
        float usableHeight = Mathf.Max(0.01f, worldHeight - horizontalLanePadding * 2f);
        float rowStep = rows <= 1 ? 0f : usableHeight / (rows - 1);
        float topCenter = worldHeight * 0.5f - horizontalLanePadding;
        float laneHeight = rows <= 1 ? usableHeight : rowStep * 0.86f;
        int slot = rows <= 0 ? unitIndex : unitIndex / rows;
        int slotsPerRow = Mathf.Max(1, Mathf.CeilToInt(horizontalUnits.Length / (float)rows));
        float slotStep = worldWidth / slotsPerRow;

        bool stripe = Random.value < horizontalStripeProbability;
        if (stripe)
        {
            unit.width = RandomRange(horizontalStripeWidthRange, 0.002f);
            unit.height = horizontalBarcodeAlignment
                ? Mathf.Min(laneHeight, RandomRange(horizontalStripeHeightRange, 0.01f))
                : RandomRange(horizontalStripeHeightRange, 0.01f);
        }
        else
        {
            unit.width = PickHorizontalWidth();
            unit.height = RandomRange(horizontalHeightRange, 0.01f);

            if (unit.height > unit.width * 0.45f)
                unit.height = Mathf.Max(0.01f, unit.width * Random.Range(0.08f, 0.25f));
        }

        if (horizontalBarcodeAlignment)
        {
            float barcodeX = -worldWidth * 0.5f + slot * slotStep + RandomRange(horizontalBarcodeGapRange, 0f);
            unit.x = randomizeX
                ? barcodeX + Random.Range(-slotStep * 0.18f, slotStep * 0.18f)
                : -worldWidth * 0.5f - unit.width - Random.Range(0f, worldWidth * 0.12f);
        }
        else
        {
            unit.x = randomizeX
                ? Random.Range(-worldWidth * 0.5f, worldWidth * 0.5f)
                : -worldWidth * 0.5f - unit.width - Random.Range(0f, worldWidth * 0.2f);
        }

        unit.y = ResolveHorizontalUnitTopY(row, rows, unit.height, topCenter, rowStep);
        unit.speed = RandomRange(horizontalSpeedRange, 0f);
        unit.group = PickSignalGroup(stripe ? 0.52f : 0.78f);
        unit.outline = !stripe && Random.value < horizontalOutlineProbability;
        unit.visible = true;
        unit.stepTimer = Random.Range(0f, Mathf.Max(0.01f, horizontalStepInterval));
    }

    float ResolveHorizontalUnitTopY(int row, int rows, float unitHeight, float topCenter, float rowStep)
    {
        float jitter = Random.Range(-horizontalGridJitter, horizontalGridJitter);

        if (horizontalBarcodeAlignment && rows == 3)
        {
            float topEdge = horizontalLanePadding;
            float bottomEdge = -horizontalLanePadding;

            if (row == 0)
                return topEdge + jitter;

            if (row == 1)
                return unitHeight * 0.5f + jitter;

            return bottomEdge + unitHeight + jitter;
        }

        float rowCenterY = topCenter - row * rowStep + jitter;
        return rowCenterY + unitHeight * 0.5f;
    }

    float PickHorizontalWidth()
    {
        float roll = Random.value;

        if (roll < horizontalLongBarProbability)
            return RandomRange(horizontalLongWidthRange, 0.02f);

        if (roll < horizontalLongBarProbability + horizontalShortBarProbability)
            return RandomRange(horizontalShortWidthRange, 0.02f);

        return RandomRange(horizontalWidthRange, 0.02f);
    }

    void UpdateDataWaterfallVertical()
    {
        if (verticalStreams == null)
            return;

        int activeCount = Mathf.Clamp(Mathf.RoundToInt(verticalStreams.Length * Mathf.Max(0f, densityMultiplier)), 0, verticalStreams.Length);

        for (int i = 0; i < verticalStreams.Length; i++)
        {
            VerticalStream stream = verticalStreams[i];
            if (i >= activeCount)
                continue;

            stream.y -= stream.speed * Mathf.Max(0f, speedMultiplier) * (1f + pulse * 0.6f) * Time.deltaTime;

            if (isGlitching && Random.value < glitchProbability)
                stream.x += Random.Range(-jitterAmount, jitterAmount) * 8f;

            if (Random.value < (resetProbability + pulse * pulseProbability * 0.4f) * Time.deltaTime)
                ResetVerticalStream(stream, true);

            if (stream.y < -worldHeight * 0.5f - stream.height)
                ResetVerticalStream(stream, false);
        }
    }

    void RebuildDataWaterfallVerticalMeshes()
    {
        if (verticalStreams == null)
            return;

        AddDataWaterfallScaffold();

        int activeCount = Mathf.Clamp(Mathf.RoundToInt(verticalStreams.Length * Mathf.Max(0f, densityMultiplier)), 0, verticalStreams.Length);

        for (int i = 0; i < activeCount; i++)
        {
            VerticalStream stream = verticalStreams[i];
            if (stream.segments == null)
                continue;

            float jitter = (Mathf.PerlinNoise(stream.phase, Time.time * 3.5f) - 0.5f) * jitterAmount;

            foreach (VerticalSegment segment in stream.segments)
            {
                if (segment == null || !segment.visible)
                    continue;

                int group = segment.group;
                float x = stream.x + segment.offsetX + jitter;
                float y = stream.y + segment.offsetY;
                AddRectFill(fillLayers[group], x, y, segment.width, segment.height);
            }
        }
    }

    void ResetVerticalStream(VerticalStream stream, bool randomizeY)
    {
        int columns = Mathf.Max(1, verticalColumnCount);
        int column = Random.Range(0, columns);
        float step = worldWidth / columns;
        float left = -worldWidth * 0.5f;

        stream.x = left + step * (column + 0.5f) + Random.Range(-step * 0.22f, step * 0.22f);
        stream.y = randomizeY
            ? Random.Range(-worldHeight * 0.5f, worldHeight * 0.5f)
            : worldHeight * 0.5f + Random.Range(0f, worldHeight * 0.35f);
        stream.speed = Random.Range(Mathf.Max(0f, fallSpeedMin), Mathf.Max(fallSpeedMin, fallSpeedMax));
        stream.phase = Random.value * 100f;

        int count = Random.Range(Mathf.Max(1, segmentsPerStreamMin), Mathf.Max(segmentsPerStreamMin + 1, segmentsPerStreamMax + 1));
        stream.segments = new VerticalSegment[count];
        float cursor = 0f;

        for (int i = 0; i < stream.segments.Length; i++)
        {
            VerticalSegment segment = new VerticalSegment();
            segment.width = Random.Range(Mathf.Max(0.001f, segmentWidthMin), Mathf.Max(segmentWidthMin, segmentWidthMax));
            segment.height = Random.Range(Mathf.Max(0.001f, segmentHeightMin), Mathf.Max(segmentHeightMin, segmentHeightMax));
            segment.offsetX = Random.Range(-jitterAmount, jitterAmount);
            segment.offsetY = cursor;
            float brightnessBias = Mathf.InverseLerp(brightnessMin, brightnessMax, Random.Range(brightnessMin, brightnessMax));
            segment.group = PickSignalGroup(brightnessBias);
            segment.visible = true;
            cursor += segment.height + Random.Range(Mathf.Max(0.001f, segmentGapMin), Mathf.Max(segmentGapMin, segmentGapMax));
            stream.segments[i] = segment;
        }

        stream.height = cursor;
    }

    int PickSignalGroup(float brightnessBias)
    {
        float chance = Mathf.Clamp01(accentProbability + accentTrigger * 0.2f);
        if (Random.value <= chance)
            return Random.value < 0.65f ? AccentGroup : SecondaryAccentGroup;

        float bias = Mathf.Clamp01(brightnessBias);
        float dimChance = Mathf.Lerp(0.48f, 0.12f, bias);
        float brightChance = Mathf.Lerp(0.05f, 0.22f, bias) + pulse * 0.08f;
        float roll = Random.value;

        if (roll < dimChance)
            return DimGroup;

        if (roll > 1f - brightChance)
            return BrightGroup;

        return DefaultGroup;
    }

    void AddTestPatternScaffold()
    {
        if (!showCalibrationFrame && !horizontalShowLaneGuides)
            return;

        float inset = Mathf.Max(0f, horizontalFrameInset);
        float left = -worldWidth * 0.5f + inset;
        float right = worldWidth * 0.5f - inset;
        float top = worldHeight * 0.5f - inset;
        float bottom = -worldHeight * 0.5f + inset;

        if (showCalibrationFrame)
            AddRectOutline(lineLayers[DimGroup], left, top, Mathf.Max(0.01f, right - left), Mathf.Max(0.01f, top - bottom));

        if (!horizontalShowLaneGuides)
            return;

        int rows = Mathf.Max(1, horizontalRowCount);

        if (horizontalBarcodeAlignment && rows == 3)
        {
            AddLine(lineLayers[DimGroup], new Vector3(left, horizontalLanePadding, zOffset - 0.02f), new Vector3(right, horizontalLanePadding, zOffset - 0.02f));
            AddLine(lineLayers[DimGroup], new Vector3(left, 0f, zOffset - 0.02f), new Vector3(right, 0f, zOffset - 0.02f));
            AddLine(lineLayers[DimGroup], new Vector3(left, -horizontalLanePadding, zOffset - 0.02f), new Vector3(right, -horizontalLanePadding, zOffset - 0.02f));
            return;
        }

        float usableHeight = Mathf.Max(0.01f, worldHeight - horizontalLanePadding * 2f);
        float rowStep = rows <= 1 ? 0f : usableHeight / (rows - 1);
        float firstY = worldHeight * 0.5f - horizontalLanePadding;

        for (int row = 0; row < rows; row++)
        {
            float y = firstY - row * rowStep;
            AddLine(lineLayers[DimGroup], new Vector3(left, y, zOffset - 0.02f), new Vector3(right, y, zOffset - 0.02f));
        }
    }

    void AddDataWaterfallScaffold()
    {
        if (!showCalibrationFrame && !verticalShowColumnGuides)
            return;

        float inset = Mathf.Max(0f, verticalFrameInset);
        float left = -worldWidth * 0.5f + inset;
        float right = worldWidth * 0.5f - inset;
        float top = worldHeight * 0.5f - inset;
        float bottom = -worldHeight * 0.5f + inset;

        if (showCalibrationFrame)
            AddRectOutline(lineLayers[DimGroup], left, top, Mathf.Max(0.01f, right - left), Mathf.Max(0.01f, top - bottom));

        if (!verticalShowColumnGuides)
            return;

        int columns = Mathf.Max(1, verticalColumnCount);
        int every = Mathf.Max(1, verticalGuideEveryColumns);
        float step = worldWidth / columns;
        float z = zOffset - 0.02f;

        for (int column = every; column < columns; column += every)
        {
            float x = -worldWidth * 0.5f + step * column;
            AddLine(lineLayers[DimGroup], new Vector3(x, top, z), new Vector3(x, bottom, z));
        }
    }

    void AddRectOutline(MeshLayer layer, float x, float y, float width, float height)
    {
        float z = zOffset - 0.01f;
        Vector3 a = new Vector3(x, y, z);
        Vector3 b = new Vector3(x + width, y, z);
        Vector3 c = new Vector3(x + width, y - height, z);
        Vector3 d = new Vector3(x, y - height, z);

        AddLine(layer, a, b);
        AddLine(layer, b, c);
        AddLine(layer, c, d);
        AddLine(layer, d, a);
    }

    void AddLine(MeshLayer layer, Vector3 a, Vector3 b)
    {
        if (layer == null) return;

        int start = layer.vertices.Count;
        layer.vertices.Add(a);
        layer.vertices.Add(b);
        layer.indices.Add(start);
        layer.indices.Add(start + 1);
    }

    void AddRectFill(MeshLayer layer, float x, float y, float width, float height)
    {
        if (layer == null) return;

        int start = layer.vertices.Count;
        float z = zOffset;

        layer.vertices.Add(new Vector3(x, y, z));
        layer.vertices.Add(new Vector3(x + width, y, z));
        layer.vertices.Add(new Vector3(x + width, y - height, z));
        layer.vertices.Add(new Vector3(x, y - height, z));

        layer.indices.Add(start);
        layer.indices.Add(start + 1);
        layer.indices.Add(start + 2);
        layer.indices.Add(start);
        layer.indices.Add(start + 2);
        layer.indices.Add(start + 3);
    }

    void ClearLayers(MeshLayer[] layers)
    {
        if (layers == null) return;

        foreach (MeshLayer layer in layers)
        {
            if (layer == null) continue;
            layer.vertices.Clear();
            layer.indices.Clear();
        }
    }

    void UploadLayers(MeshLayer[] layers, MeshTopology topology)
    {
        if (layers == null) return;

        foreach (MeshLayer layer in layers)
        {
            if (layer == null || layer.mesh == null) continue;
            layer.mesh.Clear();

            if (layer.vertices.Count > 0)
            {
                layer.mesh.SetVertices(layer.vertices);
                layer.mesh.SetIndices(layer.indices, topology, 0);
            }

            layer.mesh.RecalculateBounds();
        }
    }

    void ApplyLayerColors()
    {
        SetLayerColor(lineLayers[DimGroup], dimColor, scaffoldAlphaMultiplier);
        SetLayerColor(lineLayers[DefaultGroup], defaultColor);
        SetLayerColor(lineLayers[BrightGroup], brightColor);
        SetLayerColor(lineLayers[AccentGroup], accentColor);
        SetLayerColor(lineLayers[SecondaryAccentGroup], secondaryAccentColor);
        SetLayerColor(fillLayers[DimGroup], dimColor);
        SetLayerColor(fillLayers[DefaultGroup], defaultColor);
        SetLayerColor(fillLayers[BrightGroup], brightColor);
        SetLayerColor(fillLayers[AccentGroup], accentColor);
        SetLayerColor(fillLayers[SecondaryAccentGroup], secondaryAccentColor);
    }

    void SetLayerColor(MeshLayer layer, Color color, float alphaMultiplier = 1f)
    {
        if (layer == null || layer.material == null) return;

        float alpha = Mathf.Clamp01(baseAlpha * Mathf.Max(0f, globalIntensity));
        alpha = Mathf.Clamp01(alpha + pulse * 0.22f + (isGlitching ? glitchProbability * 0.45f : 0f));
        color.a *= alpha * Mathf.Clamp01(alphaMultiplier);

        if (layer.material.HasProperty("_BaseColor"))
            layer.material.SetColor("_BaseColor", color);

        if (layer.material.HasProperty("_Color"))
            layer.material.SetColor("_Color", color);
    }

    float RandomRange(Vector2 range, float minValue)
    {
        float min = Mathf.Max(minValue, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return Random.Range(min, max);
    }

    void CleanupLayers(MeshLayer[] layers)
    {
        if (layers == null) return;

        foreach (MeshLayer layer in layers)
        {
            if (layer == null) continue;

            if (layer.material != null)
                Destroy(layer.material);

            if (layer.mesh != null)
                Destroy(layer.mesh);
        }
    }
}
