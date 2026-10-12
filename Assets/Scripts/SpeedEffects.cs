using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

public class SpeedEffects : MonoBehaviour
{
    private Vehicle activeVehicle;
    private Rigidbody car;
    private Volume volume;
    private AudioSource windSource;

    public float slowSpeedThreshold = 20f;

    public float maxSpeed = 130f;
    public AudioMixer carEffectsMixer;
    
    public float normalCutoff = 22000f;
    public float maxMuffleCutoff = 3000f;
    
    public AnimationCurve speedCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(100f, 0f),
        new Keyframe(180f, 0.15f),
        new Keyframe(250f, 0.5f),
        new Keyframe(300f, 1f)
    );


    
    void Start()
    {
        activeVehicle = GetComponentInParent<Vehicle>();
        car = activeVehicle.rb;
        windSource = GetComponent<AudioSource>();
        volume = GetComponent<Volume>();
        volume.weight = 0f;
        windSource.volume = 0f;
        carEffectsMixer.SetFloat("Lowpass",normalCutoff);
    }
    
    void Update()
    {
        float speed = car.linearVelocity.magnitude * 3.6f;
        if (speed < slowSpeedThreshold)
        {
            volume.weight = 0f;
            windSource.volume = 0f;
        }
        else
        {
            float currentIntensity = Mathf.InverseLerp(slowSpeedThreshold, maxSpeed, speed);
            float targetIntensity = speedCurve.Evaluate(currentIntensity);
            volume.weight = targetIntensity;
            windSource.volume = targetIntensity;
            carEffectsMixer.SetFloat("Lowpass", Mathf.Lerp(
                normalCutoff,
                maxMuffleCutoff,
                targetIntensity
            ));

        }
    }
}
