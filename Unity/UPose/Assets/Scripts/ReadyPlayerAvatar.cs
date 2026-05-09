using UnityEngine;
using GLTFast;
using System;
using UnityEngine.Rendering;

public class ReadyPlayerAvatar : MonoBehaviour
{
    public enum AvatarGlitchType
    {
        Video,
        Mesh
    }

    private enum MeshGlitchPhase
    {
        None,
        EnterFlicker,
        Hold,
        ExitFlicker
    }

    [Header("Motion Source")]
    [SerializeField] private MonoBehaviour serverComponent;
    private MotionTrackingPose server;

    public int Delay = 0;

    [Header("Lost Tracking Fallback")]
    public bool enableLostTrackingFallback = true;
    public bool fallbackToLastValidPose = true;
    public float lostPoseIdentityAngle = 2f;
    public float lostPoseBlendSpeed = 12f;
    public bool logLostTrackingFallback = false;

    [Header("Motion Smoothing")]
    public bool enableMotionSmoothing = true;
    public float motionSmoothingSpeed = 18f;

    private Transform Hips;
    private Transform Spine;
    private Transform LeftUpLeg;
    private Transform LeftLeg;
    private Transform LeftFoot;
    private Transform RightUpLeg;
    private Transform RightLeg;
    private Transform RightFoot;
    private Transform LeftShoulder;
    private Transform LeftArm;
    private Transform LeftForeArm;
    private Transform LeftHand;
    private Transform LeftPalm;
    private Transform RightShoulder;
    private Transform RightArm;
    private Transform RightForeArm;
    private Transform RightHand;
    private Transform RightPalm;

    private bool AVATAR_LOADED = false;
    private bool lostTrackingFallbackActive;
    private bool hasLastValidPose;
    private Quaternion restHipsRotation;
    private Quaternion restSpineRotation;
    private Quaternion restRightArmRotation;
    private Quaternion restLeftArmRotation;
    private Quaternion restLeftForeArmRotation;
    private Quaternion restRightForeArmRotation;
    private Quaternion restRightUpLegRotation;
    private Quaternion restLeftUpLegRotation;
    private Quaternion restLeftLegRotation;
    private Quaternion restRightLegRotation;
    private Quaternion lastValidHipsRotation;
    private Quaternion lastValidSpineRotation;
    private Quaternion lastValidRightArmRotation;
    private Quaternion lastValidLeftArmRotation;
    private Quaternion lastValidLeftForeArmRotation;
    private Quaternion lastValidRightForeArmRotation;
    private Quaternion lastValidRightUpLegRotation;
    private Quaternion lastValidLeftUpLegRotation;
    private Quaternion lastValidLeftLegRotation;
    private Quaternion lastValidRightLegRotation;

    public enum AvatarChoice
    {
        UseLocalFile,
        FemaleGymClothing,
        FemaleDress,
        FemaleCasual,
        MaleCasual,
        MaleTshirt,
        MaleArmored,
        FemaleYogaOutfit,
        TestAvatar
    }

    public AvatarChoice onlineAvatar;
    public string localFilename = "67e21d1a79ac9bcf81a46385.glb";

    public bool moveToFloor = false;
    public float floorLevel = -1f;

    [Header("Root Placement")]
    public bool lockAvatarRootToOrigin = true;
    public Vector3 avatarRootOffset = Vector3.zero;
    public bool orientInitialRestPoseToViewer = true;
    public Vector3 initialViewerFacingRootEuler = new Vector3(0f, 180f, 0f);
    public bool keepRootAtOrigin = false;
    public bool debugRootPlacement = false;

    [Header("Material Override")]
    public bool overrideAvatarMaterials = true;
    public Material avatarMaterial;
    public Color fallbackAvatarColor = Color.white;

    private Material runtimeAvatarMaterial;
    private Renderer[] avatarRenderers;
    private MaterialPropertyBlock avatarMaterialBlock;
    private Color avatarBaseColor = Color.white;
    private Texture avatarBaseTexture;

    [Header("Glitch Visual")]
    public bool enableAvatarGlitch = true;
    public Vector2 glitchIntervalRange = new Vector2(4.0f, 9.0f);
    public Vector2 videoGlitchDurationRange = new Vector2(0.28f, 0.75f);
    public float glitchFlickerRate = 30f;
    [Range(0f, 1f)] public float glitchFrameDropChance = 0.28f;

    [Header("Video Glitch")]
    public bool useGlitchTexture = false;
    public Texture glitchTexture;
    public bool invertGlitchTexture = true;
    [Range(0f, 1f)] public float videoGlitchChance = 0.75f;
    public bool tintGlitchTextureWithColor = false;
    public Color glitchTextureTint = Color.white;

    [Header("Mesh Glitch")]
    public bool useMeshGlitch = true;
    [Range(0f, 1f)] public float meshGlitchChance = 0.25f;
    public bool suppressMeshGlitchInCollectiveSlotCameras = true;
    public string collectiveSlotCameraRootPrefix = "FragmentSlot_C";
    public Vector2 meshGlitchEnterFlickerDurationRange = new Vector2(0.08f, 0.16f);
    public Vector2 meshGlitchHoldDurationRange = new Vector2(0.8f, 1.6f);
    public Vector2 meshGlitchExitFlickerDurationRange = new Vector2(0.08f, 0.16f);
    public Color meshGlitchLineColor = Color.white;
    [Range(0f, 1f)] public float meshGlitchSolidAlpha = 0.18f;
    public float meshGlitchScaleBoost = 1.0f;
    public float meshGlitchNormalOffset = 0.01f;
    public float meshGlitchBottomTrim = 0.025f;
    public int maxMeshGlitchEdgesPerRenderer = 900;
    public bool randomizeMeshGlitchEdges = true;
    public int meshGlitchFullEdgeCount = 12000;
    [Range(0f, 1f)] public float meshGlitchReducedChance = 0.25f;
    public int[] meshGlitchReducedEdgeCountOptions = new int[] { 900, 1800, 3600 };
    public float meshGlitchRefreshRate = 18f;

