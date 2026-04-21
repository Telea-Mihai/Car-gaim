using UnityEngine;

public class DynamicCamera : MonoBehaviour
{
    [System.Serializable]
    public struct CameraState
    {
        public float SpeedToggle;
        public float DistanceFromTarget;
        public float HeightOffset;
        public float FOV;
        public float PositionLerpSpeed;
        public float RotationLerpSpeed;
    }

    [Header("Target")]
    public Vehicle activeVehicle;

    [Header("Feel")]
    public float velocityLookAhead = 0.15f;
    public float lowSpeedThreshold = 2f;
    public float rollInfluence     = 0.04f;

    [Header("Camera Weight")]
    [Tooltip("How strongly camera resists turning with the car. Higher = more side view.")]
    public float yawLagStrength    = 0.85f;   // 0 = no lag, 1 = never turns
    [Tooltip("How fast the camera realigns behind the car when straight.")]
    public float yawRecoverSpeed   = 2.5f;
    [Tooltip("Speed below which yaw lag fades out so parking isn't horrible.")]
    public float yawLagMinSpeed    = 10f;

    [Header("Shake")]
    public float shakeSpeedThreshold = 80f;
    public float shakeIntensity      = 0.03f;
    public float shakeFrequency      = 12f;

    [Header("States")]
    public CameraState[] states;

    private Camera      cam;
    private Rigidbody   vehicleRB;
    private CameraState currentState;

    private Vector3    smoothLocalOffset;
    private float      smoothFOV;
    private float      shakeTime;

    // The camera's current world-space "forward" direction — driven independently
    private Vector3 smoothCameraForward;

    void Start()
    {
        cam           = GetComponent<Camera>();
        vehicleRB     = activeVehicle.rb;
        currentState  = states[0];

        smoothLocalOffset   = new Vector3(0f, states[0].HeightOffset, -states[0].DistanceFromTarget);
        smoothFOV           = states[0].FOV;
        smoothCameraForward = activeVehicle.transform.forward;
    }

    void LateUpdate()
    {
        float     speedKPH = vehicleRB.linearVelocity.magnitude * 3.6f;
        Transform car      = activeVehicle.transform;

        UpdateState(speedKPH);
        UpdateCameraForward(car, speedKPH);

        // --- Position ---
        // Use smoothCameraForward to place the camera, not car.forward
        // This is what causes the side-view effect — camera is still where it was
        // a moment ago while the car has already turned
        Vector3 targetLocalOffset = new Vector3(0f,
            currentState.HeightOffset,
            -currentState.DistanceFromTarget);

        smoothLocalOffset = Vector3.Lerp(smoothLocalOffset, targetLocalOffset,
                                          Time.deltaTime * currentState.PositionLerpSpeed);

        // Place camera behind the car along the LAGGED forward, not car.forward
        Vector3 worldPos    = car.position
                            - smoothCameraForward * currentState.DistanceFromTarget
                            + Vector3.up * currentState.HeightOffset;

        transform.position  = worldPos + CalculateShake(speedKPH);

        // --- Rotation ---
        // Look ahead along velocity or car forward at low speed
        Vector3 lookTarget;
        if (speedKPH > lowSpeedThreshold)
            lookTarget = car.position + vehicleRB.linearVelocity * velocityLookAhead;
        else
            lookTarget = car.position + car.forward * 10f;

        Vector3    lookDir        = (lookTarget - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(lookDir);

        // Roll — car tilting into a corner rolls the camera slightly
        float carRoll   = Vector3.Dot(car.right, Vector3.up);
        targetRotation *= Quaternion.AngleAxis(
            carRoll * rollInfluence * Mathf.Rad2Deg, Vector3.forward);

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                                               Time.deltaTime * currentState.RotationLerpSpeed);

        // --- FOV ---
        smoothFOV       = Mathf.Lerp(smoothFOV, currentState.FOV, Time.deltaTime * 3f);
        cam.fieldOfView = smoothFOV;
    }

    void UpdateCameraForward(Transform car, float speedKPH)
    {
        // Target direction: where the car is actually going, not where it faces
        // At low speed fall back to car.forward so it doesn't drift weirdly
        float   speedBlend    = Mathf.Clamp01((speedKPH - yawLagMinSpeed) / 20f);
        Vector3 velocityDir   = vehicleRB.linearVelocity.magnitude > 0.5f
                                ? vehicleRB.linearVelocity.normalized
                                : car.forward;

        // Blend between velocity direction and car forward based on speed
        Vector3 targetForward = Vector3.Slerp(car.forward, velocityDir, speedBlend);

        // Lag: camera forward chases the target forward slowly
        // The lag strength reduces how fast it can turn to follow
        float laggedSpeed = currentState.RotationLerpSpeed
                          * (1f - yawLagStrength * speedBlend);

        smoothCameraForward = Vector3.Slerp(smoothCameraForward, targetForward,
                                             Time.deltaTime * Mathf.Max(laggedSpeed, yawRecoverSpeed));
        smoothCameraForward.y = 0f;           // keep camera from pitching up/down
        smoothCameraForward.Normalize();
    }

    void UpdateState(float speedKPH)
    {
        // Find the two states we're between
        CameraState lower = states[0];
        CameraState upper = states[states.Length - 1];

        for (int i = 0; i < states.Length - 1; i++)
        {
            if (speedKPH >= states[i].SpeedToggle && speedKPH < states[i + 1].SpeedToggle)
            {
                lower = states[i];
                upper = states[i + 1];
                break;
            }
            // Past all thresholds — stay at last state
            if (speedKPH >= states[states.Length - 1].SpeedToggle)
            {
                lower = upper = states[states.Length - 1];
                break;
            }
        }

        // How far between the two states (0 = fully lower, 1 = fully upper)
        float t = 0f;
        float range = upper.SpeedToggle - lower.SpeedToggle;
        if (range > 0f)
            t = Mathf.Clamp01((speedKPH - lower.SpeedToggle) / range);

        // Ease the blend so it doesn't feel linear/mechanical
        t = Mathf.SmoothStep(0f, 1f, t);

        // Blend all state values
        currentState = BlendStates(lower, upper, t);
    }

    CameraState BlendStates(CameraState a, CameraState b, float t)
    {
        return new CameraState
        {
            SpeedToggle          = Mathf.Lerp(a.SpeedToggle,          b.SpeedToggle,          t),
            DistanceFromTarget   = Mathf.Lerp(a.DistanceFromTarget,   b.DistanceFromTarget,   t),
            HeightOffset         = Mathf.Lerp(a.HeightOffset,         b.HeightOffset,         t),
            FOV                  = Mathf.Lerp(a.FOV,                  b.FOV,                  t),
            PositionLerpSpeed    = Mathf.Lerp(a.PositionLerpSpeed,    b.PositionLerpSpeed,    t),
            RotationLerpSpeed    = Mathf.Lerp(a.RotationLerpSpeed,    b.RotationLerpSpeed,    t),
        };
    }

    Vector3 CalculateShake(float speedKPH)
    {
        if (speedKPH < shakeSpeedThreshold) return Vector3.zero;

        float t         = Mathf.Clamp01((speedKPH - shakeSpeedThreshold)
                                        / (200f - shakeSpeedThreshold));
        shakeTime      += Time.deltaTime * shakeFrequency;

        return new Vector3(
            Mathf.PerlinNoise(shakeTime, 0f) * 2f - 1f,
            Mathf.PerlinNoise(0f, shakeTime) * 2f - 1f,
            0f
        ) * shakeIntensity * t;
    }
}