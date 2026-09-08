using System.Collections;
using UnityEngine;

public class EngineSound : MonoBehaviour
{
    [Header("Engine Audio")]
    public AudioSource baseAudio;
    public AudioSource upperAudio;
    public AudioClip BaseEngineSound;
    public AudioClip UpperEngineSound;

    [Header("RPM Range")]
    public float RPMThreshold;        // crossfade midpoint
    public float CrossfadeWidth = 500f; // RPM band over which blend happens

    [Header("Pitch")]
    public float basePitch = 1f;
    public float upperPitch = 1f;

    [Header("Volume")]
    public float minVolume = 0.1f;
    public float volumeLerpSpeed = 5f;
    public float pitchLerpSpeed = 8f;

    public AudioSource specialEffectsSource;
    [Header("Turbo Blowoff")] 
    public float blowoffRPMThreshold = 4000f;
    public float ThrottleDropThreshold = 0.4f;
    public float blowoffCooldown = 1.5f;
    public Vector2 blowoffPitchRange = new Vector2(0.9f, 1.1f);
    public AudioClip TurboBlowoutSound;
    private float lastBlowoffTime = -999f;
    private float lastThrottle;
    
    [Header("Exhaust Pops")]
    public AudioClip[] ExhaustPops;

    public float popMinRPM = 2500f;
    public float popIntervalMin = 0.05f;
    public float popIntervalMax = 0.1f;
    public Vector2 popPitchRange = new Vector2(0.85f, 1.15f);
    public Vector2 popVolumeRange = new Vector2(0.4f, 0.8f);
    private Coroutine popRoutine = null;
    
    private Vehicle vehicle;
    private float targetVolume;

    void Start()
    {
        vehicle = GetComponent<Vehicle>();
        if(!vehicle)
            vehicle = GetComponentInParent<Vehicle>();

        baseAudio.clip = BaseEngineSound;
        baseAudio.loop = true;
        baseAudio.Play();

        upperAudio.clip = UpperEngineSound;
        upperAudio.loop = true;
        upperAudio.Play();         // both play the whole time
        upperAudio.volume = 0f;
    }

    void Update()
    {
        float rpm = vehicle.engineRPM;
        float t = Mathf.InverseLerp(RPMThreshold - CrossfadeWidth * 0.5f,
                                     RPMThreshold + CrossfadeWidth * 0.5f,
                                     rpm);

        // t=0 → full base, t=1 → full upper
        targetVolume = Mathf.Max(minVolume, vehicle.throttleInput);
        baseAudio.volume  = Mathf.Lerp(baseAudio.volume,  (1f - t) * targetVolume, Time.deltaTime * volumeLerpSpeed);
        upperAudio.volume = Mathf.Lerp(upperAudio.volume,        t * targetVolume,  Time.deltaTime * volumeLerpSpeed);

        float rpmNorm = rpm / vehicle.MaxRPM;
        float targetBasePitch  = basePitch  + rpmNorm;
        float targetUpperPitch = upperPitch + rpmNorm;

        baseAudio.pitch  = Mathf.Lerp(baseAudio.pitch,  targetBasePitch,  Time.deltaTime * pitchLerpSpeed);
        upperAudio.pitch = Mathf.Lerp(upperAudio.pitch, targetUpperPitch, Time.deltaTime * pitchLerpSpeed);
        
        CheckBlowoff();
        CheckPops();
        
        lastThrottle = vehicle.throttleInput;
    }

    void CheckBlowoff()
    {
        float rpm = vehicle.engineRPM;
        float throttle = vehicle.throttleInput;

        float throttleDelta = Mathf.Abs(throttle - lastThrottle);
        bool sharpLift = throttleDelta > ThrottleDropThreshold;
        bool highRPM = rpm > blowoffRPMThreshold;
        bool offCooldown = Time.time - lastBlowoffTime > blowoffCooldown;
        Debug.Log(highRPM + " " + sharpLift + " " + offCooldown);
        if (highRPM && sharpLift && offCooldown)
        {
            specialEffectsSource.pitch = Random.Range(blowoffPitchRange.x, blowoffPitchRange.y);
            specialEffectsSource.volume = Random.Range(0.8f, 1f);
            specialEffectsSource.PlayOneShot(TurboBlowoutSound);
            lastBlowoffTime = Time.time;
        }
        
    }

    void CheckPops()
    {
        if (vehicle.throttleInput < 0.1f && lastThrottle > 0.3f && vehicle.engineRPM > popMinRPM && popRoutine == null)
            popRoutine = StartCoroutine(PopBurst(vehicle.engineRPM));
    }

    IEnumerator PopBurst(float rpmAtStart)
    {
        while (vehicle.engineRPM > popMinRPM * 0.7f && vehicle.throttleInput < 0.15f)
        {
            var clip = ExhaustPops[Random.Range(0, ExhaustPops.Length)];
            specialEffectsSource.pitch = Random.Range(popPitchRange.x, popPitchRange.y);
            specialEffectsSource.PlayOneShot(clip, Random.Range(popVolumeRange.x, popVolumeRange.y));
           
            float rpmFactor = Mathf.InverseLerp(popMinRPM, vehicle.MaxRPM, vehicle.engineRPM);
            float interval = Mathf.Lerp(popIntervalMax, popIntervalMin, rpmFactor);
            yield return new WaitForSeconds(interval * Random.Range(0.7f, 1.3f));
        }
        popRoutine = null;
    }
}