using System;
using UnityEngine;

public class EngineSound : MonoBehaviour
{
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
    }
}