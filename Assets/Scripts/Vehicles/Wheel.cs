using System;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

[RequireComponent(typeof(WheelCollider))]
public class Wheel : MonoBehaviour
{
    [Header("Power Delivery")]
    public bool Driven;
    public bool Reversed = false;
    public float PowerFactor = 1.0f;

    [Header("Steering")]
    public bool Steerable;
    public bool InversedSteering = false;
    public float SteeringFactor = 1.0f;

    [Header("Braking")]
    public bool Brakeable;
    public float BrakeFactor = 1.0f;
    public bool HasHandbrake = false;

    [Header("ABS")]
    public bool ABSEnabled = true;
    public float ABSSlipThreshold = 0.2f;
    public float ABSReleaseSpeed = 10f;
    public float ABSReapplySpeed = 8f;   // was 3 — needs to be fast to maintain avg force
    [Range(0f, 1f)]
    public float ABSModulatorFloor = 0.6f; // never release below this — key change
    
    [Header("Traction Control")]
    public bool TCEnabled = true;
    [Range(0f, 1f)]
    public float TCSlipThreshold = 0.35f;   // higher = more wheelspin allowed before cutting
    [Range(0f, 1f)]
    public float TCMinTorque = 0.4f;        // never cut below this — keeps drift momentum alive
    public float TCReleaseSpeed = 12f;      // how fast it cuts
    public float TCReapplySpeed = 5f;       // how fast it gives torque back

    private float tcModulator = 1f;
    public bool TCActive { get; private set; }

    [Header("Wheel Model")]
    public GameObject WheelModel;
    public Quaternion Offset;
    public Vector3 PositionOffset;

    [Header("Visual Effects")] 
    public float SmokeThreshold=0.5f;
    public GameObject effectsHolder;
    public int maxEmissionRate = 200;
    [Header("Sounds")] public float SlipSoundThreshold = 0.5f;
    public AudioClip slipSound;
    public AudioSource slipSoundSource;
    public AudioMixerGroup audioMixerGroup;
    public float slipSoundVolumeFactor = 0.5f;
    public float slipSoundMinPitch;
    public float slipSoundMaxPitch;

    public WheelCollider wheelCollider;
    public Rigidbody carRB;
    public float lowSpeedThreshold = 10f;
    private float speed;
    
    // ── Gizmo settings ──────────────────────────────────────────────────────
    [Header("Gizmos")]
    public bool ShowGizmos = true;
    [Tooltip("Scale applied to force magnitudes for gizmo arrow length")]
    public float GizmoForceScale  = 0.002f;
    [Tooltip("Scale applied to torque values for arc gizmos")]
    public float GizmoTorqueScale = 0.003f;
 
    public Color GizmoMotorColor    = new Color(1.0f, 0.85f, 0.1f, 1f);
    public Color GizmoBrakeColor    = new Color(1.0f, 0.2f, 0.2f, 1f);
    public Color GizmoSlipFwdColor  = new Color(1.0f, 0.5f, 0.0f, 1f);
    public Color GizmoSlipLatColor  = new Color(0.8f, 0.2f, 1.0f, 1f);
    public Color GizmoNormalColor   = new Color(0.2f, 0.7f, 1.0f, 1f);
    public Color GizmoABSColor      = new Color(0.0f, 1.0f, 0.8f, 1f);
    public Color GizmoTCColor       = new Color(1.0f, 0.4f, 0.9f, 1f);
    public Color GizmoGroundColor   = new Color(0.3f, 1.0f, 0.3f, 1f);
    
    // Cached per-frame values for gizmos
    private WheelHit  _lastHit;
    private bool      _hasGroundHit;
    private float     _appliedMotorTorque;
    private float     _appliedBrakeTorque;
    
    [HideInInspector] public float SteeringAngle;
    [HideInInspector] public float BrakeTorque;
    [HideInInspector] public float DrivenTorque;
    [HideInInspector] public float HandbrakeTorque;
    [HideInInspector] public bool TCCounterSteering;

    // How much of the requested brake torque is actually applied (0–1), pulsed by ABS
    private float absModulator = 1f;
    // Whether ABS is currently actively intervening (useful to expose to dashboard/sound)
    public bool ABSActive { get; private set; }

    private List<ParticleSystem> effectsWhenSlipping = new List<ParticleSystem>();
    [HideInInspector] public bool initialized = false;

