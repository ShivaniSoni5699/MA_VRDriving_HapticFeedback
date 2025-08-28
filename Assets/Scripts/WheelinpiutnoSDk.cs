using UnityEngine;
using UnityEngine.InputSystem;

public class InputManagerWheel : MonoBehaviour
{
    [Header("Input System Asset")]
    public InputActionAsset inputActions;

    private InputAction steering, gas, brake, clutch, reverseButton;

    // Car controller expects these fields:
    [HideInInspector] public float gasInput;     // 0..1
    [HideInInspector] public float brakeInput;   // 0..1
    [HideInInspector] public float clutchInput;  // 0..1
    [HideInInspector] public float steerInput;   // -1..+1
    [HideInInspector] public bool reverseButtonPressed;

    [Header("Deadzones")]
    [Range(0f, 0.5f)] public float pedalDeadzone = 0.20f;
    [Range(0f, 0.2f)] public float steerDeadzone = 0.03f;

    [Header("Invert (use if a pedal reads ~1.0 at rest)")]
    public bool invertGas   = false;
    public bool invertBrake = false;
    public bool invertClutch= false;

    void OnEnable()
    {
        var map = inputActions.FindActionMap("Driving");
        steering      = map.FindAction("Steering");
        gas           = map.FindAction("Gas");
        brake         = map.FindAction("Brake");
        clutch        = map.FindAction("Clutch");
        reverseButton = map.FindAction("Reverse");
        map.Enable();
    }

    void OnDisable()
    {
        steering?.Disable(); gas?.Disable(); brake?.Disable(); clutch?.Disable(); reverseButton?.Disable();
    }

    void Update()
    {
        // Raw values may be in [-1..+1], [0..1], or reversed [1..0] depending on device
        float rawSteer  = steering.ReadValue<float>();
        float rawGas    = gas.ReadValue<float>();
        float rawBrake  = brake.ReadValue<float>();
        float rawClutch = clutch.ReadValue<float>();

        gasInput    = NormalizePedal(rawGas,   invertGas,   pedalDeadzone);   // -> 0..1
        brakeInput  = NormalizePedal(rawBrake, invertBrake, pedalDeadzone);   // -> 0..1
        clutchInput = NormalizePedal(rawClutch,invertClutch,pedalDeadzone);   // -> 0..1

        steerInput  = ApplySteerDeadzone(rawSteer, steerDeadzone);            // keep -1..+1

        reverseButtonPressed = reverseButton.ReadValue<float>() > 0.5f;
    }

    float NormalizePedal(float raw, bool invert, float dz)
    {
        // Map [-1..+1] to [0..1] if needed
        float v01 = (raw >= -1f && raw <= 1f) ? 0.5f * (raw + 1f) : Mathf.Clamp01(raw);
        if (invert) v01 = 1f - v01;

        // Deadzone at the low end
        if (v01 <= dz) return 0f;
        return (v01 - dz) / (1f - dz);
    }

    float ApplySteerDeadzone(float raw, float dz)
    {
        raw = Mathf.Clamp(raw, -1f, 1f);
        if (Mathf.Abs(raw) < dz) return 0f;
        // rescale so you still get full lock after the deadzone
        return Mathf.Sign(raw) * Mathf.InverseLerp(dz, 1f, Mathf.Abs(raw));
    }
}
