using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;


#endif

/// Attach to the *visual* steering wheel mesh (cockpit model).
/// Reads Logitech G923 via Input System (or auto-detect), and falls back to keyboard A/D or arrows.
/// Absolute rotation (no "beyblade"), smoothing, deadzone, invert, and curve shaping included.
[AddComponentMenu("Driving/Steering Wheel Visualizer")]
public class SteeringWheelVisualizer : MonoBehaviour
{
    [Header("Visual Wheel")]
    [Tooltip("Wheel rotation each side in degrees (try 180–270).")]
    public float maxVisualSteerAngle = 200f;
    [Tooltip("How fast the visual wheel chases the input.")]
    public float responsiveness = 8f;
    [Tooltip("Invert if your model spins the wrong direction.")]
    public bool invert = false;

    public enum Axis { X, Y, Z }
    [Tooltip("Local axis your steering wheel rotates around (usually Z).")]
    public Axis rotateAround = Axis.Z;

    [Header("Input — G923 + Keyboard")]
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Bind to <SteeringWheel>/steering (optional; leave empty to auto-detect).")]
    public InputActionReference steeringAction;
#endif
    [Range(0f, 0.2f), Tooltip("Wheel must move past this to override keyboard.")]
    public float wheelDeadzone = 0.03f;
    [Range(0f, 0.3f), Tooltip("Extra low-pass on the wheel value (0 = raw).")]
    public float smoothing = 0.06f;
    [Tooltip("How quickly A/D ramps to full lock (units per second).")]
    public float keyboardSteerSpeed = 3f;

    [Header("Shaping")]
    [Tooltip("Keeps the curve from overshooting. Leave ON.")]
    public bool clampCurveOutput = true;
    [Tooltip("Shape -1..+1 input → -1..+1 output. Keep within ±1 vertically!")]
    public AnimationCurve responseCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

    // internals
    Quaternion _initialLocalRotation;
    float _visualSteer; // smoothed -1..+1 for the mesh
    float _kbSteer;     // keyboard ramp
    float _smoothedWheel;

    void Start()
    {
        _initialLocalRotation = transform.localRotation;
#if ENABLE_INPUT_SYSTEM
        if (steeringAction != null && steeringAction.action != null && !steeringAction.action.enabled)
            steeringAction.action.Enable();
#endif
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (steeringAction != null && steeringAction.action != null && steeringAction.action.enabled)
            steeringAction.action.Disable();
#endif
    }

    void Update()
    {
        float wheel = ReadWheel();      // -1..+1 from G923 (or 0 if none)
        float keyboard = ReadKeyboard(); // -1..+1 from A/D or arrows

        // Prefer wheel when it moves; otherwise keep keyboard control
        float raw = Mathf.Abs(wheel) > wheelDeadzone ? wheel : keyboard;

        // Optional shaping
        float shaped = Mathf.Sign(raw) * Mathf.Abs(responseCurve.Evaluate(raw));
        if (clampCurveOutput) shaped = Mathf.Clamp(shaped, -1f, 1f);

        // Smooth visual motion (no overshoot)
        _visualSteer = Mathf.Lerp(_visualSteer, shaped, 1f - Mathf.Exp(-responsiveness * Time.deltaTime));

        // Apply absolute rotation relative to the original pose
        float angle = _visualSteer * maxVisualSteerAngle * (invert ? -1f : 1f);
        Vector3 axis = rotateAround == Axis.X ? Vector3.right :
                       rotateAround == Axis.Y ? Vector3.up :
                                                Vector3.forward;

        transform.localRotation = _initialLocalRotation * Quaternion.AngleAxis(angle, axis);
    }

    float ReadKeyboard()
    {
        float target = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) target -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) target += 1f;

        _kbSteer = Mathf.MoveTowards(_kbSteer, target, keyboardSteerSpeed * Time.deltaTime);
        return Mathf.Clamp(_kbSteer, -1f, 1f);
    }

    float ReadWheel()
    {
        float v = 0f;

#if ENABLE_INPUT_SYSTEM
        if (steeringAction != null && steeringAction.action != null)
        {
            v = steeringAction.action.ReadValue<float>();
        }
#endif

        v = Mathf.Clamp(v, -1f, 1f);

        // Extra low-pass smoothing on wheel
        if (smoothing > 0f)
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(1e-4f, smoothing));
            _smoothedWheel = Mathf.Lerp(_smoothedWheel, v, k);
            return _smoothedWheel;
        }
        return v;
    }
}