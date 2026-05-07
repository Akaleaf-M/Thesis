using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum OutputMode
{
    Fragment,
    WaterfallA,
    WaterfallB,
    Full
}

[Serializable]
public class OutputModeProfile
{
    public OutputMode mode = OutputMode.Fragment;
    public int width = 1280;
    public int height = 800;
    public bool fullscreen = false;
    public bool applyResolution = true;
    public GameObject[] objectsToEnable;
    public GameObject[] objectsToDisable;
}

[DefaultExecutionOrder(-10000)]
public class OutputModeManager : MonoBehaviour
{
    [Header("Mode Selection")]
    public OutputMode defaultMode = OutputMode.Fragment;
    public bool logSelectedMode = true;

    [Header("Root Name Fallback")]
    public bool useRootNameFallback = true;
    public string[] waterfallRootNames = new string[] { "BackgroundRoot" };
    public string[] sharedRootNames = new string[] { "Main Camera", "Directional Light", "Global Volume", "OutputModeManager" };

    [Header("Profiles")]
    public OutputModeProfile[] profiles = new OutputModeProfile[]
    {
        new OutputModeProfile
        {
            mode = OutputMode.Fragment,
            width = 1280,
            height = 800
        },
        new OutputModeProfile
        {
            mode = OutputMode.WaterfallA,
            width = 1280,
            height = 800
        },
        new OutputModeProfile
        {
            mode = OutputMode.WaterfallB,
            width = 1024,
            height = 768
        },
        new OutputModeProfile
        {
            mode = OutputMode.Full,
            width = 1280,
            height = 800
        }
    };

    public OutputMode CurrentMode { get; private set; }

    void Awake()
    {
        Application.runInBackground = true;

        CurrentMode = GetRequestedMode(defaultMode);
        ApplyMode(CurrentMode);
    }

    void ApplyMode(OutputMode mode)
    {
        OutputModeProfile profile = FindProfile(mode);

        if (profile == null)
        {
            Debug.LogWarning($"[OutputModeManager] No profile found for mode {mode}. No output mode changes applied.");
            return;
        }

        if (useRootNameFallback)
            ApplyRootNameFallback(mode);

        SetObjectsActive(profile.objectsToDisable, false);
        SetObjectsActive(profile.objectsToEnable, true);

        if (profile.applyResolution && profile.width > 0 && profile.height > 0)
            Screen.SetResolution(profile.width, profile.height, profile.fullscreen);

        if (logSelectedMode)
        {
            Debug.Log(
                $"[OutputModeManager] mode={mode}, resolution={profile.width}x{profile.height}, fullscreen={profile.fullscreen}"
            );
        }
    }

    OutputModeProfile FindProfile(OutputMode mode)
    {
        if (profiles == null) return null;

        foreach (OutputModeProfile profile in profiles)
        {
            if (profile != null && profile.mode == mode)
                return profile;
        }

        return null;
    }

    void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;
            obj.SetActive(active);
        }
    }

    void ApplyRootNameFallback(OutputMode mode)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        HashSet<string> waterfallRoots = BuildNameSet(waterfallRootNames);
        HashSet<string> sharedRoots = BuildNameSet(sharedRootNames);

        if (mode == OutputMode.Fragment)
        {
            foreach (GameObject root in roots)
            {
                if (root != null && waterfallRoots.Contains(root.name))
                    root.SetActive(false);
            }

            return;
        }

        if (mode == OutputMode.Full)
        {
            foreach (GameObject root in roots)
            {
                if (root != null)
                    root.SetActive(true);
            }

            return;
        }

        foreach (GameObject root in roots)
        {
            if (root == null) continue;

            bool shouldStayActive =
                root == gameObject ||
                waterfallRoots.Contains(root.name) ||
                sharedRoots.Contains(root.name);

            root.SetActive(shouldStayActive);
        }
    }

    HashSet<string> BuildNameSet(string[] names)
    {
        HashSet<string> set = new HashSet<string>();

        if (names == null) return set;

        foreach (string name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
                set.Add(name.Trim());
        }

        return set;
    }

    OutputMode GetRequestedMode(OutputMode fallback)
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.IsNullOrEmpty(arg)) continue;

            if (arg.Equals("--mode", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-mode", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--output-mode", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && TryParseMode(args[i + 1], out OutputMode mode))
                    return mode;
            }

            if (TryReadInlineModeArg(arg, "--mode=", out OutputMode inlineMode) ||
                TryReadInlineModeArg(arg, "-mode=", out inlineMode) ||
                TryReadInlineModeArg(arg, "--output-mode=", out inlineMode))
            {
                return inlineMode;
            }
        }

        return fallback;
    }

    bool TryReadInlineModeArg(string arg, string prefix, out OutputMode mode)
    {
        mode = defaultMode;

        if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string value = arg.Substring(prefix.Length);
        return TryParseMode(value, out mode);
    }

    bool TryParseMode(string value, out OutputMode mode)
    {
        mode = defaultMode;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty);

        if (normalized.Equals("fragment", StringComparison.OrdinalIgnoreCase))
        {
            mode = OutputMode.Fragment;
            return true;
        }

        if (normalized.Equals("waterfalla", StringComparison.OrdinalIgnoreCase))
        {
            mode = OutputMode.WaterfallA;
            return true;
        }

        if (normalized.Equals("waterfallb", StringComparison.OrdinalIgnoreCase))
        {
            mode = OutputMode.WaterfallB;
            return true;
        }

        if (normalized.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            mode = OutputMode.Full;
            return true;
        }

        Debug.LogWarning($"[OutputModeManager] Unknown output mode '{value}'. Falling back to {defaultMode}.");
        return false;
    }
}
