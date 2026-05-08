using System.IO;
using UnityEngine;

public class WaterfallAudioReactiveController : MonoBehaviour
{
    public enum AudioInputMode
    {
        Microphone,
        AudioSource
    }

    [Header("Target")]
    public WaterfallController waterfall;
    public bool onlyAffectWaterfallB = true;

    [Header("Input")]
    public AudioInputMode inputMode = AudioInputMode.Microphone;
    public string microphoneDeviceName = "BlackHole 2ch";
    public bool fallbackToDefaultMicrophone = true;
    public AudioSource audioSource;
    public int microphoneSampleRate = 48000;
    public int microphoneLoopSeconds = 1;

    [Header("Analysis")]
    [Range(64, 4096)] public int sampleWindow = 1024;
    public float rmsFloor = 0.06f;
    public float rmsCeil = 0.07f;
    [Range(0f, 0.99f)] public float smoothing = 0f;
    [Range(0.01f, 0.99f)] public float peakDecay = 0.92f;

    [Header("Lane Padding")]
    public bool controlLanePadding = true;
    [Range(0f, 2f)] public float lanePaddingMin = 0.18f;
    [Range(0f, 2f)] public float lanePaddingMax = 1.35f;

    [Header("Intensity")]
    public bool controlIntensity = true;
    public float intensityMin = 0.62f;
    public float intensityMax = 1.22f;

    [Header("Pulse")]
    public bool triggerPulse = true;
    public float transientThreshold = 0.16f;
    public float transientCooldown = 0.09f;
    [Range(0f, 1f)] public float pulseAmount = 0.85f;

    [Header("Accent")]
    public bool triggerAccent = true;
    [Range(0f, 1f)] public float highBandStart = 0.48f;
    public float highRatioThreshold = 0.34f;
    [Range(0f, 1f)] public float accentAmount = 0.55f;

    [Header("Debug Readout")]
    [SerializeField] private float currentRms;
    [SerializeField] private float normalizedRms;
    [SerializeField] private float smoothedRms;
    [SerializeField] private float peakRms;
    [SerializeField] private float highRatio;
    [SerializeField] private float transientAmount;

    [Header("Saved Settings")]
    public bool loadSavedSettingsOnAwake = true;
    public string savedSettingsFileName = "WaterfallB_AudioReactiveSettings.json";

    private AudioClip microphoneClip;
    private string activeMicrophoneDevice;
    private float[] samples;
    private float[] microphoneWrapBuffer;
    private float[] spectrum;
    private float lastPulseTime = -999f;

    public float CurrentRms => currentRms;
    public float NormalizedRms => normalizedRms;
    public float SmoothedRms => smoothedRms;
    public float PeakRms => peakRms;
    public float HighRatio => highRatio;
    public float TransientAmount => transientAmount;
    public string ActiveMicrophoneDevice => activeMicrophoneDevice;

    void Awake()
    {
        if (waterfall == null)
            waterfall = GetComponent<WaterfallController>();

        if (waterfall == null)
            waterfall = GetComponentInChildren<WaterfallController>();

        AllocateBuffers();

        if (loadSavedSettingsOnAwake)
            LoadSavedSettings();
    }

    void OnEnable()
    {
        if (inputMode == AudioInputMode.Microphone)
            StartMicrophone();
    }

    void OnDisable()
    {
        StopMicrophone();
    }

    void OnValidate()
    {
        ClampSettings();
    }

    void ClampSettings()
    {
        sampleWindow = Mathf.ClosestPowerOfTwo(Mathf.Clamp(sampleWindow, 64, 4096));
        lanePaddingMin = Mathf.Clamp(lanePaddingMin, 0f, 2f);
        lanePaddingMax = Mathf.Clamp(lanePaddingMax, 0f, 2f);

        if (lanePaddingMax < lanePaddingMin)
            lanePaddingMax = lanePaddingMin;
    }

    [ContextMenu("Save Current Settings")]
    public void SaveCurrentSettings()
    {
        WaterfallAudioReactiveSettings settings = CaptureSettings();
        string json = JsonUtility.ToJson(settings, true);
        string path = GetSavedSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        Debug.Log($"[WaterfallAudioReactiveController] Saved settings to {path}");
    }

    [ContextMenu("Load Saved Settings")]
    public void LoadSavedSettings()
    {
        string path = GetSavedSettingsPath();
        if (!File.Exists(path))
            return;

        string json = File.ReadAllText(path);
        WaterfallAudioReactiveSettings settings = JsonUtility.FromJson<WaterfallAudioReactiveSettings>(json);
        ApplySettings(settings);
        ClampSettings();
        Debug.Log($"[WaterfallAudioReactiveController] Loaded settings from {path}");
    }

    string GetSavedSettingsPath()
    {
        return Path.Combine(Application.dataPath, "StreamingAssets", savedSettingsFileName);
    }

