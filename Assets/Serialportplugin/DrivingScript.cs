using UnityEngine;

public class NewCarcontrol : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider FrontLeftWheel;
    public WheelCollider FrontRightWheel;
    public WheelCollider RearLeftWheel;
    public WheelCollider RearRightWheel;

    [Header("Center of Mass")]
    public Transform CarcenterofMass;
    public Rigidbody rigidbody;

    [Header("Input Source")]
    public bool useInputSystem = true;                 // toggle in Inspector
    public InputManagerWheel inputSystemManager;       // new one (Input System)
    public InputManager sdkInputManager;               // old one (SDK/keyboard)

    [Header("Vehicle Settings")]
    public float motorForce = 1500f;
    public float brakeForce = 3000f;
    public float rollingResistance = 0f;
    public float reverseFactor = 0.6f;
    public float speedThreshold = 0.1f;

    [Header("Steering")]
    public float maxSteerAngle = 30f;
    public float steerReductionSpeed = 100f;

    [Header("Drive Layout")]
    public bool allWheelDrive = false;   // false = RWD, true = AWD

    [Header("Stability")]
    public float downforce = 50f;        // N per (m/s)

    void Start()
    {
        if (!rigidbody)
        {
            rigidbody = GetComponent<Rigidbody>();
            if (!rigidbody) { Debug.LogError("[Drive] Rigidbody not assigned."); enabled = false; return; }
        }
        if (CarcenterofMass) rigidbody.centerOfMass = CarcenterofMass.localPosition;

        // (optional but helps visuals)
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // WheelCollider substeps = smoother sim
        ConfigureSubsteps(FrontLeftWheel);
        ConfigureSubsteps(FrontRightWheel);
        ConfigureSubsteps(RearLeftWheel);
        ConfigureSubsteps(RearRightWheel);
    }

    void ConfigureSubsteps(WheelCollider wc)
    {
        if (!wc) return;
        wc.ConfigureVehicleSubsteps(5f, 12, 15);
    }

    void FixedUpdate()
    {
        // ----- read inputs -----
        float gas=0f, brake=0f, steer=0f; bool reverse=false;

        if (useInputSystem && inputSystemManager)
        {
            gas = Mathf.Clamp01(inputSystemManager.gasInput);
            brake = Mathf.Clamp01(inputSystemManager.brakeInput);
            steer = Mathf.Clamp(inputSystemManager.steerInput, -1f, 1f);
            reverse = inputSystemManager.reverseButtonPressed;   // HOLD to reverse
        }
        else if (sdkInputManager)
        {
            gas = Mathf.Clamp01(sdkInputManager.gasInput);
            brake = Mathf.Clamp01(sdkInputManager.brakeInput);
            steer = Mathf.Clamp(sdkInputManager.steerInput, -1f, 1f);
            reverse = sdkInputManager.reverseButtonPressed;      // HOLD to reverse
        }
        else { Debug.LogWarning("[Drive] No InputManager assigned!"); return; }

        // ----- steering with speed-based reduction -----
        float speed = rigidbody.linearVelocity.magnitude;          // m/s  (fixed from .linearVelocity)
        float speedKmh = speed * 3.6f;
        float steerScale = 1f / (1f + (speedKmh / Mathf.Max(1f, steerReductionSpeed)));
        float steerAngle = maxSteerAngle * steer * Mathf.Clamp01(steerScale * 2f);
        ApplySteering(steerAngle);

        // ----- motor/brake with HOLD-TO-REVERSE behavior -----
        float motorTorque = 0f;
        float brakeTorque = 0f;

        bool isAccelerating = gas   > 0.1f;
        bool isBraking      = brake > 0.1f;

        if (reverse)
        {
            if (isAccelerating) { motorTorque = -gas * motorForce * reverseFactor; brakeTorque = 0f; }
            else if (isBraking) { motorTorque = 0f; brakeTorque = brake * brakeForce; }
        }
        else
        {
            if (isAccelerating) { motorTorque =  gas * motorForce;                  brakeTorque = 0f; }
            else if (isBraking) { motorTorque = 0f;                                 brakeTorque = brake * brakeForce; }
        }

        // If trying to accelerate opposite to current motion, help with braking
        float forwardVel = Vector3.Dot(rigidbody.linearVelocity, transform.forward);
        if (!reverse && forwardVel < -0.5f && gas > 0.1f) brakeTorque = brakeForce;
        if ( reverse && forwardVel >  0.5f && gas > 0.1f) brakeTorque = brakeForce;

        ApplyDrive(motorTorque, brakeTorque);

        // rolling resistance (optional)
        if (rollingResistance > 0f)
        {
            float rrTorque = rollingResistance * speed; // proportional to speed
            ApplyExtraBrake(rrTorque);
        }

        // simple downforce
        if (downforce > 0f)
            rigidbody.AddForce(-transform.up * (downforce * speed), ForceMode.Force);
    }

    void ApplySteering(float steerAngle)
    {
        if (FrontLeftWheel)  FrontLeftWheel.steerAngle  = steerAngle;
        if (FrontRightWheel) FrontRightWheel.steerAngle = steerAngle;
    }

    void ApplyDrive(float perWheelMotor, float brakeTorque)
    {
        if (allWheelDrive)
        {
            if (FrontLeftWheel)  FrontLeftWheel.motorTorque  = perWheelMotor;
            if (FrontRightWheel) FrontRightWheel.motorTorque = perWheelMotor;
            if (RearLeftWheel)   RearLeftWheel.motorTorque   = perWheelMotor;
            if (RearRightWheel)  RearRightWheel.motorTorque  = perWheelMotor;
        }
        else
        {
            if (FrontLeftWheel)  FrontLeftWheel.motorTorque  = 0f;
            if (FrontRightWheel) FrontRightWheel.motorTorque = 0f;
            if (RearLeftWheel)   RearLeftWheel.motorTorque   = perWheelMotor;
            if (RearRightWheel)  RearRightWheel.motorTorque  = perWheelMotor;
        }

        if (FrontLeftWheel)  FrontLeftWheel.brakeTorque  = brakeTorque;
        if (FrontRightWheel) FrontRightWheel.brakeTorque = brakeTorque;
        if (RearLeftWheel)   RearLeftWheel.brakeTorque   = brakeTorque;
        if (RearRightWheel)  RearRightWheel.brakeTorque  = brakeTorque;
    }

    void ApplyExtraBrake(float extraBrakeTorque)
    {
        if (extraBrakeTorque <= 0f) return;
        if (FrontLeftWheel)  FrontLeftWheel.brakeTorque  += extraBrakeTorque;
        if (FrontRightWheel) FrontRightWheel.brakeTorque += extraBrakeTorque;
        if (RearLeftWheel)   RearLeftWheel.brakeTorque   += extraBrakeTorque;
        if (RearRightWheel)  RearRightWheel.brakeTorque  += extraBrakeTorque;
    }
}
