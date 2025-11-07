using UnityEngine;
using Logitech;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Minimal, robust wrapper around LogitechGSDK with sensible deadzones
/// so the car does NOT creep when pedals are at rest.
/// </summary>
public static class LogitechInput
{
    private static LogitechGSDK.DIJOYSTATE2ENGINES rec;
    private static bool initialized = false;
    private static bool triedInit = false;

    // Safety margins against pedal noise at rest
    private const float PedalZeroClamp = 0.12f; // anything under 12% -> 0
    private const float SteerDeadzone = 0.03f; // small steering noise

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(System.IntPtr hModule);

#if UNITY_EDITOR
    static LogitechInput()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }
#endif

    /// <summary>Call once lazily.</summary>
    private static void Initialize()
    {
        if (initialized || triedInit) return;
        triedInit = true;
        initialized = LogitechGSDK.LogiSteeringInitialize(false);
    }

#if UNITY_EDITOR
    private static void OnPlayModeStateChanged(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingPlayMode)
        {
            Shutdown();
        }
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReloadCleanup()
    {
        Shutdown();
    }

    public static void Shutdown()
    {
        if (!initialized) return;

        LogitechGSDK.LogiSteeringShutdown();
        initialized = false;

#if UNITY_EDITOR
        // Try to force-unload wrapper DLL to prevent “connected once only” issues in Editor
        var modules = System.Diagnostics.Process.GetCurrentProcess().Modules;
        foreach (System.Diagnostics.ProcessModule m in modules)
        {
            if (m.ModuleName.Contains("LogitechSteeringWheelEnginesWrapper"))
            {
                FreeLibrary(m.BaseAddress);
                break;
            }
        }
#endif
    }

    /// <summary>Must be called once per frame from your Update() to keep SDK state fresh.</summary>
    public static void Poll()
    {
        if (!initialized) return;
        LogitechGSDK.LogiUpdate(); // pull latest device state
    }

    public static bool IsConnected()
    {
        Initialize();
        if (!initialized) return false;
        return LogitechGSDK.LogiIsConnected((int)LogitechKeyCode.FirstIndex);
    }

    public static float GetAxis(string axisName)
    {
        if (!IsConnected()) return 0f;

        rec = LogitechGSDK.LogiGetStateUnity((int)LogitechKeyCode.FirstIndex);

        switch (axisName)
        {
            case "Steering Horizontal":
                {
                    // Range typically [-32768..+32767]
                    float v = Mathf.Clamp(rec.lX / 32767f, -1f, 1f);
                    // small deadzone so tiny offsets do not steer
                    if (Mathf.Abs(v) < SteerDeadzone) v = 0f;
                    return v;
                }

            case "Gas Vertical":
                {
                    float v = NormalizePedal(rec.lY);
                    return (v < PedalZeroClamp) ? 0f : v;
                }

            case "Brake Vertical":
                {
                    float v = NormalizePedal(rec.lRz);
                    return (v < PedalZeroClamp) ? 0f : v;
                }

            case "Clutch Vertical":
                {
                    // Some wheels map clutch to slider[1]; others to slider[0]
                    int raw = rec.rglSlider.Length > 1 ? rec.rglSlider[1] : rec.rglSlider[0];
                    float v = NormalizePedal(raw);
                    return (v < PedalZeroClamp) ? 0f : v;
                }
        }

        return 0f;
    }

    /// <summary>
    /// Convert raw pedal to [0..1], where 0 = at rest, 1 = fully pressed.
    /// Logitech reports ~32767 at rest and ~0 when pressed (often inverted).
    /// </summary>
    private static float NormalizePedal(int raw)
    {
        // Guard against negative readings that appear on some setups
        if (raw < 0) raw = -raw;

        // Map 32767 -> 0 and 0 -> 1
        float v = (32767f - raw) / 32767f;

        // Clamp and clean tiny noise
        if (v < 0f) v = 0f;
        if (v > 1f) v = 1f;

        return v;
    }

    public static bool GetButton(int buttonIndex)
    {
        if (!IsConnected()) return false;
        return LogitechGSDK.LogiButtonIsPressed((int)LogitechKeyCode.FirstIndex, buttonIndex);
    }

    public static bool GetKey(LogitechKeyCode keyCode)
    {
        if (!IsConnected()) return false;
        return LogitechGSDK.LogiButtonIsPressed((int)LogitechKeyCode.FirstIndex, (int)keyCode);
    }
}
