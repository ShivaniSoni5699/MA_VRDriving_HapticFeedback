using UnityEngine;
using System.IO.Ports;
using System.Globalization; // for invariant formatting of numbers


//class + inspector knobs (ports, limits, scaling)
public class CarControlHaptics : MonoBehaviour

{
    [Header("Vehicle (Car body)")]
    public Rigidbody carRb;

    [Header("Haptic devices (COM ports)")]
    public bool useFront = true; public string frontPort = "COM7";   // forward pull (braking)
    public bool useBack = true; public string backPort = "COM8";   // backward push (accel)
    public bool useLeft = true; public string leftPort = "COM9";   // left belt (turn right)
    public bool useRight = true; public string rightPort = "COM10";  // right belt (turn left)
    public int baudRate = 115200;

    //Commented header and also changed public variables to private 
    //[Header("Send rate limiting")]
    private readonly float maxSendHz = 60f;

    //[Header("AMG GT R reference (real-world)")]
    private float accelForFullBack = 8.0f;   // ~0.87 g full throttle
    private float decelForFullPull = 9.8f;   // ~1.00 g full brake
    private float latForFullPull = 11.0f;  // ~1.14 g max corner

    //[Header("Strength Caps")]
    private float frontMax = 2.0f; // max forward pull belt
    private float backMax = 0.6f; // max backward push motor
    private float sideMax = 1.1f; // max side belt pull


    [Header("Vibration")]
    public bool enableVibration = true;
    private float vibrationAmplitude = 0.3f; // 0..1.5 (firmware cap)
    private float vibrationFrequency = 100f; // 0..400 Hz (firmware cap)

    [Header("Damping (global)")]
    [Tooltip("Resistance against sudden changes. 0 = off. Try 0.15–0.35 if the feel is too snappy.")]
    [Range(0f, 1.0f)] public float damping = 0f;

    [Header("Response Curves (0..1 to 0..1)")]

    [Tooltip("Maps forward acceleration --> seat shove (back device)")]
    public AnimationCurve accelToBack = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Maps braking deceleration --> belt pull (front device)")]
    public AnimationCurve brakeToFront = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Maps lateral cornering force --> side belts")]
    public AnimationCurve lateralToSide = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Maps car speed --> vibration amplitude (engine/road rumble)")]
    public AnimationCurve speedToVibe = AnimationCurve.EaseInOut(0, 0, 1, 1);

    

    // --- private runtime ---
    private SerialPort spFront, spBack, spLeft, spRight; //open serial connections to each device
    private Vector3 prevVelocity; //car’s velocity from the last physics frame
    private float lastSendFront, lastSendBack, lastSendLeft, lastSendRight;

    //Start(): open ports + init
    void Start()
    {
        if (carRb == null)
        {
            Debug.LogError("[HAPTIC] Please assign carRb (Rigidbody).");
            enabled = false; return;
        }

        // remember current velocity so first frame accel isn’t huge
        prevVelocity = carRb.linearVelocity;

        // open any enabled device
        spFront = useFront ? OpenSerial(frontPort) : null;
        spBack = useBack ? OpenSerial(backPort) : null;
        spLeft = useLeft ? OpenSerial(leftPort) : null;
        spRight = useRight ? OpenSerial(rightPort) : null;

        // Send neutral (zero) to all devices
        string zero = FormatHaptic(0f, 0f, vibrationFrequency);
        SafeSend(spFront, zero);
        SafeSend(spBack, zero);
        SafeSend(spLeft, zero);
        SafeSend(spRight, zero);
    }