    private float glitchTimer;
    private float nextGlitchTime;
    private float currentGlitchDuration;
    private float meshGlitchEnterDuration;
    private float meshGlitchHoldDuration;
    private float meshGlitchExitDuration;
    private bool isGlitching;
    private AvatarGlitchType activeGlitchType = AvatarGlitchType.Video;
    private string visualState = "DEFAULT";
    private MeshGlitchOverlay[] meshGlitchOverlays;
    private Material runtimeMeshGlitchMaterial;
    private float meshGlitchRefreshTimer;
    private int currentMeshGlitchEdgesPerRenderer;
    private bool meshGlitchVisibleThisFrame;
    private Quaternion initialRootRotation;
    private bool useInitialViewerFacingRoot;

    private class MeshGlitchOverlay
    {
        public SkinnedMeshRenderer source;
        public Mesh bakedMesh;
        public Mesh lineMesh;
        public MeshFilter filter;
        public MeshRenderer renderer;
    }

    private void Start()
    {
        initialRootRotation = transform.rotation;

        if (serverComponent != null)
        {
            server = serverComponent as MotionTrackingPose;
            if (server == null)
            {
                Debug.LogError($"[{name}] Assigned serverComponent does not implement MotionTrackingPose.");
                return;
            }
        }
        else
        {
            server = GetComponentInParent<PoseMemory>();
            if (server == null)
                server = GetComponentInParent<UPose>();

            if (server == null)
            {
                server = FindFirstObjectByType<PoseMemory>();
                if (server == null)
                    server = FindFirstObjectByType<UPose>();
            }

            if (server == null)
            {
                Debug.LogError($"[{name}] No MotionTrackingPose source found.");
                return;
            }
        }

        InitializeAvatar();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private Transform FindBone(string boneName)
    {
        var all = GetComponentsInChildren<Transform>(true);

        foreach (var t in all)
        {
            if (t.name == boneName)
                return t;

            if (t.name == "mixamorig:" + boneName)
                return t;

            if (t.name.StartsWith(boneName) || t.name.StartsWith("mixamorig:" + boneName))
                return t;
        }

        Debug.LogWarning($"[{name}] Bone not found: {boneName}");
        return null;
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;

        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private async void InitializeAvatar()
    {
        var gltfImport = new GltfImport();
        string avatarUrl = "";

        switch (onlineAvatar)
        {
            case AvatarChoice.UseLocalFile:
                avatarUrl = "";
                break;
            case AvatarChoice.FemaleGymClothing:
                avatarUrl = "avatar.glb";
                break;
            case AvatarChoice.FemaleDress:
                avatarUrl = "avatar1.glb";
                break;
            case AvatarChoice.FemaleCasual:
                avatarUrl = "67e20a7fc5f8c4a77988b853.glb";
                break;
            case AvatarChoice.MaleCasual:
                avatarUrl = "67d411b30787acbf58ce58ac.glb";
                break;
            case AvatarChoice.MaleTshirt:
                avatarUrl = "67e21d1a79ac9bcf81a46385.glb";
                break;
            case AvatarChoice.MaleArmored:
                avatarUrl = "67e21f3db6349f1f57421ba0.glb";
                break;
            case AvatarChoice.FemaleYogaOutfit:
                avatarUrl = "67f433b69dc08cf26d2cf585.glb";
                break;
            case AvatarChoice.TestAvatar:
                avatarUrl = "male_human_low-poly_base.glb";
                break;
            default:
                avatarUrl = "avatar.glb";
                break;
        }

        bool loaded;
        if (avatarUrl.Length == 0)
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, localFilename);
            loaded = await gltfImport.Load(path);
        }
        else
        {
            loaded = await gltfImport.Load("https://digitalworlds.github.io/UPose/UPose/Assets/StreamingAssets/" + avatarUrl);
        }

        if (!loaded)
        {
            Debug.LogError($"[{name}] ERROR: GLTF file failed to load!");
            return;
        }

        var instantiator = new GameObjectInstantiator(gltfImport, transform);
        var success = await gltfImport.InstantiateMainSceneAsync(instantiator);

        if (!success)
        {
            Debug.LogError($"[{name}] ERROR: GLTF file is NOT instantiated!");
            return;
        }

        Debug.Log($"[{name}] GLTF file is loaded.");

        useInitialViewerFacingRoot = true;
        ApplyAvatarRootPlacement();
        ApplyAvatarMaterialOverride();
        InitializeMeshGlitchOverlays();
        ScheduleNextGlitch();

        SetLayerRecursively(gameObject, gameObject.layer);

        Hips = FindBone("Hips");
        Spine = FindBone("Spine");

        Transform Spine1 = FindBone("Spine1");
        if (Spine1 != null)
        {
            Spine1.localRotation = Quaternion.identity;
            Spine1.localRotation = Quaternion.Euler(0, 0, 0);
        }

        Transform Spine2 = FindBone("Spine2");
        if (Spine2 != null)
        {
            Spine2.localRotation = Quaternion.identity;
            Spine2.localRotation = Quaternion.Euler(0, 0, 0);
        }

        LeftUpLeg = FindBone("LeftUpLeg");
        LeftLeg = FindBone("LeftLeg");

        RightUpLeg = FindBone("RightUpLeg");
        RightLeg = FindBone("RightLeg");

        LeftFoot = FindBone("LeftFoot");
        if (LeftFoot != null)
        {
            GameObject colliderHolder = new GameObject("LeftFootCollider");
            colliderHolder.transform.SetParent(LeftFoot);
            colliderHolder.transform.localPosition = new Vector3(0, 0.125f, 0);
            colliderHolder.transform.localRotation = Quaternion.Euler(-55, 0, 0);

            Rigidbody rb = colliderHolder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            BoxCollider footCollider = colliderHolder.AddComponent<BoxCollider>();
            footCollider.size = new Vector3(0.15f, 0.1f, 0.3f);

            colliderHolder.AddComponent<KickForce>();
        }

        RightFoot = FindBone("RightFoot");
        if (RightFoot != null)
        {
            GameObject colliderHolder = new GameObject("RightFootCollider");
            colliderHolder.transform.SetParent(RightFoot);
            colliderHolder.transform.localPosition = new Vector3(0, 0.125f, 0);
            colliderHolder.transform.localRotation = Quaternion.Euler(-55, 0, 0);

            Rigidbody rb = colliderHolder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            BoxCollider footCollider = colliderHolder.AddComponent<BoxCollider>();
            footCollider.size = new Vector3(0.15f, 0.1f, 0.3f);

            colliderHolder.AddComponent<KickForce>();
        }

        LeftShoulder = FindBone("LeftShoulder");
        if (LeftShoulder != null)
            LeftShoulder.localRotation = Quaternion.Euler(0, 0, 90);

        LeftArm = FindBone("LeftArm");
        LeftForeArm = FindBone("LeftForeArm");
        LeftHand = FindBone("LeftHand");

        if (LeftHand != null)
        {
            GameObject leftPalm = new GameObject("LeftPalm");
            leftPalm.transform.SetParent(LeftHand);
            leftPalm.transform.localPosition = new Vector3(0, 0.07f, 0.04f);
            leftPalm.transform.localRotation = Quaternion.identity;
            LeftPalm = leftPalm.transform;

            GameObject colliderHolder = new GameObject("LeftHandCollider");
            colliderHolder.transform.SetParent(LeftHand);
            colliderHolder.transform.localPosition = new Vector3(0, 0.1f, 0);
            colliderHolder.transform.localRotation = Quaternion.Euler(-90, 0, 0);

            Rigidbody rb = colliderHolder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            BoxCollider handCollider = colliderHolder.AddComponent<BoxCollider>();
            handCollider.size = new Vector3(0.15f, 0.1f, 0.2f);

            colliderHolder.AddComponent<KickForce>();
        }

        RightShoulder = FindBone("RightShoulder");
        if (RightShoulder != null)
            RightShoulder.localRotation = Quaternion.Euler(0, 0, -90);

        RightArm = FindBone("RightArm");
        RightForeArm = FindBone("RightForeArm");
        RightHand = FindBone("RightHand");

        if (RightHand != null)
        {
            GameObject rightPalm = new GameObject("RightPalm");
            rightPalm.transform.SetParent(RightHand);
            rightPalm.transform.localPosition = new Vector3(0, 0.07f, 0.04f);
            rightPalm.transform.localRotation = Quaternion.identity;
            RightPalm = rightPalm.transform;

            GameObject colliderHolder = new GameObject("RightHandCollider");
            colliderHolder.transform.SetParent(RightHand);
            colliderHolder.transform.localPosition = new Vector3(0, 0.1f, 0);
            colliderHolder.transform.localRotation = Quaternion.Euler(-90, 0, 0);

            Rigidbody rb = colliderHolder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            BoxCollider handCollider = colliderHolder.AddComponent<BoxCollider>();
            handCollider.size = new Vector3(0.15f, 0.1f, 0.2f);

            colliderHolder.AddComponent<KickForce>();
        }

        SetLayerRecursively(gameObject, gameObject.layer);
        CaptureRestPose();

        AVATAR_LOADED = true;
    }