    public void initWheel()
    {
        wheelCollider = GetComponent<WheelCollider>();
        if (effectsHolder)
        {
            foreach (ParticleSystem system in effectsHolder.GetComponentsInChildren<ParticleSystem>())
            {
                effectsWhenSlipping.Add(system);
            }
        }

        slipSoundSource = GetComponent<AudioSource>();
        if(!slipSoundSource)
            slipSoundSource = gameObject.AddComponent<AudioSource>();
        slipSoundSource.outputAudioMixerGroup =  audioMixerGroup;
        slipSoundSource.Stop();
        slipSoundSource.loop = true;
        slipSoundSource.spatialBlend = 1f;
        if(slipSound)
            slipSoundSource.clip = slipSound;

        initialized = true;
    }

    void Update()
    {
        if(!initialized)
            return;
        
        if (Driven)
            wheelCollider.motorTorque = (Reversed ? -DrivenTorque : DrivenTorque) * PowerFactor;

        if (Steerable)
            wheelCollider.steerAngle = Mathf.Lerp(wheelCollider.steerAngle,(InversedSteering ? -SteeringAngle : SteeringAngle) * SteeringFactor, 0.4f);

        if (Brakeable)
            wheelCollider.brakeTorque = (BrakeTorque * BrakeFactor * absModulator) 
                                        + HandbrakeTorque; 

        if (WheelModel)
        {
            wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            WheelModel.transform.position = pos + PositionOffset;
            WheelModel.transform.rotation = rot;

            foreach (ParticleSystem sys in effectsWhenSlipping )
            {
                sys.transform.position = pos + PositionOffset;
            }
        }

        slipSoundAndEffects();
    }

    void FixedUpdate()
    {
        if(!initialized)
            return;
        speed = carRB.linearVelocity.magnitude*3.6f;
        if (ABSEnabled && Brakeable)
            SimulateABS();
        if (TCEnabled && Driven)
            SimulateTCS();
    }

    void SimulateABS()
    {
        if (BrakeTorque <= 0f)
        {
            absModulator = 1f;
            ABSActive = false;
            return;
        }

        if (!wheelCollider.GetGroundHit(out WheelHit hit))
        {
            absModulator = 1f;
            ABSActive = false;
            return;
        }

        float slip = Mathf.Abs(hit.forwardSlip);

        if (slip > ABSSlipThreshold)
        {
            // Clamp floor — never fully release, just reduce enough to restore spin
            absModulator = Mathf.MoveTowards(absModulator, ABSModulatorFloor, 
                Time.fixedDeltaTime * ABSReleaseSpeed);
            ABSActive = true;
        }
        else
        {
            absModulator = Mathf.MoveTowards(absModulator, 1f, 
                Time.fixedDeltaTime * ABSReapplySpeed);
            ABSActive = slip > ABSSlipThreshold * 0.5f;
        }
    }
    
    void SimulateTCS()
    {
        if (HandbrakeTorque > 0f)
        {
            tcModulator = 1f;
            TCActive = false;
            return;
        }
        
        if (!Driven || DrivenTorque <= 0f) { tcModulator = 1f; TCActive = false; return; }
        if (!wheelCollider.GetGroundHit(out WheelHit hit)) { tcModulator = 1f; TCActive = false; return; }

        float slip = Mathf.Abs(hit.forwardSlip);

        // Counter-steering = intentional drift — give full torque back quickly
        if (TCCounterSteering)
        {
            tcModulator = Mathf.MoveTowards(tcModulator, 1f, Time.fixedDeltaTime * TCReapplySpeed * 2f);
            TCActive = false;
            return;
        }

        if (slip > TCSlipThreshold)
        {
            tcModulator = Mathf.MoveTowards(tcModulator, TCMinTorque, Time.fixedDeltaTime * TCReleaseSpeed);
            TCActive = true;
        }
        else
        {
            tcModulator = Mathf.MoveTowards(tcModulator, 1f, Time.fixedDeltaTime * TCReapplySpeed);
            TCActive = slip > TCSlipThreshold * 0.5f;
        }
    }

