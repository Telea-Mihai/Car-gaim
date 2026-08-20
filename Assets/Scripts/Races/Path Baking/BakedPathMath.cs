using System;
using UnityEngine;

public static class BakedPathMath
{
    public struct PathSample
    {
        public int segmentIndex;
        public float t;
        public float s;
        public Vector3 position;
        public Vector3 tangent;
    }

    public static float ForwardDeltaS(BakedRacePath path, float fromS, float toS)
    {
        if (path == null)
            return 0f;

        float length = path.TotalLength;
        if (length < 0.001f)
            return 0f;

        float d = toS - fromS;
        if (path.isClosed)
        {
            d %= length;
            if (d < 0f)
                d += length;
            return d;
        }

        return d;
    }

    public static float ShortestDeltaS(BakedRacePath path, float fromS, float toS)
    {
        float length = path != null ? path.TotalLength : 0f;
        float d = toS - fromS;
        if (path != null && path.isClosed && length > 0.001f)
        {
            d %= length;
            if (d > length * 0.5f)
                d -= length;
            if (d < -length * 0.5f)
                d += length;
        }

        return d;
    }

    public static PathSample Project(
        BakedRacePath path,
        Vector3 worldPos,
        int hintSegment = -1,
        float previousS = -1f,
        Vector3 preferredForward = default)
    {
        var sample = new PathSample();
        if (path == null || path.PointCount < 2)
            return sample;

        int segCount = path.isClosed ? path.PointCount : path.PointCount - 1;
        if (segCount < 1)
            return sample;

        int searchStart = 0;
        int searchEnd = segCount - 1;
        if (hintSegment >= 0)
        {
            searchStart = Mathf.Max(0, hintSegment - 12);
            searchEnd = Mathf.Min(segCount - 1, hintSegment + 24);
        }

        preferredForward = Vector3.ProjectOnPlane(preferredForward, Vector3.up);
        bool useForward = preferredForward.sqrMagnitude > 0.01f;
        if (useForward)
            preferredForward.Normalize();

        float bestScore = float.MaxValue;
        float bestDist = float.MaxValue;
        int bestSeg = 0;
        float bestT = 0f;

        void Consider(int i)
        {
            Vector3 a = path.points[i];
            Vector3 b = path.points[NextIndex(i, path.PointCount, path.isClosed)];
            Vector3 ab = b - a;
            ab.y = 0f;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return;

            Vector3 ap = worldPos - a;
            ap.y = 0f;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / lenSq);
            Vector3 proj = a + (b - a) * t;
            Vector3 delta = worldPos - proj;
            delta.y = 0f;
            float dist = delta.magnitude;

            float s = SampleS(path, i, t);
            float score = dist;

            if (previousS >= 0f)
            {
                float jump = Mathf.Abs(ShortestDeltaS(path, previousS, s));
                score += jump * 0.35f;
                if (jump > 40f)
                    score += jump;
            }

            if (useForward)
            {
                Vector3 tan = SampleTangent(path, i, t);
                float align = Vector3.Dot(tan, preferredForward);
                score += (1f - align) * 8f;
            }

            if (score < bestScore || (Mathf.Abs(score - bestScore) < 0.01f && dist < bestDist))
            {
                bestScore = score;
                bestDist = dist;
                bestSeg = i;
                bestT = t;
            }
        }

        for (int i = searchStart; i <= searchEnd; i++)
            Consider(i);

        if (path.isClosed && hintSegment >= 0)
        {
            Consider(0);
            Consider(segCount - 1);
        }

        if (hintSegment >= 0 && bestDist > 25f)
            return Project(path, worldPos, -1, previousS, preferredForward);

