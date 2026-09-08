using System;
using UnityEngine;

public class RollBar : MonoBehaviour
{
    public float AntiRollStiffness;
    public Wheel leftWheel;
    public Wheel rightWheel;
    public Rigidbody rb;

    private void Start()
    {
        if (!rb)
        {
            GetComponentInParent<Rigidbody>();
        }
    }

    void FixedUpdate()
    {
        
        if(!leftWheel || !rightWheel || !rb || !leftWheel.initialized || !rightWheel.initialized) return;
        WheelHit hit;
        float travelL = 1f, travelR = 1f;

        bool groundedL = leftWheel.wheelCollider.GetGroundHit(out hit);
        if (groundedL)
            travelL = (-leftWheel.wheelCollider.transform.InverseTransformPoint(hit.point).y
                       - leftWheel.wheelCollider.radius)
                      / leftWheel.wheelCollider.suspensionDistance;

        bool groundedR = rightWheel.wheelCollider.GetGroundHit(out hit);
        if (groundedR)
            travelR = (-rightWheel.wheelCollider.transform.InverseTransformPoint(hit.point).y
                       - rightWheel.wheelCollider.radius)
                      / rightWheel.wheelCollider.suspensionDistance;

        float antiRollForce = (travelL - travelR) * AntiRollStiffness;

        if (groundedL) rb.AddForceAtPosition(leftWheel.transform.up  * -antiRollForce, leftWheel.transform.position);
        if (groundedR) rb.AddForceAtPosition(rightWheel.transform.up *  antiRollForce, rightWheel.transform.position);
    }
}