    private void CaptureRestPose()
    {
        restHipsRotation = Hips != null ? Hips.localRotation : Quaternion.identity;
        restSpineRotation = Spine != null ? Spine.localRotation : Quaternion.identity;
        restRightArmRotation = RightArm != null ? RightArm.localRotation : Quaternion.identity;
        restLeftArmRotation = LeftArm != null ? LeftArm.localRotation : Quaternion.identity;
        restLeftForeArmRotation = LeftForeArm != null ? LeftForeArm.localRotation : Quaternion.identity;
        restRightForeArmRotation = RightForeArm != null ? RightForeArm.localRotation : Quaternion.identity;
        restRightUpLegRotation = RightUpLeg != null ? RightUpLeg.localRotation : Quaternion.identity;
        restLeftUpLegRotation = LeftUpLeg != null ? LeftUpLeg.localRotation : Quaternion.identity;
        restLeftLegRotation = LeftLeg != null ? LeftLeg.localRotation : Quaternion.identity;
        restRightLegRotation = RightLeg != null ? RightLeg.localRotation : Quaternion.identity;
    }

    private void ApplyAvatarMaterialOverride()
    {
        if (!overrideAvatarMaterials) return;

        Material material = GetOrCreateRuntimeAvatarMaterial();
        if (material == null) return;
        ConfigureMaterialForAlpha(material);

        avatarRenderers = GetComponentsInChildren<Renderer>(true);
        avatarMaterialBlock = new MaterialPropertyBlock();
        avatarBaseColor = GetMaterialColor(material, fallbackAvatarColor);
        avatarBaseTexture = GetMaterialTexture(material);

        foreach (Renderer renderer in avatarRenderers)
        {
            if (renderer == null) continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = material;
                continue;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }

        ApplyAvatarVisual(avatarBaseColor, avatarBaseTexture, true);
    }

