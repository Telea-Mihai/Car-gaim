using System;
using EasyRoads3Dv3;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Driver))]
public class Police : MonoBehaviour
{
    [Header("Target")]
    public Vehicle target;

    [Header("Road Network")]
    [SerializeField] private float roadSearchRadius = 50f;
    [SerializeField] private float roadWaypointSpacing = 15f;
    [SerializeField] private float onRoadThreshold = 8f;
    [SerializeField] private int lookAheadPoints = 6;

    [Header("Navigation Distances")]
    [SerializeField] private float waypointReachDistance = 10f;
    [SerializeField] private float finalApproachDistance = 60f;
    [SerializeField] private float finalApproachResumeDistance = 90f;

    [Header("NavMesh")]
    [SerializeField] private float repathInterval = 0.25f;
    [SerializeField] private float startSampleRadius = 4f;
    [SerializeField] private float targetSampleRadius = 8f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;
    [SerializeField] private Transform referenceTransform = null;

    private enum NavState
    {
        RoadFollowing,
        NavmeshToRoad,
        NavmeshFinal
    }

    private NavState state = NavState.RoadFollowing;
    private Driver driver;
    private ERRoadNetwork roadNetwork;
    private PathBuffer buffer;

    private Vector3[] roadWaypoints = Array.Empty<Vector3>();
    private int pathIndex = 0;

    private NavMeshPath navPath;
    private Vector3[] navCorners = Array.Empty<Vector3>();
    private int navCornerIndex = 0;

    private float nextRepathTime;
    private Vector3 lastTargetPosition;
    private Vector3 lastSelfPosition;

    private ERRoad lastStartRoad;
    private ERRoad lastTargetRoad;


    void Awake()
    {
        driver = GetComponent<Driver>();
        navPath = new NavMeshPath();

        if (roadNetwork == null)
            roadNetwork = new ERRoadNetwork();
    }

    void Start()
    {
    }


    void Update()
    {
        if (driver == null)
            return;

        if (target == null)
        {
            ClearNavigation();
            return;
        }

        Vector3 targetPos = target.transform.position;
        Vector3 selfPos = GetReferencePosition();

        AdvanceIndex(selfPos);

        bool selfMoved = (selfPos - lastSelfPosition).sqrMagnitude > 1f;
        bool targetMoved = (targetPos - lastTargetPosition).sqrMagnitude > 4f;
        bool timeExpired = Time.time >= nextRepathTime;

        if (targetMoved || selfMoved || lastStartRoad == null)
            TryBuildRoadPath(selfPos, targetPos);

        if (timeExpired || targetMoved || selfMoved)
            TryRecalculateNavPath(selfPos, targetPos);

        UpdateState(selfPos, targetPos);
        UpdateDriver(selfPos, targetPos);

        if (debugLogging)
            Debug.Log($"[Police] state={state} dist={Vector3.Distance(selfPos, targetPos):F0} " +
                      $"roadWps={roadWaypoints.Length} pathIdx={pathIndex} " +
                      $"navCorners={navCorners.Length} navIdx={navCornerIndex}");

        lastSelfPosition = selfPos;
        lastTargetPosition = targetPos;
    }


    private void ClearNavigation()
    {
        roadWaypoints = Array.Empty<Vector3>();
        navCorners = Array.Empty<Vector3>();
        state = NavState.RoadFollowing;
        driver.pathBuffer = default;
    }


    private void AdvanceIndex(Vector3 selfPos)
    {
        if (roadWaypoints.Length != 0)
        {
            while (pathIndex < roadWaypoints.Length - 1 &&
                   Vector3.Distance(selfPos, roadWaypoints[pathIndex]) <= waypointReachDistance)
                pathIndex++;
        }

        if (navCorners.Length != 0)
        {
            navCornerIndex = Mathf.Clamp(navCornerIndex, 0, navCorners.Length - 1);

            while (navCornerIndex < navCorners.Length - 1 &&
                   Vector3.Distance(selfPos, navCorners[navCornerIndex]) <= waypointReachDistance)
                navCornerIndex++;
        }
    }


