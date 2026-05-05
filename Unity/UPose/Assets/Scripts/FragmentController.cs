using UnityEngine;

public enum CollectiveSlotScaleMode
{
    UseFragmentSlotScaleList,
    UniformRange
}

public class FragmentController : MonoBehaviour
{
    [Header("Fixed Solo Slots (P1-P4)")]
    public FragmentSlot[] fixedSoloSlots; // assign FragmentSlot_P1 ~ P4 in inspector

    [Header("Random Collective Slots")]
    public FragmentSlot[] randomCollectiveSlots; // assign the remaining collective slots

    [Header("Collective Slot Scale")]
    public bool randomizeCollectiveSlotScale = true;
    public CollectiveSlotScaleMode collectiveSlotScaleMode = CollectiveSlotScaleMode.UseFragmentSlotScaleList;
    public float collectiveScaleMin = 1f;
    public float collectiveScaleMax = 2f;
    public bool animateCollectiveSlotScale = false;
    public float collectiveScaleChangeIntervalMin = 2.5f;
    public float collectiveScaleChangeIntervalMax = 5.0f;
    public float collectiveScaleLerpSpeed = 1.6f;
    public bool collectiveUseAspectJitter = true;
    [Range(0f, 0.35f)] public float collectiveAspectJitter = 0.12f;
    public bool preserveCollectiveTextureAspectWithCrop = true;

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
    private Vector3[] fixedSoloScreenScales;

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
    private Vector3[] collectiveTargetScreenScales;
    private float[] collectiveScaleTimers;
    private float[] collectiveNextScaleChangeTimes;

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
        CacheFixedSoloScreenScales();
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

        UpdateCollectiveScaleAnimation();
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

