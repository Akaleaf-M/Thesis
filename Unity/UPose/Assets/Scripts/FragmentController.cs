using UnityEngine;

public class FragmentController : MonoBehaviour
{
    [Header("Fixed Solo Slots (P1-P4)")]
    public FragmentSlot[] fixedSoloSlots; // assign FragmentSlot_P1 ~ P4 in inspector

    [Header("Random Collective Slots")]
    public FragmentSlot[] randomCollectiveSlots; // assign the remaining collective slots

    [Header("Fixed Solo Corner Positions")]
    public Vector3 topLeftPos = new Vector3(-3.2f, 1.6f, 0.12f);
    public Vector3 topRightPos = new Vector3(3.2f, 1.6f, 0.12f);
    public Vector3 bottomLeftPos = new Vector3(-3.2f, -1.6f, 0.12f);
    public Vector3 bottomRightPos = new Vector3(3.2f, -1.6f, 0.12f);

    [Header("Solo Slot Lifetime")]
    public bool keepSoloSlotsAlwaysOn = true;
    public float soloLifeTime = 9999f;

    [Header("Collective Density Control")]
    public int minActiveCollectiveSlots = 2;
    public int maxActiveCollectiveSlots = 4;

    [Header("Collective Spawn Timing")]
    public float minSpawnInterval = 0.2f;
    public float maxSpawnInterval = 0.6f;

    [Header("Collective Region")]
    public Vector2 regionX = new Vector2(-2.5f, 2.5f);
    public Vector2 regionY = new Vector2(-1.2f, 1.2f);

    [Header("Collective Z Layering")]
    public float baseZ = 0f;
    public float zStep = 0.03f;
    public int zLayerCount = 4;
    [Range(0f, 1f)] public float sameLayerChance = 0.15f;

    [Header("Collective Overlap Control")]
    public float minSlotDistance = 1.2f;
    [Range(0f, 1f)] public float overlapChance = 0.15f;
    public int maxPositionTries = 12;

    [Header("Bone Pool")]
    public string[] stableBones = new string[]
    {
        "mixamorig:Hips",
        "mixamorig:Spine",
        "mixamorig:Spine2"
    };

    public string[] mediumBones = new string[]
    {
        "mixamorig:Head",
        "mixamorig:LeftArm",
        "mixamorig:RightArm"
    };

    public string[] unstableBones = new string[]
    {
        "mixamorig:LeftForeArm",
        "mixamorig:RightForeArm",
        "mixamorig:LeftHand",
        "mixamorig:RightHand"
    };

    [Header("Bone Weights")]
    [Range(0f, 1f)] public float stableWeight = 0.65f;
    [Range(0f, 1f)] public float mediumWeight = 0.25f;
    [Range(0f, 1f)] public float unstableWeight = 0.10f;

    [Header("Drift Range")]
    public Vector2 driftOffsetX = new Vector2(-0.8f, 0.8f);
    public Vector2 driftOffsetY = new Vector2(-0.4f, 0.4f);

    [Header("Profile Ranges")]
    public Vector2 lifeTimeRange = new Vector2(3.0f, 5.0f);
    public Vector2 fadeInRange = new Vector2(0.2f, 0.5f);
    public Vector2 fadeOutRange = new Vector2(0.4f, 0.8f);
    public Vector2 moveSpeedRange = new Vector2(0.8f, 1.6f);

    [Range(0f, 1f)] public float distortionChance = 0.15f;

    private float spawnTimer = 0f;
    private float nextSpawnTime = 0.5f;

    void Start()
    {
        ActivateFixedSoloSlots();
        ScheduleNextSpawn();
    }

