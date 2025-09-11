using UnityEngine;

public class CarAudio : MonoBehaviour
{
    [Header("Assign clips")]
    public AudioSource startOneShot;   // one-shot
    public AudioSource idleLoop;       // loop
    public AudioSource engineLoop;     // loop
    public AudioSource reverseLoop;    // loop
    public AudioSource brakeOneShot;   // one-shot

    [Header("tuning")]
    [Range(0f,1f)] public float idleVolume   = 0.15f; // how loud idle is
    public float accelRef     = 6f;                   // m/s² that feels like “full gas”
    public float brakeGate    = 1.2f;                 // m/s² decel to auto fire brake SFX
    public float inputDeadzone= 0.14f;                // ignores tiny pedal jitter

    [Header("Physics")]
    public Rigidbody rb;                               // auto-found if on car root

    // ---- fixed “sane” constants (not shown in Inspector) ----
    const float idleSpeed   = 0.6f;   // m/s considered stopped
    const float engStart    = 0.2f;   // keeps engine alive when slightly rolling
    const float revMinSpeed = 0.6f;   // reverse audible only past this
    const float fade        = 8f;     // how fast volumes/pitches chase
    const float shotCDMin   = 0.35f;  // min cooldown for brake one-shot
    const float impactGate  = 4f;     // m/s relative vel = impact (mute brakes briefly)
    const float impactMute  = 0.5f;   // s to ignore brake after impact

    // ---- runtime ----
    float prevFwd, prevAbs, a01, lastBrakeShot=-999f, lastImpact=-999f, prevBrake01;
    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        PrepLoop(idleLoop); PrepLoop(engineLoop); PrepLoop(reverseLoop);
        PrepShot(startOneShot); PrepShot(brakeOneShot);
    }
    void Start()
    {
        if (startOneShot && startOneShot.clip) startOneShot.Play();
        PlayIfClip(idleLoop); PlayIfClip(engineLoop); PlayIfClip(reverseLoop);

        var v = rb ? rb.linearVelocity : Vector3.zero;
        prevFwd = Vector3.Dot(v, transform.forward);
        prevAbs = Mathf.Abs(prevFwd);
    }
    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        // --- Inputs: G923 via Input System (if present) + keyboard fallback ---
        float throttle01, brake01; ReadThrottleBrake(out throttle01, out brake01);
        throttle01 = Deadzone(throttle01, inputDeadzone);
        brake01    = Deadzone(brake01,    inputDeadzone);

        // --- Kinematics ---
        Vector3 vel = rb ? rb.linearVelocity : Vector3.zero;
        float fwd = Vector3.Dot(vel, transform.forward);
        float spd = Mathf.Abs(fwd);
        float acc = (fwd - prevFwd) / dt; prevFwd = fwd;

        // --- Smooth “engine demand”: physics accel OR throttle intent ---
        float physA01 = Mathf.Clamp01(acc / Mathf.Max(0.001f, accelRef));
        float target  = Mathf.Max(physA01, throttle01);
        a01 += (target - a01) * (1f - Mathf.Exp(-dt * 8f));

        // ---------- Idle ----------
        if (idleLoop)
        {
            bool wantIdle = (throttle01 == 0f && brake01 == 0f && spd < idleSpeed);
            float tv = wantIdle ? idleVolume : 0f;
            idleLoop.volume = Mathf.MoveTowards(idleLoop.volume, tv, dt * fade);
        }

        // ---------- Reverse (only when actually moving backward) ----------
        if (reverseLoop)
        {
            bool reversing = (fwd < -revMinSpeed);
            float r01 = reversing ? Mathf.Clamp01((Mathf.Abs(fwd) - revMinSpeed) / 10f) : 0f;
            reverseLoop.volume = Mathf.MoveTowards(reverseLoop.volume, Mathf.Lerp(0f, 0.8f, r01), dt * fade);
            reverseLoop.pitch  = Mathf.MoveTowards(reverseLoop.pitch,  Mathf.Lerp(0.9f, 1.3f,  r01), dt * fade);
        }

        // ---------- Brake one-shot (edge + big decel; no loop) ----------
        float decelAbs = Mathf.Max(0f, (prevAbs - spd) / dt); prevAbs = spd;
        bool brakeEdge = (brake01 > 0.6f && prevBrake01 <= 0.6f); prevBrake01 = brake01;
        bool notImpact = (Time.time - lastImpact) > impactMute;
        float shotCD   = (brakeOneShot && brakeOneShot.clip)
                         ? Mathf.Max(shotCDMin, brakeOneShot.clip.length * 0.85f)
                         : shotCDMin;

        bool canShot = brakeOneShot && brakeOneShot.clip && spd > 1.0f &&
                       (brakeEdge || (decelAbs > brakeGate && fwd > 0.2f && notImpact)) &&
                       (Time.time - lastBrakeShot) > shotCD;

        if (canShot) { brakeOneShot.Play(); lastBrakeShot = Time.time; }

        // ---------- Engine (forward only; reverse sound owns backward motion) ----------
        if (engineLoop)
        {
            bool reversingNow = (fwd < -revMinSpeed);
            bool on = !reversingNow && (a01 > 0.04f || spd > engStart);
            float v = on ? Mathf.Clamp01(0.8f * a01) : 0f;
            engineLoop.volume = Mathf.MoveTowards(engineLoop.volume, v, dt * fade);
            float p01 = Mathf.Clamp01(0.85f*a01 + 0.15f*(spd/40f));
            engineLoop.pitch = Mathf.MoveTowards(engineLoop.pitch, Mathf.Lerp(0.9f, 1.6f, p01), dt * fade);
        }
    }

    // ---- Impact mute so crashes don’t sound like “brake” ----
    void OnCollisionEnter(Collision c)
    {
        if (c.relativeVelocity.magnitude > impactGate) lastImpact = Time.time;
    }

    // ---- helpers ----
    void PrepLoop(AudioSource s){ if(!s)return; s.playOnAwake=false; s.loop=true;  s.spatialBlend=0f; s.dopplerLevel=0f; s.volume=0f; s.Stop(); }
    void PrepShot(AudioSource s){ if(!s)return; s.playOnAwake=false; s.loop=false; s.spatialBlend=0f; s.dopplerLevel=0f; s.Stop(); }
    void PlayIfClip(AudioSource s){ if(s && s.clip && !s.isPlaying) s.Play(); }
    static float Deadzone(float x, float dz) => (Mathf.Abs(x) < dz) ? 0f : Mathf.Clamp01(x);

    void ReadThrottleBrake(out float th, out float br)
    {
        th = 0f; br = 0f;
#if ENABLE_INPUT_SYSTEM
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null){ th = Mathf.Max(th, Mathf.Clamp01(pad.rightTrigger.ReadValue())); br = Mathf.Max(br, Mathf.Clamp01(pad.leftTrigger.ReadValue())); }
        foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
        {
            var a = d.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("accelerator");
            var t = d.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("throttle");
            var b = d.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("brake");
            if (a!=null) th = Mathf.Max(th, Mathf.Clamp01(a.ReadValue()));
            if (t!=null) th = Mathf.Max(th, Mathf.Clamp01(t.ReadValue()));
            if (b!=null) br = Mathf.Max(br, Mathf.Clamp01(b.ReadValue()));
        }
#endif
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) th = 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Space)) br = 1f;

        // If your G923 pedals are inverted (0 when pressed), flip here:
        // th = 1f - th; br = 1f - br;
        th = Mathf.Clamp01(th); br = Mathf.Clamp01(br);
    }
}
