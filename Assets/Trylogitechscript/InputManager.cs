using UnityEngine;
using Logitech;

public enum InputCondition
{
    Keyboard = 0,
    Mobile = 1,
    Driving = 2
}

/// <summary>
/// Reads either keyboard or Logitech wheel and exposes clean inputs to your car script.
/// Designed to prevent self-acceleration by applying solid deadzones.
/// </summary>
public class InputManager : MonoBehaviour
{
    [HideInInspector] public float gasInput;     // 0..1
    [HideInInspector] public float brakeInput;   // 0..1
    [HideInInspector] public float clutchInput;  // 0..1
    [HideInInspector] public float steerInput;   // -1..+1
    [HideInInspector] public bool reverseButtonPressed;

    public InputCondition inputCondition = InputCondition.Driving;

    // Tweakable deadzones on top of LogitechInput’s internal clamps
    [Header("Deadzones")]
    [Range(0f, 0.5f)] public float pedalDeadzone = 0.15f; // extra guard
    [Range(0f, 0.2f)] public float steerDeadzone = 0.03f; // extra guard

    // Reverse mapping (PS/Xbox "Cross/A" is typically button 2 on G923)
    [Header("Reverse")]
    [Tooltip("Wheel button index to toggle reverse (e.g., 2 = Cross/A).")]
    public int reverseButtonIndex = 2;

    [Tooltip("Require gas > deadzone while holding reverse button to engage reverse.")]
    public bool requireGasForReverse = true;

    void Update()
    {
        // Keep the Logitech SDK updated every frame
        LogitechInput.Poll();

        switch (inputCondition)
        {
            case InputCondition.Driving when LogitechInput.IsConnected():
                HandleDrivingWheelInput();
                break;

            default:
                HandleKeyboardInput();
                break;
        }
    }

    private void HandleKeyboardInput()
    {
        steerInput = Input.GetAxisRaw("Horizontal");
        gasInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
        brakeInput = Input.GetKey(KeyCode.S) ? 1f : 0f;
        clutchInput = 0f;

        reverseButtonPressed = Input.GetKey(KeyCode.R) && (!requireGasForReverse || gasInput > pedalDeadzone);

        // Clean small steer noise if any
        if (Mathf.Abs(steerInput) < steerDeadzone) steerInput = 0f;
    }

    private void HandleDrivingWheelInput()
    {
        float rawGas = LogitechInput.GetAxis("Gas Vertical");        // 0..1
        float rawBrake = LogitechInput.GetAxis("Brake Vertical");      // 0..1
        float rawCluth = LogitechInput.GetAxis("Clutch Vertical");     // 0..1
        float rawSteer = LogitechInput.GetAxis("Steering Horizontal"); // -1..1

        gasInput = (rawGas > pedalDeadzone) ? rawGas : 0f;
        brakeInput = (rawBrake > pedalDeadzone) ? rawBrake : 0f;
        clutchInput = (rawCluth > pedalDeadzone) ? rawCluth : 0f;
        steerInput = (Mathf.Abs(rawSteer) > steerDeadzone) ? rawSteer : 0f;

        bool reverseHeld = LogitechInput.GetButton(reverseButtonIndex);
        reverseButtonPressed = reverseHeld && (!requireGasForReverse || gasInput > pedalDeadzone);
    }
}