    void Update()
    {
        MaintainSoloSlots();

        int activeCount = GetActiveCollectiveSlotCount();

        if (activeCount < minActiveCollectiveSlots)
        {
            while (GetActiveCollectiveSlotCount() < minActiveCollectiveSlots)
            {
                if (!TrySpawnCollectiveFragment()) break;
            }

            spawnTimer = 0f;
            ScheduleNextSpawn();
            return;
        }

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= nextSpawnTime && activeCount < maxActiveCollectiveSlots)
        {
            TrySpawnCollectiveFragment();
            spawnTimer = 0f;
            ScheduleNextSpawn();
        }
    }

    void ActivateFixedSoloSlots()
    {
        if (fixedSoloSlots == null || fixedSoloSlots.Length == 0) return;

        Vector3[] positions = new Vector3[]
        {
            topLeftPos,
            topRightPos,
            bottomLeftPos,
            bottomRightPos
        };

        for (int s = 0; s < fixedSoloSlots.Length && s < 4; s++)
        {
            FragmentSlot slot = fixedSoloSlots[s];
            if (slot == null) continue;

            FragmentProfile profile = GenerateFixedSoloProfile(s, positions[s]);
            slot.Activate(profile);
        }
    }

    void MaintainSoloSlots()
    {
        if (!keepSoloSlotsAlwaysOn || fixedSoloSlots == null) return;

        Vector3[] positions = new Vector3[]
        {
            topLeftPos,
            topRightPos,
            bottomLeftPos,
            bottomRightPos
        };

        for (int s = 0; s < fixedSoloSlots.Length && s < 4; s++)
        {
            FragmentSlot slot = fixedSoloSlots[s];
            if (slot == null) continue;

            if (!slot.IsActive())
            {
                FragmentProfile profile = GenerateFixedSoloProfile(s, positions[s]);
                slot.Activate(profile);
            }
        }
    }

    FragmentProfile GenerateFixedSoloProfile(int soloIdx, Vector3 fixedPos)
    {
        FragmentProfile profile = new FragmentProfile();

        profile.sourceType = FragmentSourceType.Solo;
        profile.soloIndex = soloIdx + 1; // P1..P4

        profile.boneName = GetWeightedRandomBone();
        profile.startPos = fixedPos;
        profile.targetPos = fixedPos;

        profile.lifeTime = keepSoloSlotsAlwaysOn ? soloLifeTime : 8f;
        profile.fadeInTime = 0.2f;
        profile.fadeOutTime = 0.2f;
        profile.moveSpeed = 0f;
        profile.useDistortion = false;

        ApplyShotProfile(profile);
        return profile;
    }

    void ScheduleNextSpawn()
    {
        nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    bool TrySpawnCollectiveFragment()
    {
        FragmentSlot freeSlot = GetFreeCollectiveSlot();
        if (freeSlot == null) return false;

        FragmentProfile profile = GenerateRandomCollectiveProfile();
        freeSlot.Activate(profile);
        return true;
    }

    FragmentSlot GetFreeCollectiveSlot()
    {
        foreach (FragmentSlot slot in randomCollectiveSlots)
        {
            if (slot == null) continue;
            if (!slot.IsActive())
                return slot;
        }
        return null;
    }

    int GetActiveCollectiveSlotCount()
    {
        int count = 0;
        foreach (FragmentSlot slot in randomCollectiveSlots)
        {
            if (slot == null) continue;
            if (slot.IsActive()) count++;
        }
        return count;
    }

    FragmentProfile GenerateRandomCollectiveProfile()
    {
        FragmentProfile profile = new FragmentProfile();

        profile.sourceType = FragmentSourceType.Collective;
        profile.soloIndex = -1;

        profile.boneName = GetWeightedRandomBone();
        profile.startPos = GetSpawnPosition();

        Vector3 drifted = profile.startPos + new Vector3(
            Random.Range(driftOffsetX.x, driftOffsetX.y),
            Random.Range(driftOffsetY.x, driftOffsetY.y),
            0f
        );

        profile.targetPos = ClampToRegionKeepZ(drifted, profile.startPos.z);

        profile.lifeTime = Random.Range(lifeTimeRange.x, lifeTimeRange.y);
        profile.fadeInTime = Random.Range(fadeInRange.x, fadeInRange.y);
        profile.fadeOutTime = Random.Range(fadeOutRange.x, fadeOutRange.y);

        float maxAllowedFade = profile.lifeTime * 0.45f;
        profile.fadeInTime = Mathf.Min(profile.fadeInTime, maxAllowedFade);
        profile.fadeOutTime = Mathf.Min(profile.fadeOutTime, maxAllowedFade);

        profile.moveSpeed = Random.Range(moveSpeedRange.x, moveSpeedRange.y);
        profile.useDistortion = Random.value < distortionChance;

        ApplyShotProfile(profile);

        return profile;
    }

    void ApplyShotProfile(FragmentProfile profile)
    {
        if (profile == null) return;

        string bone = profile.boneName;
        int variant = Random.Range(0, 5);

        profile.cameraOffset = new Vector3(0f, 0f, -2f);
        profile.cameraFOV = Random.Range(30f, 38f);
        profile.targetSmooth = 6f;
        profile.positionSmooth = 4f;
        profile.lookSmooth = 4f;
        profile.useBoneRotation = false;

        if (bone == "mixamorig:Hips")
        {
            if (variant == 0)
            {
                profile.cameraOffset = new Vector3(-1.0f, 0.1f, -1.8f);
                profile.cameraFOV = Random.Range(30f, 36f);
            }
            else if (variant == 1)
            {
                profile.cameraOffset = new Vector3(1.0f, 0.1f, -1.8f);
                profile.cameraFOV = Random.Range(30f, 36f);
            }
            else if (variant == 2)
            {
                profile.cameraOffset = new Vector3(0.2f, 0.8f, -1.6f);
                profile.cameraFOV = Random.Range(26f, 32f);
            }
            else if (variant == 3)
            {
                profile.cameraOffset = new Vector3(0.0f, 0.1f, -1.4f);
                profile.cameraFOV = Random.Range(28f, 34f);
            }
            else
            {
                profile.cameraOffset = new Vector3(-0.6f, 0.5f, -2.1f);
                profile.cameraFOV = Random.Range(32f, 40f);
            }
        }
        else if (bone == "mixamorig:Spine" || bone == "mixamorig:Spine2")
        {
            if (variant == 0)
            {
                profile.cameraOffset = new Vector3(-0.9f, 0.15f, -1.6f);
                profile.cameraFOV = Random.Range(30f, 36f);
            }
            else if (variant == 1)
            {
                profile.cameraOffset = new Vector3(0.9f, 0.15f, -1.6f);
                profile.cameraFOV = Random.Range(30f, 36f);
            }
            else if (variant == 2)
            {
                profile.cameraOffset = new Vector3(0.0f, 0.8f, -1.7f);
                profile.cameraFOV = Random.Range(24f, 30f);
            }
            else if (variant == 3)
            {
                profile.cameraOffset = new Vector3(0.0f, 0.05f, -1.2f);
                profile.cameraFOV = Random.Range(26f, 32f);
            }
            else
            {
                profile.cameraOffset = new Vector3(-0.3f, 0.3f, -2.3f);
                profile.cameraFOV = Random.Range(34f, 42f);
            }
        }
        else if (bone == "mixamorig:Head")
        {
            if (variant == 0)
            {
                profile.cameraOffset = new Vector3(-0.5f, 0.2f, -1.2f);
                profile.cameraFOV = Random.Range(24f, 30f);
            }
            else if (variant == 1)
            {
                profile.cameraOffset = new Vector3(0.5f, 0.2f, -1.2f);
                profile.cameraFOV = Random.Range(24f, 30f);
            }
            else if (variant == 2)
            {
                profile.cameraOffset = new Vector3(0.0f, 0.7f, -1.3f);
                profile.cameraFOV = Random.Range(22f, 28f);
            }
            else if (variant == 3)
            {
                profile.cameraOffset = new Vector3(-0.2f, 0.1f, -1.6f);
                profile.cameraFOV = Random.Range(30f, 38f);
            }
            else
            {
                profile.cameraOffset = new Vector3(0.0f, 0.0f, -0.9f);
                profile.cameraFOV = Random.Range(20f, 26f);
            }
        }
        else if (bone == "mixamorig:LeftArm" || bone == "mixamorig:RightArm")
        {
            if (variant == 0)
            {
                profile.cameraOffset = new Vector3(-0.6f, 0.2f, -1.4f);
                profile.cameraFOV = Random.Range(32f, 40f);
            }
            else if (variant == 1)
            {
                profile.cameraOffset = new Vector3(0.6f, 0.2f, -1.4f);
                profile.cameraFOV = Random.Range(32f, 40f);
            }
            else if (variant == 2)
            {
                profile.cameraOffset = new Vector3(0.0f, 0.5f, -1.5f);
                profile.cameraFOV = Random.Range(28f, 36f);
            }
            else if (variant == 3)
            {
                profile.cameraOffset = new Vector3(0.0f, 0.1f, -1.1f);
                profile.cameraFOV = Random.Range(36f, 46f);
            }
            else
            {
                profile.cameraOffset = new Vector3(-0.2f, 0.4f, -1.9f);
                profile.cameraFOV = Random.Range(38f, 48f);
            }

            profile.targetSmooth = 5f;
            profile.positionSmooth = 3.5f;
            profile.lookSmooth = 3.5f;
        }
        else if (
            bone == "mixamorig:LeftForeArm" || bone == "mixamorig:RightForeArm" ||
            bone == "mixamorig:LeftHand" || bone == "mixamorig:RightHand"
        )
        {
            if (variant == 0)
            {
                profile.cameraOffset = new Vector3(0.0f, 0.0f, -1.0f);
                profile.cameraFOV = Random.Range(36f, 46f);
            }
            else if (variant == 1)
            {
                profile.cameraOffset = new Vector3(0.3f, 0.15f, -1.1f);
                profile.cameraFOV = Random.Range(34f, 42f);
            }
            else if (variant == 2)
            {
                profile.cameraOffset = new Vector3(-0.3f, 0.25f, -1.2f);
                profile.cameraFOV = Random.Range(30f, 38f);
            }
            else if (variant == 3)
            {
                profile.cameraOffset = new Vector3(0.0f, 0.4f, -1.3f);
                profile.cameraFOV = Random.Range(28f, 36f);
            }
            else
            {
                profile.cameraOffset = new Vector3(0.0f, 0.1f, -0.9f);
                profile.cameraFOV = Random.Range(40f, 52f);
            }

            profile.targetSmooth = 4.5f;
            profile.positionSmooth = 3f;
            profile.lookSmooth = 3f;
            profile.useBoneRotation = true;
        }
    }

    string GetWeightedRandomBone()
    {
        float total = stableWeight + mediumWeight + unstableWeight;
        float roll = Random.value * total;

        if (roll < stableWeight)
            return GetRandomFromArray(stableBones, "mixamorig:Spine2");
        else if (roll < stableWeight + mediumWeight)
            return GetRandomFromArray(mediumBones, "mixamorig:Head");
        else
            return GetRandomFromArray(unstableBones, "mixamorig:LeftHand");
    }

    string GetRandomFromArray(string[] arr, string fallback)
    {
        if (arr == null || arr.Length == 0) return fallback;
        return arr[Random.Range(0, arr.Length)];
    }

    Vector3 GetSpawnPosition()
    {
        bool allowOverlap = Random.value < overlapChance;
        bool allowSameLayer = Random.value < sameLayerChance;

        float z = GetRandomZLayer();

        for (int i = 0; i < maxPositionTries; i++)
        {
            Vector3 candidate = GetRandomPositionInRegion(z);

            if (allowOverlap || !IsPositionTooClose(candidate, allowSameLayer))
                return candidate;
        }

        return GetRandomPositionInRegion(z);
    }

    float GetRandomZLayer()
    {
        int layer = Random.Range(0, Mathf.Max(1, zLayerCount));
        return baseZ + layer * zStep;
    }

    Vector3 GetRandomPositionInRegion(float z)
    {
        return new Vector3(
            Random.Range(regionX.x, regionX.y),
            Random.Range(regionY.x, regionY.y),
            z
        );
    }

    Vector3 ClampToRegionKeepZ(Vector3 pos, float z)
    {
        pos.x = Mathf.Clamp(pos.x, regionX.x, regionX.y);
        pos.y = Mathf.Clamp(pos.y, regionY.x, regionY.y);
        pos.z = z;
        return pos;
    }

    bool IsPositionTooClose(Vector3 candidate, bool allowSameLayer)
    {
        foreach (FragmentSlot slot in randomCollectiveSlots)
        {
            if (slot == null || !slot.IsActive()) continue;

            Vector3 existing = slot.transform.localPosition;

            if (!allowSameLayer)
            {
                if (Mathf.Abs(existing.z - candidate.z) > zStep * 0.5f)
                    continue;
            }

            Vector2 a = new Vector2(existing.x, existing.y);
            Vector2 b = new Vector2(candidate.x, candidate.y);

            if (Vector2.Distance(a, b) < minSlotDistance)
                return true;
        }

        return false;
    }
}