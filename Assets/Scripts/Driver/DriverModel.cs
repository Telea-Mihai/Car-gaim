using System;
using UnityEngine;

public class DriverModel : MonoBehaviour {
    private Animator animator;
    private Vehicle vehicle;
    private CollisionDeformationDistribuitor cdd;
    private Rigidbody car;

    public Vector2 LeftRightLimitsHead;
    public Transform HeadTarget;
    public Transform HeadTargetPivot;
    public float HeadRotationStep = 2f;
    public float HeadRotationMultiplier = 1f;
    
    public Transform SteeringWheel;
    public float SteeringWheelAngle;
    public Vector3 SteeringWheelAxis;
    public float SteeringWheelTurnStep = 0.1f;
    public float steeringWheelRotationMultiplier = 1.4f;
    public Transform LeftHandTarget;
    public Transform RightHandTarget;

    public bool ikActive;
    private float steeringAngle=0;
    private float finalHeadAngle = 0;
    private bool lastCollisionState = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
        vehicle = GetComponentInParent<Vehicle>();
        cdd = vehicle.GetComponent<CollisionDeformationDistribuitor>();
        car = vehicle.rb;
    }

    // Update is called once per frame
    void Update()
    {
        float baseSteeringAngle = vehicle.BaseSteeringAngle;
        float appliedSteeringAngle = vehicle.steeringAngle;
        float direction = vehicle.steeringInput < 0 ? -1 : 1;
        float factor = Mathf.InverseLerp(0, direction*baseSteeringAngle, appliedSteeringAngle);
        factor *= direction;
        steeringAngle = Mathf.Lerp(steeringAngle, factor , Time.deltaTime * SteeringWheelTurnStep);
        Vector3 finalSteringWheelAngle = -steeringAngle * SteeringWheelAxis * SteeringWheelAngle * steeringWheelRotationMultiplier;
        SteeringWheel.localRotation = Quaternion.Euler(finalSteringWheelAngle);

        Vector3 forwardDirection = car.linearVelocity.normalized; 
        forwardDirection = Vector3.ProjectOnPlane(forwardDirection, Vector3.up); 
        float angle = Vector3.SignedAngle(forwardDirection, car.transform.forward, Vector3.up); 
        angle = Mathf.Clamp(angle, LeftRightLimitsHead.x, LeftRightLimitsHead.y); 
        finalHeadAngle = Mathf.Lerp(finalHeadAngle, angle, Time.deltaTime * HeadRotationStep); 
        HeadTargetPivot.localRotation = Quaternion.Euler(0, 0, finalHeadAngle);

        if (lastCollisionState != cdd.impactStarted)
        {
            if (lastCollisionState == false)
            {
                lastCollisionState = true;
                animator.Play("fah");
            }
            lastCollisionState = cdd.impactStarted;
        } else if (cdd.reaction)
        {
            animator.Play("fah");
            cdd.reaction = false;
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (ikActive)
        {
            if (RightHandTarget != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
                animator.SetIKPosition(AvatarIKGoal.RightHand, RightHandTarget.position);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
                animator.SetIKRotation(AvatarIKGoal.RightHand, RightHandTarget.rotation);
            }

            if (LeftHandTarget != null)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
                animator.SetIKPosition(AvatarIKGoal.LeftHand, LeftHandTarget.position);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
                animator.SetIKRotation(AvatarIKGoal.LeftHand, LeftHandTarget.rotation);
            }

            if (HeadTarget != null)
            {
                animator.SetLookAtWeight(1);
                animator.SetLookAtPosition(HeadTarget.position);
            }
        }
    }
}
