using UnityEngine;

public class DynamicCluster : MonoBehaviour
{
    public Transform target;
    public Transform camera;

    [Header("Translation Reaction")]
    public float reactionStrength = 0.03f;
    public float reactionSmoothTime = 0.08f;
    public float maxReaction = 0.05f;

    [Header("Rotation Reaction")]
    public float rotationReactionStrength = 0.01f;
    public float rotationReactionSmoothTime = 0.08f;
    public float maxRotationReaction = 0.03f;

    [Header("Rotation")]
    public bool followRotation = true;
    public float rotationFollowSpeed = 6f;

    private Vector3 previousTargetPosition;
    private Vector3 previousVelocity;

    private Quaternion previousTargetRotation;

    private Vector3 reactionVelocity;
    private Vector3 reactionOffset;

    private Vector3 rotationReactionVelocity;
    private Vector3 rotationReactionOffset;

    void Start()
    {
        if (target != null)
        {
            previousTargetPosition = target.position;
            previousTargetRotation = target.rotation;

            previousVelocity = Vector3.zero;
        }
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // ==================================================
        // TRANSLATION REACTION
        // ==================================================

        Vector3 newVelocity =
            (target.position - previousTargetPosition) / dt;

        Vector3 acceleration =
            (newVelocity - previousVelocity) / dt;

        previousTargetPosition = target.position;
        previousVelocity = newVelocity;

        // Convert acceleration into target-local space
        Vector3 localAcceleration =
            target.InverseTransformDirection(acceleration);

        Vector3 desiredOffset =
            localAcceleration * reactionStrength;

        desiredOffset = Vector3.ClampMagnitude(
            desiredOffset,
            maxReaction
        );

        reactionOffset = Vector3.SmoothDamp(
            reactionOffset,
            desiredOffset,
            ref reactionVelocity,
            reactionSmoothTime
        );

        transform.localPosition = reactionOffset;

        // ==================================================
        // ROTATION REACTION
        // ==================================================

        Quaternion deltaRotation =
            target.rotation *
            Quaternion.Inverse(previousTargetRotation);

        deltaRotation.ToAngleAxis(
            out float angle,
            out Vector3 axis
        );

        // Convert 0-360 angle to -180..180
        if (angle > 180f)
            angle -= 360f;

        // Angular velocity in degrees/second
        Vector3 angularVelocity =
            axis * angle / dt;

        // Convert to target-local space
        Vector3 localAngularVelocity =
            target.InverseTransformDirection(angularVelocity);

        // React opposite to the target's rotation.
        //
        // X = pitch
        // Y = yaw
        // Z = roll
        Vector3 desiredRotationOffset =
            -localAngularVelocity * rotationReactionStrength;

        desiredRotationOffset = Vector3.ClampMagnitude(
            desiredRotationOffset,
            maxRotationReaction
        );

        rotationReactionOffset = Vector3.SmoothDamp(
            rotationReactionOffset,
            desiredRotationOffset,
            ref rotationReactionVelocity,
            rotationReactionSmoothTime
        );

        previousTargetRotation = target.rotation;
        
        transform.Rotate(rotationReactionOffset);

        // ==================================================
        // ROTATION
        // ==================================================

        if (followRotation)
        {
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                camera.rotation,
                rotationFollowSpeed * dt
            );
        }
    }
}
