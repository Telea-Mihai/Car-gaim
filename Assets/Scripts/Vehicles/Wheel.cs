using System;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

[RequireComponent(typeof(WheelCollider))]
public class Wheel : MonoBehaviour
{
    [Header("Power Delivery")] public bool Driven;
    public bool Reversed = false;
    public float PowerFactor = 1.0f;

    [Header("Steering")] public bool Steerable;
    public bool InversedSteering = false;
    public float SteeringFactor = 1.0f;

    [Header("Braking")] public bool Brakeable;
    public float BrakeFactor = 1.0f;
    public bool HasHandbrake = false;

    [Header("ABS")] public bool ABSEnabled = true;
    public float ABSSlipThreshold = 0.2f;
    public float ABSReleaseSpeed = 10f;
    public float ABSReapplySpeed = 8f; // was 3 — needs to be fast to maintain avg force
    [Range(0f, 1f)] public float ABSModulatorFloor = 0.6f; // never release below this — key change

    [Header("Traction Control")] public bool TCEnabled = true;
    [Range(0f, 1f)] public float TCSlipThreshold = 0.35f; // higher = more wheelspin allowed before cutting
    [Range(0f, 1f)] public float TCMinTorque = 0.4f; // never cut below this — keeps drift momentum alive
    public float TCReleaseSpeed = 12f; // how fast it cuts
    public float TCReapplySpeed = 5f; // how fast it gives torque back

    private float tcModulator = 1f;
    public bool TCActive { get; private set; }
    
    [Header("Surfaces")]
    public Surface[] surfaces;

    public int activeSurface = 0;
    [SerializeField] private float minSpeedForResistance = 2f;
    [SerializeField] private float maxSpeedForResistance = 30f;
    [SerializeField] private float lowSpeedResistance = 0.1f;

    [Header("Wheel Model")] public GameObject WheelModel;
    public Quaternion Offset;
    public Vector3 PositionOffset;

    [Header("Visual Effects")] public float SmokeThreshold = 0.5f;
    private GameObject effectsHolder;
    public GameObject[] driftEffects;
    private int activeEffectIndex = 0;
    public int maxEmissionRate = 200;
    [Header("Sounds")] public float SlipSoundThreshold = 0.5f;
    public AudioClip slipSound;
    public AudioSource slipSoundSource;
    public AudioMixerGroup audioMixerGroup;
    public float slipSoundVolumeFactor = 0.5f;
    public float slipSoundMinPitch;
    public float slipSoundMaxPitch;

    public AudioSource rollingSoundSource;
    public float rollingSoundRPMMaxVolume = 100;

    public WheelCollider wheelCollider;
    public Rigidbody carRB;
    private float speed;
    private float activeResistanceCoefficient;

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

    private void Awake()
    {
        wheelCollider = GetComponent<WheelCollider>();
    }

    public void initWheel()
    {
        activeEffectIndex = 0;
        effectsHolder = driftEffects[activeEffectIndex];
        if (effectsHolder)
        {
            foreach (ParticleSystem system in effectsHolder.GetComponentsInChildren<ParticleSystem>())
            {
                effectsWhenSlipping.Add(system);
            }
        }
        
        slipSoundSource = GetComponent<AudioSource>();
        if (!slipSoundSource)
            slipSoundSource = gameObject.AddComponent<AudioSource>();
        
        activeSurface = 0;
        if(surfaces[activeSurface].driftSound)
            slipSoundSource.clip = surfaces[activeSurface].driftSound;
        slipSoundMaxPitch = surfaces[activeSurface].slipSoundMaxPitch;
        slipSoundMinPitch = surfaces[activeSurface].slipSoundMinPitch;
        slipSoundVolumeFactor = surfaces[activeSurface].slipSoundVolumeFactor;
        slipSoundSource.outputAudioMixerGroup = audioMixerGroup;
        slipSoundSource.spatialBlend = 1f;
        slipSoundSource.Stop();
        slipSoundSource.loop = true;
        slipSoundSource.spatialBlend = 1f;
        
        initialized = true;
    }

    

    void Update()
    {
        if (!initialized)
            return;

        if (Driven)
            wheelCollider.motorTorque = (Reversed ? -DrivenTorque : DrivenTorque) * PowerFactor;

        if (Steerable)
            wheelCollider.steerAngle = Mathf.Lerp(wheelCollider.steerAngle,
                (InversedSteering ? -SteeringAngle : SteeringAngle) * SteeringFactor, 0.4f);

        if (Brakeable)
            wheelCollider.brakeTorque = (BrakeTorque * BrakeFactor * absModulator)
                                        + HandbrakeTorque;

        if (WheelModel)
        {
            wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            WheelModel.transform.position = pos + PositionOffset;
            WheelModel.transform.rotation = rot;

            foreach (ParticleSystem sys in effectsWhenSlipping)
            {
                sys.transform.position = pos + PositionOffset;
            }
        }
        
        UpdateSurfaceSettings();
        slipSoundAndEffects();
    }

