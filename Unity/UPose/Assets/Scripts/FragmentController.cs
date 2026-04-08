using UnityEngine;

public class FragmentController : MonoBehaviour
{
    [Header("Fragment Slots")]
    public FragmentSlot[] slots;

    [Header("Density Control")]
    public int minActiveSlots = 6;
    public int maxActiveSlots = 8;

    [Header("Spawn Timing")]
    public float minSpawnInterval = 0.2f;
    public float maxSpawnInterval = 0.6f;

    [Header("Main Wall Region")]
    public Vector2 regionX = new Vector2(-3.5f, 3.5f);
    public Vector2 regionY = new Vector2(-1.8f, 1.8f);

    [Header("Overlap Control")]
    public float minSlotDistance = 1.4f;
    [Range(0f, 1f)]
    public float overlapChance = 0.15f;
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

    [Range(0f, 1f)]
    public float distortionChance = 0.15f;

    private float spawnTimer = 0f;
    private float nextSpawnTime = 0.5f;

    void Start()
    {
        ScheduleNextSpawn();
    }

    void Update()
    {
        int activeCount = GetActiveSlotCount();

        // 先补足最低密度
        if (activeCount < minActiveSlots)
        {
            while (GetActiveSlotCount() < minActiveSlots)
            {
                if (!TrySpawnFragment()) break;
            }

            spawnTimer = 0f;
            ScheduleNextSpawn();
            return;
        }

        // 再在最大密度以内继续补充
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= nextSpawnTime && activeCount < maxActiveSlots)
        {
            TrySpawnFragment();
            spawnTimer = 0f;
            ScheduleNextSpawn();
        }
    }

    void ScheduleNextSpawn()
    {
        nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    bool TrySpawnFragment()
    {
        FragmentSlot freeSlot = GetFreeSlot();
        if (freeSlot == null) return false;

        FragmentProfile profile = GenerateRandomProfile();
        freeSlot.Activate(profile);
        return true;
    }

    FragmentSlot GetFreeSlot()
    {
        foreach (FragmentSlot slot in slots)
        {
            if (!slot.IsActive())
                return slot;
        }
        return null;
    }

    int GetActiveSlotCount()
    {
        int count = 0;
        foreach (FragmentSlot slot in slots)
        {
            if (slot.IsActive()) count++;
        }
        return count;
    }

    FragmentProfile GenerateRandomProfile()
    {
        FragmentProfile profile = new FragmentProfile();

        profile.boneName = GetWeightedRandomBone();

        profile.startPos = GetSpawnPosition();

        Vector3 drifted = profile.startPos + new Vector3(
            Random.Range(driftOffsetX.x, driftOffsetX.y),
            Random.Range(driftOffsetY.x, driftOffsetY.y),
            0f
        );

        profile.targetPos = ClampToRegion(drifted);

        profile.lifeTime = Random.Range(lifeTimeRange.x, lifeTimeRange.y);
        profile.fadeInTime = Random.Range(fadeInRange.x, fadeInRange.y);
        profile.fadeOutTime = Random.Range(fadeOutRange.x, fadeOutRange.y);

        // 防止淡入淡出总和过大
        float maxAllowedFade = profile.lifeTime * 0.45f;
        profile.fadeInTime = Mathf.Min(profile.fadeInTime, maxAllowedFade);
        profile.fadeOutTime = Mathf.Min(profile.fadeOutTime, maxAllowedFade);

        profile.moveSpeed = Random.Range(moveSpeedRange.x, moveSpeedRange.y);
        profile.useDistortion = Random.value < distortionChance;

        return profile;
    }

    string GetWeightedRandomBone()
    {
        float total = stableWeight + mediumWeight + unstableWeight;
        float roll = Random.value * total;

        if (roll < stableWeight)
        {
            return GetRandomFromArray(stableBones, "mixamorig:Spine2");
        }
        else if (roll < stableWeight + mediumWeight)
        {
            return GetRandomFromArray(mediumBones, "mixamorig:Head");
        }
        else
        {
            return GetRandomFromArray(unstableBones, "mixamorig:LeftHand");
        }
    }

    string GetRandomFromArray(string[] arr, string fallback)
    {
        if (arr == null || arr.Length == 0) return fallback;
        return arr[Random.Range(0, arr.Length)];
    }

    Vector3 GetSpawnPosition()
    {
        bool allowOverlap = Random.value < overlapChance;

        for (int i = 0; i < maxPositionTries; i++)
        {
            Vector3 candidate = GetRandomPositionInRegion();

            if (allowOverlap || !IsPositionTooClose(candidate))
            {
                return candidate;
            }
        }

        // 实在找不到就退而求其次
        return GetRandomPositionInRegion();
    }

    Vector3 GetRandomPositionInRegion()
    {
        return new Vector3(
            Random.Range(regionX.x, regionX.y),
            Random.Range(regionY.x, regionY.y),
            0f
        );
    }

    Vector3 ClampToRegion(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, regionX.x, regionX.y);
        pos.y = Mathf.Clamp(pos.y, regionY.x, regionY.y);
        pos.z = 0f;
        return pos;
    }

    bool IsPositionTooClose(Vector3 candidate)
    {
        foreach (FragmentSlot slot in slots)
        {
            if (!slot.IsActive()) continue;

            Vector3 existing = slot.GetCurrentScreenLocalPosition();
            if (Vector3.Distance(candidate, existing) < minSlotDistance)
            {
                return true;
            }
        }

        return false;
    }
}