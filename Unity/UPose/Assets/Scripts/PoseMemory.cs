using UnityEngine;

public class PoseMemory : MonoBehaviour, MotionTrackingPose
{
    [Header("Source")]
    public UPose server;

    private long previous_frame = 0;

    public int BufferSize = 101;

    private Quaternion[] Pelvis;
    private Quaternion[] Spine;
    private Quaternion[] RightShoulder;
    private Quaternion[] LeftShoulder;
    private Quaternion[] LeftForeArm;
    private Quaternion[] RightForeArm;
    private Quaternion[] RightUpLeg;
    private Quaternion[] LeftUpLeg;
    private Quaternion[] LeftLeg;
    private Quaternion[] RightLeg;

    private int i = 0;

    void Start()
    {
        if (server == null)
        {
            server = GetComponent<UPose>();
        }

        if (server == null)
        {
            server = FindFirstObjectByType<UPose>();
        }

        if (server == null)
        {
            Debug.LogError($"[{name}] You must assign a UPose server!");
            return;
        }

        Pelvis = new Quaternion[BufferSize];
        Spine = new Quaternion[BufferSize];
        RightShoulder = new Quaternion[BufferSize];
        LeftShoulder = new Quaternion[BufferSize];
        RightForeArm = new Quaternion[BufferSize];
        LeftForeArm = new Quaternion[BufferSize];
        RightUpLeg = new Quaternion[BufferSize];
        LeftUpLeg = new Quaternion[BufferSize];
        RightLeg = new Quaternion[BufferSize];
        LeftLeg = new Quaternion[BufferSize];

        for (int k = 0; k < BufferSize; k++)
        {
            Pelvis[k] = Quaternion.identity;
            Spine[k] = Quaternion.identity;
            RightShoulder[k] = Quaternion.identity;
            LeftShoulder[k] = Quaternion.identity;
            RightForeArm[k] = Quaternion.identity;
            LeftForeArm[k] = Quaternion.identity;
            RightUpLeg[k] = Quaternion.Euler(0, 0, 180);
            LeftUpLeg[k] = Quaternion.Euler(0, 0, 180);
            RightLeg[k] = Quaternion.identity;
            LeftLeg[k] = Quaternion.identity;
        }

        Debug.Log($"[{name}] PoseMemory connected to UPose on port {server.port}");
    }

    void Update()
    {
        if (server == null) return;

        long current_frame = server.getFrameCounter();
        if (current_frame <= previous_frame) return;

        Pelvis[i] = server.GetRotation(Landmark.PELVIS);
        Spine[i] = server.GetRotation(Landmark.SHOULDER_CENTER);
        RightShoulder[i] = server.GetRotation(Landmark.RIGHT_SHOULDER);
        LeftShoulder[i] = server.GetRotation(Landmark.LEFT_SHOULDER);
        LeftForeArm[i] = server.GetRotation(Landmark.LEFT_ELBOW);
        RightForeArm[i] = server.GetRotation(Landmark.RIGHT_ELBOW);
        RightUpLeg[i] = server.GetRotation(Landmark.RIGHT_HIP);
        LeftUpLeg[i] = server.GetRotation(Landmark.LEFT_HIP);
        LeftLeg[i] = server.GetRotation(Landmark.LEFT_KNEE);
        RightLeg[i] = server.GetRotation(Landmark.RIGHT_KNEE);

        i += 1;
        if (i >= BufferSize) i = 0;

        previous_frame = current_frame;
    }

    public Quaternion GetRotation(Landmark landmark, int back_in_time)
    {
        int j = i - 1 + back_in_time;
        while (j < 0) j += BufferSize;
        j = j % BufferSize;

        switch (landmark)
        {
            case Landmark.PELVIS:
                return Pelvis[j];
            case Landmark.SHOULDER_CENTER:
                return Spine[j];
            case Landmark.RIGHT_SHOULDER:
                return RightShoulder[j];
            case Landmark.LEFT_SHOULDER:
                return LeftShoulder[j];
            case Landmark.RIGHT_ELBOW:
                return RightForeArm[j];
            case Landmark.LEFT_ELBOW:
                return LeftForeArm[j];
            case Landmark.RIGHT_HIP:
                return RightUpLeg[j];
            case Landmark.LEFT_HIP:
                return LeftUpLeg[j];
            case Landmark.RIGHT_KNEE:
                return RightLeg[j];
            case Landmark.LEFT_KNEE:
                return LeftLeg[j];
            default:
                return Quaternion.identity;
        }
    }

    public Quaternion GetRotation(Landmark landmark)
    {
        return GetRotation(landmark, 0);
    }

    public long getFrameCounter()
    {
        return previous_frame;
    }
}