using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class HandLandmarkVisualizer : MonoBehaviour
{
    [Header("UDP Input")]
    public string host = "0.0.0.0";
    public int port = 55010;
    public bool listenOnStart = true;
    public bool logPackets = false;

    [Header("Layout")]
    public WaterfallController waterfall;
    public float worldWidth = 12.8f;
    public float worldHeight = 8f;
    public float zOffset = -0.18f;
    public bool mirrorX = false;
    public bool hideWhenStale = true;
    public float staleAfterSeconds = 0.75f;

    [Header("Visibility")]
    public bool visible = true;
    public KeyCode toggleVisibilityKey = KeyCode.H;

    [Header("Style")]
    public float jointRadius = 0.035f;
    public float lineWidth = 0.018f;
    public float positionResponse = 28f;
    public Color jointColor = new Color(0f, 0.88f, 1f, 0.9f);
    public Color lineColor = new Color(1f, 1f, 1f, 0.42f);
    public Color pointColor = new Color(0.3f, 1f, 0.62f, 1f);
    public Color fistColor = new Color(1f, 1f, 1f, 0.82f);
    public float gestureColorResponse = 8f;

    private static readonly int[][] Connections = new int[][]
    {
        new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 },
        new[] { 0, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 8 },
        new[] { 0, 9 }, new[] { 9, 10 }, new[] { 10, 11 }, new[] { 11, 12 },
        new[] { 0, 13 }, new[] { 13, 14 }, new[] { 14, 15 }, new[] { 15, 16 },
        new[] { 0, 17 }, new[] { 17, 18 }, new[] { 18, 19 }, new[] { 19, 20 }
    };

    private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
    private UdpClient udpClient;
    private Thread listenThread;
    private volatile bool running;
    private readonly List<HandView> handViews = new List<HandView>();
    private HandFrame latestFrame = new HandFrame();
    private float lastPacketTime = -999f;
    private Material jointMaterial;
    private Material lineMaterial;
    private Color currentJointColor;

    private class HandFrame
    {
        public readonly List<Vector3[]> hands = new List<Vector3[]>();
        public float point;
        public float open;
        public float fist;
        public float swipe;
    }

    private class HandView
    {
        public Transform root;
        public Transform[] joints;
        public LineRenderer[] lines;
        public Vector3[] targetPositions;
        public bool initialized;
    }

    void Awake()
    {
        if (waterfall == null)
            waterfall = GetComponent<WaterfallController>();

        if (waterfall == null)
            waterfall = GetComponentInParent<WaterfallController>();

        if (waterfall == null)
            waterfall = FindFirstObjectByType<WaterfallController>();

        currentJointColor = jointColor;
        CreateMaterials();
    }

    void Start()
    {
        if (listenOnStart)
            StartListening();
    }

    void Update()
    {
        if (toggleVisibilityKey != KeyCode.None && Input.GetKeyDown(toggleVisibilityKey))
            SetVisible(!visible);

        DrainMessages();

        if (!visible)
        {
            SetAllHandsActive(false);
            return;
        }

        bool stale = Time.time - lastPacketTime > staleAfterSeconds;
        if (hideWhenStale && stale)
        {
            SetAllHandsActive(false);
            return;
        }

        ApplyFrame();
    }

    void OnDestroy()
    {
        StopListening();
    }

    public void StartListening()
    {
        if (running)
            return;

        try
        {
            IPAddress bindAddress = string.IsNullOrWhiteSpace(host) || host == "0.0.0.0"
                ? IPAddress.Any
                : IPAddress.Parse(host);
            udpClient = new UdpClient(new IPEndPoint(bindAddress, port));
            udpClient.Client.ReceiveTimeout = 1000;
        }
        catch (Exception e)
        {
            Debug.LogError($"[HandLandmarkVisualizer] Failed to bind UDP {host}:{port}: {e.Message}");
            udpClient = null;
            return;
        }

        running = true;
        listenThread = new Thread(ListenLoop) { IsBackground = true };
        listenThread.Start();
        Debug.Log($"[HandLandmarkVisualizer] Listening on UDP {host}:{port}");
    }

    public void StopListening()
    {
        running = false;

        try
        {
            udpClient?.Close();
            udpClient?.Dispose();
        }
        catch { }

        udpClient = null;

        try
        {
            if (listenThread != null && listenThread.IsAlive)
                listenThread.Join(200);
        }
        catch { }

        listenThread = null;
    }

    public void SetVisible(bool isVisible)
    {
        visible = isVisible;
        SetAllHandsActive(visible);
        Debug.Log($"[HandLandmarkVisualizer] Visibility {(visible ? "on" : "off")}");
    }

    private void ListenLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remote);
                if (data == null || data.Length == 0)
                    continue;

                messages.Enqueue(Encoding.UTF8.GetString(data));
            }
            catch (SocketException se)
            {
                if (!running)
                    break;

                if (se.SocketErrorCode == SocketError.TimedOut)
                    continue;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HandLandmarkVisualizer] ListenLoop exception: {e.Message}");
            }
        }
    }

    private void DrainMessages()
    {
        while (messages.TryDequeue(out string message))
        {
            if (TryParseFrame(message, out HandFrame frame))
            {
                latestFrame = frame;
                lastPacketTime = Time.time;

                if (logPackets)
                    Debug.Log($"[HandLandmarkVisualizer] hands={frame.hands.Count} point={frame.point:F2} fist={frame.fist:F2}");
            }
        }
    }

    private bool TryParseFrame(string message, out HandFrame frame)
    {
        frame = new HandFrame();

        if (string.IsNullOrWhiteSpace(message))
            return false;

        string[] lines = message.Replace("<EOM>", "").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "hland")
            return false;

        Dictionary<int, Vector3[]> handMap = new Dictionary<int, Vector3[]>();

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            string[] parts = line.Split('|');
            if (parts.Length == 3 && parts[0] == "gesture")
            {
                if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    continue;

                value = Mathf.Clamp01(value);
                switch (parts[1])
                {
                    case "point":
                        frame.point = value;
                        break;
                    case "open":
                        frame.open = value;
                        break;
                    case "fist":
                        frame.fist = value;
                        break;
                    case "swipe":
                        frame.swipe = value;
                        break;
                }
                continue;
            }

            if (parts.Length != 5)
                continue;

            if (!int.TryParse(parts[0], out int handIndex) || !int.TryParse(parts[1], out int pointIndex))
                continue;

            if (pointIndex < 0 || pointIndex >= 21)
                continue;

            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                continue;

            if (!handMap.TryGetValue(handIndex, out Vector3[] points))
            {
                points = new Vector3[21];
                handMap[handIndex] = points;
            }

            points[pointIndex] = new Vector3(x, y, z);
        }

        foreach (KeyValuePair<int, Vector3[]> entry in handMap)
            frame.hands.Add(entry.Value);

        return true;
    }

    private void ApplyFrame()
    {
        float width = waterfall != null ? waterfall.worldWidth : worldWidth;
        float height = waterfall != null ? waterfall.worldHeight : worldHeight;
        EnsureHandViewCount(latestFrame.hands.Count);

        float colorT = Mathf.Clamp01(Mathf.Max(latestFrame.point, latestFrame.fist * 0.65f));
        Color targetColor = Color.Lerp(jointColor, Color.Lerp(pointColor, fistColor, latestFrame.fist), colorT);
        float t = 1f - Mathf.Exp(-Mathf.Max(0.001f, gestureColorResponse) * Time.deltaTime);
        currentJointColor = Color.Lerp(currentJointColor, targetColor, t);

        for (int handIndex = 0; handIndex < handViews.Count; handIndex++)
        {
            HandView view = handViews[handIndex];
            bool active = handIndex < latestFrame.hands.Count;
            view.root.gameObject.SetActive(active);

            if (!active)
                continue;

            Vector3[] points = latestFrame.hands[handIndex];
            float positionT = 1f - Mathf.Exp(-Mathf.Max(0.001f, positionResponse) * Time.deltaTime);
            for (int i = 0; i < view.joints.Length; i++)
            {
                Vector3 point = points[i];
                float x = mirrorX ? 1f - point.x : point.x;
                float worldX = (x - 0.5f) * width;
                float worldY = (0.5f - point.y) * height;
                float worldZ = zOffset + Mathf.Clamp(point.z, -0.25f, 0.25f);
                view.targetPositions[i] = new Vector3(worldX, worldY, worldZ);

                if (!view.initialized)
                    view.joints[i].localPosition = view.targetPositions[i];
                else
                    view.joints[i].localPosition = Vector3.Lerp(view.joints[i].localPosition, view.targetPositions[i], positionT);

                view.joints[i].localScale = Vector3.one * jointRadius;
                Renderer renderer = view.joints[i].GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial.color = currentJointColor;
            }
            view.initialized = true;

            for (int i = 0; i < view.lines.Length; i++)
            {
                int a = Connections[i][0];
                int b = Connections[i][1];
                LineRenderer line = view.lines[i];
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.startColor = lineColor;
                line.endColor = lineColor;
                line.SetPosition(0, view.joints[a].localPosition);
                line.SetPosition(1, view.joints[b].localPosition);
            }
        }
    }

    private void EnsureHandViewCount(int count)
    {
        while (handViews.Count < count)
            handViews.Add(CreateHandView(handViews.Count));
    }

    private HandView CreateHandView(int index)
    {
        GameObject root = new GameObject($"HandLandmarkView_{index + 1}");
        root.transform.SetParent(transform, false);

        HandView view = new HandView
        {
            root = root.transform,
            joints = new Transform[21],
            lines = new LineRenderer[Connections.Length],
            targetPositions = new Vector3[21]
        };

        for (int i = 0; i < view.joints.Length; i++)
        {
            GameObject joint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            joint.name = $"Joint_{i:00}";
            joint.transform.SetParent(root.transform, false);
            Destroy(joint.GetComponent<Collider>());
            Renderer renderer = joint.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = jointMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            view.joints[i] = joint.transform;
        }

        for (int i = 0; i < view.lines.Length; i++)
        {
            GameObject lineObject = new GameObject($"Line_{Connections[i][0]:00}_{Connections[i][1]:00}");
            lineObject.transform.SetParent(root.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.material = lineMaterial;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            view.lines[i] = line;
        }

        return view;
    }

    private void CreateMaterials()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        jointMaterial = new Material(shader) { name = "MAT_HandLandmark_Joints" };
        lineMaterial = new Material(shader) { name = "MAT_HandLandmark_Lines" };
        jointMaterial.color = jointColor;
        lineMaterial.color = lineColor;
    }

    private void SetAllHandsActive(bool active)
    {
        foreach (HandView view in handViews)
        {
            if (view.root != null)
                view.root.gameObject.SetActive(active);
        }
    }
}
