using UnityEngine;

[DisallowMultipleComponent]
public class CarAudio : MonoBehaviour
{
    [Header("Engine layers ")]
    public AudioClip lowAccelClip, lowDecelClip, highAccelClip, highDecelClip;

    [Header("Optional SFX")]
    public AudioClip startClip, idleClip, reverseClip, brakeClip;

    [Header("Tuning")]
    [Range(0f, 1f)] public float idleVolume = 0.15f;
    public float accelRef = 6f;
    public float pitchMin = 0.9f, pitchMax = 1.6f;
    public float speedForMaxPitch = 40f;     // m/s for max pitch (set ≈ top speed / 3.6)
    public float reverseMinSpeed = 0.6f, reverseHysteresis = 0.3f;
    public float brakeMinSpeed = 1.0f, brakeCooldown = 0.35f;
    public float inputDeadzone = 0.13f;

    [Header("Equalizer (drag the one on the Camera/AudioListener)")]
    public SEF_Equalizer eq;                 // MUST be on the AudioListener
    public float eqStartOffset = 0.35f, eqLerpSpeed = 15f;

    [Header("References (set ONE)")]
    public Transform carRoot;
    public Rigidbody rb;

    // ---- internals ----
    const float fade = 8f, idleSpeed = 0.6f, engStart = 0.2f;
    float prevFwd, prevAbs, load01, lastBrake = -999f, prevBrake01;
    bool reverseActive;

    // created sources (2D, doppler 0)
    AudioSource sLowAcc, sLowDec, sHighAcc, sHighDec, sIdle, sRev, sStart, sBrake;

    // ----- lifecycle -----
    void Awake()
    {
        if (!rb && carRoot) rb = carRoot.GetComponent<Rigidbody>();
        if (!rb && transform.root) rb = transform.root.GetComponent<Rigidbody>();

        // Clean any leftover CA_* sources (prevents stacking across play sessions)
        CleanupOldSources();

        // Create sources (or replace if missing)
        sLowAcc = MakeLoop("CA_lowAcc", lowAccelClip);
        sLowDec = MakeLoop("CA_lowDec", lowDecelClip);
        sHighAcc = MakeLoop("CA_highAcc", highAccelClip);
        sHighDec = MakeLoop("CA_highDec", highDecelClip);

        sIdle = MakeLoop("CA_idle", idleClip);
        sRev = MakeLoop("CA_reverse", reverseClip);
        sStart = MakeShot("CA_start", startClip);
        sBrake = MakeShot("CA_brake", brakeClip);

        if (sStart && sStart.clip) sStart.Play();   // only Start at spawn

        if (!eq) eq = FindObjectOfType<SEF_Equalizer>(); // should be on the AudioListener
        if (eq) { eq.filterOn = true; eq.lowFreq = 1f; eq.midFreq = eqStartOffset; eq.highFreq = eqStartOffset; }

        Vector3 v = rb ? GetVel(rb) : Vector3.zero;
        prevFwd = Vector3.Dot(v, GetForward());
        prevAbs = Mathf.Abs(prevFwd);
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        // Inputs (keyboard + wheel via Input System if present)
        float throttle01 = ReadThrottle();
        float brake01 = ReadBrake();
        throttle01 = Deadzone(throttle01, inputDeadzone);
        brake01 = Deadzone(brake01, inputDeadzone);

        // Kinematics
        Vector3 fwdDir = GetForward();
        Vector3 vel = rb ? GetVel(rb) : Vector3.zero;
        float fwd = Vector3.Dot(vel, fwdDir);
        float spd = Mathf.Abs(fwd);
        float acc = (fwd - prevFwd) / dt; prevFwd = fwd;

        // Engine LOAD (accel vs decel)
        float physA01 = Mathf.Clamp01(acc / Mathf.Max(0.001f, accelRef));
        float targetLoad = Mathf.Max(physA01, throttle01);
        load01 += (targetLoad - load01) * (1f - Mathf.Exp(-dt * 8f));

        // Pitch proxy from speed
        float rpm01 = Mathf.Clamp01(spd / Mathf.Max(1f, speedForMaxPitch));
        float pitch = Mathf.Lerp(pitchMin, pitchMax, rpm01);

        // Reverse with hysteresis
        if (!reverseActive && fwd <= -reverseMinSpeed - reverseHysteresis) reverseActive = true;
        else if (reverseActive && fwd >= -reverseMinSpeed + reverseHysteresis) reverseActive = false;

        // Engine gate (no engine at spawn; mute while reversing)
        bool engineOn = (throttle01 > 0.05f) || (spd > engStart);
        bool muteEngine = !engineOn || reverseActive;

        // 4-layer blend
        SetPitch(sLowAcc, pitch);
        SetPitch(sLowDec, pitch);
        SetPitch(sHighAcc, pitch * (0.25f + 0.75f * rpm01));
        SetPitch(sHighDec, sHighAcc ? sHighAcc.pitch : pitch);

        float highFade = Ease(Mathf.InverseLerp(0.2f, 0.8f, rpm01));
        float lowFade = Ease(1f - Mathf.InverseLerp(0.2f, 0.8f, rpm01));
        float accFade = Ease(load01);
        float decFade = Ease(1f - load01);
        float baseVol = Mathf.Lerp(0.2f, 1f, rpm01);

        if (!muteEngine)
        {
            SetVol(sLowAcc, baseVol * lowFade * accFade, dt);
            SetVol(sHighAcc, baseVol * highFade * accFade, dt);
            SetVol(sLowDec, baseVol * lowFade * decFade, dt);
            SetVol(sHighDec, baseVol * highFade * decFade, dt);
        }
        else
        {
            SetVol(sLowAcc, 0f, dt); SetVol(sHighAcc, 0f, dt);
            SetVol(sLowDec, 0f, dt); SetVol(sHighDec, 0f, dt);
        }

        // Idle
        if (sIdle)
        {
            bool wantIdle = (throttle01 == 0f && brake01 == 0f && spd < idleSpeed && !reverseActive);
            sIdle.volume = Mathf.MoveTowards(sIdle.volume, wantIdle ? idleVolume : 0f, dt * fade);
        }

        // Reverse
        // Reverse (plays only if: moving backwards AND gas pressed AND Button 6 held)
        if (sRev)
        {

            // change to KeyCode.JoystickButton6 maybe
            bool reverseBtn = Input.GetKey(KeyCode.JoystickButton5) || Input.GetKey(KeyCode.R);

            // tiny gas threshold so micro noise doesn't trigger it
            bool gas = throttle01 > 0.06f;

            bool ok = reverseActive && reverseBtn && gas;
            float r01 = ok ? Mathf.Clamp01((Mathf.Abs(fwd) - reverseMinSpeed) / 10f) : 0f;

            sRev.volume = Mathf.MoveTowards(sRev.volume, Mathf.Lerp(0f, 0.8f, r01), dt * fade);
            sRev.pitch = Mathf.MoveTowards(sRev.pitch, Mathf.Lerp(0.9f, 1.3f, r01), dt * fade);
        }


        // Brake (edge only)
        bool edge = (brake01 > 0.6f && prevBrake01 <= 0.6f); prevBrake01 = brake01;
        bool canBrake = sBrake && sBrake.clip && spd > brakeMinSpeed &&
                        edge && (Time.time - lastBrake) > brakeCooldown;
        if (canBrake) { sBrake.PlayOneShot(sBrake.clip, 1f); lastBrake = Time.time; }

        // EQ follows LOAD
        if (eq)
        {
            float target = eqStartOffset + load01 / 1.5f;
            eq.midFreq = Mathf.Lerp(eq.midFreq, target, eqLerpSpeed * dt);
            eq.highFreq = Mathf.Lerp(eq.highFreq, target, eqLerpSpeed * dt);
            eq.lowFreq = 1f;
        }

        prevAbs = spd;
    }