    private Material GetOrCreateRuntimeAvatarMaterial()
    {
        if (runtimeAvatarMaterial != null)
            return runtimeAvatarMaterial;

        if (avatarMaterial != null)
        {
            runtimeAvatarMaterial = new Material(avatarMaterial);
            runtimeAvatarMaterial.name = avatarMaterial.name + "_Runtime";
            return runtimeAvatarMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogWarning($"[{name}] Avatar material override skipped: no Unlit shader found.");
            return null;
        }

        runtimeAvatarMaterial = new Material(shader);
        runtimeAvatarMaterial.name = "MAT_Runtime_Avatar_Unlit";
        SetMaterialColor(runtimeAvatarMaterial, fallbackAvatarColor);
        return runtimeAvatarMaterial;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void ConfigureMaterialForAlpha(Material material)
    {
        if (material == null) return;

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

        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
    }

    private Color GetMaterialColor(Material material, Color fallback)
    {
        if (material == null) return fallback;

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");

        return fallback;
    }

    private Texture GetMaterialTexture(Material material)
    {
        if (material == null) return null;

        if (material.HasProperty("_BaseMap"))
            return material.GetTexture("_BaseMap");

        if (material.HasProperty("_MainTex"))
            return material.GetTexture("_MainTex");

        return null;
    }

    private void ScheduleNextGlitch()
    {
        glitchTimer = 0f;
        nextGlitchTime = RandomFromRange(glitchIntervalRange, 0.1f);
    }

    private void UpdateAvatarGlitch()
    {
        if (!enableAvatarGlitch || avatarRenderers == null || avatarRenderers.Length == 0)
        {
            ApplyAvatarVisual(avatarBaseColor, avatarBaseTexture, true);
            meshGlitchVisibleThisFrame = false;
            SetMeshGlitchVisible(false);
            visualState = "DEFAULT";
            return;
        }

        glitchTimer += Time.deltaTime;

        if (!isGlitching && glitchTimer >= nextGlitchTime)
        {
            if (!TryStartNextGlitch())
            {
                ScheduleNextGlitch();
            }
        }

        if (!isGlitching)
        {
            ApplyAvatarVisual(avatarBaseColor, avatarBaseTexture, true);
            meshGlitchVisibleThisFrame = false;
            SetMeshGlitchVisible(false);
            visualState = "DEFAULT";
            return;
        }

        if (activeGlitchType == AvatarGlitchType.Mesh)
            UpdateMeshGlitchVisual();
        else
            UpdateVideoGlitchVisual();

        visualState = GetActiveGlitchStateName();

        if (glitchTimer >= currentGlitchDuration)
        {
            isGlitching = false;
            ApplyAvatarVisual(avatarBaseColor, avatarBaseTexture, true);
            meshGlitchVisibleThisFrame = false;
            SetMeshGlitchVisible(false);
            ScheduleNextGlitch();
            visualState = "DEFAULT";
        }
    }

    private bool TryStartNextGlitch()
    {
        if (!TryPickNextGlitchType(out AvatarGlitchType glitchType))
            return false;

        isGlitching = true;
        glitchTimer = 0f;
        activeGlitchType = glitchType;
        ConfigureActiveGlitch(activeGlitchType);
        currentGlitchDuration = GetDurationForGlitchType(activeGlitchType);
        visualState = GetActiveGlitchStateName();

        return true;
    }

    private void UpdateVideoGlitchVisual()
    {
        meshGlitchVisibleThisFrame = false;
        SetMeshGlitchVisible(false);

        Color color = tintGlitchTextureWithColor ? glitchTextureTint : Color.white;
        color.a = 1f;
        ApplyAvatarVisual(color, glitchTexture, true, invertGlitchTexture);
    }

    private void UpdateMeshGlitchVisual()
    {
        MeshGlitchPhase phase = GetMeshGlitchPhase();
        bool flickerPhase = phase == MeshGlitchPhase.EnterFlicker || phase == MeshGlitchPhase.ExitFlicker;
        bool visible = true;

        if (flickerPhase)
        {
            float flickerFrame = Mathf.Floor(glitchTimer * Mathf.Max(1f, glitchFlickerRate));
            visible = ((int)flickerFrame % 2 == 0) && UnityEngine.Random.value > glitchFrameDropChance;
        }

        meshGlitchVisibleThisFrame = visible;

        if (visible)
            UpdateMeshGlitch();
        else
            SetMeshGlitchVisible(false);

        Color color = avatarBaseColor;
        color.a = visible ? meshGlitchSolidAlpha : 0f;
        ApplyAvatarVisual(color, avatarBaseTexture, true);
    }

    private bool TryPickNextGlitchType(out AvatarGlitchType glitchType)
    {
        bool canUseMesh = useMeshGlitch && HasMeshGlitchOverlays();
        bool canUseVideo = useGlitchTexture && glitchTexture != null;

        if (canUseMesh && canUseVideo)
        {
            float meshWeight = Mathf.Clamp01(meshGlitchChance);
            float videoWeight = Mathf.Clamp01(videoGlitchChance);
            float totalWeight = meshWeight + videoWeight;

            if (totalWeight <= 0f)
            {
                glitchType = AvatarGlitchType.Video;
                return false;
            }

            glitchType = UnityEngine.Random.value < meshWeight / totalWeight
                ? AvatarGlitchType.Mesh
                : AvatarGlitchType.Video;
            return true;
        }

        if (canUseMesh && UnityEngine.Random.value < meshGlitchChance)
        {
            glitchType = AvatarGlitchType.Mesh;
            return true;
        }

        if (canUseVideo && UnityEngine.Random.value < videoGlitchChance)
        {
            glitchType = AvatarGlitchType.Video;
            return true;
        }

        glitchType = AvatarGlitchType.Video;
        return false;
    }

    private float GetDurationForGlitchType(AvatarGlitchType glitchType)
    {
        if (glitchType == AvatarGlitchType.Mesh)
            return meshGlitchEnterDuration + meshGlitchHoldDuration + meshGlitchExitDuration;

        return RandomFromRange(videoGlitchDurationRange, 0.01f);
    }

    private string GetActiveGlitchStateName()
    {
        if (activeGlitchType == AvatarGlitchType.Mesh)
        {
            MeshGlitchPhase phase = GetMeshGlitchPhase();

            if (phase == MeshGlitchPhase.EnterFlicker)
                return "MESH_GLITCH_ENTER";

            if (phase == MeshGlitchPhase.ExitFlicker)
                return "MESH_GLITCH_EXIT";

            return "MESH_GLITCH";
        }

        return "VIDEO_GLITCH";
    }

    private void ConfigureActiveGlitch(AvatarGlitchType glitchType)
    {
        if (glitchType == AvatarGlitchType.Mesh)
        {
            PickMeshGlitchEdgeCount();
            meshGlitchEnterDuration = RandomFromRange(meshGlitchEnterFlickerDurationRange, 0.01f);
            meshGlitchHoldDuration = RandomFromRange(meshGlitchHoldDurationRange, 0.01f);
            meshGlitchExitDuration = RandomFromRange(meshGlitchExitFlickerDurationRange, 0.01f);
        }
    }

    private MeshGlitchPhase GetMeshGlitchPhase()
    {
        if (activeGlitchType != AvatarGlitchType.Mesh)
            return MeshGlitchPhase.None;

        if (glitchTimer < meshGlitchEnterDuration)
            return MeshGlitchPhase.EnterFlicker;

        if (glitchTimer < meshGlitchEnterDuration + meshGlitchHoldDuration)
            return MeshGlitchPhase.Hold;

        return MeshGlitchPhase.ExitFlicker;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!suppressMeshGlitchInCollectiveSlotCameras) return;
        if (!AVATAR_LOADED || avatarRenderers == null || avatarRenderers.Length == 0) return;
        if (!isGlitching || activeGlitchType != AvatarGlitchType.Mesh) return;
        if (camera == null) return;

        if (IsCollectiveSlotCamera(camera))
        {
            ApplyAvatarVisual(avatarBaseColor, avatarBaseTexture, true);
            SetMeshGlitchVisible(false);
            return;
        }

        Color color = avatarBaseColor;
        color.a = meshGlitchVisibleThisFrame ? meshGlitchSolidAlpha : 0f;
        ApplyAvatarVisual(color, avatarBaseTexture, true);
        SetMeshGlitchVisible(meshGlitchVisibleThisFrame);
    }

