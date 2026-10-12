using UnityEngine;

public class PlayerLinePath : MonoBehaviour
{
    [Header("References")]
    [SerializeField] BakedRacePath path;
    Transform target;          // the player car
    Rigidbody targetBody;      // for speed

    [Header("Look Ahead")]
    [SerializeField] float minLookAhead = 25f;
    [SerializeField] float maxLookAhead = 90f;
    [SerializeField] float maxSpeedKmh = 120f;
    [SerializeField] float lookAheadSmoothTime = 0.4f;
    [SerializeField] float behindDistance = 3f;

    [Header("Line")]
    [SerializeField] float pointSpacing = 2.5f;
    [SerializeField] float heightOffset = 0.15f;
    [SerializeField] int maxPoints = 128;

    [Header("Ground Snap (optional)")]
    [SerializeField] bool snapToGround;
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float raycastHeight = 3f;

    [SerializeField]LineRenderer line;
    Vector3[] points;
    float pathS;
    int hintSegment = -1;
    bool initialized = false;
    float currentLookAhead;
    float lookAheadVelocity;

    void Awake()
    {
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        points = new Vector3[maxPoints];
        currentLookAhead = minLookAhead;

        target = transform;
        targetBody = target.GetComponent<Rigidbody>();
    }

    public void SetPath(BakedRacePath baked)
    {
        path = baked;
        initialized = true;
        Debug.Log("Set path");
    }

    void LateUpdate()
    {
        if (path == null || !path.IsValid || target == null)
        {
            line.positionCount = 0;
            return;
        }
        
        Vector3 face = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        var sample = BakedPathMath.Project(path, target.position,
            initialized ? hintSegment : -1, initialized ? pathS : -1f, face);

        float jump = initialized
            ? Mathf.Abs(BakedPathMath.ShortestDeltaS(path, pathS, sample.s))
            : 0f;

        if (initialized && jump > 35f && path.TotalLength > 70f)
        {
            sample = BakedPathMath.SampleAtS(path, pathS);
        }
        else
        {
            pathS = sample.s;
        }
        hintSegment = sample.segmentIndex;
        initialized = true;
        
        float speedKmh = targetBody != null ? targetBody.linearVelocity.magnitude * 3.6f : 0f;
        float desired = Mathf.Lerp(minLookAhead, maxLookAhead, Mathf.InverseLerp(0f, maxSpeedKmh, speedKmh));
        currentLookAhead = Mathf.SmoothDamp(currentLookAhead, desired, ref lookAheadVelocity, lookAheadSmoothTime);

        // --- Sample points ---
        float total = currentLookAhead + behindDistance;
        int count = Mathf.Clamp(Mathf.CeilToInt(total / pointSpacing) + 1, 2, maxPoints);
        float step = total / (count - 1);
        float startOffset = -behindDistance;

        for (int i = 0; i < count; i++)
        {
            float s = BakedPathMath.AdvanceS(path, pathS, startOffset + step * i);
            Vector3 p = BakedPathMath.SampleAtS(path, s).position;

            if (snapToGround &&
                Physics.Raycast(p + Vector3.up * raycastHeight, Vector3.down, out var hit,
                    raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                p.y = hit.point.y;
            }

            p.y += heightOffset;
            points[i] = p;
        }

        line.positionCount = count;
        line.SetPositions(points);
    }
}