    void CacheFixedSoloScreenScales()
    {
        int count = fixedSoloSlots != null ? fixedSoloSlots.Length : 0;
        fixedSoloScreenScales = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            FragmentSlot slot = fixedSoloSlots[i];
            fixedSoloScreenScales[i] = slot != null ? slot.GetCurrentScreenLocalScale() : Vector3.one;
        }
    }

    void InitCollectiveBrownianArrays()
    {
        int count = randomCollectiveSlots != null ? randomCollectiveSlots.Length : 0;

        collectiveVelocities = new Vector3[count];
        collectiveAccelerations = new float[count];
        collectiveMaxSpeeds = new float[count];
        collectiveDrags = new float[count];
        collectiveTargetScreenScales = new Vector3[count];
        collectiveScaleTimers = new float[count];
        collectiveNextScaleChangeTimes = new float[count];

        for (int i = 0; i < count; i++)
        {
            ResetCollectiveBrownianParams(i);
            ResetCollectiveScaleAnimation(i);
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
            slot.SetTextureAspectCropEnabled(false);

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
                slot.SetTextureAspectCropEnabled(false);

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
        slot.SetTextureAspectCropEnabled(false);
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
        profile.randomizeScreenScale = false;

        if (fixedSoloScreenScales != null && soloIdx >= 0 && soloIdx < fixedSoloScreenScales.Length)
        {
            profile.overrideScreenScale = true;
            profile.screenScale = fixedSoloScreenScales[soloIdx];
        }

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
        freeSlot.SetTextureAspectCropEnabled(preserveCollectiveTextureAspectWithCrop);

        ResetCollectiveBrownianParams(freeIndex);
        ResetCollectiveScaleAnimation(freeIndex);

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
        ApplyCollectiveScaleProfile(profile);

        ApplyShotProfile(profile);

        return profile;
    }

    void ApplyCollectiveScaleProfile(FragmentProfile profile)
    {
        if (profile == null) return;

        profile.randomizeScreenScale = randomizeCollectiveSlotScale;
        profile.overrideScreenScale = false;

        if (!randomizeCollectiveSlotScale) return;
        if (collectiveSlotScaleMode != CollectiveSlotScaleMode.UniformRange) return;

        float minScale = Mathf.Max(0.01f, Mathf.Min(collectiveScaleMin, collectiveScaleMax));
        float maxScale = Mathf.Max(minScale, Mathf.Max(collectiveScaleMin, collectiveScaleMax));
        float scale = Random.Range(minScale, maxScale);

        profile.overrideScreenScale = true;
        profile.screenScale = new Vector3(scale, scale, 1f);
    }

    void ResetCollectiveScaleAnimation(int idx)
    {
        if (collectiveTargetScreenScales == null) return;
        if (idx < 0 || idx >= collectiveTargetScreenScales.Length) return;

        FragmentSlot slot = GetCollectiveSlot(idx);
        Vector3 currentScale = slot != null ? slot.GetScreenScale() : Vector3.one;

        if (slot != null)
            slot.SetTextureAspectCropEnabled(preserveCollectiveTextureAspectWithCrop);

        collectiveTargetScreenScales[idx] = currentScale;
        collectiveScaleTimers[idx] = 0f;
        collectiveNextScaleChangeTimes[idx] = GetRandomScaleChangeInterval();

        if (animateCollectiveSlotScale)
            PickNewCollectiveTargetScale(idx);
    }

    void UpdateCollectiveScaleAnimation()
    {
        if (!animateCollectiveSlotScale) return;
        if (randomCollectiveSlots == null) return;
        if (collectiveTargetScreenScales == null || collectiveScaleTimers == null || collectiveNextScaleChangeTimes == null) return;

        for (int i = 0; i < randomCollectiveSlots.Length; i++)
        {
            FragmentSlot slot = GetCollectiveSlot(i);
            if (slot == null || !slot.IsActive()) continue;

            collectiveScaleTimers[i] += Time.deltaTime;

            if (collectiveScaleTimers[i] >= collectiveNextScaleChangeTimes[i])
            {
                PickNewCollectiveTargetScale(i);
                collectiveScaleTimers[i] = 0f;
                collectiveNextScaleChangeTimes[i] = GetRandomScaleChangeInterval();
            }

            Vector3 currentScale = slot.GetScreenScale();
            Vector3 targetScale = collectiveTargetScreenScales[i];
            Vector3 nextScale = Vector3.Lerp(currentScale, targetScale, Time.deltaTime * Mathf.Max(0.01f, collectiveScaleLerpSpeed));

            slot.SetScreenScale(nextScale);
        }
    }

    void PickNewCollectiveTargetScale(int idx)
    {
        if (collectiveTargetScreenScales == null) return;
        if (idx < 0 || idx >= collectiveTargetScreenScales.Length) return;

        collectiveTargetScreenScales[idx] = GenerateCollectiveTargetScale(idx);
    }

    Vector3 GenerateCollectiveTargetScale(int idx)
    {
        float uniformScale = Random.Range(GetSafeCollectiveScaleMin(), GetSafeCollectiveScaleMax());
        Vector3 target = new Vector3(uniformScale, uniformScale, 1f);

        if (collectiveSlotScaleMode == CollectiveSlotScaleMode.UseFragmentSlotScaleList)
        {
            FragmentSlot slot = GetCollectiveSlot(idx);
            target = PickScaleFromSlotList(slot);
        }

        if (collectiveUseAspectJitter && collectiveAspectJitter > 0f)
        {
            float jitter = Mathf.Clamp(collectiveAspectJitter, 0f, 0.35f);
            float aspectX = Random.Range(1f - jitter, 1f + jitter);
            float aspectY = Random.Range(1f - jitter, 1f + jitter);
            target.x *= aspectX;
            target.y *= aspectY;
        }

        target.x = Mathf.Max(0.01f, target.x);
        target.y = Mathf.Max(0.01f, target.y);
        target.z = Mathf.Max(0.01f, target.z);

        return target;
    }

    Vector3 PickScaleFromSlotList(FragmentSlot slot)
    {
        if (slot != null)
        {
            if (slot.normalScreenScales != null && slot.normalScreenScales.Length > 0)
                return slot.normalScreenScales[Random.Range(0, slot.normalScreenScales.Length)];
        }

        float uniformScale = Random.Range(GetSafeCollectiveScaleMin(), GetSafeCollectiveScaleMax());
        return new Vector3(uniformScale, uniformScale, 1f);
    }

    float GetRandomScaleChangeInterval()
    {
        float minInterval = Mathf.Max(0.1f, Mathf.Min(collectiveScaleChangeIntervalMin, collectiveScaleChangeIntervalMax));
        float maxInterval = Mathf.Max(minInterval, Mathf.Max(collectiveScaleChangeIntervalMin, collectiveScaleChangeIntervalMax));
        return Random.Range(minInterval, maxInterval);
    }

    float GetSafeCollectiveScaleMin()
    {
        return Mathf.Max(0.01f, Mathf.Min(collectiveScaleMin, collectiveScaleMax));
    }

    float GetSafeCollectiveScaleMax()
    {
        float minScale = GetSafeCollectiveScaleMin();
        return Mathf.Max(minScale, Mathf.Max(collectiveScaleMin, collectiveScaleMax));
    }

    FragmentSlot GetCollectiveSlot(int idx)
    {
        if (randomCollectiveSlots == null) return null;
        if (idx < 0 || idx >= randomCollectiveSlots.Length) return null;
        return randomCollectiveSlots[idx];
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