        sample.segmentIndex = bestSeg;
        sample.t = bestT;
        sample.position = path.points[bestSeg] +
                          (path.points[NextIndex(bestSeg, path.PointCount, path.isClosed)] - path.points[bestSeg]) * bestT;
        sample.s = SampleS(path, bestSeg, bestT);
        sample.tangent = SampleTangent(path, bestSeg, bestT);
        return sample;
    }

    public static float AdvanceS(BakedRacePath path, float s, float deltaMetres)
    {
        if (path == null || path.PointCount < 2)
            return 0f;

        float length = path.TotalLength;
        if (length < 0.001f)
            return 0f;

        float next = s + deltaMetres;
        if (path.isClosed)
        {
            next %= length;
            if (next < 0f)
                next += length;
            return next;
        }

        return Mathf.Clamp(next, 0f, length);
    }

    public static PathSample SampleAtS(BakedRacePath path, float s)
    {
        var sample = new PathSample();
        if (path == null || path.PointCount < 2)
            return sample;

        float length = path.TotalLength;
        if (path.isClosed && length > 0.001f)
        {
            s %= length;
            if (s < 0f)
                s += length;
        }
        else
        {
            s = Mathf.Clamp(s, 0f, length);
        }

        int segCount = path.isClosed ? path.PointCount : path.PointCount - 1;
        for (int i = 0; i < segCount; i++)
        {
            int next = NextIndex(i, path.PointCount, path.isClosed);
            float s0 = path.cumulativeDistance[i];
            float s1;
            if (path.isClosed && next == 0)
                s1 = length;
            else
                s1 = path.cumulativeDistance[Mathf.Clamp(next, 0, path.cumulativeDistance.Length - 1)];

            if (s > s1 && i < segCount - 1)
                continue;

            float span = Mathf.Max(0.0001f, s1 - s0);
            float t = Mathf.Clamp01((s - s0) / span);
            sample.segmentIndex = i;
            sample.t = t;
            sample.s = s;
            sample.position = Vector3.Lerp(path.points[i], path.points[next], t);
            sample.tangent = SampleTangent(path, i, t);
            return sample;
        }

        int last = path.PointCount - 1;
        sample.segmentIndex = Mathf.Max(0, last - 1);
        sample.t = 1f;
        sample.s = length;
        sample.position = path.points[last];
        sample.tangent = path.tangents != null && path.tangents.Length > last ? path.tangents[last] : Vector3.forward;
        return sample;
    }

    public static float LateralDistance(BakedRacePath path, Vector3 worldPos, int hintSegment = -1)
    {
        PathSample sample = Project(path, worldPos, hintSegment);
        Vector3 delta = worldPos - sample.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    public static int NextIndex(int index, int count, bool closed)
    {
        int next = index + 1;
        if (next >= count)
            return closed ? 0 : count - 1;
        return next;
    }

    public static float SampleS(BakedRacePath path, int segment, float t)
    {
        if (path.cumulativeDistance == null || path.cumulativeDistance.Length == 0)
            return 0f;

        segment = Mathf.Clamp(segment, 0, path.cumulativeDistance.Length - 1);
        float s0 = path.cumulativeDistance[segment];
        int next = NextIndex(segment, path.PointCount, path.isClosed);
        float s1;
        if (path.isClosed && next == 0)
            s1 = path.TotalLength;
        else
            s1 = path.cumulativeDistance[Mathf.Clamp(next, 0, path.cumulativeDistance.Length - 1)];

        return Mathf.Lerp(s0, s1, t);
    }

    static Vector3 SampleTangent(BakedRacePath path, int segment, float t)
    {
        if (path.tangents == null || path.tangents.Length == 0)
            return Vector3.forward;

        int next = NextIndex(segment, path.PointCount, path.isClosed);
        Vector3 a = path.tangents[Mathf.Clamp(segment, 0, path.tangents.Length - 1)];
        Vector3 b = path.tangents[Mathf.Clamp(next, 0, path.tangents.Length - 1)];
        Vector3 tan = Vector3.Lerp(a, b, t);
        tan.y = 0f;
        return tan.sqrMagnitude > 0.01f ? tan.normalized : Vector3.forward;
    }

    public static bool RunSelfTest(out string report)
    {
        var fake = ScriptableObject.CreateInstance<BakedRacePath>();
        try
        {
            var pts = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(0, 0, 10),
                new Vector3(0, 0, 20),
                new Vector3(0, 0, 30),
                new Vector3(10, 0, 40)
            };
            var tans = new Vector3[pts.Length];
            var cum = new float[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                if (i == 0)
                    cum[i] = 0f;
                else
                    cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
                tans[i] = i < pts.Length - 1
                    ? (pts[i + 1] - pts[i]).normalized
                    : (pts[i] - pts[i - 1]).normalized;
            }

            fake.SetData(pts, tans, cum, false);

            PathSample mid = Project(fake, new Vector3(1f, 0f, 15f));
            if (Mathf.Abs(mid.s - 15f) > 1.5f)
            {
                report = $"Project failed: expected s~15 got {mid.s}";
                return false;
            }

            float advanced = AdvanceS(fake, 10f, 12f);
            if (Mathf.Abs(advanced - 22f) > 0.5f)
            {
                report = $"AdvanceS failed: expected ~22 got {advanced}";
                return false;
            }

            report = $"OK project.s={mid.s:F1} advance={advanced:F1}";
            return true;
        }
        finally
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(fake);
            else
                UnityEngine.Object.DestroyImmediate(fake);
        }
    }
}
