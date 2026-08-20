using System;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Vehicle))]
public class Driver : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] public PathBuffer pathBuffer;

    private int pathIndex;
    public bool hasTarget;
    public bool allowReverse = false;
    [SerializeField] private float arrivalDistance = 8f;
    [SerializeField] private float reverseDistance = 18f;

    [Header("Driving")]
    [Header("Speed calculation (km/h)")]
    [SerializeField] private float maxForwardSpeed = 70f;
    [SerializeField] private float maxReverseSpeed = 20f;
    public float cornerSpeed = 90f;
    public float minCornerSpeed = 12f;
    [SerializeField] private float turnAngle = 70f;
    [SerializeField] private float slowTurnAngle = 100f;
    [SerializeField] private float maxTurnAngle = 280f;
    [SerializeField] private float handbrakeTurnAngle = 95f;
    
    [Header("Steering")]
    [SerializeField] private float steeringResponseAngle = 45f;
    [SerializeField] private int steerPointIndex = 2;
    [SerializeField] private SteeringPID steeringPID = new SteeringPID();
    
    [Header("Braking")]
    [SerializeField] private float slowTurnStoppingDistance = 10f;
    [SerializeField] private float maxStoppingDistance = 1700f;

    [Header("Obstacle Avoidance")] 
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask otherCarLayer;
    [SerializeField] private float obstacleDetectionDistance=20f;
    [SerializeField] private float speedFactor = 0.5f;
    [SerializeField] private float obstacleDetectionRadius=0.7f;
    [SerializeField] private float obstacleAvoidanceWeight=1f;
    [SerializeField] private float sensorOffset=1.2f;
    [SerializeField] private float sensorArc=20f;
    [SerializeField] private float vehicleMargin=1.5f;
    [Header("Other Vehicle Detection")]
    [SerializeField] private float otherCarDetectionDistance=10f;
    [SerializeField] private float separationDistance = 4f;
    [SerializeField] private float vehiclePredictionTime = 2.5f;
    [SerializeField] private float vehicleAvoidanceWeight = 0.8f;
    [SerializeField] private float maximumAvoidanceOffset = 4f;
    [SerializeField] private float avoidanceOffsetResponse = 5f;
    [SerializeField] private float minimumAvoidanceClearance = 2f;
    [SerializeField] private float avoidanceSpeedReduction = 0.8f;
    
    [SerializeField] private bool debug;
    private int apexIndex;

    private Vehicle vehicle;
    public Rigidbody rb;

    private float distanceToBend;
    private float driveSpeed = 0;
    private float currentObstacleAvoidanceOffset = 0f;
    private float currentVehicleAvoidanceOffset = 0f;
    private Vector3 lastOffsetPursuitTarget;

    struct ObstacleInfo
    {
        public bool detected;
        
        public float distance;
        public float lateralOffset;
        
        public bool leftBlocked;
        public float leftClearance;
        
        public bool rightBlocked;
        public float rightClearance;

        public float detectionDistance;
    }
    
    [System.Serializable]
    public struct VehicleThreat
    {
        public Vehicle vehicle;
        public float distance;
        public float timeToCollision;
        public float lateralOffset;
        public bool isAhead;
        public float closingSpeed;
        public float threat;
    }
    
    private ObstacleInfo lastObstacleInfo;
    private VehicleThreat[] lastThreats = System.Array.Empty<VehicleThreat>();

    void Awake()
    {
        vehicle = GetComponent<Vehicle>();
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (vehicle == null || rb == null)
            return;

        if (!hasTarget)
        {
            ApplyInputs(0f, 0f, 0f, 0f);
            return;
        }

        DriveTowardsTarget();
    }

    public void ResetPathIndex()
    {
        pathIndex = 0;
    }

    
    private void DriveTowardsTarget()
    {
        if (pathBuffer.points == null || pathBuffer.points.Length == 0)
        {
            HoldPosition();
            return;
        }

        if (Vector3.Distance(transform.position, pathBuffer.points[pathIndex]) < 3f)
        {
            pathIndex = Mathf.Min(pathIndex + 1, pathBuffer.points.Length - 1);
        }

        Vector3 finalTarget = pathBuffer.points[pathBuffer.points.Length - 1];
        float distanceToEnd = Vector3.Distance(
            Vector3.ProjectOnPlane(transform.position, Vector3.up),
            Vector3.ProjectOnPlane(finalTarget, Vector3.up));

        if (distanceToEnd <= arrivalDistance && pathBuffer.points.Length > 3)
        {
            HoldPosition();
            return;
        }

        int steerIdx = pathBuffer.points.Length <= 2
            ? 0
            : Mathf.Clamp(steerPointIndex, 1, pathBuffer.points.Length - 1);
        steerIdx = Mathf.Clamp(steerIdx, 0, pathBuffer.points.Length - 1);
        Vector3 pursuitTarget = pathBuffer.points[steerIdx];

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        bool reversing = false;
        Vector3 driveDir = reversing ? -flatForward : flatForward;
        driveSpeed = Vector3.Dot(rb.linearVelocity, driveDir) * 3.6f;

        ObstacleInfo obstacle = DetectStaticObstacles();
        VehicleThreat[] threats = DetectVehicles();

        lastObstacleInfo = obstacle;
        lastThreats = threats;

        float desiredObstacleOffset = CalculateObstacleAvoidanceOffset(obstacle);
        float desiredVehicleOffset = CalculateVehicleAvoidanceOffset(threats);

        currentObstacleAvoidanceOffset = Mathf.MoveTowards(
            currentObstacleAvoidanceOffset,
            desiredObstacleOffset,
            avoidanceOffsetResponse * Time.fixedDeltaTime);

        currentVehicleAvoidanceOffset = Mathf.MoveTowards(
            currentVehicleAvoidanceOffset,
            desiredVehicleOffset,
            avoidanceOffsetResponse * Time.fixedDeltaTime);

        float combinedAvoidanceOffset =
            currentObstacleAvoidanceOffset * obstacleAvoidanceWeight +
            currentVehicleAvoidanceOffset * vehicleAvoidanceWeight;

        Vector3 offsetPursuitTarget = pursuitTarget + flatRight * combinedAvoidanceOffset;
        lastOffsetPursuitTarget = offsetPursuitTarget;
        Vector3 flatToTarget = Vector3.ProjectOnPlane(offsetPursuitTarget - transform.position, Vector3.up);
        if (flatToTarget.sqrMagnitude < 0.01f)
        {
            flatToTarget = flatForward;
        }
        else
        {
            flatToTarget = flatToTarget.normalized;
        }

        float signedAngle = Vector3.SignedAngle(flatForward, flatToTarget, Vector3.up);

        float steering = steeringPID.Update(signedAngle, Time.fixedDeltaTime);
        if (reversing)
            steering = -steering;

        steering = Mathf.Clamp(steering, -1f, 1f);

        if (debug)
        {
            Debug.Log(
                $"Avoidance offset: {combinedAvoidanceOffset:F2}m, steer angle: {signedAngle:F1}°, input: {steering:F2}");
        }

        GetCurve();
        float targetSpeed = GetTargetSpeed(pathBuffer.roadAngle);
        // if (debug)
        //     Debug.Log(">Bend + Speed + Distance:" + pathBuffer.roadAngle + "::" + targetSpeed + "::" + distanceToBend);

        float speedError = targetSpeed - driveSpeed;

        if (pathBuffer.emergencyBrake)
        {
            ApplyInputs(steering, 0f, 1f, 0f);
            vehicle.CurrentGear = 0;
            return;
        }

        float throttle = speedError > 1f ? Mathf.Clamp01(speedError * speedError / Mathf.Max(1f, targetSpeed)) : 0f;
        float brake = speedError < -1f ? Mathf.Clamp01(-speedError / Mathf.Max(1f, targetSpeed)) : 0f;
        float handbrake = 0;

        if (reversing)
            vehicle.CurrentGear = -1;
        else if(!reversing && vehicle.CurrentGear == -1)
            vehicle.CurrentGear = 0;

        float avoidanceSpeed = ModifySpeedForAvoidance(targetSpeed, obstacle, threats);
        
        speedError = avoidanceSpeed - driveSpeed;
        
        throttle = speedError > 1f ? Mathf.Clamp01(speedError * speedError / Mathf.Max(1f, avoidanceSpeed)) : 0f;
        brake = speedError < -1f ? Mathf.Clamp01(-speedError / Mathf.Max(1f, avoidanceSpeed)) : 0f;
        
        ApplyInputs(steering, throttle, brake, handbrake);
    }
    
    
    //Useful for onTheFly driving
    private float GetCurve()
    {
        if (pathBuffer.points.Length < 3)
        {
            distanceToBend = 0f;
            apexIndex = 0;
            return 0f;
        }

        float accumulated = 0f;
        float totalAngle = 0f;
        float sharpestAngle = 0f;
        int sharpestVertex = 1;
        float distToSharpest = 0f;

        for (int i = 0; i < pathBuffer.points.Length - 2; i++)
        {
            Vector3 a = Vector3.ProjectOnPlane(pathBuffer.points[i + 1] - pathBuffer.points[i], Vector3.up);
            Vector3 b = Vector3.ProjectOnPlane(pathBuffer.points[i + 2] - pathBuffer.points[i + 1], Vector3.up);
            accumulated += Vector3.Distance(
                Vector3.ProjectOnPlane(pathBuffer.points[i], Vector3.up),
                Vector3.ProjectOnPlane(pathBuffer.points[i + 1], Vector3.up));

            if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f)
                continue;

            float angle = Mathf.Abs(Vector3.Angle(a, b));
            totalAngle += angle;

            if (angle > sharpestAngle)
            {
                sharpestAngle = angle;
                sharpestVertex = i + 1;
                distToSharpest = accumulated;
            }
        }

        apexIndex = sharpestVertex;
        distanceToBend = distToSharpest;
        return totalAngle;
    }

    private ObstacleInfo DetectStaticObstacles()
    {
        ObstacleInfo info = new ObstacleInfo();
        info.detected = true;

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        float speedMS = Mathf.Abs(driveSpeed) / 3.6f;

        float detectionDistance = obstacleDetectionDistance + speedMS * speedFactor;
        info.detectionDistance = detectionDistance;
        
        Vector3 center = transform.position + Vector3.up * 0.7f;

        bool centerHit = Physics.SphereCast(center, obstacleDetectionRadius, flatForward, out RaycastHit centerResult,
            detectionDistance, obstacleLayer, QueryTriggerInteraction.Ignore);
        
        Vector3 leftOrigin =
            center - flatRight * sensorOffset;

        Vector3 rightOrigin =
            center + flatRight * sensorOffset;

        Vector3 leftDirection =
            Quaternion.AngleAxis(-sensorArc, Vector3.up) * flatForward;

        Vector3 rightDirection =
            Quaternion.AngleAxis(sensorArc, Vector3.up) * flatForward;

        bool leftHit = Physics.SphereCast(
            leftOrigin,
            obstacleDetectionRadius,
            leftDirection,
            out RaycastHit leftResult,
            detectionDistance,
            obstacleLayer,
            QueryTriggerInteraction.Ignore);

        bool rightHit = Physics.SphereCast(
            rightOrigin,
            obstacleDetectionRadius,
            rightDirection,
            out RaycastHit rightResult,
            detectionDistance,
            obstacleLayer,
            QueryTriggerInteraction.Ignore);
        
        info.leftBlocked = leftHit;
        info.rightBlocked = rightHit;
        info.leftClearance = leftHit ? leftResult.distance : detectionDistance;
        info.rightClearance = rightHit ? rightResult.distance : detectionDistance;

        if(!centerHit && !leftHit && !rightHit)
        {
            info.detected = false;
            return info;
        }
        
        float closestDistance = detectionDistance;
        Vector3 closestPoint = transform.position;
        
        if(centerHit && centerResult.distance < closestDistance)
        {
            closestDistance = centerResult.distance;
            closestPoint = centerResult.point;
        }
        
        if(leftHit && leftResult.distance < closestDistance)
        {
            closestDistance = leftResult.distance;
            closestPoint = leftResult.point;
        }

        if (rightHit && rightResult.distance < closestDistance)
        {
            closestDistance = rightResult.distance;
            closestPoint = rightResult.point;
        }
        
        info.distance = closestDistance;
        
        info.lateralOffset = Vector3.Dot(closestPoint-transform.position, flatRight);

        return info;
    }

    private VehicleThreat[] DetectVehicles()
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        float speedMS = Mathf.Abs(driveSpeed) / 3.6f;
        
        float detectionRadius = otherCarDetectionDistance + speedMS * speedFactor;
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, otherCarLayer, QueryTriggerInteraction.Ignore);
        
        List<VehicleThreat> threats = new List<VehicleThreat>();
        
        HashSet<Vehicle> processed = new HashSet<Vehicle>();

        foreach (Collider other in colliders)
        {
            Vehicle otherVehicle = other.GetComponent<Vehicle>();
            if(otherVehicle == null)
                    otherVehicle = other.GetComponentInParent<Vehicle>();
            
            if(otherVehicle==null)
                continue;
            
            if(otherVehicle == vehicle)
                continue;
            
            if(!processed.Add(otherVehicle))
                continue;

            Rigidbody otherRB = otherVehicle.rb;

            if (otherRB == null)
                continue;
            
            Vector3 relativePosition = Vector3.ProjectOnPlane(otherRB.position - transform.position, Vector3.up);
            
            float forwardDistance = Vector3.Dot(relativePosition, flatForward);
            float lateralOffset = Vector3.Dot(relativePosition, flatRight);
            if(forwardDistance < -separationDistance)
                continue;
            
            Vector3 relativeVelocity = Vector3.ProjectOnPlane(otherRB.linearVelocity - rb.linearVelocity, Vector3.up);
            
            float closingSpeed = Vector3.Dot(rb.linearVelocity-otherRB.linearVelocity, flatForward);

            float timeToCollision = Mathf.Infinity;
            
            if(closingSpeed > 0.1f && forwardDistance > 0f)
            {
                timeToCollision = forwardDistance / closingSpeed;
            }

            float closestApproachTime = 0f;

            if (relativeVelocity.sqrMagnitude > 0.01f)
            {
                closestApproachTime = Mathf.Clamp(-Vector3.Dot(relativeVelocity, relativePosition)/relativeVelocity.sqrMagnitude, 0f,  vehiclePredictionTime);
            }
            
            Vector3 predictedPosition = relativePosition + relativeVelocity * closestApproachTime;
            
            float predictedDistance = predictedPosition.magnitude;

            float combinedRadius = vehicleMargin + 1.5f;
            
            bool predictedCollision = predictedDistance < combinedRadius;

            // Side-by-side vehicles that aren't on a collision course are ignored.
            if (Mathf.Abs(lateralOffset) > combinedRadius && !predictedCollision)
                continue;
            
            bool isAhead = forwardDistance > 0;
            
            if(!isAhead)
                continue;
            
            if (!predictedCollision && timeToCollision > vehiclePredictionTime && forwardDistance > separationDistance)
                continue;

            float threat = CalculateVehicleThreat(forwardDistance, closingSpeed, timeToCollision, predictedDistance,
                combinedRadius);
            
            VehicleThreat threatInfo =
                new VehicleThreat
                {
                    vehicle = otherVehicle,
                    distance = relativePosition.magnitude,
                    timeToCollision = timeToCollision,
                    lateralOffset = lateralOffset,
                    isAhead = isAhead,
                    closingSpeed = Mathf.Max(0f, closingSpeed),
                    threat = threat
                };

            threats.Add(threatInfo);
        }
        
        return threats.ToArray();
    }
    
    private float CalculateVehicleThreat(
        float forwardDistance,
        float closingSpeed,
        float timeToCollision,
        float predictedDistance,
        float collisionRadius)
    {
        float distanceThreat =
            1f -
            Mathf.InverseLerp(
                separationDistance,
                otherCarDetectionDistance,
                Mathf.Max(0f, forwardDistance));

        float closingThreat =
            Mathf.InverseLerp(
                0f,
                20f,
                closingSpeed);

        float timeThreat = 0f;

        if (!float.IsInfinity(timeToCollision))
        {
            timeThreat =
                1f -
                Mathf.InverseLerp(
                    0f,
                    vehiclePredictionTime,
                    timeToCollision);
        }

        float collisionThreat =
            1f -
            Mathf.InverseLerp(
                0f,
                collisionRadius * 3f,
                predictedDistance);
        
        return Mathf.Clamp01(
            Mathf.Max(
                collisionThreat,
                distanceThreat * 0.3f +
                closingThreat * 0.2f +
                timeThreat * 0.25f));
    }

    private float CalculateObstacleAvoidanceOffset(ObstacleInfo obstacle)
    {
        if (!obstacle.detected)
            return 0f;

        float detectionRange = Mathf.Max(0.1f, obstacle.detectionDistance);
        float threat =
            1f -
            Mathf.InverseLerp(
                0f,
                detectionRange,
                obstacle.distance);

        threat = Mathf.Clamp01(threat);
        float scaledOffset = maximumAvoidanceOffset * threat;

        bool leftAvailable =
            !obstacle.leftBlocked &&
            obstacle.leftClearance >= minimumAvoidanceClearance;

        bool rightAvailable =
            !obstacle.rightBlocked &&
            obstacle.rightClearance >= minimumAvoidanceClearance;

        float desiredOffset;

        if (Mathf.Abs(obstacle.lateralOffset) < 1.5f)
        {
            if (leftAvailable && rightAvailable)
            {
                desiredOffset = obstacle.leftClearance > obstacle.rightClearance
                    ? -scaledOffset
                    : scaledOffset;
            }
            else if (leftAvailable)
            {
                desiredOffset = -scaledOffset;
            }
            else if (rightAvailable)
            {
                desiredOffset = scaledOffset;
            }
            else
            {
                desiredOffset = 0f;
            }
        }
        else
        {
            desiredOffset =
                -Mathf.Sign(obstacle.lateralOffset) *
                scaledOffset;
        }

        return ClampAvoidanceOffset(desiredOffset);
    }

    private float CalculateVehicleAvoidanceOffset(VehicleThreat[] threats)
    {
        float desiredOffset = 0f;
        float strongestThreat = 0f;

        foreach (VehicleThreat threat in threats)
        {
            if (!threat.isAhead)
                continue;

            if (threat.threat < strongestThreat)
                continue;

            float scaledOffset = maximumAvoidanceOffset * threat.threat;

            if (Mathf.Abs(threat.lateralOffset) > 1f)
            {
                strongestThreat = threat.threat;

                desiredOffset =
                    -Mathf.Sign(threat.lateralOffset) *
                    scaledOffset;
            }
            else
            {
                float leftThreat = 0f;
                float rightThreat = 0f;

                foreach (VehicleThreat other in threats)
                {
                    if (other.lateralOffset < 0f)
                        leftThreat = Mathf.Max(leftThreat, other.threat);

                    if (other.lateralOffset > 0f)
                        rightThreat = Mathf.Max(rightThreat, other.threat);
                }

                strongestThreat = threat.threat;

                desiredOffset = leftThreat < rightThreat
                    ? -scaledOffset
                    : scaledOffset;
            }
        }

        return ClampAvoidanceOffset(desiredOffset);
    }

    private float ClampAvoidanceOffset(float desiredOffset)
    {
        float roadHalfWidth = Mathf.Max(0f, pathBuffer.roadWidth * 0.5f);
        float allowedOffset = Mathf.Min(
            maximumAvoidanceOffset,
            Mathf.Max(0f, roadHalfWidth - minimumAvoidanceClearance));

        return Mathf.Clamp(desiredOffset, -allowedOffset, allowedOffset);
    }

    private float ModifySpeedForAvoidance(
        float baseSpeed,
        ObstacleInfo obstacle,
        VehicleThreat[] threats)
    {
        float targetSpeed = baseSpeed;

        /*
         * Static obstacle threat.
         */
        if (obstacle.detected)
        {
            float speedMS =
                Mathf.Abs(driveSpeed) / 3.6f;

            float stoppingDistance =
                speedMS * speedMS /
                Mathf.Max(
                    0.1f,
                    2f * 8f);

            float threatDistance =
                Mathf.Max(
                    obstacle.distance,
                    0.1f);

            float obstacleThreat =
                Mathf.Clamp01(
                    stoppingDistance /
                    threatDistance);

            /*
             * Only reduce speed significantly if the obstacle
             * is actually threatening the current trajectory.
             */
            if (Mathf.Abs(obstacle.lateralOffset) < 2f)
            {
                targetSpeed *=
                    Mathf.Lerp(
                        1f,
                        0.15f,
                        obstacleThreat *
                        avoidanceSpeedReduction);
            }
        }

        /*
         * Vehicle threats.
         */
        foreach (VehicleThreat threat in threats)
        {
            if (!threat.isAhead)
                continue;

            /*
             * A car directly ahead is more important than a car
             * far to the side.
             */
            float lateralFactor =
                1f -
                Mathf.InverseLerp(
                    0f,
                    5f,
                    Mathf.Abs(
                        threat.lateralOffset));

            float threatFactor =
                threat.threat *
                lateralFactor;

            if (threatFactor <= 0f)
                continue;

            float speedMultiplier =
                Mathf.Lerp(
                    1f,
                    0.35f,
                    threatFactor *
                    avoidanceSpeedReduction);

            targetSpeed =
                Mathf.Min(
                    targetSpeed,
                    baseSpeed * speedMultiplier);

            /*
             * If collision is extremely close and we're not
             * successfully moving around the car, brake hard.
             */
            if (threat.timeToCollision < 0.5f &&
                Mathf.Abs(
                    currentObstacleAvoidanceOffset * obstacleAvoidanceWeight +
                    currentVehicleAvoidanceOffset * vehicleAvoidanceWeight) < 0.5f)
            {
                targetSpeed =
                    Mathf.Min(
                        targetSpeed,
                        driveSpeed * 0.3f);
            }
        }

        return Mathf.Clamp(
            targetSpeed,
            0f,
            maxForwardSpeed);
    }

    
    private float GetTargetSpeed(float angle)
    {
        float targetSpeed;
        float crawlFloor = Mathf.Max(maxTurnAngle, slowTurnAngle + 1f);

        if (angle > slowTurnAngle)
        {
            float angleFactor = Mathf.InverseLerp(slowTurnAngle, crawlFloor, angle);
            targetSpeed = Mathf.Lerp(minCornerSpeed, 0f, angleFactor);
        }
        else if (angle > turnAngle)
        {
            float angleFactor = Mathf.InverseLerp(turnAngle, slowTurnAngle, angle);
            targetSpeed = Mathf.Lerp(cornerSpeed, minCornerSpeed, angleFactor);
        }
        else
        {
            float angleFactor = Mathf.InverseLerp(0f, turnAngle, angle);
            targetSpeed = Mathf.Lerp(maxForwardSpeed, cornerSpeed, angleFactor);
        }

        float speedFactor = Mathf.InverseLerp(0f, maxForwardSpeed, driveSpeed);
        float dynamicStoppingDistance = Mathf.Lerp(slowTurnStoppingDistance, maxStoppingDistance, speedFactor);
        float brakeFactor = Mathf.InverseLerp(0f, dynamicStoppingDistance, distanceToBend);

        return Mathf.Lerp(targetSpeed, maxForwardSpeed, brakeFactor);
    }

    private void HoldPosition()
    {
        vehicle.CurrentGear = 0;
        ApplyInputs(0f, 0f, 1f, 0f);
    }

    private void ApplyInputs(float steering, float throttle, float brake, float handbrake)
    {
        vehicle.steeringInput = steering;
        vehicle.throttleInput = throttle;
        vehicle.brakeInput = brake;
        vehicle.handbrakeInput = handbrake;
    }

    private void OnDrawGizmos()
    {
        if (debug)
        {
            if (pathBuffer.points == null)
                return;

            for (int i = 0; i < pathBuffer.points.Length; i++)
            {
                if (apexIndex == i)
                    Gizmos.color = new Color(0f, 0.5f, 0f);
                else
                    Gizmos.color = Color.blue;

                Gizmos.DrawSphere(pathBuffer.points[i], 0.5f);
            }
            
            Vector3 forward =
            Vector3.ProjectOnPlane(
                transform.forward,
                Vector3.up).normalized;

        Vector3 right =
            Vector3.ProjectOnPlane(
                transform.right,
                Vector3.up).normalized;

        float speedMS =
            Mathf.Abs(driveSpeed) / 3.6f;

        float detectionDistance =
            obstacleDetectionDistance +
            speedMS * speedFactor;

        Vector3 center =
            transform.position +
            Vector3.up * 0.7f;

        Vector3 leftOrigin =
            center -
            right * sensorOffset;

        Vector3 rightOrigin =
            center +
            right * sensorOffset;;

        Vector3 leftDirection =
            Quaternion.AngleAxis(
                -sensorArc,
                Vector3.up) * forward;

        Vector3 rightDirection =
            Quaternion.AngleAxis(
                sensorArc,
                Vector3.up) * forward;

        Gizmos.color =
            lastObstacleInfo.detected
                ? Color.red
                : Color.green;

        Gizmos.DrawWireSphere(
            center +
            forward * detectionDistance,
            obstacleDetectionRadius);

        Gizmos.DrawLine(
            center,
            center +
            forward * detectionDistance);

        Gizmos.DrawLine(
            leftOrigin,
            leftOrigin +
            leftDirection *
            detectionDistance);

        Gizmos.DrawLine(
            rightOrigin,
            rightOrigin +
            rightDirection *
            detectionDistance);

        /*
         * Draw offset pursuit target (where the PID actually steers).
         */
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(lastOffsetPursuitTarget, 0.5f);
        Gizmos.DrawLine(transform.position, lastOffsetPursuitTarget);

        /*
         * Draw lateral avoidance displacement.
         */
        Gizmos.color = Color.yellow;

        Vector3 avoidancePoint =
            transform.position +
            right * (currentObstacleAvoidanceOffset + currentVehicleAvoidanceOffset);

        Gizmos.DrawSphere(
            avoidancePoint,
            0.5f);

        Gizmos.DrawLine(
            transform.position,
            avoidancePoint);

        /*
         * Draw detected vehicles.
         */
        if (lastThreats != null)
        {
            foreach (VehicleThreat threat in lastThreats)
            {
                if (threat.vehicle == null)
                    continue;

                Gizmos.color =
                    threat.threat > 0.7f
                        ? Color.red
                        : Color.yellow;

                Gizmos.DrawLine(
                    transform.position,
                    threat.vehicle.transform.position);

                Gizmos.DrawWireSphere(
                    threat.vehicle.transform.position,
                    1f);
            }
        }
            
        }
    }
}

