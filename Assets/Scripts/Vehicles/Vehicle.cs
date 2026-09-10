using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Vehicle : MonoBehaviour
{
    [Header("Wheels")]
    public Wheel[] Wheels;
    [Header("Steering")]
    public float BaseSteeringAngle;
    [Tooltip("Is used to decrease steering with speed")]
    public AnimationCurve SteeringCurve;
    [Header("Engine")]
    public float BaseEngineTorque;
    public AnimationCurve EngineCurve;
    public float MaxRPM;
    public float IdleRPM;
    public float EngineDrag;
    [Header("DriveTrain")] 
    public float finalDrive;
    public float[] GearRatios;
    public int CurrentGear=1;
    public float ReverseGearRatio;
    public float TransmissionEfficency;
    public float DecoupleSpeed = 10f;
    
    [Header("Automatic Gearbox")]
    public bool AutomaticGearbox;
    public float downshiftRPM = 4000f;
    public float upshiftRPM = 6900f;
    public float shiftDelay = 0.2f;
    private float shiftTime;
    
    [Header("Braking")]
    public float BrakeTorque;
    
    [Header("Weight Transfer")]
    public float ThrottleWeightShift = 800f;  // tune this
    public float BrakeWeightShift    = 1200f;
    [Header("Downforce")]
    public float DownforceCoefficient=2.5f;
    [Header("ESC")]
    public float ESCYawThreshold     = 10f;
    public float ESCDriftTolerance   = 10f;
    public float ESCBrakeStrength    = 1000f;

    [Header("Inputs")]
    public float steeringInput;
    public float throttleInput;
    public float brakeInput;
    public float handbrakeInput;
    public bool forcedBrake=false;
    
    
    [Header("EngineData")]
    public float engineRPM;
    public float engineTorque;
    
    public Rigidbody rb;
    private float speed;
    
    // ── Gizmo settings ──────────────────────────────────────────────────────
    [Header("Gizmos")]
    public bool ShowGizmos = true;
    [Tooltip("Global scale applied to all force arrows")]
    public float GizmoForceScale = 0.002f;
    [Tooltip("Scale for the ESC torque arc radius")]
    public float GizmoESCTorqueScale = 0.001f;
    public Color GizmoDownforceColor      = new Color(0.2f, 0.6f, 1.0f, 1f);
    public Color GizmoWeightTransferColor = new Color(1.0f, 0.6f, 0.1f, 1f);
    public Color GizmoVelocityColor       = new Color(0.1f, 1.0f, 0.3f, 1f);
    public Color GizmoESCColor            = new Color(1.0f, 0.2f, 0.8f, 1f);
    public Color GizmoEngineTorqueColor   = new Color(1.0f, 0.9f, 0.1f, 1f);
    public Color GizmoLateralSlideColor   = new Color(1.0f, 0.2f, 0.2f, 1f);
    // ────────────────────────────────────────────────────────────────────────
    // Cached values written each FixedUpdate so gizmos can read them
    private float _cachedDownforce;
    private Vector3 _cachedWeightTransferForce;
    private float _cachedESCCorrection;
    private float _cachedLateralVel;
    
    private float steeringAngle;
    private float finalTorque;
    private float brakeTorque;

    private int drivenWheelCount;
    private bool ESCActive;
    private bool NoDrivenContact = false;

    public void IncreaseGear()
    {
        shiftTime = Time.time;
        if (CurrentGear < GearRatios.Length)
            CurrentGear++;
    }
    
    public void DecreaseGear()
    {
        shiftTime = Time.time;
        if (CurrentGear > -1)
            CurrentGear--;
    }
    
    void Start()
    {
        
        rb = gameObject.GetComponent<Rigidbody>();
        drivenWheelCount = 0;
        foreach (Wheel wheel in Wheels)
        {
            if (wheel.Driven)
            {
                drivenWheelCount++;
            }
        }
    }

    void Update()
    {
        UpdateWheels(); 
    }
    
    void FixedUpdate()
    {
        speed = rb.linearVelocity.magnitude*3.6f;
        SimulateEngine();
        SimulateDrivetrain();
        SimulateESC();
        ApplyWeightTransfer();
        ApplyDownforce();
        
        brakeTorque = BrakeTorque * brakeInput;
        if((NoDrivenContact || speed < DecoupleSpeed )  && throttleInput == 0f)
            brakeTorque = BrakeTorque;
            
        steeringAngle = BaseSteeringAngle * steeringInput * SteeringCurve.Evaluate(rb.linearVelocity.magnitude / 80f);
        
        if(AutomaticGearbox && Time.time - shiftTime > shiftDelay)
            if(engineRPM < downshiftRPM && CurrentGear>1)
                    DecreaseGear();
            else if (engineRPM > upshiftRPM )
                IncreaseGear();
    }
    
    void ApplyDownforce()
    {
        float speed     = rb.linearVelocity.magnitude;
        float downforce = DownforceCoefficient * speed * speed;
        rb.AddForce(-transform.up * downforce, ForceMode.Force);
    }
    
    void ApplyWeightTransfer()
    {
        Vector3 force = transform.forward * 
                        ((brakeInput * BrakeWeightShift) - (throttleInput * ThrottleWeightShift));
        rb.AddForceAtPosition(force, rb.worldCenterOfMass + Vector3.up * 0.4f);
    }

    void UpdateWheels()
    {
        UpdateTCCountersteer();
        foreach (Wheel wheel in Wheels)
        {
            if (forcedBrake)
            {
                wheel.BrakeTorque = BrakeTorque;
                wheel.DrivenTorque = 0f;
                wheel.HandbrakeTorque = 0f;
                continue;
            }
            wheel.SteeringAngle = steeringAngle;
            wheel.BrakeTorque = brakeTorque;
            wheel.DrivenTorque = finalTorque/drivenWheelCount;
            wheel.HandbrakeTorque = wheel.HasHandbrake ? BrakeTorque * 2f * handbrakeInput : 0f;
        }
    }

    void CalculateEngineSpeed()
    {
        float avgWheelRPM = 0f;
        int count = 0;
        foreach (Wheel w in Wheels)
        {
            if (!w.Driven || !w.wheelCollider.isGrounded) continue;
            avgWheelRPM += Mathf.Abs(w.wheelCollider.rpm); // abs handles reverse
            count++;
        }
        
        NoDrivenContact = count == 0;
        
        if (CurrentGear == 0 || NoDrivenContact)
        {
            engineRPM = Mathf.MoveTowards(engineRPM, IdleRPM + throttleInput * MaxRPM, Time.fixedDeltaTime * 3000f);
            return;
        }
        if (count == 0) return;
        avgWheelRPM /= count;

        float gearRatio = CurrentGear == -1 ? -ReverseGearRatio : GearRatios[CurrentGear - 1];
        float wheelDrivenRPM = avgWheelRPM * gearRatio * finalDrive;
        
        engineRPM = Mathf.Lerp(engineRPM, Mathf.Max(IdleRPM, wheelDrivenRPM), Time.fixedDeltaTime * 8f);
    }

    void SimulateEngine()
    {
        CalculateEngineSpeed();
        engineRPM = Math.Clamp(engineRPM, IdleRPM, MaxRPM);
        
        float curvePoint = Mathf.InverseLerp(IdleRPM, MaxRPM, engineRPM);
        
        engineTorque = BaseEngineTorque * EngineCurve.Evaluate(curvePoint);
        engineTorque *= throttleInput;
        
        float engineBraking = EngineDrag * curvePoint * (1f - throttleInput);
        engineTorque -= engineBraking;
        
        if (engineRPM >= MaxRPM * 0.98f)
            engineTorque = Mathf.Min(engineTorque, 0f);
    }

    void SimulateDrivetrain()
    {
        if (CurrentGear == 0)
            return;
        finalTorque = engineTorque * (CurrentGear == -1 ? ReverseGearRatio : GearRatios[CurrentGear-1]) * finalDrive;
        finalTorque*=TransmissionEfficency;
        if((NoDrivenContact || speed < DecoupleSpeed ) && throttleInput == 0f) 
            finalTorque = 0f;
    }
    
    // In Vehicle.cs — pass this into each driven Wheel before SimulateTCS runs
    void UpdateTCCountersteer()
    {
        float lateralVel = transform.InverseTransformDirection(rb.linearVelocity).x;
        // If steering opposes the slide, driver is counter-steering — relax TC
        bool counterSteering = Mathf.Sign(steeringInput) != Mathf.Sign(lateralVel) 
                               && Mathf.Abs(lateralVel) > 2f;

        foreach (Wheel wheel in Wheels)
            wheel.TCCounterSteering = counterSteering;
    }
    
    void SimulateESC()
    {
        ESCActive = false;

        if (handbrakeInput > 0.1f) return;

        float actualYaw   = rb.angularVelocity.y;
        float intendedYaw = steeringInput * (rb.linearVelocity.magnitude / 10f) * 0.8f;
        float yawError    = actualYaw - intendedYaw;

        float threshold = ESCYawThreshold + ESCDriftTolerance * 0.5f;

        if (Mathf.Abs(yawError) < threshold) return;

        ESCActive = true;

        float correction = (yawError - Mathf.Sign(yawError) * threshold)
                           * ESCBrakeStrength;

        // Just push back against the unwanted rotation directly
        rb.AddTorque(Vector3.up * -correction, ForceMode.Force);
    }
    
    // ── Gizmos ──────────────────────────────────────────────────────────────
 
    void OnDrawGizmos()
    {
        if (!ShowGizmos || !Application.isPlaying) return;
        DrawVehicleGizmos();
    }
 
    void OnDrawGizmosSelected()
    {
        if (!ShowGizmos) return;
        DrawVehicleGizmos();
    }

    public void forceHoldStop()
    {
        forcedBrake = true;
        CurrentGear = 0;
        AutomaticGearbox = false;
    }

    public void startCar()
    {
        forcedBrake = false;
        CurrentGear = 1;
        AutomaticGearbox = true;
    }
 
    void DrawVehicleGizmos()
    {
        if (rb == null) return;
 
        Vector3 com = rb.worldCenterOfMass;
 
        // ── 1. Velocity vector ───────────────────────────────────────────
        Gizmos.color = GizmoVelocityColor;
        DrawArrow(com, rb.linearVelocity * GizmoForceScale * 5f);
        DrawLabel(com + rb.linearVelocity * GizmoForceScale * 5f,
                  $"vel {rb.linearVelocity.magnitude:F1} m/s", GizmoVelocityColor);
 
        // ── 2. Downforce (downward from CoM) ────────────────────────────
        if (_cachedDownforce > 0f)
        {
            Gizmos.color = GizmoDownforceColor;
            Vector3 dfVec = -transform.up * _cachedDownforce * GizmoForceScale;
            DrawArrow(com, dfVec);
            DrawLabel(com + dfVec, $"downforce {_cachedDownforce:F0} N", GizmoDownforceColor);
        }
 
        // ── 3. Weight-transfer force (applied above CoM) ─────────────────
        if (_cachedWeightTransferForce.sqrMagnitude > 0.01f)
        {
            Gizmos.color = GizmoWeightTransferColor;
            Vector3 wtBase  = com + Vector3.up * 0.4f;
            Vector3 wtArrow = _cachedWeightTransferForce * GizmoForceScale;
            DrawArrow(wtBase, wtArrow);
            DrawLabel(wtBase + wtArrow,
                      $"wt-transfer {_cachedWeightTransferForce.magnitude:F0} N",
                      GizmoWeightTransferColor);
        }
 
        // ── 4. Engine torque (arc around forward axis) ───────────────────
        if (Mathf.Abs(engineTorque) > 0.1f)
        {
            Gizmos.color = GizmoEngineTorqueColor;
            // Draw a flat circle on the XZ plane scaled to torque magnitude
            DrawTorqueArc(com, transform.up, engineTorque * GizmoForceScale * 0.3f, 1.5f);
            DrawLabel(com + transform.up * 1.8f,
                      $"eng torque {engineTorque:F0} Nm  RPM {engineRPM:F0}", GizmoEngineTorqueColor);
        }
 
        // ── 5. Final drivetrain torque arrow (forward / backward) ────────
        if (Mathf.Abs(finalTorque) > 0.1f)
        {
            Gizmos.color = GizmoEngineTorqueColor * new Color(1f, 1f, 1f, 0.6f);
            Vector3 ftArrow = transform.forward * Mathf.Sign(finalTorque) *
                              Mathf.Min(Mathf.Abs(finalTorque) * GizmoForceScale, 6f);
            DrawArrow(com, ftArrow);
            DrawLabel(com + ftArrow, $"drivetrain {finalTorque:F0} Nm", GizmoEngineTorqueColor);
        }
 
        // ── 6. Lateral slide / counter-steer indicator ──────────────────
        if (Mathf.Abs(_cachedLateralVel) > 0.5f)
        {
            Gizmos.color = GizmoLateralSlideColor;
            Vector3 latArrow = transform.right * _cachedLateralVel * GizmoForceScale * 5f;
            DrawArrow(com, latArrow);
            DrawLabel(com + latArrow, $"lat slide {_cachedLateralVel:F2} m/s", GizmoLateralSlideColor);
        }
 
        // ── 7. ESC correction torque ─────────────────────────────────────
        if (ESCActive)
        {
            Gizmos.color = GizmoESCColor;
            DrawTorqueArc(com, Vector3.up, -_cachedESCCorrection * GizmoESCTorqueScale, 2.2f);
            DrawLabel(com + Vector3.up * 2.5f,
                      $"ESC {_cachedESCCorrection:F0} Nm", GizmoESCColor);
        }
    }
 
    // ── Helper: arrow (line + cone tip) ─────────────────────────────────────
    static void DrawArrow(Vector3 origin, Vector3 vec)
    {
        if (vec.sqrMagnitude < 0.0001f) return;
        Gizmos.DrawLine(origin, origin + vec);
 
        // Cone tip
        Vector3 tip  = origin + vec;
        float   size = vec.magnitude * 0.15f;
        Vector3 perp = Vector3.Cross(vec.normalized, Vector3.up).normalized;
        if (perp.sqrMagnitude < 0.001f) perp = Vector3.right;
        Gizmos.DrawLine(tip, tip - vec.normalized * size + perp * size * 0.5f);
        Gizmos.DrawLine(tip, tip - vec.normalized * size - perp * size * 0.5f);
    }
 
    // ── Helper: torque arc (curved arrow in a plane) ─────────────────────────
    static void DrawTorqueArc(Vector3 centre, Vector3 axis, float torqueScaled, float radius)
    {
        int   segments  = 24;
        float arcDeg    = Mathf.Clamp(Mathf.Abs(torqueScaled) * 30f, 20f, 270f);
        float dir       = Mathf.Sign(torqueScaled);
        Vector3 perp    = Vector3.Cross(axis, Vector3.forward).normalized;
        if (perp.sqrMagnitude < 0.001f) perp = Vector3.right;
 
        Vector3 prev = centre + Quaternion.AngleAxis(0f, axis) * perp * radius;
        for (int i = 1; i <= segments; i++)
        {
            float   angle = dir * arcDeg * i / segments;
            Vector3 cur   = centre + Quaternion.AngleAxis(angle, axis) * perp * radius;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
        // Arrow head at arc end
        Vector3 tangent = Quaternion.AngleAxis(dir * arcDeg, axis) * 
                          Quaternion.AngleAxis(dir * 10f, axis) * perp;
        Gizmos.DrawLine(prev, prev + tangent.normalized * 0.4f);
    }
 
    // ── Helper: coloured label using Handles (editor only) ──────────────────
    static void DrawLabel(Vector3 pos, string text, Color col)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = col;
        UnityEditor.Handles.Label(pos, text);
#endif
    }
}