    private bool IsCollectiveSlotCamera(Camera camera)
    {
        if (camera == null || string.IsNullOrEmpty(collectiveSlotCameraRootPrefix))
            return false;

        Transform current = camera.transform;

        while (current != null)
        {
            if (current.name.StartsWith(collectiveSlotCameraRootPrefix, StringComparison.Ordinal))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void PickMeshGlitchEdgeCount()
    {
        currentMeshGlitchEdgesPerRenderer = Mathf.Max(1, maxMeshGlitchEdgesPerRenderer);

        if (!randomizeMeshGlitchEdges)
            return;

        bool useReduced = UnityEngine.Random.value < meshGlitchReducedChance;

        if (!useReduced)
        {
            currentMeshGlitchEdgesPerRenderer = Mathf.Max(1, meshGlitchFullEdgeCount);
            return;
        }

        if (meshGlitchReducedEdgeCountOptions == null || meshGlitchReducedEdgeCountOptions.Length == 0)
            return;

        int selected = meshGlitchReducedEdgeCountOptions[
            UnityEngine.Random.Range(0, meshGlitchReducedEdgeCountOptions.Length)
        ];

        currentMeshGlitchEdgesPerRenderer = Mathf.Max(1, selected);
    }

    private float RandomFromRange(Vector2 range, float minValue)
    {
        float min = Mathf.Max(minValue, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return UnityEngine.Random.Range(min, max);
    }

    private void ApplyAvatarVisual(Color color, Texture texture, bool visible, bool invertTexture = false)
    {
        if (avatarRenderers == null) return;

        foreach (Renderer renderer in avatarRenderers)
        {
            if (renderer == null) continue;

            renderer.enabled = visible;
            avatarMaterialBlock.Clear();

            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor"))
                avatarMaterialBlock.SetColor("_BaseColor", color);

            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
                avatarMaterialBlock.SetColor("_Color", color);

            if (texture != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseMap"))
                avatarMaterialBlock.SetTexture("_BaseMap", texture);

            if (texture != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_MainTex"))
                avatarMaterialBlock.SetTexture("_MainTex", texture);

            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_InvertTexture"))
                avatarMaterialBlock.SetFloat("_InvertTexture", invertTexture ? 1f : 0f);

            renderer.SetPropertyBlock(avatarMaterialBlock);
        }
    }

    private void InitializeMeshGlitchOverlays()
    {
        if (!useMeshGlitch) return;

        SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (skinnedRenderers == null || skinnedRenderers.Length == 0) return;

        runtimeMeshGlitchMaterial = CreateMeshGlitchMaterial();
        if (runtimeMeshGlitchMaterial == null) return;

        meshGlitchOverlays = new MeshGlitchOverlay[skinnedRenderers.Length];

        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer source = skinnedRenderers[i];
            if (source == null) continue;

            GameObject overlayObject = new GameObject("MeshGlitchOverlay");
            overlayObject.transform.SetParent(transform, false);
            overlayObject.transform.localPosition = Vector3.zero;
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = Vector3.one;

            MeshFilter filter = overlayObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = overlayObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = runtimeMeshGlitchMaterial;
            renderer.enabled = false;

            Mesh lineMesh = new Mesh();
            lineMesh.name = $"{source.name}_MeshGlitchLines";
            lineMesh.MarkDynamic();
            filter.sharedMesh = lineMesh;

            MeshGlitchOverlay overlay = new MeshGlitchOverlay
            {
                source = source,
                bakedMesh = new Mesh(),
                lineMesh = lineMesh,
                filter = filter,
                renderer = renderer
            };

            overlay.bakedMesh.name = $"{source.name}_MeshGlitchBake";
            overlay.bakedMesh.MarkDynamic();
            meshGlitchOverlays[i] = overlay;
        }
    }

    private Material CreateMeshGlitchMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.name = "MAT_Runtime_Mesh_Glitch";
        SetMaterialColor(material, meshGlitchLineColor);
        ConfigureMaterialForAlpha(material);
        return material;
    }

    private bool HasMeshGlitchOverlays()
    {
        if (meshGlitchOverlays == null) return false;

        foreach (MeshGlitchOverlay overlay in meshGlitchOverlays)
        {
            if (overlay != null && overlay.source != null && overlay.renderer != null)
                return true;
        }

        return false;
    }

    private void UpdateMeshGlitch()
    {
        meshGlitchRefreshTimer += Time.deltaTime;
        float refreshInterval = 1f / Mathf.Max(1f, meshGlitchRefreshRate);

        if (meshGlitchRefreshTimer < refreshInterval)
        {
            SetMeshGlitchVisible(true);
            return;
        }

        meshGlitchRefreshTimer = 0f;
        RebuildMeshGlitchLines();
        SetMeshGlitchVisible(true);
    }

    private void RebuildMeshGlitchLines()
    {
        if (meshGlitchOverlays == null) return;

        foreach (MeshGlitchOverlay overlay in meshGlitchOverlays)
        {
            if (overlay == null || overlay.source == null || overlay.lineMesh == null || overlay.bakedMesh == null)
                continue;

            SyncMeshGlitchOverlayTransform(overlay);
            overlay.source.BakeMesh(overlay.bakedMesh);
            Vector3[] vertices = overlay.bakedMesh.vertices;
            int[] triangles = overlay.bakedMesh.triangles;

            if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length < 3)
                continue;

            ConvertMeshGlitchVerticesToOverlayLocal(overlay, vertices);
            AlignMeshGlitchVerticesToSourceBounds(overlay, vertices);
            Bounds alignedBounds = CalculateLocalBounds(vertices);
            ExpandMeshGlitchVertices(vertices, null, alignedBounds.center);
            TrimMeshGlitchBottom(overlay, vertices);

            int triangleCount = triangles.Length / 3;
            int edgeLimit = Mathf.Max(1, currentMeshGlitchEdgesPerRenderer);
            int maxTriangles = Mathf.Max(1, edgeLimit / 3);
            int step = Mathf.Max(1, triangleCount / maxTriangles);
            int selectedTriangleCount = Mathf.CeilToInt((float)triangleCount / step);
            int[] lineIndices = new int[selectedTriangleCount * 6];

            int idx = 0;

            for (int tri = 0; tri < triangleCount; tri += step)
            {
                int baseIndex = tri * 3;
                if (baseIndex + 2 >= triangles.Length || idx + 5 >= lineIndices.Length)
                    break;

                int a = triangles[baseIndex];
                int b = triangles[baseIndex + 1];
                int c = triangles[baseIndex + 2];

                lineIndices[idx++] = a;
                lineIndices[idx++] = b;
                lineIndices[idx++] = b;
                lineIndices[idx++] = c;
                lineIndices[idx++] = c;
                lineIndices[idx++] = a;
            }

            overlay.lineMesh.Clear();
            overlay.lineMesh.indexFormat = vertices.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            overlay.lineMesh.vertices = vertices;

            if (idx < lineIndices.Length)
            {
                int[] trimmed = new int[idx];
                Array.Copy(lineIndices, trimmed, idx);
                lineIndices = trimmed;
            }

            overlay.lineMesh.SetIndices(lineIndices, MeshTopology.Lines, 0);
            overlay.lineMesh.RecalculateBounds();
        }
    }

    private void ExpandMeshGlitchVertices(Vector3[] vertices, Vector3[] normals, Vector3 center)
    {
        if (vertices == null) return;

        float scale = Mathf.Max(0.001f, meshGlitchScaleBoost);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 expanded = center + (vertices[i] - center) * scale;

            if (normals != null && i < normals.Length)
                expanded += normals[i] * meshGlitchNormalOffset;

            vertices[i] = expanded;
        }
    }

    private void TrimMeshGlitchBottom(MeshGlitchOverlay overlay, Vector3[] vertices)
    {
        if (vertices == null || vertices.Length == 0) return;
        if (overlay == null || overlay.source == null || overlay.filter == null) return;
        if (meshGlitchBottomTrim <= 0f) return;

        Transform overlayTransform = overlay.filter.transform;
        float bottomWorldY = overlay.source.bounds.min.y + meshGlitchBottomTrim;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = overlayTransform.TransformPoint(vertices[i]);

            if (world.y < bottomWorldY)
            {
                world.y = bottomWorldY;
                vertices[i] = overlayTransform.InverseTransformPoint(world);
            }
        }
    }