[System.Serializable]
public struct PathBuffer
{
    public Vector3[] points;
    public float roadAngle;
    public float targetSpeed;
    public bool emergencyBrake;
    public float urgency;
    public float roadWidth;

    public PathBuffer(Vector3[] points, float targetSpeed, float roadAngle, bool emergencyBrake, float urgency, float roadWidth = 20f)
    {
        this.points = points;
        this.targetSpeed = targetSpeed;
        this.emergencyBrake = emergencyBrake;
        this.urgency = urgency;
        this.roadWidth = roadWidth;
        this.roadAngle = roadAngle;
    }
}

[System.Serializable]
public class SteeringPID
{
    public float Kp = 2.0f;
    public float Ki = 0.1f;
    public float Kd = 0.05f;
    public float integral = 0f;
    public float previousError = 0f;
    public float maxOutput = 1f;
    
    public float Update(float error, float deltaTime)
    {
        integral += error * deltaTime;
        float derivative = (error - previousError) / deltaTime;
        
        // Clamp integral to prevent windup
        integral = Mathf.Clamp(integral, -10f, 10f);
        
        float output = Kp * error + Ki * integral + Kd * derivative;
        output = Mathf.Clamp(output, -maxOutput, maxOutput);
        
        previousError = error;
        return output;
    }
    
    public void Reset()
    {
        integral = 0f;
        previousError = 0f;
    }
}

