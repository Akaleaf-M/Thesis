using UnityEngine;

public class FragmentController : MonoBehaviour
{
    [Header("Fragment Slots")]
    public FragmentSlot[] slots;

    [Header("Spawn Timing")]
    public float minSpawnInterval = 1.5f;
    public float maxSpawnInterval = 2.5f;

    [Header("Bone Pool")]
    public string[] boneNames = new string[]
    {
        "mixamorig:Hips",
        "mixamorig:Spine",
        "mixamorig:Spine2",
        "mixamorig:Head",
        "mixamorig:LeftArm",
        "mixamorig:RightArm"
    };

    [Header("Screen Layout Anchors (local positions)")]
    public Vector3[] anchorPositions = new Vector3[]
    {
        new Vector3(-2.5f,  1.2f, 0f),
        new Vector3( 2.5f,  1.2f, 0f),
        new Vector3(-2.0f,  0.2f, 0f),
        new Vector3( 2.0f,  0.2f, 0f),
        new Vector3( 0.0f, -0.8f, 0f)
    };

    [Header("Drift Range")]
    public Vector2 driftOffsetX = new Vector2(-0.8f, 0.8f);
    public Vector2 driftOffsetY = new Vector2(-0.4f, 0.4f);

    private float spawnTimer = 0f;
    private float nextSpawnTime = 2f;

    void Start()
    {
        ScheduleNextSpawn();
    }

    void Update()
    {
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= nextSpawnTime)
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

    void TrySpawnFragment()
    {
        FragmentSlot freeSlot = GetFreeSlot();
        if (freeSlot == null) return;

        string bone = GetRandomBone();
        Vector3 startPos = GetRandomAnchor();
        Vector3 targetPos = startPos + new Vector3(
            Random.Range(driftOffsetX.x, driftOffsetX.y),
            Random.Range(driftOffsetY.x, driftOffsetY.y),
            0f
        );

        freeSlot.Activate(bone, startPos, targetPos);
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

    string GetRandomBone()
    {
        if (boneNames == null || boneNames.Length == 0)
            return "mixamorig:Spine2";

        return boneNames[Random.Range(0, boneNames.Length)];
    }

    Vector3 GetRandomAnchor()
    {
        if (anchorPositions == null || anchorPositions.Length == 0)
            return Vector3.zero;

        return anchorPositions[Random.Range(0, anchorPositions.Length)];
    }
}