    private void SetMeshGlitchVisible(bool visible)
    {
        if (meshGlitchOverlays == null) return;

        foreach (MeshGlitchOverlay overlay in meshGlitchOverlays)
        {
            if (overlay != null && overlay.renderer != null)
                overlay.renderer.enabled = visible;
        }
    }

    private void SyncMeshGlitchOverlayTransform(MeshGlitchOverlay overlay)
    {
        if (overlay == null || overlay.source == null || overlay.filter == null) return;

        Transform overlayTransform = overlay.filter.transform;

        if (overlayTransform.parent != transform)
            overlayTransform.SetParent(transform, false);

        overlayTransform.localPosition = Vector3.zero;
        overlayTransform.localRotation = Quaternion.identity;
        overlayTransform.localScale = Vector3.one;
    }

    private void ConvertMeshGlitchVerticesToOverlayLocal(MeshGlitchOverlay overlay, Vector3[] vertices)
    {
        if (overlay == null || overlay.source == null || overlay.filter == null || vertices == null) return;

        Transform sourceTransform = overlay.source.transform;
        Transform overlayTransform = overlay.filter.transform;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = sourceTransform.TransformPoint(vertices[i]);
            vertices[i] = overlayTransform.InverseTransformPoint(world);
        }
    }

    private void AlignMeshGlitchVerticesToSourceBounds(MeshGlitchOverlay overlay, Vector3[] vertices)
    {
        if (overlay == null || overlay.source == null || overlay.filter == null || vertices == null || vertices.Length == 0)
            return;

        Bounds currentBounds = CalculateLocalBounds(vertices);
        Bounds targetBounds = GetSourceBoundsInOverlayLocal(overlay);

        Vector3 currentSize = currentBounds.size;
        Vector3 targetSize = targetBounds.size;

        Vector3 scale = new Vector3(
            SafeBoundsScale(targetSize.x, currentSize.x),
            SafeBoundsScale(targetSize.y, currentSize.y),
            SafeBoundsScale(targetSize.z, currentSize.z)
        );

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 relative = vertices[i] - currentBounds.center;
            relative = Vector3.Scale(relative, scale);
            vertices[i] = targetBounds.center + relative;
        }
    }

    private Bounds CalculateLocalBounds(Vector3[] vertices)
    {
        Bounds bounds = new Bounds(vertices[0], Vector3.zero);

        for (int i = 1; i < vertices.Length; i++)
        {
            bounds.Encapsulate(vertices[i]);
        }

        return bounds;
    }

    private Bounds GetSourceBoundsInOverlayLocal(MeshGlitchOverlay overlay)
    {
        Bounds sourceBounds = overlay.source.bounds;
        Transform overlayTransform = overlay.filter.transform;

        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;

        Vector3[] corners = new Vector3[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        Bounds localBounds = new Bounds(overlayTransform.InverseTransformPoint(corners[0]), Vector3.zero);

        for (int i = 1; i < corners.Length; i++)
        {
            localBounds.Encapsulate(overlayTransform.InverseTransformPoint(corners[i]));
        }

        return localBounds;
    }

    private float SafeBoundsScale(float target, float current)
    {
        if (Mathf.Abs(current) < 0.0001f)
            return 1f;

        return target / current;
    }

    private void OnDestroy()
    {
        if (runtimeAvatarMaterial != null)
        {
            Destroy(runtimeAvatarMaterial);
            runtimeAvatarMaterial = null;
        }

        if (runtimeMeshGlitchMaterial != null)
        {
            Destroy(runtimeMeshGlitchMaterial);
            runtimeMeshGlitchMaterial = null;
        }

        if (meshGlitchOverlays != null)
        {
            foreach (MeshGlitchOverlay overlay in meshGlitchOverlays)
            {
                if (overlay == null) continue;

                if (overlay.bakedMesh != null)
                    Destroy(overlay.bakedMesh);

                if (overlay.lineMesh != null)
                    Destroy(overlay.lineMesh);
            }
        }
    }

    public bool isLoaded() { return AVATAR_LOADED; }
    public string GetVisualState() { return visualState; }

    public GameObject getLeftHand() { return LeftHand != null ? LeftHand.gameObject : null; }
    public GameObject getRightHand() { return RightHand != null ? RightHand.gameObject : null; }
    public GameObject getLeftFoot() { return LeftFoot != null ? LeftFoot.gameObject : null; }
    public GameObject getRightFoot() { return RightFoot != null ? RightFoot.gameObject : null; }
    public GameObject getLeftForeArm() { return LeftForeArm != null ? LeftForeArm.gameObject : null; }
    public GameObject getRightForeArm() { return RightForeArm != null ? RightForeArm.gameObject : null; }
    public GameObject getLeftLeg() { return LeftLeg != null ? LeftLeg.gameObject : null; }
    public GameObject getRightLeg() { return RightLeg != null ? RightLeg.gameObject : null; }
    public GameObject getLeftShoulder() { return LeftShoulder != null ? LeftShoulder.gameObject : null; }
    public GameObject getRightShoulder() { return RightShoulder != null ? RightShoulder.gameObject : null; }
    public GameObject getLeftUpLeg() { return LeftUpLeg != null ? LeftUpLeg.gameObject : null; }
    public GameObject getRightUpLeg() { return RightUpLeg != null ? RightUpLeg.gameObject : null; }
    public GameObject getLeftPalm() { return LeftPalm != null ? LeftPalm.gameObject : null; }
    public GameObject getRightPalm() { return RightPalm != null ? RightPalm.gameObject : null; }

    public Quaternion getRightHipRotation() { return server.GetRotation(Landmark.RIGHT_HIP); }
    public Quaternion getLeftHipRotation() { return server.GetRotation(Landmark.LEFT_HIP); }
    public Quaternion getRightElbowRotation() { return server.GetRotation(Landmark.RIGHT_ELBOW); }
    public Quaternion getLeftElbowRotation() { return server.GetRotation(Landmark.LEFT_ELBOW); }

    private bool IsValidQuaternion(Quaternion q)
    {
        if (!IsFinite(q.x) || !IsFinite(q.y) || !IsFinite(q.z) || !IsFinite(q.w))
            return false;

        float lengthSq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        return lengthSq > 0.0001f;
    }

    private bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private bool IsLikelyLostTrackingPose(
        Quaternion pelvis,
        Quaternion torso,
        Quaternion leftShoulder,
        Quaternion rightShoulder,
        Quaternion leftElbow,
        Quaternion rightElbow,
        Quaternion leftHip,
        Quaternion rightHip,
        Quaternion leftKnee,
        Quaternion rightKnee)
    {
        if (!enableLostTrackingFallback)
            return false;

        if (!IsValidQuaternion(pelvis) || !IsValidQuaternion(torso) ||
            !IsValidQuaternion(leftShoulder) || !IsValidQuaternion(rightShoulder) ||
            !IsValidQuaternion(leftElbow) || !IsValidQuaternion(rightElbow) ||
            !IsValidQuaternion(leftHip) || !IsValidQuaternion(rightHip) ||
            !IsValidQuaternion(leftKnee) || !IsValidQuaternion(rightKnee))
            return true;

        float threshold = Mathf.Max(0.01f, lostPoseIdentityAngle);
        return Quaternion.Angle(Quaternion.identity, pelvis) <= threshold &&
               Quaternion.Angle(Quaternion.identity, torso) <= threshold &&
               Quaternion.Angle(Quaternion.identity, leftShoulder) <= threshold &&
               Quaternion.Angle(Quaternion.identity, rightShoulder) <= threshold &&
               Quaternion.Angle(Quaternion.identity, leftElbow) <= threshold &&
               Quaternion.Angle(Quaternion.identity, rightElbow) <= threshold &&
               Quaternion.Angle(Quaternion.identity, leftHip) <= threshold &&
               Quaternion.Angle(Quaternion.identity, rightHip) <= threshold &&
               Quaternion.Angle(Quaternion.identity, leftKnee) <= threshold &&
               Quaternion.Angle(Quaternion.identity, rightKnee) <= threshold;
    }

    private void ApplyLostTrackingFallback()
    {
        if (fallbackToLastValidPose && !hasLastValidPose)
            return;

        float t = 1f - Mathf.Exp(-Mathf.Max(0.001f, lostPoseBlendSpeed) * Time.deltaTime);

        bool useLastValid = fallbackToLastValidPose && hasLastValidPose;

        Hips.localRotation = Quaternion.Slerp(Hips.localRotation, useLastValid ? lastValidHipsRotation : restHipsRotation, t);
        Spine.localRotation = Quaternion.Slerp(Spine.localRotation, useLastValid ? lastValidSpineRotation : restSpineRotation, t);
        RightArm.localRotation = Quaternion.Slerp(RightArm.localRotation, useLastValid ? lastValidRightArmRotation : restRightArmRotation, t);
        LeftArm.localRotation = Quaternion.Slerp(LeftArm.localRotation, useLastValid ? lastValidLeftArmRotation : restLeftArmRotation, t);
        LeftForeArm.localRotation = Quaternion.Slerp(LeftForeArm.localRotation, useLastValid ? lastValidLeftForeArmRotation : restLeftForeArmRotation, t);
        RightForeArm.localRotation = Quaternion.Slerp(RightForeArm.localRotation, useLastValid ? lastValidRightForeArmRotation : restRightForeArmRotation, t);
        RightUpLeg.localRotation = Quaternion.Slerp(RightUpLeg.localRotation, useLastValid ? lastValidRightUpLegRotation : restRightUpLegRotation, t);
        LeftUpLeg.localRotation = Quaternion.Slerp(LeftUpLeg.localRotation, useLastValid ? lastValidLeftUpLegRotation : restLeftUpLegRotation, t);
        LeftLeg.localRotation = Quaternion.Slerp(LeftLeg.localRotation, useLastValid ? lastValidLeftLegRotation : restLeftLegRotation, t);
        RightLeg.localRotation = Quaternion.Slerp(RightLeg.localRotation, useLastValid ? lastValidRightLegRotation : restRightLegRotation, t);
    }

    private void CaptureLastValidPose()
    {
        lastValidHipsRotation = Hips.localRotation;
        lastValidSpineRotation = Spine.localRotation;
        lastValidRightArmRotation = RightArm.localRotation;
        lastValidLeftArmRotation = LeftArm.localRotation;
        lastValidLeftForeArmRotation = LeftForeArm.localRotation;
        lastValidRightForeArmRotation = RightForeArm.localRotation;
        lastValidRightUpLegRotation = RightUpLeg.localRotation;
        lastValidLeftUpLegRotation = LeftUpLeg.localRotation;
        lastValidLeftLegRotation = LeftLeg.localRotation;
        lastValidRightLegRotation = RightLeg.localRotation;
        hasLastValidPose = true;
    }

    private Quaternion SmoothRotation(Transform bone, Quaternion targetRotation, float t)
    {
        if (!enableMotionSmoothing)
            return targetRotation;

        return Quaternion.Slerp(bone.localRotation, targetRotation, t);
    }

    private void ApplyTrackedPose(
        Quaternion pelvis,
        Quaternion torso,
        Quaternion leftShoulder,
        Quaternion rightShoulder,
        Quaternion leftElbow,
        Quaternion rightElbow,
        Quaternion leftHip,
        Quaternion rightHip,
        Quaternion leftKnee,
        Quaternion rightKnee)
    {
        float t = enableMotionSmoothing
            ? 1f - Mathf.Exp(-Mathf.Max(0.001f, motionSmoothingSpeed) * Time.deltaTime)
            : 1f;

        Hips.localRotation = SmoothRotation(Hips, pelvis, t);
        Spine.localRotation = SmoothRotation(Spine, torso, t);
        RightArm.localRotation = SmoothRotation(RightArm, Quaternion.Euler(0, 0, 90) * rightShoulder, t);
        LeftArm.localRotation = SmoothRotation(LeftArm, Quaternion.Euler(0, 0, -90) * leftShoulder, t);
        LeftForeArm.localRotation = SmoothRotation(LeftForeArm, leftElbow, t);
        RightForeArm.localRotation = SmoothRotation(RightForeArm, rightElbow, t);
        RightUpLeg.localRotation = SmoothRotation(RightUpLeg, rightHip, t);
        LeftUpLeg.localRotation = SmoothRotation(LeftUpLeg, leftHip, t);
        LeftLeg.localRotation = SmoothRotation(LeftLeg, leftKnee, t);
        RightLeg.localRotation = SmoothRotation(RightLeg, rightKnee, t);
    }

    public void MoveToFloor(float floorY)
    {
        if (LeftFoot == null || RightFoot == null) return;

        Vector3 pos = transform.position;
        float min = Mathf.Min(LeftFoot.position.y, RightFoot.position.y);
        transform.position = new Vector3(pos.x, pos.y + (floorY - min), pos.z);
    }

    private void ApplyAvatarRootPlacement()
    {
        if (!lockAvatarRootToOrigin) return;

        transform.position = avatarRootOffset;
        if (orientInitialRestPoseToViewer && useInitialViewerFacingRoot)
            transform.rotation = Quaternion.Euler(initialViewerFacingRootEuler);
        else
            transform.rotation = initialRootRotation;

        if (debugRootPlacement)
            Debug.Log($"[{name}] Avatar root placed at {transform.position}, rotation {transform.rotation.eulerAngles}.");
    }

    private void Update()
    {
        if (!AVATAR_LOADED || server == null) return;
        UpdateAvatarGlitch();

        if (Hips == null || Spine == null) return;
        if (RightArm == null || LeftArm == null || RightForeArm == null || LeftForeArm == null) return;
        if (RightUpLeg == null || LeftUpLeg == null || LeftLeg == null || RightLeg == null) return;

        Quaternion pelvis = server.GetRotation(Landmark.PELVIS, Delay);
        Quaternion torso = server.GetRotation(Landmark.SHOULDER_CENTER, Delay);
        Quaternion leftShoulder = server.GetRotation(Landmark.LEFT_SHOULDER, Delay);
        Quaternion rightShoulder = server.GetRotation(Landmark.RIGHT_SHOULDER, Delay);
        Quaternion leftElbow = server.GetRotation(Landmark.LEFT_ELBOW, Delay);
        Quaternion rightElbow = server.GetRotation(Landmark.RIGHT_ELBOW, Delay);
        Quaternion leftHip = server.GetRotation(Landmark.LEFT_HIP, Delay);
        Quaternion rightHip = server.GetRotation(Landmark.RIGHT_HIP, Delay);
        Quaternion leftKnee = server.GetRotation(Landmark.LEFT_KNEE, Delay);
        Quaternion rightKnee = server.GetRotation(Landmark.RIGHT_KNEE, Delay);

        bool lostTracking = IsLikelyLostTrackingPose(
            pelvis,
            torso,
            leftShoulder,
            rightShoulder,
            leftElbow,
            rightElbow,
            leftHip,
            rightHip,
            leftKnee,
            rightKnee
        );

        if (lostTracking)
        {
            if (!lostTrackingFallbackActive && logLostTrackingFallback)
                Debug.Log($"[{name}] Lost tracking pose detected. Applying last valid pose fallback.");

            lostTrackingFallbackActive = true;
            useInitialViewerFacingRoot = !hasLastValidPose;
            ApplyLostTrackingFallback();
        }
        else
        {
            if (lostTrackingFallbackActive && logLostTrackingFallback)
                Debug.Log($"[{name}] Tracking pose restored.");

            lostTrackingFallbackActive = false;
            useInitialViewerFacingRoot = false;
            ApplyAvatarRootPlacement();

            ApplyTrackedPose(
                pelvis,
                torso,
                leftShoulder,
                rightShoulder,
                leftElbow,
                rightElbow,
                leftHip,
                rightHip,
                leftKnee,
                rightKnee
            );
            CaptureLastValidPose();
        }

        if (!lockAvatarRootToOrigin && moveToFloor) MoveToFloor(floorLevel);
        if (keepRootAtOrigin) ApplyAvatarRootPlacement();
    }
}
