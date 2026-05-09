using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public enum WaterfallPulseResponseTarget
{
    PulseOnly,
    Speed,
    Glitch,
    Accent,
    Density,
    Freeze
}

public class WaterfallBodyControlReceiver : MonoBehaviour
{
    [Header("UDP Input")]
    public string host = "0.0.0.0";
    public int port = 55000;
    public bool listenOnStart = true;
    public bool logPackets = false;

    [Header("Target")]
    public WaterfallController waterfall;
    public bool onlyAffectWaterfallA = true;

    [Header("Response")]
    public float responseSpeed = 4f;
    public float staleAfterSeconds = 1.25f;

    [Header("Speed / Density")]
    public Vector2 speedRange = new Vector2(0.35f, 2.25f);
    public Vector2 densityRange = new Vector2(0.65f, 1.45f);
    public Vector2 intensityRange = new Vector2(0.55f, 1.2f);

    [Header("Glitch / Accent")]
    public Vector2 glitchRange = new Vector2(0.02f, 0.2f);
    public Vector2 accentRange = new Vector2(0.005f, 0.14f);
    public float pulseTriggerThreshold = 0.5f;
    public float accentTriggerThreshold = 0.72f;
    public WaterfallPulseResponseTarget pulseResponseTarget = WaterfallPulseResponseTarget.Glitch;
    public float pulseBoostDuration = 0.22f;
    public float pulseSpeedBoost = 1.15f;
    public float pulseDensityBoost = 0.18f;
    public float pulseGlitchBoost = 0.35f;
    public float pulseAccentBoost = 0.75f;
    public float pulseFreezeBoost = 0.22f;

    [Header("WaterfallA Vertical")]
    public Vector2 upwardStreamProbabilityRange = new Vector2(0.18f, 0.62f);
    public Vector2 freezeProbabilityRange = new Vector2(0.08f, 0.34f);
    public Vector2 labelAlphaRange = new Vector2(0.34f, 0.78f);
    public float asymmetryRecomposeThreshold = 0.08f;
    public float asymmetryRecomposeGain = 1.8f;
    public float asymmetryRecomposeCooldown = 0.35f;
    public float asymmetryDeltaTrigger = 0.06f;

    private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
    private UdpClient udpClient;
    private Thread listenThread;
    private volatile bool running;

    private BodyControls targetControls = BodyControls.Default;
    private BodyControls smoothedControls = BodyControls.Default;
    private float lastPacketTime = -999f;
    private float previousPulse;
    private float previousAccentSource;
    private float previousAsymmetry;
    private float lastRecomposeTime = -999f;
    private float pulseBoostTimer;

    [Serializable]
    private struct BodyControls
    {
        public float energy;
        public float stillness;
        public float presence;
        public float pulse;
        public float asymmetry;
        public float height;
        public float upper;
        public float lower;

        public static BodyControls Default
        {
            get
            {
                return new BodyControls
                {
                    energy = 0f,
                    stillness = 1f,
                    presence = 0f,
                    pulse = 0f,
                    asymmetry = 0f,
                    height = 1f,
                    upper = 0f,
                    lower = 0f
                };
            }
        }

        public void Clamp()
        {
            energy = Mathf.Clamp01(energy);
            stillness = Mathf.Clamp01(stillness);
            presence = Mathf.Clamp01(presence);
            pulse = Mathf.Clamp01(pulse);
            asymmetry = Mathf.Clamp01(asymmetry);
            height = Mathf.Clamp01(height);
            upper = Mathf.Clamp01(upper);
            lower = Mathf.Clamp01(lower);
        }

        public static BodyControls Lerp(BodyControls a, BodyControls b, float t)
        {
            return new BodyControls
            {
                energy = Mathf.Lerp(a.energy, b.energy, t),
                stillness = Mathf.Lerp(a.stillness, b.stillness, t),
                presence = Mathf.Lerp(a.presence, b.presence, t),
                pulse = Mathf.Lerp(a.pulse, b.pulse, t),
                asymmetry = Mathf.Lerp(a.asymmetry, b.asymmetry, t),
                height = Mathf.Lerp(a.height, b.height, t),
                upper = Mathf.Lerp(a.upper, b.upper, t),
                lower = Mathf.Lerp(a.lower, b.lower, t)
            };
        }
    }