    void ChangeSurface(int layer)
    {
        activeSurface = Array.FindIndex(surfaces, s => s.layer == layer);
        activeSurface = Mathf.Clamp(activeSurface, 0, surfaces.Length - 1);
        WheelFrictionCurve side, forward;
        forward = new WheelFrictionCurve();
        forward.extremumSlip = surfaces[activeSurface].fExtremumSlip;
        forward.extremumValue = surfaces[activeSurface].fExtremumValue;
        forward.asymptoteSlip = surfaces[activeSurface].fAsymptoteSlip;
        forward.asymptoteValue = surfaces[activeSurface].fAsymptoteValue;
        forward.stiffness = surfaces[activeSurface].fStiffness;
        
        // wheelCollider.sidewaysFriction = surfaces[activeSurface].sideFrictionCurve;
        wheelCollider.forwardFriction = forward;
        activeResistanceCoefficient = surfaces[activeSurface].resistanceCoefficient;
        
        activeEffectIndex = surfaces[activeSurface].driftEffectIndex;
        foreach (ParticleSystem system in effectsWhenSlipping)
        {
            system.Stop();
        }
        effectsWhenSlipping.Clear();
        effectsHolder = driftEffects[activeEffectIndex];
        if (effectsHolder)
        {
            foreach (ParticleSystem system in effectsHolder.GetComponentsInChildren<ParticleSystem>())
            {
                effectsWhenSlipping.Add(system);
            }
        }
        
        if(surfaces[activeSurface].driftSound)
            slipSoundSource.clip = surfaces[activeSurface].driftSound;
        slipSoundMaxPitch = surfaces[activeSurface].slipSoundMaxPitch;
        slipSoundMinPitch = surfaces[activeSurface].slipSoundMinPitch;
        slipSoundVolumeFactor = surfaces[activeSurface].slipSoundVolumeFactor;
        if (surfaces[activeSurface].rollingSound)
        {
            rollingSoundSource.clip = surfaces[activeSurface].rollingSound;
            rollingSoundSource.Play();
        }
        else
        {
            rollingSoundSource.Stop();
        }
    }
    
    void UpdateSurfaceSettings()
    {
        if (wheelCollider.GetGroundHit(out WheelHit hit))
        {
            if (hit.collider.gameObject.layer != surfaces[activeSurface].layer)
            {
                ChangeSurface(hit.collider.gameObject.layer);
            }
            
            ApplyRollingResistance(hit);
        }
    }

    void ApplyRollingResistance(WheelHit hit)
    {
        float normalForce = hit.force;

        Vector3 velocity = carRB.GetPointVelocity(hit.point);
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, hit.normal);

        float speed = horizontalVelocity.magnitude;

        if (speed < 0.1f)
            return;

        // Resistance increases with speed
        float speedFactor = Mathf.InverseLerp(
            minSpeedForResistance,
            maxSpeedForResistance,
            speed
        );

        // Keep some resistance at low speeds
        speedFactor = Mathf.Lerp(lowSpeedResistance, 1f, speedFactor);

        float rollingResistance = normalForce * activeResistanceCoefficient * speedFactor;

        Vector3 resistanceForce = -horizontalVelocity.normalized * rollingResistance;

        carRB.AddForceAtPosition(resistanceForce, hit.point);
    }

    void FixedUpdate()
    {
        if (!initialized)
            return;
        speed = carRB.linearVelocity.magnitude * 3.6f;
        
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

        if (!Driven || DrivenTorque <= 0f)
        {
            tcModulator = 1f;
            TCActive = false;
            return;
        }

        if (!wheelCollider.GetGroundHit(out WheelHit hit))
        {
            tcModulator = 1f;
            TCActive = false;
            return;
        }

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

        if (wheelCollider.rpm < 100 && Math.Abs(hit.forwardSlip) <= 0.5f && speed < 10f)
        {
            if (slipSoundSource.isPlaying)
                slipSoundSource.Stop();
            foreach (ParticleSystem system in effectsWhenSlipping)
                if (system.isPlaying)
                    system.Stop();
            return;
        }

        float totalSlip = Math.Abs(hit.forwardSlip) + Math.Abs(hit.sidewaysSlip);
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

        float volume = Mathf.InverseLerp(0, rollingSoundRPMMaxVolume, Mathf.Abs(wheelCollider.rpm));
        if(Mathf.Abs(wheelCollider.rpm) < 20f)
            volume = 0f;
        rollingSoundSource.volume = volume;
    }

}



[System.Serializable]
public class Surface
{
    public int layer;
    public float resistanceCoefficient;
    [Header("Forward curve")]
    public float fExtremumValue = 1f;
    public float fExtremumSlip = 0.4f;
    public float fAsymptoteSlip = 0.85f;
    public float fAsymptoteValue = 0.75f;
    public float fStiffness = 1f;
    [Header("Side curve")]
    public float sExtremumValue = 1f;
    public float sAsymptoteSlip = 0.85f;
    public float sAsymptoteValue = 0.75f;

    public float sStiffness = 1f;
    [Header("Effects")] 
    public AudioClip driftSound;
    public float slipSoundVolumeFactor = 0.5f;
    public float slipSoundMinPitch;
    public float slipSoundMaxPitch;
    public AudioClip rollingSound;
    public int driftEffectIndex;
}