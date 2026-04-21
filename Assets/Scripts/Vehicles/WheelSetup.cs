using UnityEngine;

public class WheelSetup : MonoBehaviour
{
    private Vehicle vehicle;
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
    [Header("Forward Friction")] public float ExtremumSlip = 0.4f;

    public float ExtremumValue = 1f;

    public float AsymptoteSlip = 0.85f;
    public float AsymptoteValue = 0.75f;

    public float Stiffness = 1f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        WheelFrictionCurve friction = new WheelFrictionCurve();
        friction.extremumSlip = ExtremumSlip;
        friction.asymptoteSlip = AsymptoteSlip;
        friction.stiffness = Stiffness;
        friction.asymptoteValue = AsymptoteValue;
        friction.extremumValue = ExtremumValue;
        vehicle = GetComponent<Vehicle>();
        foreach (Wheel wheel in vehicle.Wheels)
        {
            wheel.ABSEnabled = ABSEnabled;
            wheel.ABSSlipThreshold = ABSSlipThreshold;
            wheel.ABSReleaseSpeed = ABSReleaseSpeed;
            wheel.ABSReapplySpeed = ABSReapplySpeed;
            wheel.ABSModulatorFloor = ABSModulatorFloor;
            
            wheel.TCEnabled = TCEnabled;
            wheel.TCSlipThreshold = TCSlipThreshold;
            wheel.TCMinTorque = TCMinTorque;
            wheel.TCReleaseSpeed = TCReleaseSpeed;
            wheel.TCReapplySpeed = TCReapplySpeed;
            
            WheelCollider col = wheel.wheelCollider;
            col.forwardFriction = friction;
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