    WaterfallAudioReactiveSettings CaptureSettings()
    {
        return new WaterfallAudioReactiveSettings
        {
            onlyAffectWaterfallB = onlyAffectWaterfallB,
            inputMode = inputMode,
            microphoneDeviceName = microphoneDeviceName,
            fallbackToDefaultMicrophone = fallbackToDefaultMicrophone,
            microphoneSampleRate = microphoneSampleRate,
            microphoneLoopSeconds = microphoneLoopSeconds,
            sampleWindow = sampleWindow,
            rmsFloor = rmsFloor,
            rmsCeil = rmsCeil,
            smoothing = smoothing,
            peakDecay = peakDecay,
            controlLanePadding = controlLanePadding,
            lanePaddingMin = lanePaddingMin,
            lanePaddingMax = lanePaddingMax,
            controlIntensity = controlIntensity,
            intensityMin = intensityMin,
            intensityMax = intensityMax,
            triggerPulse = triggerPulse,
            transientThreshold = transientThreshold,
            transientCooldown = transientCooldown,
            pulseAmount = pulseAmount,
            triggerAccent = triggerAccent,
            highBandStart = highBandStart,
            highRatioThreshold = highRatioThreshold,
            accentAmount = accentAmount
        };
    }

    void ApplySettings(WaterfallAudioReactiveSettings settings)
    {
        if (settings == null) return;

        onlyAffectWaterfallB = settings.onlyAffectWaterfallB;
        inputMode = settings.inputMode;
        microphoneDeviceName = settings.microphoneDeviceName;
        fallbackToDefaultMicrophone = settings.fallbackToDefaultMicrophone;
        microphoneSampleRate = settings.microphoneSampleRate;
        microphoneLoopSeconds = settings.microphoneLoopSeconds;
        sampleWindow = settings.sampleWindow;
        rmsFloor = settings.rmsFloor;
        rmsCeil = settings.rmsCeil;
        smoothing = settings.smoothing;
        peakDecay = settings.peakDecay;
        controlLanePadding = settings.controlLanePadding;
        lanePaddingMin = settings.lanePaddingMin;
        lanePaddingMax = settings.lanePaddingMax;
        controlIntensity = settings.controlIntensity;
        intensityMin = settings.intensityMin;
        intensityMax = settings.intensityMax;
        triggerPulse = settings.triggerPulse;
        transientThreshold = settings.transientThreshold;
        transientCooldown = settings.transientCooldown;
        pulseAmount = settings.pulseAmount;
        triggerAccent = settings.triggerAccent;
        highBandStart = settings.highBandStart;
        highRatioThreshold = settings.highRatioThreshold;
        accentAmount = settings.accentAmount;
    }

    [System.Serializable]
    class WaterfallAudioReactiveSettings
    {
        public bool onlyAffectWaterfallB;
        public AudioInputMode inputMode;
        public string microphoneDeviceName;
        public bool fallbackToDefaultMicrophone;
        public int microphoneSampleRate;
        public int microphoneLoopSeconds;
        public int sampleWindow;
        public float rmsFloor;
        public float rmsCeil;
        public float smoothing;
        public float peakDecay;
        public bool controlLanePadding;
        public float lanePaddingMin;
        public float lanePaddingMax;
        public bool controlIntensity;
        public float intensityMin;
        public float intensityMax;
        public bool triggerPulse;
        public float transientThreshold;
        public float transientCooldown;
        public float pulseAmount;
        public bool triggerAccent;
        public float highBandStart;
        public float highRatioThreshold;
        public float accentAmount;
    }

    void Update()
    {
        if (waterfall == null)
            return;

        if (onlyAffectWaterfallB && waterfall.visualMode != WaterfallVisualMode.TestPatternHorizontal)
            return;

        AllocateBuffers();

        bool hasAudio = inputMode == AudioInputMode.Microphone
            ? ReadMicrophoneSamples()
            : ReadAudioSourceSamples();

        if (!hasAudio)
            return;

        AnalyzeAmplitude();
        AnalyzeSpectrum();
        ApplyToWaterfall();
    }

    void AllocateBuffers()
    {
        int size = Mathf.ClosestPowerOfTwo(Mathf.Clamp(sampleWindow, 64, 4096));
        if (samples == null || samples.Length != size)
            samples = new float[size];

        if (microphoneWrapBuffer == null || microphoneWrapBuffer.Length != size)
            microphoneWrapBuffer = new float[size];

        if (spectrum == null || spectrum.Length != size)
            spectrum = new float[size];
    }

