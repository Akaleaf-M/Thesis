using UnityEngine;
using GLTFast;
using System;

public class ReadyPlayerAvatar : MonoBehaviour
{
    [Header("Motion Source")]
    [SerializeField] private MonoBehaviour serverComponent;
    private MotionTrackingPose server;

    public int Delay = 0;

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

    private void Start()
    {
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

        AVATAR_LOADED = true;
    }

    public bool isLoaded() { return AVATAR_LOADED; }

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

    public void MoveToFloor(float floorY)
    {
        if (LeftFoot == null || RightFoot == null) return;

        Vector3 pos = transform.position;
        float min = Mathf.Min(LeftFoot.position.y, RightFoot.position.y);
        transform.position = new Vector3(pos.x, pos.y + (floorY - min), pos.z);
    }

    private void Update()
    {
        if (!AVATAR_LOADED || server == null) return;
        if (Hips == null || Spine == null) return;
        if (RightArm == null || LeftArm == null || RightForeArm == null || LeftForeArm == null) return;
        if (RightUpLeg == null || LeftUpLeg == null || LeftLeg == null || RightLeg == null) return;

        Hips.localRotation = server.GetRotation(Landmark.PELVIS, Delay);
        Spine.localRotation = server.GetRotation(Landmark.SHOULDER_CENTER, Delay);
        RightArm.localRotation = Quaternion.Euler(0, 0, 90) * server.GetRotation(Landmark.RIGHT_SHOULDER, Delay);
        LeftArm.localRotation = Quaternion.Euler(0, 0, -90) * server.GetRotation(Landmark.LEFT_SHOULDER, Delay);
        LeftForeArm.localRotation = server.GetRotation(Landmark.LEFT_ELBOW, Delay);
        RightForeArm.localRotation = server.GetRotation(Landmark.RIGHT_ELBOW, Delay);
        RightUpLeg.localRotation = server.GetRotation(Landmark.RIGHT_HIP, Delay);
        LeftUpLeg.localRotation = server.GetRotation(Landmark.LEFT_HIP, Delay);
        LeftLeg.localRotation = server.GetRotation(Landmark.LEFT_KNEE, Delay);
        RightLeg.localRotation = server.GetRotation(Landmark.RIGHT_KNEE, Delay);

        if (moveToFloor) MoveToFloor(floorLevel);
    }
}