    private Vector3[] SliceLookahead(Vector3[] sourcePoints, int fromIndex, Vector3 finalTarget)
    {
        if(sourcePoints.Length == 0) return new Vector3[] {finalTarget};
        
        int start = Mathf.Clamp(fromIndex, 0, sourcePoints.Length - 1);
        int end = Math.Min(start + lookAheadPoints, sourcePoints.Length);
        bool reachedEnd = end >= sourcePoints.Length;
        
        int sliceLen = reachedEnd ? (end-start+1):(end-start);
        Vector3[] slice = new Vector3[sliceLen]
            ;
        for(int i=0; i<end-start; i++)
            slice[i] = sourcePoints[start + i];
        
        if(reachedEnd)
            slice[sliceLen - 1] = finalTarget;
        
        return slice;
    }


    private void UpdateState(Vector3 selfPos, Vector3 targetPos)
    {
        bool selfOnRoad = IsNearRoad(selfPos, onRoadThreshold);
        bool targetOnRoad = IsNearRoad(targetPos, onRoadThreshold);
        float dist = Vector3.Distance(selfPos, targetPos);

        switch (state)
        {
            case NavState.RoadFollowing:
                if (!selfOnRoad)
                {
                    state = NavState.NavmeshToRoad;
                    RecalculateNavPathToNearestRoad(selfPos, targetPos);
                    if (debugLogging) Debug.Log("[Police] -> NavmeshToRoad");
                    break;
                }

                if (!targetOnRoad && dist <= finalApproachDistance)
                {
                    state = NavState.NavmeshFinal;
                    TryRecalculateNavPath(selfPos, targetPos);
                    if (debugLogging) Debug.Log("[Police] -> NavmeshFinal");
                }
                break;

            case NavState.NavmeshToRoad:
                if (selfOnRoad)
                {
                    state = NavState.RoadFollowing;
                    lastStartRoad = null;
                    pathIndex = 0;
                    TryBuildRoadPath(selfPos, targetPos);
                    if (debugLogging) Debug.Log("[Police] -> RoadFollowing (rejoined)");
                }
                break;

            case NavState.NavmeshFinal:
                if (targetOnRoad || dist >= finalApproachResumeDistance)
                {
                    state = NavState.RoadFollowing;
                    lastStartRoad = null;
                    TryBuildRoadPath(selfPos, targetPos);
                    if (debugLogging) Debug.Log("[Police] -> RoadFollowing (target on road / too far)");
                }
                break;
        }
    }


    private void UpdateDriver(Vector3 selfPos, Vector3 targetPos)
    {
        switch (state)
        {
            case NavState.RoadFollowing:
                FollowRoadWaypoints(selfPos, targetPos);
                break;
            case NavState.NavmeshToRoad:
            case NavState.NavmeshFinal:
                FollowNavMeshPath(selfPos, targetPos);
                break;
        }
    }

    private void FollowRoadWaypoints(Vector3 selfPos, Vector3 targetPos)
    {
        Vector3[] slice = SliceLookahead(roadWaypoints, pathIndex, targetPos);
        //driver.pathBuffer = new PathBuffer(slice, 100, false, 1);
    }

    private void FollowNavMeshPath(Vector3 selfPos, Vector3 targetPos)
    {
        Vector3[] slice = SliceLookahead(navCorners, navCornerIndex, targetPos);
        //driver.pathBuffer = new PathBuffer(slice, 100, false, 1);
    }


