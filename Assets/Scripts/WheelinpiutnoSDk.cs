using UnityEngine;
using UnityEngine.InputSystem;

public class InputManagerWheel : MonoBehaviour
{
    [Header("Input System Asset")]
    public InputActionAsset inputActions;

    private InputAction steering;
    private InputAction gas;
    private InputAction brake;
    private InputAction clutch;
    private InputAction reverseButton;

    // Same outputs as your old InputManager (CarControl expects these)
    [HideInInspector] public float gasInput;     // 0..1
    [HideInInspector] public float brakeInput;   // 0..1
    [HideInInspector] public float clutchInput;  // 0..1
    [HideInInspector] public float steerInput;   // -1..+1
    [HideInInspector] public bool reverseButtonPressed;

    [Header("Deadzones")]
    [Range(0f, 0.5f)] public float pedalDeadzone = 0.15f;
    [Range(0f, 0.2f)] public float steerDeadzone = 0.03f;

    [Header("Debug (Read-Only)")]
    [SerializeField] private float gasDisplay;
    [SerializeField] private float brakeDisplay;
    [SerializeField] private float clutchDisplay;
    [SerializeField] private float steerDisplay;
    [SerializeField] private bool reverseDisplay;

    void OnEnable()
    {
        // Look for the action map called "Driving" in your InputActionAsset
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
        // Read values directly from Input System
        float rawGas    = gas.ReadValue<float>();
        float rawBrake  = brake.ReadValue<float>();
        float rawClutch = clutch.ReadValue<float>();
        float rawSteer  = steering.ReadValue<float>();

        gasInput    = (rawGas   > pedalDeadzone) ? rawGas   : 0f;
        brakeInput  = (rawBrake > pedalDeadzone) ? rawBrake : 0f;
        clutchInput = (rawClutch > pedalDeadzone) ? rawClutch : 0f;
        steerInput  = (Mathf.Abs(rawSteer) > steerDeadzone) ? rawSteer : 0f;

        reverseButtonPressed = reverseButton.ReadValue<float>() > 0.5f;

        // Debug mirror for Inspector
        gasDisplay     = gasInput;
        brakeDisplay   = brakeInput;
        clutchDisplay  = clutchInput;
        steerDisplay   = steerInput;
        reverseDisplay = reverseButtonPressed;
    }
}
