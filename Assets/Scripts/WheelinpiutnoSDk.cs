using UnityEngine;
using UnityEngine.InputSystem;

public class WheelInputReader : MonoBehaviour
{
    public InputActionAsset inputActions;
    private InputAction steering;
    private InputAction gas;
    private InputAction brake;
    private InputAction clutch;
    private InputAction reverseButton;

    [Header("Deadzones")]
    public float gasDeadzone = 0.2f;
    public float brakeDeadzone = 0.2f;
    public float clutchDeadzone = 0.2f;

    [Header("Live Outputs (Read Only)")]
    [SerializeField] private float steeringValue;
    [SerializeField] private float gasValue;
    [SerializeField] private float brakeValue;
    [SerializeField] private float clutchValue;
    [SerializeField] private bool isReversing;

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
        steering?.Disable();
        gas?.Disable();
        brake?.Disable();
        clutch?.Disable();
        reverseButton?.Disable();
    }

    void Update()
    {
        steeringValue = steering.ReadValue<float>();
        gasValue      = ApplyDeadzone(gas.ReadValue<float>(), gasDeadzone);
        brakeValue    = ApplyDeadzone(brake.ReadValue<float>(), brakeDeadzone);
        clutchValue   = ApplyDeadzone(clutch.ReadValue<float>(), clutchDeadzone);
        isReversing   = reverseButton.ReadValue<float>() > 0.5f;

        // 🔎 Print values each frame
        Debug.Log($"[Wheel Input] Steering={steeringValue:F2} | Gas={gasValue:F2} | Brake={brakeValue:F2} | Clutch={clutchValue:F2} | Reverse={isReversing}");
    }

    float ApplyDeadzone(float value, float threshold)
    {
        return Mathf.Abs(value) < threshold ? 0f : value;
    }
}
