using UnityEngine;

[RequireComponent(typeof(Driver))]
public class RacePathFollower : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] BakedRacePath path;
    [SerializeField] float steerLookAhead = 18f;
    [SerializeField] float assistLookAhead = 30f;
    [SerializeField] private float speedLookAheadMultiplier = 2f;
    [SerializeField] private float maxSpeed = 120f;
    [SerializeField] float roadWidthFallback = 10f;

    [Header("Debug")]
    [SerializeField] bool debugGizmos;

    Driver driver;
    float pathS;
    int hintSegment;
    bool progressInitialized;
    Vector3[] chasePoints = new Vector3[2];

    public BakedRacePath Path => path;
    public float PathS => pathS;

    public void SetPath(BakedRacePath baked)
    {
        path = baked;
        SyncToNearest();
    }

    public void SnapToS(float s, bool alignFacing = false)
    {
        if (path == null || !path.IsValid)
            return;

        pathS = s;
        if (path.isClosed && path.TotalLength > 0.001f)
        {
            pathS %= path.TotalLength;
            if (pathS < 0f)
                pathS += path.TotalLength;
        }

        var sample = BakedPathMath.SampleAtS(path, pathS);
        hintSegment = sample.segmentIndex;
        progressInitialized = true;

        if (alignFacing && sample.tangent.sqrMagnitude > 0.01f)
        {
            Vector3 flat = Vector3.ProjectOnPlane(sample.tangent, Vector3.up).normalized;
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        }
    }

    public void SyncToNearest()
    {
        if (path == null || !path.IsValid)
            return;

        Vector3 face = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        var sample = BakedPathMath.Project(path, transform.position, -1, -1f, face);
        pathS = sample.s;
        hintSegment = sample.segmentIndex;
        progressInitialized = true;
    }

    void Awake()
    {
        driver = GetComponent<Driver>();
    }

    void Start()
    {
        if (!progressInitialized && path != null && path.IsValid)
            SyncToNearest();
    }

    void Update()
    {
        if (driver == null)
            return;

        if (path == null || !path.IsValid)
        {
            driver.hasTarget = false;
            return;
        }

        Vector3 face = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        var sample = BakedPathMath.Project(path, transform.position, hintSegment, pathS, face);

        float jump = Mathf.Abs(BakedPathMath.ShortestDeltaS(path, pathS, sample.s));
        if (jump > 35f && path.TotalLength > 70f)
            sample = BakedPathMath.SampleAtS(path, pathS);
        else
            pathS = sample.s;

        hintSegment = sample.segmentIndex;
        
        float assistDist = assistLookAhead * speedLookAheadMultiplier * Mathf.InverseLerp(30, maxSpeed, driver.rb.linearVelocity.magnitude * 3.6f);
        float steerDist = Mathf.Max(4f, steerLookAhead);
        BakedPathMath.PathSample chaseSample = BakedPathMath.SampleAtS(path, BakedPathMath.AdvanceS(path, pathS, steerDist));
        BakedPathMath.PathSample assistSample =
            BakedPathMath.SampleAtS(path, BakedPathMath.AdvanceS(path, pathS, assistDist));
        Vector3 chase = chaseSample.position;
        Vector3 assist = assistSample.position;
        
        Vector3 along = Vector3.ProjectOnPlane(sample.tangent, Vector3.up);
        if (along.sqrMagnitude > 0.01f)
        {
            along.Normalize();
            Vector3 toChase = Vector3.ProjectOnPlane(chase - transform.position, Vector3.up);
            if (Vector3.Dot(toChase, along) < 2f)
            {
                steerDist += 20f;
                chaseSample = BakedPathMath.SampleAtS(path, BakedPathMath.AdvanceS(path, pathS, steerDist));
                assistSample = BakedPathMath.SampleAtS(path, BakedPathMath.AdvanceS(path, pathS, assistDist));
                chase = chaseSample.position;
                assist = assistSample.position;
            }
        }

        float y = transform.position.y;
        chase.y = y;
        assist.y = y;
        chasePoints[0] = chase;
        chasePoints[1] = assist;
        
        Vector3 tangent = assistSample.tangent;
        Vector3 carForward = transform.forward;
        
        float roadAngle = Vector3.SignedAngle(carForward, tangent, Vector3.up);
        
        driver.pathBuffer = new PathBuffer(chasePoints, 0f,Mathf.Abs(roadAngle), false, 1f, roadWidthFallback);
        driver.hasTarget = true;
        driver.allowReverse = false;
    }

    void OnDrawGizmosSelected()
    {
        if (!debugGizmos || path == null || !path.IsValid)
            return;

        path.DrawGizmos(new Color(0.2f, 0.8f, 1f, 0.5f), 0.2f);

        var sample = BakedPathMath.SampleAtS(path, pathS);
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(sample.position, 1.2f);
        Gizmos.DrawLine(transform.position, sample.position);
    }
}