    void StartMicrophone()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[WaterfallAudioReactiveController] No microphone/input devices found.");
            return;
        }

        activeMicrophoneDevice = ResolveMicrophoneDeviceName();
        if (string.IsNullOrEmpty(activeMicrophoneDevice))
            return;

        microphoneClip = Microphone.Start(
            activeMicrophoneDevice,
            true,
            Mathf.Max(1, microphoneLoopSeconds),
            Mathf.Max(8000, microphoneSampleRate)
        );

        Debug.Log($"[WaterfallAudioReactiveController] Listening to audio input: {activeMicrophoneDevice}");
    }

    void StopMicrophone()
    {
        if (!string.IsNullOrEmpty(activeMicrophoneDevice) && Microphone.IsRecording(activeMicrophoneDevice))
            Microphone.End(activeMicrophoneDevice);

        microphoneClip = null;
    }

    string ResolveMicrophoneDeviceName()
    {
        string requested = microphoneDeviceName != null ? microphoneDeviceName.Trim() : string.Empty;

        if (!string.IsNullOrEmpty(requested))
        {
            foreach (string device in Microphone.devices)
            {
                if (device == requested || device.ToLowerInvariant().Contains(requested.ToLowerInvariant()))
                    return device;
            }

            Debug.LogWarning($"[WaterfallAudioReactiveController] Requested input '{requested}' was not found.");
        }

        if (fallbackToDefaultMicrophone && Microphone.devices.Length > 0)
            return Microphone.devices[0];

        return null;
    }

    bool ReadMicrophoneSamples()
    {
        if (microphoneClip == null)
            return false;

        int position = Microphone.GetPosition(activeMicrophoneDevice);
        if (position <= 0)
            return false;

        int start = position - samples.Length;
        int clipSamples = microphoneClip.samples;

        if (start >= 0)
        {
            microphoneClip.GetData(samples, start);
            return true;
        }

        int firstCount = -start;
        int secondCount = samples.Length - firstCount;
        microphoneClip.GetData(microphoneWrapBuffer, clipSamples - firstCount);
        for (int i = 0; i < firstCount; i++)
            samples[i] = microphoneWrapBuffer[i];

        microphoneClip.GetData(microphoneWrapBuffer, 0);
        for (int i = 0; i < secondCount; i++)
            samples[firstCount + i] = microphoneWrapBuffer[i];

        return true;
    }

    bool ReadAudioSourceSamples()
    {
        if (audioSource == null || !audioSource.isPlaying)
            return false;

        audioSource.GetOutputData(samples, 0);
        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        return true;
    }

    void AnalyzeAmplitude()
    {
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        currentRms = Mathf.Sqrt(sum / Mathf.Max(1, samples.Length));
        normalizedRms = Mathf.InverseLerp(rmsFloor, Mathf.Max(rmsFloor + 0.0001f, rmsCeil), currentRms);
        normalizedRms = Mathf.Clamp01(normalizedRms);

        smoothedRms = Mathf.Lerp(normalizedRms, smoothedRms, smoothing);
        peakRms = Mathf.Max(normalizedRms, peakRms * peakDecay);
        transientAmount = Mathf.Clamp01(normalizedRms - smoothedRms);
    }

    void AnalyzeSpectrum()
    {
        if (inputMode == AudioInputMode.Microphone)
            ComputeSimpleSpectrum();

        int start = Mathf.Clamp(Mathf.RoundToInt(spectrum.Length * highBandStart), 0, spectrum.Length - 1);
        float total = 0f;
        float high = 0f;

        for (int i = 0; i < spectrum.Length; i++)
        {
            float value = Mathf.Abs(spectrum[i]);
            total += value;
            if (i >= start)
                high += value;
        }

        highRatio = total > 0.000001f ? Mathf.Clamp01(high / total) : 0f;
    }

    void ComputeSimpleSpectrum()
    {
        int half = spectrum.Length / 2;
        for (int i = 0; i < spectrum.Length; i++)
            spectrum[i] = 0f;

        for (int band = 0; band < half; band++)
        {
            int sampleIndex = Mathf.Clamp(band * samples.Length / half, 0, samples.Length - 1);
            int nextIndex = Mathf.Clamp(sampleIndex + 1, 0, samples.Length - 1);
            spectrum[band] = Mathf.Abs(samples[nextIndex] - samples[sampleIndex]);
        }
    }

    void ApplyToWaterfall()
    {
        if (controlLanePadding)
        {
            float lanePadding = Mathf.Lerp(lanePaddingMin, lanePaddingMax, smoothedRms);
            waterfall.SetLanePadding(lanePadding);
        }

        if (controlIntensity)
        {
            float intensity = Mathf.Lerp(intensityMin, intensityMax, smoothedRms);
            waterfall.SetIntensity(intensity);
        }

        if (triggerPulse && transientAmount >= transientThreshold && Time.time - lastPulseTime >= transientCooldown)
        {
            waterfall.TriggerPulse(pulseAmount);
            lastPulseTime = Time.time;
        }

        if (triggerAccent && highRatio >= highRatioThreshold)
            waterfall.TriggerAccent(accentAmount * highRatio);
    }
}