    // ----- helpers -----
    void CleanupOldSources()
    {
        var all = GetComponents<AudioSource>();
        foreach (var s in all)
        {
            if (!s) continue;
            if (s.name.StartsWith("CA_")) DestroyImmediate(s); // nuke our old ones
        }
    }

    AudioSource MakeLoop(string name, AudioClip clip)
    {
        if (!clip) return null;
        var s = gameObject.AddComponent<AudioSource>();
        s.name = name; s.clip = clip; s.loop = true; s.playOnAwake = false;
        s.spatialBlend = 0f; s.dopplerLevel = 0f; s.volume = 0f;
        s.time = Random.Range(0f, Mathf.Max(0.01f, clip.length)); s.Play();
        return s;
    }
    AudioSource MakeShot(string name, AudioClip clip)
    {
        if (!clip) return null;
        var s = gameObject.AddComponent<AudioSource>();
        s.name = name; s.clip = clip; s.loop = false; s.playOnAwake = false;
        s.spatialBlend = 0f; s.dopplerLevel = 0f; if (clip) clip.LoadAudioData();
        return s;
    }

    static float Ease(float x) { return 1f - (1f - x) * (1f - x); }
    static void SetPitch(AudioSource s, float p) { if (s) s.pitch = p; }
    static void SetVol(AudioSource s, float v, float dt) { if (s) s.volume = Mathf.MoveTowards(s.volume, v, dt * fade); }
    static float Deadzone(float x, float dz) => (Mathf.Abs(x) < dz) ? 0f : Mathf.Clamp01(x);

    float ReadThrottle()
    {
        float t = 0f;
#if ENABLE_INPUT_SYSTEM
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null) t = Mathf.Max(t, Mathf.Clamp01(pad.rightTrigger.ReadValue()));
        foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
        {
            var a = d.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("accelerator");
            var th = d.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("throttle");
            if (a != null) t = Mathf.Max(t, Mathf.Clamp01(a.ReadValue()));
            if (th != null) t = Mathf.Max(t, Mathf.Clamp01(th.ReadValue()));
        }
#endif
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) t = 1f;
        return Mathf.Clamp01(t);
    }
    float ReadBrake()
    {
        float b = 0f;
#if ENABLE_INPUT_SYSTEM
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null) b = Mathf.Max(b, Mathf.Clamp01(pad.leftTrigger.ReadValue()));
        foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
        {
            var br = d.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("brake");
            if (br != null) b = Mathf.Max(b, Mathf.Clamp01(br.ReadValue()));
        }
#endif
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Space)) b = 1f;
        return Mathf.Clamp01(b);
    }

    Vector3 GetForward() { return carRoot ? carRoot.forward : (rb ? rb.transform.forward : transform.forward); }
    static Vector3 GetVel(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body ? body.linearVelocity : Vector3.zero;
#else
        return body ? body.velocity : Vector3.zero;
#endif
    }
}