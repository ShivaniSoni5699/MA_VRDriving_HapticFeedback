using UnityEngine;

/// Attach to the *visual* steering wheel mesh
public class SteeringWheelAnimator : MonoBehaviour
{
    [Header("Input (pick one)")]
    public InputManager inputManager;   // optional; uses inputManager.steerInput (-1..+1)

    [Header("Visual Settings")]
    [Tooltip("Wheel rotation each side in degrees. Try 180–270 first.")]
    public float maxVisualSteerAngle = 200f;
    [Tooltip("How fast the visual wheel chases the input.")]
    public float responsiveness = 8f;
    [Tooltip("Invert if your model spins the wrong direction.")]
    public bool invert = false;

    public enum Axis { X, Y, Z }
    [Tooltip("Local axis your steering wheel rotates around (usually Z).")]
    public Axis rotateAround = Axis.Z;

    [Header("Shaping")]
    [Tooltip("Keeps the curve from overshooting. Leave ON.")]
    public bool clampCurveOutput = true;
    [Tooltip("Shape -1..+1 input → -1..+1 output. Keep within ±1 vertically!")]
    public AnimationCurve responseCurve = AnimationCurve.Linear(-1f, -1f, 1f, 1f);

    [Header("Keyboard feel (when no InputManager)")]
    [Tooltip("How quickly A/D ramps to full lock (units per second).")]
    public float keyboardSteerSpeed = 3f;

    Quaternion _initialLocalRotation;
    float _visualSteer; // smoothed -1..+1
    float _kbSteer;     // ramped keyboard steer

    void Start()
    {
        _initialLocalRotation = transform.localRotation;
    }

    void Update()
    {
        float raw = GetSteerInput(); // -1..+1 (or ramped for keyboard)
        float shaped = Mathf.Sign(raw) * Mathf.Abs(responseCurve.Evaluate(raw));
        if (clampCurveOutput) shaped = Mathf.Clamp(shaped, -1f, 1f); // <-- anti-beyblade

        // Smooth toward target without overshoot
        _visualSteer = Mathf.Lerp(_visualSteer, shaped, 1f - Mathf.Exp(-responsiveness * Time.deltaTime));

        // Apply rotation (absolute, not additive → no endless spin)
        float angle = _visualSteer * maxVisualSteerAngle * (invert ? -1f : 1f);
        Vector3 axis = rotateAround == Axis.X ? Vector3.right :
                       rotateAround == Axis.Y ? Vector3.up    :
                                                Vector3.forward;

        transform.localRotation = _initialLocalRotation * Quaternion.AngleAxis(angle, axis);
    }

    float GetSteerInput()
    {
        if (inputManager != null)
        {
            // Make sure your InputManager outputs in [-1, +1]
            return Mathf.Clamp(inputManager.steerInput, -1f, 1f);
        }

        // Keyboard fallback with gentle ramp instead of instant full lock
        float target = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  target -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) target += 1f;

        _kbSteer = Mathf.MoveTowards(_kbSteer, target, keyboardSteerSpeed * Time.deltaTime);
        return Mathf.Clamp(_kbSteer, -1f, 1f);
    }
}
