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

    [Header("Solo Front Layer")]
    public float soloFrontZ = 0.30f;

    [Header("Solo Slot Lifetime")]
    public bool keepSoloSlotsAlwaysOn = true;
    public float soloLifeTime = 9999f;

    [Header("Solo Slot View Refresh")]
    public bool refreshSoloViewsWhileFixed = true;
    public Vector2 soloViewRefreshRange = new Vector2(5.0f, 9.0f);

    [Tooltip("If true, solo slots will refresh one by one at different times instead of all refreshing together.")]
    public bool staggerSoloRefresh = true;

    private float[] soloRefreshTimers;
    private float[] soloNextRefreshTimes;

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

    [Header("Avoid Solo Corner Area")]
    public bool collectiveAvoidSoloCorners = true;
    public float soloCornerAvoidDistance = 1.45f;
    public int maxSoloAvoidTries = 20;

    [Header("Collective Brownian Motion")]
    public bool useCollectiveBrownianMotion = true;

    [Tooltip("How strong the random push is. Higher = more restless movement.")]
    public Vector2 brownianAccelerationRange = new Vector2(0.8f, 1.4f);

    [Tooltip("Maximum movement speed for collective slots.")]
    public Vector2 brownianMaxSpeedRange = new Vector2(0.22f, 0.45f);

    [Tooltip("Drag prevents infinite acceleration. Lower = more floating, higher = more damped.")]
    public Vector2 brownianDragRange = new Vector2(0.25f, 0.55f);

    [Tooltip("Initial random speed when a collective slot spawns.")]
    public Vector2 brownianInitialSpeedRange = new Vector2(0.06f, 0.18f);

    [Tooltip("How much velocity is preserved after bouncing off region bounds.")]
    [Range(0f, 1f)] public float brownianBounce = 0.65f;

    [Tooltip("How strongly collective slots are pushed away from solo corners.")]
    public float soloAvoidPushStrength = 1.2f;

    private Vector3[] collectiveVelocities;
    private float[] collectiveAccelerations;
    private float[] collectiveMaxSpeeds;
    private float[] collectiveDrags;

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

    [Header("Old Drift Range - Kept For Compatibility")]
    public Vector2 driftOffsetX = new Vector2(-0.8f, 0.8f);
    public Vector2 driftOffsetY = new Vector2(-0.4f, 0.4f);

    [Header("Profile Ranges")]
    public Vector2 lifeTimeRange = new Vector2(4.5f, 7.0f);
    public Vector2 fadeInRange = new Vector2(0.2f, 0.5f);
    public Vector2 fadeOutRange = new Vector2(0.4f, 0.8f);

    [Tooltip("Only used if Brownian motion is disabled.")]
    public Vector2 moveSpeedRange = new Vector2(0.8f, 1.6f);

    [Range(0f, 1f)] public float distortionChance = 0.15f;

    private float spawnTimer = 0f;
    private float nextSpawnTime = 0.5f;

    void Start()
    {
        InitSoloRefreshTimers();
        InitCollectiveBrownianArrays();

        ActivateFixedSoloSlots();
        ScheduleNextSpawn();
    }

    void Update()
    {
        MaintainSoloSlots();
        UpdateSoloViewRefresh();

        int activeCount = GetActiveCollectiveSlotCount();

        if (activeCount < minActiveCollectiveSlots)
        {
            while (GetActiveCollectiveSlotCount() < minActiveCollectiveSlots)
            {
                if (!TrySpawnCollectiveFragment()) break;
            }

            spawnTimer = 0f;
            ScheduleNextSpawn();
        }
        else
        {
            spawnTimer += Time.deltaTime;

            if (spawnTimer >= nextSpawnTime && activeCount < maxActiveCollectiveSlots)
            {
                TrySpawnCollectiveFragment();
                spawnTimer = 0f;
                ScheduleNextSpawn();
            }
        }

        if (useCollectiveBrownianMotion)
        {
            UpdateCollectiveBrownianMotion();
        }
    }

    void InitSoloRefreshTimers()
    {
        int count = fixedSoloSlots != null ? fixedSoloSlots.Length : 0;

        soloRefreshTimers = new float[count];
        soloNextRefreshTimes = new float[count];

        for (int i = 0; i < count; i++)
        {
            soloRefreshTimers[i] = 0f;

            if (staggerSoloRefresh)
            {
                float baseDelay = Random.Range(soloViewRefreshRange.x, soloViewRefreshRange.y);
                float stagger = i * 1.2f;
                soloNextRefreshTimes[i] = baseDelay + stagger;
            }
            else
            {
                soloNextRefreshTimes[i] = Random.Range(soloViewRefreshRange.x, soloViewRefreshRange.y);
            }
        }
    }

    void InitCollectiveBrownianArrays()
    {
        int count = randomCollectiveSlots != null ? randomCollectiveSlots.Length : 0;

        collectiveVelocities = new Vector3[count];
        collectiveAccelerations = new float[count];
        collectiveMaxSpeeds = new float[count];
        collectiveDrags = new float[count];

        for (int i = 0; i < count; i++)
        {
            ResetCollectiveBrownianParams(i);
        }
    }

    void ResetCollectiveBrownianParams(int idx)
    {
        if (collectiveVelocities == null) return;
        if (idx < 0 || idx >= collectiveVelocities.Length) return;

        float initialSpeed = Random.Range(brownianInitialSpeedRange.x, brownianInitialSpeedRange.y);
        Vector2 seed = Random.insideUnitCircle.normalized * initialSpeed;

        if (seed == Vector2.zero)
            seed = Vector2.right * initialSpeed;

        collectiveVelocities[idx] = new Vector3(seed.x, seed.y, 0f);

        collectiveAccelerations[idx] = Random.Range(brownianAccelerationRange.x, brownianAccelerationRange.y);
        collectiveMaxSpeeds[idx] = Random.Range(brownianMaxSpeedRange.x, brownianMaxSpeedRange.y);
        collectiveDrags[idx] = Random.Range(brownianDragRange.x, brownianDragRange.y);
    }

    void ActivateFixedSoloSlots()
    {
        if (fixedSoloSlots == null || fixedSoloSlots.Length == 0) return;

        Vector3[] positions = GetSoloCornerPositions();

        for (int s = 0; s < fixedSoloSlots.Length && s < 4; s++)
        {
            FragmentSlot slot = fixedSoloSlots[s];
            if (slot == null) continue;

            FragmentProfile profile = GenerateFixedSoloProfile(s, positions[s]);
            slot.Activate(profile);

            // Force position and front Z after activation.
            slot.transform.localPosition = positions[s];

            ResetSoloRefreshTimer(s);
        }
    }

    void MaintainSoloSlots()
    {
        if (!keepSoloSlotsAlwaysOn || fixedSoloSlots == null) return;

        Vector3[] positions = GetSoloCornerPositions();

        for (int s = 0; s < fixedSoloSlots.Length && s < 4; s++)
        {
            FragmentSlot slot = fixedSoloSlots[s];
            if (slot == null) continue;

            if (!slot.IsActive())
            {
                FragmentProfile profile = GenerateFixedSoloProfile(s, positions[s]);
                slot.Activate(profile);

                // Force position and front Z after activation.
                slot.transform.localPosition = positions[s];

                ResetSoloRefreshTimer(s);
            }
        }
    }

    void UpdateSoloViewRefresh()
    {
        if (!refreshSoloViewsWhileFixed) return;
        if (fixedSoloSlots == null || fixedSoloSlots.Length == 0) return;
        if (soloRefreshTimers == null || soloNextRefreshTimes == null) return;

        Vector3[] positions = GetSoloCornerPositions();

        for (int s = 0; s < fixedSoloSlots.Length && s < 4; s++)
        {
            FragmentSlot slot = fixedSoloSlots[s];
            if (slot == null) continue;

            soloRefreshTimers[s] += Time.deltaTime;

            if (soloRefreshTimers[s] >= soloNextRefreshTimes[s])
            {
                RefreshFixedSoloView(s, positions[s]);
                ResetSoloRefreshTimer(s);
            }
        }
    }

    void RefreshFixedSoloView(int soloIdx, Vector3 fixedPos)
    {
        if (fixedSoloSlots == null) return;
        if (soloIdx < 0 || soloIdx >= fixedSoloSlots.Length) return;

        FragmentSlot slot = fixedSoloSlots[soloIdx];
        if (slot == null) return;

        FragmentProfile profile = GenerateFixedSoloProfile(soloIdx, fixedPos);

        slot.Activate(profile);
        slot.transform.localPosition = fixedPos;
    }

    void ResetSoloRefreshTimer(int soloIdx)
    {
        if (soloRefreshTimers == null || soloNextRefreshTimes == null) return;
        if (soloIdx < 0 || soloIdx >= soloRefreshTimers.Length) return;

        soloRefreshTimers[soloIdx] = 0f;
        soloNextRefreshTimes[soloIdx] = Random.Range(soloViewRefreshRange.x, soloViewRefreshRange.y);
    }

    Vector3[] GetSoloCornerPositions()
    {
        return new Vector3[]
        {
            ForceZ(topLeftPos, soloFrontZ),
            ForceZ(topRightPos, soloFrontZ),
            ForceZ(bottomLeftPos, soloFrontZ),
            ForceZ(bottomRightPos, soloFrontZ)
        };
    }

    Vector3 ForceZ(Vector3 pos, float z)
    {
        pos.z = z;
        return pos;
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
        profile.fadeInTime = 0.15f;
        profile.fadeOutTime = 0.15f;
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
        int freeIndex = GetFreeCollectiveSlotIndex();
        if (freeIndex < 0) return false;

        FragmentSlot freeSlot = randomCollectiveSlots[freeIndex];
        if (freeSlot == null) return false;

        FragmentProfile profile = GenerateRandomCollectiveProfile();
        freeSlot.Activate(profile);

        ResetCollectiveBrownianParams(freeIndex);

        return true;
    }

    int GetFreeCollectiveSlotIndex()
    {
        if (randomCollectiveSlots == null) return -1;

        for (int i = 0; i < randomCollectiveSlots.Length; i++)
        {
            FragmentSlot slot = randomCollectiveSlots[i];
            if (slot == null) continue;

            if (!slot.IsActive())
                return i;
        }

        return -1;
    }

    int GetActiveCollectiveSlotCount()
    {
        if (randomCollectiveSlots == null) return 0;

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

        if (useCollectiveBrownianMotion)
        {
            // Brownian mode: FragmentSlot itself should not drift toward a target.
            profile.targetPos = profile.startPos;
            profile.moveSpeed = 0f;
        }
        else
        {
            Vector3 drifted = profile.startPos + new Vector3(
                Random.Range(driftOffsetX.x, driftOffsetX.y),
                Random.Range(driftOffsetY.x, driftOffsetY.y),
                0f
            );

            profile.targetPos = ClampToRegionKeepZ(drifted, profile.startPos.z);
            profile.moveSpeed = Random.Range(moveSpeedRange.x, moveSpeedRange.y);
        }

        profile.lifeTime = Random.Range(lifeTimeRange.x, lifeTimeRange.y);
        profile.fadeInTime = Random.Range(fadeInRange.x, fadeInRange.y);
        profile.fadeOutTime = Random.Range(fadeOutRange.x, fadeOutRange.y);

        float maxAllowedFade = profile.lifeTime * 0.45f;
        profile.fadeInTime = Mathf.Min(profile.fadeInTime, maxAllowedFade);
        profile.fadeOutTime = Mathf.Min(profile.fadeOutTime, maxAllowedFade);

        profile.useDistortion = Random.value < distortionChance;

        ApplyShotProfile(profile);

        return profile;
    }

    void UpdateCollectiveBrownianMotion()
    {
        if (randomCollectiveSlots == null) return;
        if (collectiveVelocities == null) return;

        for (int i = 0; i < randomCollectiveSlots.Length; i++)
        {
            FragmentSlot slot = randomCollectiveSlots[i];
            if (slot == null || !slot.IsActive()) continue;

            Vector2 accel2D = Random.insideUnitCircle * collectiveAccelerations[i];
            Vector3 acceleration = new Vector3(accel2D.x, accel2D.y, 0f);

            collectiveVelocities[i] += acceleration * Time.deltaTime;

            collectiveVelocities[i] = Vector3.ClampMagnitude(
                collectiveVelocities[i],
                collectiveMaxSpeeds[i]
            );

            // Drag: prevents runaway speed but does not force a hard stop.
            collectiveVelocities[i] = Vector3.Lerp(
                collectiveVelocities[i],
                Vector3.zero,
                collectiveDrags[i] * Time.deltaTime
            );

            Vector3 pos = slot.transform.localPosition;
            pos += collectiveVelocities[i] * Time.deltaTime;

            pos = ResolveBrownianBounds(pos, ref collectiveVelocities[i]);
            pos = ResolveSoloAvoidance(pos, ref collectiveVelocities[i]);

            slot.transform.localPosition = pos;
        }
    }

    Vector3 ResolveBrownianBounds(Vector3 pos, ref Vector3 velocity)
    {
        if (pos.x < regionX.x || pos.x > regionX.y)
        {
            pos.x = Mathf.Clamp(pos.x, regionX.x, regionX.y);
            velocity.x *= -brownianBounce;
        }

        if (pos.y < regionY.x || pos.y > regionY.y)
        {
            pos.y = Mathf.Clamp(pos.y, regionY.x, regionY.y);
            velocity.y *= -brownianBounce;
        }

        return pos;
    }

    Vector3 ResolveSoloAvoidance(Vector3 pos, ref Vector3 velocity)
    {
        if (!collectiveAvoidSoloCorners) return pos;

        Vector3[] soloPositions = GetSoloCornerPositions();
        Vector2 p = new Vector2(pos.x, pos.y);

        Vector2 totalPush = Vector2.zero;

        for (int i = 0; i < soloPositions.Length; i++)
        {
            Vector2 s = new Vector2(soloPositions[i].x, soloPositions[i].y);
            float dist = Vector2.Distance(p, s);

            if (dist < soloCornerAvoidDistance)
            {
                Vector2 dir = p - s;

                if (dir.sqrMagnitude < 0.0001f)
                    dir = Random.insideUnitCircle;

                dir.Normalize();

                float strength = (soloCornerAvoidDistance - dist) / soloCornerAvoidDistance;
                totalPush += dir * strength * soloAvoidPushStrength;
            }
        }

        if (totalPush.sqrMagnitude > 0.0001f)
        {
            pos.x += totalPush.x * Time.deltaTime;
            pos.y += totalPush.y * Time.deltaTime;

            velocity.x += totalPush.x * Time.deltaTime;
            velocity.y += totalPush.y * Time.deltaTime;

            pos.x = Mathf.Clamp(pos.x, regionX.x, regionX.y);
            pos.y = Mathf.Clamp(pos.y, regionY.x, regionY.y);
        }

        return pos;
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
        int tries = Mathf.Max(maxPositionTries, maxSoloAvoidTries);

        for (int i = 0; i < tries; i++)
        {
            Vector3 candidate = GetRandomPositionInRegion(z);

            bool tooCloseToCollective = !allowOverlap && IsPositionTooClose(candidate, allowSameLayer);
            bool tooCloseToSolo = collectiveAvoidSoloCorners && IsTooCloseToSoloCorner(candidate);

            if (!tooCloseToCollective && !tooCloseToSolo)
                return candidate;
        }

        for (int i = 0; i < maxSoloAvoidTries; i++)
        {
            Vector3 candidate = GetRandomPositionInRegion(z);

            if (!IsTooCloseToSoloCorner(candidate))
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

        if (collectiveAvoidSoloCorners && IsTooCloseToSoloCorner(pos))
        {
            pos = PushAwayFromSoloCorners(pos);
            pos.x = Mathf.Clamp(pos.x, regionX.x, regionX.y);
            pos.y = Mathf.Clamp(pos.y, regionY.x, regionY.y);
            pos.z = z;
        }

        return pos;
    }

    bool IsTooCloseToSoloCorner(Vector3 candidate)
    {
        Vector3[] soloPositions = GetSoloCornerPositions();
        Vector2 c = new Vector2(candidate.x, candidate.y);

        for (int i = 0; i < soloPositions.Length; i++)
        {
            Vector2 s = new Vector2(soloPositions[i].x, soloPositions[i].y);

            if (Vector2.Distance(c, s) < soloCornerAvoidDistance)
                return true;
        }

        return false;
    }

    Vector3 PushAwayFromSoloCorners(Vector3 pos)
    {
        Vector3[] soloPositions = GetSoloCornerPositions();

        Vector2 p = new Vector2(pos.x, pos.y);
        Vector2 push = Vector2.zero;

        for (int i = 0; i < soloPositions.Length; i++)
        {
            Vector2 s = new Vector2(soloPositions[i].x, soloPositions[i].y);
            float dist = Vector2.Distance(p, s);

            if (dist < soloCornerAvoidDistance)
            {
                Vector2 dir = p - s;

                if (dir.sqrMagnitude < 0.0001f)
                    dir = Random.insideUnitCircle;

                dir.Normalize();

                float strength = soloCornerAvoidDistance - dist;
                push += dir * strength;
            }
        }

        p += push;

        pos.x = p.x;
        pos.y = p.y;

        return pos;
    }

    bool IsPositionTooClose(Vector3 candidate, bool allowSameLayer)
    {
        if (randomCollectiveSlots == null) return false;

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