    void FixedUpdate()
    {
        // 1) Acceleration a = dv/dt
        float dt = (Time.fixedDeltaTime > 0f) ? Time.fixedDeltaTime : 0.02f;
        Vector3 vNow = carRb.linearVelocity;
        Vector3 aWorld = (vNow - prevVelocity) / dt;
        prevVelocity = vNow;

        // 2) Split into longitudinal (+accel, -brake) and lateral (+right, -left)
        float aLong = Vector3.Dot(aWorld, transform.forward);
        float aLat = Vector3.Dot(aWorld, transform.right);

        // 3) Normalize to AMG refs (0..1)
        float normAccel = Mathf.Clamp01(aLong / Mathf.Max(0.001f, accelForFullBack));
        float normDecel = Mathf.Clamp01(-aLong / Mathf.Max(0.001f, decelForFullPull));
        float normLat = Mathf.Clamp01(Mathf.Abs(aLat) / Mathf.Max(0.001f, latForFullPull));

        // 4) Perceptual shaping with AnimationCurves (still 0..1 after Evaluate)
        float accelOut = Mathf.Clamp01(accelToBack.Evaluate(normAccel));
        float brakeOut = Mathf.Clamp01(brakeToFront.Evaluate(normDecel));
        float lateralOut = Mathf.Clamp01(lateralToSide.Evaluate(normLat));

        // 5) Map to device strengths (apply hardware caps)
        float backStrength = backMax * accelOut;   // accel → back push
        float frontStrength = frontMax * brakeOut;   // brake → front belt
        float sideStrength = sideMax * lateralOut; // corner → side belts

        // pick left/right based on sign with a small deadband
        float leftStrength = (aLat > 0.25f) ? sideStrength : 0f;
        float rightStrength = (aLat < -0.25f) ? sideStrength : 0f;

        // 6) Vibration amplitude from speed via curve (0..1 → 0..1)
        float speedRatio = Mathf.Clamp01(vNow.magnitude / 80f); // ~0..288 km/h
        float ampOut = enableVibration ? Mathf.Clamp01(speedToVibe.Evaluate(speedRatio)) : 0f;
        float amplitude = vibrationAmplitude * ampOut;          // cap applied in FormatHaptic
        float frequency = vibrationFrequency;                   // keep simple & constant

        // 7) Send (rate-limited) with logs
        RateLimitedSend(spFront, ref lastSendFront, frontStrength, amplitude, frequency, "Front", aLong);
        RateLimitedSend(spBack, ref lastSendBack, backStrength, amplitude, frequency, "Back", aLong);
        RateLimitedSend(spLeft, ref lastSendLeft, leftStrength, amplitude, frequency, "Left", aLat);
        RateLimitedSend(spRight, ref lastSendRight, rightStrength, amplitude, frequency, "Right", aLat);
    }

    // Open a serial port; returns null if it fails (we keep running without it)
    SerialPort OpenSerial(string portName)
    {
        try
        {
            var sp = new SerialPort(portName, baudRate);
            sp.NewLine = "\r\n";
            sp.Open();
            Debug.Log("[HAPTIC] Opened " + portName);
            return sp;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[HAPTIC] Could not open " + portName + " (" + ex.Message + ")");
            return null;
        }
    }

    // MCU Firmware format: force;damping;amplitude;frequency;flag
    // We use a single global damping slider and keep flag = 0.
    string FormatHaptic(float force, float amplitude, float frequency)
    {
        force = Mathf.Clamp(force, 0f, 2f);
        float damp = Mathf.Clamp(damping, 0f, 1f);
        amplitude = Mathf.Clamp(amplitude, 0f, 1.5f);
        frequency = Mathf.Clamp(frequency, 0f, 400f);


        // forces decimal(.) instead of (,)
        return string.Format(CultureInfo.InvariantCulture,
            "{0:0.00};{1:0.00};{2:0.00};{3:0};0",
            force, damp, amplitude, Mathf.RoundToInt(frequency));
    }

    // Per-device rate-limited send + debug line
    void RateLimitedSend(SerialPort sp, ref float lastSendTime,
                         float force, float amplitude, float frequency,
                         string deviceName, float accelShown)
    {
        if (sp == null || !sp.IsOpen) return;

        float minInterval = 1f / Mathf.Max(1f, maxSendHz);
        if ((Time.time - lastSendTime) < minInterval) return;

        string cmd = FormatHaptic(force, amplitude, frequency);
        SafeSend(sp, cmd);
        lastSendTime = Time.time;

        Debug.Log($"[HAPTIC] {deviceName,-5} | a:{accelShown,6:0.00} m/s² | F:{force:0.00} A:{amplitude:0.00} f:{frequency:0}Hz d:{damping:0.00} | {cmd}");
    }

    // Safe write (ignore transient IO errors)
    void SafeSend(SerialPort sp, string cmd)
    {
        try { if (sp != null && sp.IsOpen) sp.WriteLine(cmd); } catch { }
    }

    // Zero and close ports on exit
    void OnApplicationQuit()
    {
        string zero = FormatHaptic(0f, 0f, vibrationFrequency);
        SafeSend(spFront, zero); SafeSend(spBack, zero);
        SafeSend(spLeft, zero); SafeSend(spRight, zero);

        try { if (spFront != null && spFront.IsOpen) spFront.Close(); } catch { }
        try { if (spBack != null && spBack.IsOpen) spBack.Close(); } catch { }
        try { if (spLeft != null && spLeft.IsOpen) spLeft.Close(); } catch { }
        try { if (spRight != null && spRight.IsOpen) spRight.Close(); } catch { }
    }
}