    void Awake()
    {
        if (waterfall == null)
            waterfall = GetComponent<WaterfallController>();

        if (waterfall == null)
            waterfall = GetComponentInChildren<WaterfallController>();

        if (waterfall == null)
            waterfall = FindFirstObjectByType<WaterfallController>();
    }

    void Start()
    {
        if (listenOnStart)
            StartListening();
    }

    void Update()
    {
        DrainMessages();

        if (Time.time - lastPacketTime > staleAfterSeconds)
            targetControls = BodyControls.Default;

        float t = 1f - Mathf.Exp(-Mathf.Max(0.001f, responseSpeed) * Time.deltaTime);
        smoothedControls = BodyControls.Lerp(smoothedControls, targetControls, t);
        smoothedControls.Clamp();

        ApplyToWaterfall();
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
            Debug.LogError($"[WaterfallBodyControlReceiver] Failed to bind UDP {host}:{port}: {e.Message}");
            udpClient = null;
            return;
        }

        running = true;
        listenThread = new Thread(ListenLoop) { IsBackground = true };
        listenThread.Start();
        Debug.Log($"[WaterfallBodyControlReceiver] Listening on UDP {host}:{port}");
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
                Debug.LogWarning($"[WaterfallBodyControlReceiver] ListenLoop exception: {e.Message}");
            }
        }
    }

    private void DrainMessages()
    {
        while (messages.TryDequeue(out string message))
        {
            if (TryParseMessage(message, out BodyControls controls))
            {
                targetControls = controls;
                lastPacketTime = Time.time;

                if (logPackets)
                    Debug.Log($"[WaterfallBodyControlReceiver] energy={controls.energy:F2} presence={controls.presence:F2} pulse={controls.pulse:F2}");
            }
        }
    }

    private bool TryParseMessage(string message, out BodyControls controls)
    {
        controls = targetControls;

        if (string.IsNullOrWhiteSpace(message))
            return false;

        string[] lines = message.Replace("<EOM>", "").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "wctrl")
            return false;

        BodyControls next = targetControls;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            string[] parts = line.Split('|');
            if (parts.Length != 2)
                continue;

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                continue;

            value = Mathf.Clamp01(value);

            switch (parts[0])
            {
                case "energy":
                    next.energy = value;
                    break;
                case "stillness":
                    next.stillness = value;
                    break;
                case "presence":
                    next.presence = value;
                    break;
                case "pulse":
                    next.pulse = value;
                    break;
                case "asymmetry":
                    next.asymmetry = value;
                    break;
                case "height":
                    next.height = value;
                    break;
                case "upper":
                    next.upper = value;
                    break;
                case "lower":
                    next.lower = value;
                    break;
            }
        }

        next.Clamp();
        controls = next;
        return true;
    }

    private void ApplyToWaterfall()
    {
        if (waterfall == null)
            return;

        if (onlyAffectWaterfallA && waterfall.visualMode != WaterfallVisualMode.DataWaterfallVertical)
            return;

        float presenceEnergy = Mathf.Clamp01(smoothedControls.presence * 0.65f + smoothedControls.energy * 0.35f);
        float speed = Mathf.Lerp(speedRange.x, speedRange.y, smoothedControls.energy);
        float density = Mathf.Lerp(densityRange.x, densityRange.y, presenceEnergy);
        float intensity = Mathf.Lerp(intensityRange.x, intensityRange.y, smoothedControls.presence);
        float glitch = Mathf.Lerp(glitchRange.x, glitchRange.y, Mathf.Max(smoothedControls.asymmetry, smoothedControls.pulse * 0.75f));
        float accent = Mathf.Lerp(accentRange.x, accentRange.y, Mathf.Clamp01(smoothedControls.asymmetry * 0.7f + smoothedControls.upper * 0.3f));
        float freeze = Mathf.Lerp(freezeProbabilityRange.x, freezeProbabilityRange.y, smoothedControls.stillness);

        bool pulseTriggered = smoothedControls.pulse >= pulseTriggerThreshold && previousPulse < pulseTriggerThreshold;
        if (pulseTriggered)
            pulseBoostTimer = Mathf.Max(pulseBoostTimer, pulseBoostDuration);

        float pulseBoost = Mathf.Clamp01(pulseBoostTimer / Mathf.Max(0.001f, pulseBoostDuration));

        pulseBoostTimer = Mathf.Max(0f, pulseBoostTimer - Time.deltaTime);

        ApplyPulseBoosts(ref speed, ref density, ref glitch, ref accent, ref freeze, pulseBoost);

        waterfall.SetMidiControlValues(
            smoothedControls.energy,
            smoothedControls.stillness,
            smoothedControls.presence,
            smoothedControls.pulse,
            smoothedControls.asymmetry,
            smoothedControls.height
        );

        waterfall.SetSpeedMultiplier(speed);
        waterfall.SetDensityMultiplier(density);
        waterfall.SetIntensity(intensity);
        waterfall.SetGlitchAmount(glitch);

        waterfall.accentProbability = Mathf.Clamp01(accent);
        waterfall.upwardStreamProbability = Mathf.Lerp(upwardStreamProbabilityRange.x, upwardStreamProbabilityRange.y, smoothedControls.asymmetry);
        waterfall.verticalFreezeProbability = Mathf.Clamp01(freeze);
        waterfall.verticalLabelAlpha = Mathf.Lerp(labelAlphaRange.x, labelAlphaRange.y, smoothedControls.presence);

        if (pulseTriggered)
            waterfall.TriggerPulse(smoothedControls.pulse);

        float accentSource = Mathf.Clamp01(smoothedControls.asymmetry * 0.7f + smoothedControls.energy * 0.3f);
        if (accentSource >= accentTriggerThreshold && previousAccentSource < accentTriggerThreshold)
            waterfall.TriggerAccent(accentSource);

        TryRecomposeFromAsymmetry();

        previousPulse = smoothedControls.pulse;
        previousAccentSource = accentSource;
        previousAsymmetry = smoothedControls.asymmetry;
    }

    private void ApplyPulseBoosts(ref float speed, ref float density, ref float glitch, ref float accent, ref float freeze, float pulseBoost)
    {
        if (pulseBoost <= 0f)
            return;

        switch (pulseResponseTarget)
        {
            case WaterfallPulseResponseTarget.Speed:
                speed += pulseSpeedBoost * pulseBoost;
                break;
            case WaterfallPulseResponseTarget.Glitch:
                glitch += pulseGlitchBoost * pulseBoost;
                break;
            case WaterfallPulseResponseTarget.Accent:
                accent += pulseAccentBoost * pulseBoost;
                break;
            case WaterfallPulseResponseTarget.Density:
                density += pulseDensityBoost * pulseBoost;
                break;
            case WaterfallPulseResponseTarget.Freeze:
                freeze += pulseFreezeBoost * pulseBoost;
                break;
        }

        density = Mathf.Max(0f, density);
        speed = Mathf.Max(0f, speed);
        glitch = Mathf.Clamp01(glitch);
        accent = Mathf.Clamp01(accent);
        freeze = Mathf.Clamp01(freeze);
    }

    private void TryRecomposeFromAsymmetry()
    {
        float asymmetry = smoothedControls.asymmetry;
        float delta = Mathf.Abs(asymmetry - previousAsymmetry);
        bool thresholdCrossed = asymmetry >= asymmetryRecomposeThreshold && previousAsymmetry < asymmetryRecomposeThreshold;
        bool changedEnough = asymmetry >= asymmetryRecomposeThreshold && delta >= asymmetryDeltaTrigger;

        if (!thresholdCrossed && !changedEnough)
            return;

        if (Time.time - lastRecomposeTime < asymmetryRecomposeCooldown)
            return;

        float amount = Mathf.Clamp01(asymmetry * asymmetryRecomposeGain);
        waterfall.RecomposeVerticalStreams(amount);
        lastRecomposeTime = Time.time;
    }
}