    void slipSoundAndEffects()
    {
        wheelCollider.GetGroundHit(out WheelHit hit);
        
        if (wheelCollider.rpm < 100 && Math.Abs(hit.forwardSlip)<=0.5f && speed < 10f)
        {
            if (slipSoundSource.isPlaying)
                slipSoundSource.Stop();
            foreach (ParticleSystem system in effectsWhenSlipping)
                if(system.isPlaying)
                    system.Stop();
            return;
        }
        
        float totalSlip =Math.Abs(hit.forwardSlip) + Math.Abs(hit.sidewaysSlip);
        float t = Mathf.InverseLerp(SmokeThreshold, SmokeThreshold * 2f, totalSlip);
        t = t * t;
        foreach (ParticleSystem system in effectsWhenSlipping)
        {
            var emission = system.emission;

            float emissionRate = Mathf.Lerp(0f, maxEmissionRate, t);
            emission.rateOverTime = emissionRate;

            if (!system.isPlaying && totalSlip > SmokeThreshold)
                system.Play();
            else if (totalSlip <= SmokeThreshold && system.isPlaying)
                system.Stop();
        }
        t = Mathf.InverseLerp(SlipSoundThreshold, SlipSoundThreshold * 2f, totalSlip);
        t = t * t;
        slipSoundSource.volume = Mathf.Lerp(slipSoundSource.volume, slipSoundVolumeFactor * t, t);
        slipSoundSource.pitch = Mathf.Lerp(slipSoundMinPitch, slipSoundMaxPitch, t);

        if (totalSlip > SlipSoundThreshold)
        {
            if (!slipSoundSource.isPlaying)
                slipSoundSource.Play();
        }
        else
        {
            if (slipSoundSource.isPlaying)
                slipSoundSource.Stop();
        }
        
        
    }
    
        // ── Gizmos ──────────────────────────────────────────────────────────────
 
    void OnDrawGizmos()
    {
        if (!ShowGizmos || !Application.isPlaying) return;
        DrawWheelGizmos();
    }
 
    void OnDrawGizmosSelected()
    {
        if (!ShowGizmos) return;
        DrawWheelGizmos();
    }
 