    private void TryBuildRoadPath(Vector3 selfPos, Vector3 targetPos)
    {
        if (roadNetwork == null)
            return;

        ERRoad[] roads = roadNetwork.GetRoadObjects();
        if (roads == null || roads.Length == 0)
            return;

        ERRoad startRoad = FindClosestRoad(selfPos, roads);
        ERRoad targetRoad = FindClosestRoad(targetPos, roads);

        if (startRoad == null || targetRoad == null)
            return;

        if (startRoad == lastStartRoad && targetRoad == lastTargetRoad)
            return;

        lastStartRoad = startRoad;
        lastTargetRoad = targetRoad;

        Vector3[] newWaypoints = BuildRoadWaypoints(startRoad, targetRoad, selfPos, targetPos);
        if (newWaypoints.Length == 0)
            return;

        roadWaypoints = newWaypoints;
        pathIndex = 0;
    }

    private Vector3[] BuildRoadWaypoints(ERRoad startRoad, ERRoad targetRoad, Vector3 selfPos, Vector3 targetPos)
    {
        Vector3[] startPts = startRoad.GetSplinePointsCenter();
        if (startPts == null || startPts.Length == 0)
            return Array.Empty<Vector3>();

        var waypoints = new System.Collections.Generic.List<Vector3>();
        int step = Mathf.Max(1, Mathf.RoundToInt(roadWaypointSpacing / 5f));
        int startIdx = FindClosestSplineIndex(startPts, selfPos);

        Vector3 toTarget = (targetPos - selfPos).normalized;
        Vector3 forwardDir = startPts.Length > 1
            ? (startPts[Mathf.Min(startIdx + 1, startPts.Length - 1)] - startPts[Mathf.Max(startIdx - 1, 0)]).normalized
            : Vector3.zero;

        bool walkForward = Vector3.Dot(toTarget, forwardDir) >= 0f;

        if (walkForward)
        {
            for (int i = startIdx; i < startPts.Length; i += step)
                waypoints.Add(startPts[i]);
        }
        else
        {
            for (int i = startIdx; i >= 0; i -= step)
                waypoints.Add(startPts[i]);
        }

        if (startRoad != targetRoad)
        {
            Vector3[] targetPts = targetRoad.GetSplinePointsCenter();
            if (targetPts != null && targetPts.Length > 0)
            {
                Vector3 junction = waypoints.Count > 0 ? waypoints[waypoints.Count - 1] : selfPos;
                int targetStartIdx = FindClosestSplineIndex(targetPts, junction);

                Vector3 targetRoadForward = targetPts.Length > 1
                    ? (targetPts[Mathf.Min(targetStartIdx + 1, targetPts.Length - 1)] -
                       targetPts[Mathf.Max(targetStartIdx - 1, 0)]).normalized
                    : Vector3.zero;

                bool targetWalkForward = Vector3.Dot((targetPos - junction).normalized, targetRoadForward) >= 0f;

                if (targetWalkForward)
                {
                    for (int i = targetStartIdx; i < targetPts.Length; i += step)
                        waypoints.Add(targetPts[i]);
                }
                else
                {
                    for (int i = targetStartIdx; i >= 0; i -= step)
                        waypoints.Add(targetPts[i]);
                }
            }
        }

        waypoints.Add(targetPos);
        return waypoints.ToArray();
    }

    private ERRoad FindClosestRoad(Vector3 position, ERRoad[] roads)
    {
        ERRoad closest = null;
        float minDist = roadSearchRadius;

        foreach (ERRoad road in roads)
        {
            if (road == null)
                continue;

            Vector3[] pts = road.GetSplinePointsCenter();
            if (pts == null)
                continue;

            foreach (Vector3 pt in pts)
            {
                float d = Vector3.Distance(position, pt);
                if (d < minDist)
                {
                    minDist = d;
                    closest = road;
                }
            }
        }

        return closest;
    }

    private static int FindClosestSplineIndex(Vector3[] points, Vector3 position)
    {
        int best = 0;
        float min = float.MaxValue;

        for (int i = 0; i < points.Length; i++)
        {
            float d = Vector3.Distance(position, points[i]);
            if (d < min)
            {
                min = d;
                best = i;
            }
        }

        return best;
    }

