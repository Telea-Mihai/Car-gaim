using UnityEngine;
using UnityEngine.Audio;

public class OtherSounds : MonoBehaviour
{
    private Vehicle vehicle;

    private AudioSource audioSource;
    public AudioMixerGroup audioMixerGroup;

    public AudioClip[] gearShiftSounds;
    public Vector2 gearShiftPitchRange;
    public float minDurationBetweenShifts = 0.5f;

    public Vector2 handBrakePitchRange;
    public AudioClip handBrakePull;
    public AudioClip handBrakeRelease;

    private bool lastHandBrakeState = false;

    private int lastGear=0;
    private float lastGearShiftTime=0;

    void Start()
    {
        vehicle = GetComponentInParent<Vehicle>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.outputAudioMixerGroup = audioMixerGroup;
    }

    void Update()
    {
        if (lastHandBrakeState == false)
        {
            if (vehicle.handbrakeInput > 0.5f)
            {
                audioSource.pitch = Random.Range(handBrakePitchRange.x, handBrakePitchRange.y);
                audioSource.PlayOneShot(handBrakePull, GameAudioSettings.OtherVolume);
                lastHandBrakeState = true;
            }
        } else if (lastHandBrakeState == true)
        {
            if (vehicle.handbrakeInput < 0.5f)
            {
                audioSource.PlayOneShot(handBrakeRelease, GameAudioSettings.OtherVolume);
                lastHandBrakeState = false;
            }
        }
        if(vehicle.CurrentGear != lastGear)
        {
            if (lastGearShiftTime + minDurationBetweenShifts < Time.time)
            {
                audioSource.pitch = Random.Range(gearShiftPitchRange.x, gearShiftPitchRange.y);
                audioSource.PlayOneShot(
                    gearShiftSounds[Random.Range(0, gearShiftSounds.Length - 1)],
                    GameAudioSettings.OtherVolume);
                lastGearShiftTime = Time.time;
            }

            lastGear = vehicle.CurrentGear;
        }
        
    }
}