    void DrawWheelGizmos()
    {
        if (wheelCollider == null) return;
 
        wheelCollider.GetWorldPose(out Vector3 wheelPos, out Quaternion wheelRot);
        Vector3 fwd   = wheelRot * Vector3.forward;
        Vector3 right = wheelRot * Vector3.right;
        Vector3 up    = wheelRot * Vector3.up;
 
        // ── 1. Ground contact point & normal ────────────────────────────
        if (_hasGroundHit)
        {
            Gizmos.color = GizmoGroundColor;
            Gizmos.DrawSphere(_lastHit.point, 0.04f);
 
            // Normal force arrow (perpendicular to surface)
            float normalMag = _lastHit.force;
            Vector3 normalArrow = _lastHit.normal * normalMag * GizmoForceScale;
            DrawArrow(_lastHit.point, normalArrow);
            DrawLabel(_lastHit.point + normalArrow, $"Fn {normalMag:F0} N", GizmoGroundColor);
        }
 
        // ── 2. Motor torque arc (around wheel axle) ──────────────────────
        if (Driven && Mathf.Abs(_appliedMotorTorque) > 0.1f)
        {
            Gizmos.color = ABSActive ? GizmoABSColor  :
                           TCActive  ? GizmoTCColor   : GizmoMotorColor;
            DrawTorqueArc(wheelPos, right, _appliedMotorTorque * GizmoTorqueScale,
                          wheelCollider.radius + 0.15f);
            DrawLabel(wheelPos + up * (wheelCollider.radius + 0.5f),
                      $"motor {_appliedMotorTorque:F0} Nm  tc×{tcModulator:F2}", GizmoMotorColor);
        }
 
        // ── 3. Brake torque arc ──────────────────────────────────────────
        if (Brakeable && _appliedBrakeTorque > 0.1f)
        {
            Gizmos.color = GizmoBrakeColor;
            // Brake opposes wheel rotation — negate arc direction
            DrawTorqueArc(wheelPos, right, -_appliedBrakeTorque * GizmoTorqueScale,
                          wheelCollider.radius + 0.08f);
            DrawLabel(wheelPos - up * (wheelCollider.radius + 0.3f),
                      $"brake {_appliedBrakeTorque:F0} Nm  abs×{absModulator:F2}  {(ABSActive ? "[ABS]" : "")}",
                      ABSActive ? GizmoABSColor : GizmoBrakeColor);
        }
 
        // ── 4. Forward slip force ────────────────────────────────────────
        if (_hasGroundHit)
        {
            float fwdSlip = _lastHit.forwardSlip;
            if (Mathf.Abs(fwdSlip) > 0.01f)
            {
                Gizmos.color = GizmoSlipFwdColor;
                Vector3 slipFwdArrow = fwd * fwdSlip * 3f;
                DrawArrow(_lastHit.point, slipFwdArrow);
                DrawLabel(_lastHit.point + slipFwdArrow,
                          $"fwd slip {fwdSlip:F3}", GizmoSlipFwdColor);
            }
 
            // ── 5. Lateral slip force ────────────────────────────────────
            float latSlip = _lastHit.sidewaysSlip;
            if (Mathf.Abs(latSlip) > 0.01f)
            {
                Gizmos.color = GizmoSlipLatColor;
                Vector3 slipLatArrow = right * latSlip * 3f;
                DrawArrow(_lastHit.point, slipLatArrow);
                DrawLabel(_lastHit.point + slipLatArrow,
                          $"lat slip {latSlip:F3}", GizmoSlipLatColor);
            }
 
            // ── 6. Combined friction force (resultant of fwd + lat) ───────
            float combinedSlip = Mathf.Sqrt(fwdSlip * fwdSlip + latSlip * latSlip);
            if (combinedSlip > 0.02f)
            {
                Gizmos.color = Color.white;
                Vector3 frictionVec = (fwd * fwdSlip + right * latSlip) * 3f;
                DrawArrow(_lastHit.point, frictionVec);
            }
        }
 
        // ── 7. Wheel RPM / spin indicator ────────────────────────────────
        {
            float rpm = wheelCollider.rpm;
            if (Mathf.Abs(rpm) > 0.5f)
            {
                Gizmos.color = new Color(0.7f, 0.9f, 1f, 0.7f);
                DrawLabel(wheelPos + up * (wheelCollider.radius + 0.8f),
                          $"{rpm:F0} RPM", new Color(0.7f, 0.9f, 1f, 1f));
            }
        }
 
        // ── 8. TC / ABS status rings ─────────────────────────────────────
        if (TCActive)
            DrawWireCircle(wheelPos, right, wheelCollider.radius + 0.05f, GizmoTCColor);
        if (ABSActive)
            DrawWireCircle(wheelPos, right, wheelCollider.radius + 0.12f, GizmoABSColor);
 
        // ── 9. Steering direction indicator ─────────────────────────────
        if (Steerable && Mathf.Abs(SteeringAngle) > 0.5f)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            DrawArrow(wheelPos, fwd * Mathf.Sign(SteeringAngle) * 0.8f);
        }
    }
 
    // ── Helper: arrow ───────────────────────────────────────────────────────
    static void DrawArrow(Vector3 origin, Vector3 vec)
    {
        if (vec.sqrMagnitude < 0.0001f) return;
        Gizmos.DrawLine(origin, origin + vec);
        Vector3 tip  = origin + vec;
        float   size = vec.magnitude * 0.18f;
        Vector3 perp = Vector3.Cross(vec.normalized, Vector3.up).normalized;
        if (perp.sqrMagnitude < 0.001f) perp = Vector3.right;
        Gizmos.DrawLine(tip, tip - vec.normalized * size + perp * size * 0.5f);
        Gizmos.DrawLine(tip, tip - vec.normalized * size - perp * size * 0.5f);
    }
 
    // ── Helper: torque arc (curved arrow) ───────────────────────────────────
    static void DrawTorqueArc(Vector3 centre, Vector3 axis, float torqueScaled, float radius)
    {
        int   segments = 20;
        float arcDeg   = Mathf.Clamp(Mathf.Abs(torqueScaled) * 40f, 15f, 260f);
        float dir      = Mathf.Sign(torqueScaled);
        Vector3 perp   = Vector3.Cross(axis, Vector3.up).normalized;
        if (perp.sqrMagnitude < 0.001f) perp = Vector3.forward;
 
        Vector3 prev = centre + Quaternion.AngleAxis(0f, axis) * perp * radius;
        for (int i = 1; i <= segments; i++)
        {
            float   angle = dir * arcDeg * i / segments;
            Vector3 cur   = centre + Quaternion.AngleAxis(angle, axis) * perp * radius;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
        // Arrowhead at tip
        Vector3 tangent = Quaternion.AngleAxis(dir * arcDeg, axis) *
                          Quaternion.AngleAxis(dir * 12f, axis) * perp;
        Gizmos.DrawLine(prev, prev + tangent.normalized * 0.25f);
    }
 
    // ── Helper: flat wire circle in a plane defined by axis ─────────────────
    static void DrawWireCircle(Vector3 centre, Vector3 axis, float radius, Color col)
    {
        Gizmos.color = col;
        int     segs = 32;
        Vector3 perp = Vector3.Cross(axis, Vector3.up).normalized;
        if (perp.sqrMagnitude < 0.001f) perp = Vector3.forward;
        Vector3 prev = centre + Quaternion.AngleAxis(0f, axis) * perp * radius;
        for (int i = 1; i <= segs; i++)
        {
            float   a   = 360f * i / segs;
            Vector3 cur = centre + Quaternion.AngleAxis(a, axis) * perp * radius;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
 
    // ── Helper: editor label ─────────────────────────────────────────────────
    static void DrawLabel(Vector3 pos, string text, Color col)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = col;
        UnityEditor.Handles.Label(pos, text);
#endif
    }
}