    private void TryRecalculateNavPath(Vector3 selfPos, Vector3 targetPos)
    {
        if (Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + repathInterval;
        CalculateNavPath(selfPos, targetPos);
    }

    private void RecalculateNavPathToNearestRoad(Vector3 selfPos, Vector3 fallbackTarget)
    {
        if (!TryGetNearestRoadNavPoint(selfPos, out Vector3 roadPoint))
        {
            CalculateNavPath(selfPos, fallbackTarget);
            return;
        }

        CalculateNavPath(selfPos, roadPoint);
    }

    private void CalculateNavPath(Vector3 selfPos, Vector3 dest)
    {
        navCorners = Array.Empty<Vector3>();
        navCornerIndex = 0;

        if (!TrySampleNavMesh(selfPos, startSampleRadius, out Vector3 sampledSelf))
            return;
        if (!TrySampleNavMesh(dest, targetSampleRadius, out Vector3 sampledDest))
            return;

        if (!NavMesh.CalculatePath(sampledSelf, sampledDest, NavMesh.AllAreas, navPath))
            return;
        if (navPath.status == NavMeshPathStatus.PathInvalid)
            return;
        if (navPath.corners == null || navPath.corners.Length == 0)
            return;

        navCorners = navPath.corners;
        navCornerIndex = navCorners.Length > 1 ? 1 : 0;
    }

    private bool TryGetNearestRoadNavPoint(Vector3 selfPos, out Vector3 result)
    {
        result = selfPos;
        if (roadNetwork == null)
            return false;

        ERRoad[] roads = roadNetwork.GetRoadObjects();
        if (roads == null || roads.Length == 0)
            return false;

        float bestDist = float.MaxValue;
        bool found = false;

        foreach (ERRoad road in roads)
        {
            if (road == null)
                continue;

            Vector3[] pts = road.GetSplinePointsCenter();
            if (pts == null)
                continue;

            foreach (Vector3 pt in pts)
            {
                if (!NavMesh.SamplePosition(pt, out NavMeshHit hit, targetSampleRadius, NavMesh.AllAreas))
                    continue;

                float d = Vector3.Distance(selfPos, hit.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    result = hit.position;
                    found = true;
                }
            }
        }

        return found;
    }

    private bool IsNearRoad(Vector3 position, float threshold)
    {
        if (roadNetwork == null)
            return false;

        ERRoad[] roads = roadNetwork.GetRoadObjects();
        if (roads == null)
            return false;

        foreach (ERRoad road in roads)
        {
            if (road == null)
                continue;

            Vector3[] pts = road.GetSplinePointsCenter();
            if (pts == null)
                continue;

            foreach (Vector3 pt in pts)
            {
                if (Vector3.Distance(position, pt) <= threshold)
                    return true;
            }
        }

        return false;
    }

    private bool TrySampleNavMesh(Vector3 position, float radius, out Vector3 sampled)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            sampled = hit.position;
            return true;
        }

        sampled = position;
        return false;
    }

    private Vector3 GetReferencePosition()
        => referenceTransform != null ? referenceTransform.position : transform.position;

private void OnDrawGizmosSelected()
    {
        if (roadWaypoints != null && roadWaypoints.Length > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < roadWaypoints.Length - 1; i++)
                Gizmos.DrawLine(roadWaypoints[i], roadWaypoints[i + 1]);

            Gizmos.color = Color.cyan;
            if (pathIndex < roadWaypoints.Length)
                Gizmos.DrawSphere(roadWaypoints[pathIndex], 1.5f);
        }

        if (navCorners != null && navCorners.Length > 0)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < navCorners.Length - 1; i++)
                Gizmos.DrawLine(navCorners[i], navCorners[i + 1]);

            Gizmos.color = Color.red;
            if (navCornerIndex < navCorners.Length)
                Gizmos.DrawSphere(navCorners[navCornerIndex], 1.5f);
        }

        Vector3 pos = GetReferencePosition();
        Gizmos.color = state == NavState.RoadFollowing ? Color.green :
            state == NavState.NavmeshToRoad ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(pos, 3f);
    }
}
