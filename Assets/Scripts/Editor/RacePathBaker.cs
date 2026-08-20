using System;
using System.Collections.Generic;
using EasyRoads3Dv3;
using UnityEditor;
using UnityEngine;

public static class RacePathBaker
{
    public static bool Bake(RaceManager manager, BakedRacePath asset, RacePathBakeSettings settings, out string message)
    {
        if (manager == null)
        {
            message = "RaceManager is null.";
            return false;
        }

        if (asset == null)
        {
            message = "Assign a BakedRacePath asset first.";
            return false;
        }

        if (manager.Checkpoints == null || manager.Checkpoints.Count < 2)
        {
            message = "Need at least 2 checkpoints.";
            return false;
        }

        int cpCount = manager.Checkpoints.Count;
        bool closed = true;

        settings = Sanitize(settings);

        var network = new ERRoadNetwork();
        ERRoad[] roads = network.GetRoadObjects();
        if (roads == null || roads.Length == 0)
        {
            message = "No EasyRoads roads found in the scene.";
            return false;
        }

        var anchors = new CheckpointAnchor[cpCount];
        for (int i = 0; i < cpCount; i++)
        {
            Checkpoint cp = manager.Checkpoints[i];
            if (cp == null)
            {
                message = $"Checkpoint {i} is null.";
                return false;
            }

            Vector3 hintForward = Vector3.forward;
            int next = (i + 1) % cpCount;
            if (manager.Checkpoints[next] != null)
            {
                hintForward = manager.Checkpoints[next].transform.position - cp.transform.position;
                hintForward.y = 0f;
            }

            if (!TryProjectCheckpoint(cp.transform.position, hintForward, roads, settings, out anchors[i]))
            {
                message = $"Checkpoint {i} is too far from any road (>{settings.roadSearchRadius}m).";
                return false;
            }
        }

        var raw = new List<Vector3>();

        for (int i = 0; i < cpCount; i++)
        {
            if (!closed && i == cpCount - 1)
                break;

            CheckpointAnchor from = anchors[i];
            CheckpointAnchor to = anchors[(i + 1) % cpCount];
            bool skipFirst = raw.Count > 0;

            if (!WalkBetweenAnchors(from, to, raw, skipFirst, isClosingLeg: (i + 1) % cpCount == 0))
            {
                message = $"Failed to build road path between checkpoint {i} and {(i + 1) % cpCount}.";
                return false;
            }
        }

        if (raw.Count < 4)
        {
            message = $"Bake produced too few points ({raw.Count}).";
            return false;
        }

        if (Vector3.Distance(raw[0], raw[raw.Count - 1]) < settings.sampleSpacing * 0.5f)
            raw.RemoveAt(raw.Count - 1);

        List<Vector3> centerline = Resample(raw, settings.sampleSpacing, closed);
        if (centerline.Count < 4)
        {
            message = "Resampled path too short.";
            return false;
        }

        CloseLoopSeam(centerline, settings.sampleSpacing * 1.5f);

        Vector3[] points = centerline.ToArray();
        int n = points.Length;
        var tangents = new Vector3[n];
        var cum = new float[n];

        RebuildTangentsAndCum(points, closed, tangents, cum);

        if (settings.applyRacingLine)
            points = ApplyApexRacingLine(points, tangents, cum, closed, settings);

        RebuildTangentsAndCum(points, closed, tangents, cum);
        n = points.Length;

        asset.SetData(points, tangents, cum, closed);
        AssetDatabase.SaveAssets();
        message =
            $"Baked {n} points, length {asset.TotalLength:F0}m, closed={closed}, " +
            $"racingLine={settings.applyRacingLine}.";
        return true;
    }

    static RacePathBakeSettings Sanitize(RacePathBakeSettings s)
    {
        if (s.sampleSpacing < 0.5f) s.sampleSpacing = 0.5f;
        if (s.roadSearchRadius < 1f) s.roadSearchRadius = 1f;
        if (s.considerApexAngle < 0.5f) s.considerApexAngle = 0.5f;
        if (s.distanceToApexEntry < 1f) s.distanceToApexEntry = 1f;
        if (s.distanceToApexExit < 1f) s.distanceToApexExit = 1f;
        if (s.maxOuterShift < 0f) s.maxOuterShift = 0f;
        if (s.maxInnerShift < 0f) s.maxInnerShift = 0f;
        if (s.apexTransitionDistance < 0.5f) s.apexTransitionDistance = 0.5f;
        if (s.roadWidth < 1f) s.roadWidth = 1f;
        if (s.edgeMargin < 0f) s.edgeMargin = 0f;
        s.racingLineStrength = Mathf.Clamp01(s.racingLineStrength);
        return s;
    }

    struct CornerZone
    {
        public int startIndex;
        public int apexIndex;
        public int endIndex;
        public float sign;      // -1 for Left turn, +1 for Right turn
        public float strength;  // Peak curvature angle in degrees
    }

    /// <summary>
    /// Generates a smooth, arcade/street-racing Out-In-Out line respecting road bounds.
    /// </summary>
    static Vector3[] ApplyApexRacingLine(
        Vector3[] centerline,
        Vector3[] tangents,
        float[] cum,
        bool closed,
        RacePathBakeSettings settings)
    {
        int n = centerline.Length;
        if (n < 4 || settings.racingLineStrength <= 0.001f)
            return centerline;

        float halfLimit = Mathf.Max(0.5f, (settings.roadWidth * 0.5f) - settings.edgeMargin);
        float totalLen = PathLength(cum, centerline, closed);

        Vector3[] leftNormals = BuildLeftNormals(tangents, closed);
        float[] smoothK = BuildSmoothedCurvature(centerline, cum, totalLen, closed, settings);
        List<CornerZone> corners = DetectCornerZones(smoothK, cum, totalLen, closed, settings);

        if (corners.Count == 0)
            return centerline;

        float[] accumOffset = new float[n];
        float[] totalWeight = new float[n];

        float entryDist = settings.distanceToApexEntry;
        float exitDist = settings.distanceToApexExit;
        float transDist = settings.apexTransitionDistance;
        float strength = settings.racingLineStrength;

        // 1. Build smooth Out-In-Out influence fields for every corner
        foreach (CornerZone corner in corners)
        {
            float sStart = cum[corner.startIndex];
            float sApex = cum[corner.apexIndex];
            float sEnd = cum[corner.endIndex];
            float sign = corner.sign;

            // Scale apex shift based on how sharp the corner is
            float curveScale = Mathf.Clamp01((corner.strength - (settings.considerApexAngle * 0.5f)) /
                                             Mathf.Max(settings.considerApexAngle * 1.5f, 1f));
            float innerShift = Mathf.Min(settings.maxInnerShift * curveScale, halfLimit);
            float outerShift = Mathf.Min(settings.maxOuterShift * curveScale, halfLimit);

            // Coordinate System: Left turn (sign = -1) -> inside is Left (+Normal), outside is Right (-Normal).
            //                    Right turn (sign = +1) -> inside is Right (-Normal), outside is Left (+Normal).
            float targetApexOffset = -sign * innerShift;
            float targetOuterOffset = sign * outerShift;

            float reachEntry = entryDist + transDist;
            float reachExit = exitDist + transDist;

            int ptsRange = Mathf.CeilToInt((reachEntry + reachExit + (sEnd - sStart + totalLen) % totalLen) / settings.sampleSpacing) + 4;
            int apexIdx = corner.apexIndex;

            for (int di = -ptsRange; di <= ptsRange; di++)
            {
                int idx = ForwardIndex(apexIdx, di, n, closed);
                float sIdx = cum[idx];

                float dsFromStart = SignedArcDistance(sStart, sIdx, totalLen, closed);
                float dsFromEnd = SignedArcDistance(sEnd, sIdx, totalLen, closed);
                float dsFromApex = SignedArcDistance(sApex, sIdx, totalLen, closed);

                float targetVal = 0f;
                float w = 0f;

                if (dsFromStart < -reachEntry || dsFromEnd > reachExit)
                    continue;

                if (dsFromStart < 0f)
                {
                    // Zone 1: Entry approach on straight -> blend from centerline (0) to outside
                    float t = Mathf.Clamp01((dsFromStart + reachEntry) / Mathf.Max(reachEntry, 0.1f));
                    t = Smooth01(t);
                    targetVal = t * targetOuterOffset;
                    w = t * 0.6f;
                }
                else if (dsFromApex <= 0f)
                {
                    // Zone 2: Turn-in -> smooth sweep from outside edge to inner apex
                    float span = Mathf.Max(SignedArcDistance(sStart, sApex, totalLen, closed), 0.1f);
                    float t = Mathf.Clamp01(dsFromStart / span);
                    t = Smooth01(t);
                    targetVal = Mathf.Lerp(targetOuterOffset, targetApexOffset, t);
                    w = 1.0f;
                }
                else if (dsFromEnd <= 0f)
                {
                    // Zone 3: Track-out -> smooth drift from inner apex to outside exit
                    float span = Mathf.Max(SignedArcDistance(sApex, sEnd, totalLen, closed), 0.1f);
                    float t = Mathf.Clamp01(dsFromApex / span);
                    t = Smooth01(t);
                    targetVal = Mathf.Lerp(targetApexOffset, targetOuterOffset, t);
                    w = 1.0f;
                }
                else
                {
                    // Zone 4: Exit recovery -> blend smoothly from outside back to centerline (0)
                    float t = Mathf.Clamp01(dsFromEnd / Mathf.Max(reachExit, 0.1f));
                    t = Smooth01(t);
                    targetVal = (1f - t) * targetOuterOffset;
                    w = (1f - t) * 0.6f;
                }

                accumOffset[idx] += targetVal * w;
                totalWeight[idx] += w;
            }
        }

        // 2. Normalize blended offsets
        float[] lateral = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (totalWeight[i] > 0.001f)
                lateral[i] = (accumOffset[i] / totalWeight[i]) * strength;
            else
                lateral[i] = 0f;

            lateral[i] = Mathf.Clamp(lateral[i], -halfLimit, halfLimit);
        }

        // 3. Multi-pass smoothing to ensure buttery curvature transitions across straights and chicanes
        for (int pass = 0; pass < 8; pass++)
        {
            float[] smoothed = new float[n];
            for (int i = 0; i < n; i++)
            {
                int prev = ForwardIndex(i, -1, n, closed);
                int next = ForwardIndex(i, 1, n, closed);
                smoothed[i] = (0.25f * lateral[prev]) + (0.5f * lateral[i]) + (0.25f * lateral[next]);
            }
            for (int i = 0; i < n; i++)
                lateral[i] = Mathf.Clamp(smoothed[i], -halfLimit, halfLimit);
        }

        // 4. Construct final racing line points
        var result = new Vector3[n];
        for (int i = 0; i < n; i++)
            result[i] = centerline[i] + (leftNormals[i] * lateral[i]);

        return result;
    }

    static void RebuildTangentsAndCum(Vector3[] points, bool closed, Vector3[] tangents, float[] cum)
    {
        int n = points.Length;
        if (n == 0) return;

        cum[0] = 0f;
        for (int i = 0; i < n; i++)
        {
            int prev = i == 0 ? (closed ? n - 1 : 0) : i - 1;
            int next = i == n - 1 ? (closed ? 0 : n - 1) : i + 1;
            Vector3 tan = points[next] - points[prev];
            tan.y = 0f;
            tangents[i] = tan.sqrMagnitude > 0.001f ? tan.normalized : Vector3.forward;

            if (i > 0)
                cum[i] = cum[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }
    }

    static float[] BuildSmoothedCurvature(
        Vector3[] points,
        float[] cum,
        float totalLen,
        bool closed,
        RacePathBakeSettings settings)
    {
        int n = points.Length;
        var raw = new float[n];

        for (int i = 0; i < n; i++)
        {
            if (!closed && (i == 0 || i >= n - 1))
                continue;

            int prev = ForwardIndex(i, -1, n, closed);
            int next = ForwardIndex(i, 1, n, closed);

            Vector3 a = points[i] - points[prev];
            Vector3 b = points[next] - points[i];
            a.y = 0f;
            b.y = 0f;
            if (a.sqrMagnitude < 0.0001f || b.sqrMagnitude < 0.0001f)
                continue;

            raw[i] = Vector3.SignedAngle(a, b, Vector3.up);
        }

        float window = Mathf.Max(settings.sampleSpacing * 8f, 24f);
        return FastCurvatureFilter(raw, cum, totalLen, window, closed, settings.sampleSpacing);
    }

    static float[] FastCurvatureFilter(
        float[] values,
        float[] cum,
        float totalLen,
        float windowMetres,
        bool closed,
        float spacing)
    {
        int n = values.Length;
        var result = new float[n];
        float halfWin = windowMetres * 0.5f;
        int ptsCheck = Mathf.CeilToInt(halfWin / Mathf.Max(spacing, 0.5f)) + 2;

        for (int i = 0; i < n; i++)
        {
            float sum = 0f;
            float wsum = 0f;
            float s0 = cum[i];

            for (int di = -ptsCheck; di <= ptsCheck; di++)
            {
                int j = ForwardIndex(i, di, n, closed);
                float d = Mathf.Abs(SignedArcDistance(s0, cum[j], totalLen, closed));
                if (d > halfWin)
                    continue;

                float w = 1f - (d / halfWin);
                sum += values[j] * w;
                wsum += w;
            }

            result[i] = wsum > 0.0001f ? sum / wsum : values[i];
        }

        return result;
    }

    static List<CornerZone> DetectCornerZones(
        float[] smoothK,
        float[] cum,
        float totalLen,
        bool closed,
        RacePathBakeSettings settings)
    {
        int n = smoothK.Length;
        float gate = settings.considerApexAngle;
        var zones = new List<CornerZone>();
        var visited = new bool[n];

        for (int i = 0; i < n; i++)
        {
            if (visited[i] || Mathf.Abs(smoothK[i]) < gate)
                continue;

            float sign = Mathf.Sign(smoothK[i]);
            int startIdx = i;
            int steps = 0;

            // Expand backward to find where this corner starts
            while (steps++ < n)
            {
                int prev = ForwardIndex(startIdx, -1, n, closed);
                if (!closed && prev >= startIdx) break;
                if (Mathf.Abs(smoothK[prev]) < gate * 0.5f || Mathf.Sign(smoothK[prev]) != sign)
                    break;
                startIdx = prev;
            }

            // Expand forward to find where this corner ends
            int endIdx = i;
            steps = 0;
            while (steps++ < n)
            {
                int next = ForwardIndex(endIdx, 1, n, closed);
                if (!closed && next <= endIdx) break;
                if (Mathf.Abs(smoothK[next]) < gate * 0.5f || Mathf.Sign(smoothK[next]) != sign)
                    break;
                endIdx = next;
            }

            // Collect all indices in the corner zone and mark visited
            var cornerIndices = new List<int>();
            int curr = startIdx;
            while (true)
            {
                visited[curr] = true;
                cornerIndices.Add(curr);
                if (curr == endIdx) break;
                curr = ForwardIndex(curr, 1, n, closed);
                if (!closed && curr <= startIdx) break;
            }

            // Locate peak curvature point (apex)
            int bestIdx = cornerIndices[0];
            float maxK = 0f;
            foreach (int idx in cornerIndices)
            {
                float ak = Mathf.Abs(smoothK[idx]);
                if (ak > maxK)
                {
                    maxK = ak;
                    bestIdx = idx;
                }
            }

            // Late-apex bias: shift apex 10% toward the exit for earlier acceleration
            int peakPos = cornerIndices.IndexOf(bestIdx);
            int lateShift = Mathf.RoundToInt(cornerIndices.Count * 0.10f);
            int lateApexIdx = cornerIndices[Mathf.Min(cornerIndices.Count - 1, peakPos + lateShift)];

            zones.Add(new CornerZone
            {
                startIndex = startIdx,
                apexIndex = lateApexIdx,
                endIndex = endIdx,
                sign = sign,
                strength = maxK
            });
        }

        return zones;
    }

    static Vector3[] BuildLeftNormals(Vector3[] tangents, bool closed)
    {
        int n = tangents.Length;
        var left = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 tan = tangents[i];
            tan.y = 0f;
            left[i] = tan.sqrMagnitude > 0.001f
                ? Vector3.Cross(tan.normalized, Vector3.up).normalized
                : Vector3.left;

            if (i > 0 && Vector3.Dot(left[i], left[i - 1]) < 0f)
                left[i] = -left[i];
        }

        if (closed && n > 2 && Vector3.Dot(left[0], left[n - 1]) < 0f)
            left[n - 1] = -left[n - 1];

        return left;
    }

    static void CloseLoopSeam(List<Vector3> points, float tolerance)
    {
        if (points == null || points.Count < 2) return;
        if (Vector3.Distance(points[0], points[points.Count - 1]) <= tolerance)
            points[points.Count - 1] = points[0];
    }

    static float PathLength(float[] cum, Vector3[] points, bool closed)
    {
        if (cum == null || cum.Length == 0) return 0f;
        float len = cum[cum.Length - 1];
        if (closed && points.Length >= 2)
            len += Vector3.Distance(points[points.Length - 1], points[0]);
        return len;
    }

    static float SignedArcDistance(float fromS, float toS, float totalLen, bool closed)
    {
        float d = toS - fromS;
        if (!closed || totalLen < 0.001f) return d;
        if (d > totalLen * 0.5f) d -= totalLen;
        if (d < -totalLen * 0.5f) d += totalLen;
        return d;
    }

    static int ForwardIndex(int from, int delta, int n, bool closed)
    {
        if (n <= 0) return 0;
        if (closed) return ((from + delta) % n + n) % n;
        return Mathf.Clamp(from + delta, 0, n - 1);
    }

    static float Smooth01(float t) => t * t * (3f - (2f * t));

    struct CheckpointAnchor
    {
        public ERRoad road;
        public int segmentIndex;
        public float segmentT;
        public Vector3 onRoad;
    }

    static float AnchorParam(CheckpointAnchor anchor) => anchor.segmentIndex + anchor.segmentT;

    static bool TryProjectCheckpoint(
        Vector3 checkpointPos,
        Vector3 hintForward,
        ERRoad[] roads,
        RacePathBakeSettings settings,
        out CheckpointAnchor anchor)
    {
        anchor = default;
        ERRoad bestRoad = null;
        int bestSeg = 0;
        float bestSegT = 0f;
        Vector3 bestOnRoad = checkpointPos;
        float bestScore = float.MaxValue;

        hintForward.y = 0f;
        if (hintForward.sqrMagnitude > 0.01f)
            hintForward.Normalize();

        foreach (ERRoad road in roads)
        {
            if (road == null) continue;
            Vector3[] pts = road.GetSplinePointsCenter();
            if (pts == null || pts.Length < 2) continue;

            if (!ProjectOntoPolyline(pts, checkpointPos, out int seg, out Vector3 onRoad, out float segT, out float dist))
                continue;

            if (dist > settings.roadSearchRadius)
                continue;

            float score = dist;
            if (hintForward.sqrMagnitude > 0.01f)
            {
                Vector3 toProj = onRoad - checkpointPos;
                toProj.y = 0f;
                if (toProj.sqrMagnitude > 0.01f)
                {
                    float ahead = Vector3.Dot(toProj.normalized, hintForward);
                    if (ahead < 0f) score += 6f;
                }
            }

            if (score < bestScore)
            {
                bestScore = score;
                bestRoad = road;
                bestSeg = seg;
                bestSegT = segT;
                bestOnRoad = onRoad;
            }
        }

        if (bestRoad == null) return false;

        anchor.road = bestRoad;
        anchor.segmentIndex = bestSeg;
        anchor.segmentT = bestSegT;
        anchor.onRoad = bestOnRoad;
        return true;
    }

    static bool WalkBetweenAnchors(
        CheckpointAnchor from,
        CheckpointAnchor to,
        List<Vector3> waypoints,
        bool skipFirst,
        bool isClosingLeg = false)
    {
        if (from.road == null || to.road == null) return false;

        if (from.road == to.road)
        {
            Vector3[] pts = from.road.GetSplinePointsCenter();
            if (pts == null || pts.Length < 2) return false;

            float direct = HorizontalDistance(from.onRoad, to.onRoad);
            float incLen = MeasurePolylineArc(pts, from, to, true);
            float decLen = MeasurePolylineArc(pts, from, to, false);
            float roadLen = TotalPolylineLength(pts);
            float bestLen = Mathf.Min(incLen, decLen);

            if (ShouldShortCircuitArc(direct, bestLen, roadLen, isClosingLeg))
            {
                AppendDirectArc(from, to, waypoints, skipFirst);
                return waypoints.Count > 0;
            }

            bool walkIncreasing = incLen <= decLen;
            AppendPolylineArc(pts, from, to, walkIncreasing, waypoints, skipFirst);
            return waypoints.Count > 0;
        }

        Vector3[] startPts = from.road.GetSplinePointsCenter();
        if (startPts == null || startPts.Length < 2) return false;

        Vector3 travel = to.onRoad - from.onRoad;
        travel.y = 0f;
        if (travel.sqrMagnitude < 0.01f)
            travel = GetSplineForwardAt(startPts, from.segmentIndex);

        bool walkInc = Vector3.Dot(travel.normalized, GetSplineForwardAt(startPts, from.segmentIndex)) >= 0f;
        var segment = new List<Vector3>();
        CheckpointAnchor startRoadEnd = VertexAnchor(from.road, startPts, walkInc ? startPts.Length - 1 : 0);
        AppendPolylineArc(startPts, from, startRoadEnd, walkInc, segment, false);

        Vector3[] endPts = to.road.GetSplinePointsCenter();
        if (endPts == null || endPts.Length < 2) return false;

        Vector3 junction = segment.Count > 0 ? segment[segment.Count - 1] : from.onRoad;
        if (!ProjectOntoPolyline(endPts, junction, out int enterSeg, out Vector3 enterOnRoad, out float enterT, out _))
        {
            enterSeg = FindClosestSplineIndex(endPts, junction);
            enterOnRoad = endPts[enterSeg];
            enterT = 0f;
        }

        var enterAnchor = new CheckpointAnchor
        {
            road = to.road,
            segmentIndex = enterSeg,
            segmentT = enterT,
            onRoad = enterOnRoad
        };

        bool walkEndInc = ChooseWalkIncreasing(endPts, enterAnchor, to);
        AppendPolylineArc(endPts, enterAnchor, to, walkEndInc, segment, true);
        AppendUnique(waypoints, segment.ToArray(), !skipFirst);
        return waypoints.Count > 0;
    }

    static CheckpointAnchor VertexAnchor(ERRoad road, Vector3[] pts, int vertexIndex)
    {
        vertexIndex = Mathf.Clamp(vertexIndex, 0, pts.Length - 1);
        if (vertexIndex == 0)
        {
            return new CheckpointAnchor
            {
                road = road,
                segmentIndex = 0,
                segmentT = 0f,
                onRoad = pts[0]
            };
        }

        return new CheckpointAnchor
        {
            road = road,
            segmentIndex = vertexIndex - 1,
            segmentT = 1f,
            onRoad = pts[vertexIndex]
        };
    }

    static bool ShouldShortCircuitArc(float direct, float bestArc, float roadLen, bool isClosingLeg)
    {
        if (bestArc <= direct * 1.25f) return false;
        if (isClosingLeg && direct < Mathf.Max(250f, roadLen * 0.08f)) return true;
        if (bestArc > roadLen * 0.45f && direct < bestArc * 0.35f) return true;
        return false;
    }

    static void AppendDirectArc(CheckpointAnchor from, CheckpointAnchor to, List<Vector3> waypoints, bool skipFirst)
    {
        if (!skipFirst) TryAddWaypoint(waypoints, from.onRoad);
        TryAddWaypoint(waypoints, to.onRoad);
    }

    static float TotalPolylineLength(Vector3[] pts)
    {
        if (pts == null || pts.Length < 2) return 0f;
        float sum = 0f;
        for (int i = 0; i < pts.Length - 1; i++)
            sum += HorizontalDistance(pts[i], pts[i + 1]);
        return sum;
    }

    static float MeasurePolylineArc(Vector3[] pts, CheckpointAnchor from, CheckpointAnchor to, bool increasing)
    {
        float startParam = AnchorParam(from);
        float endParam = AnchorParam(to);

        if (increasing && endParam <= startParam) return float.PositiveInfinity;
        if (!increasing && endParam >= startParam) return float.PositiveInfinity;
        if (from.segmentIndex == to.segmentIndex) return HorizontalDistance(from.onRoad, to.onRoad);

        float length = 0f;
        Vector3 prev = from.onRoad;

        if (increasing)
        {
            int firstVertex = Mathf.Clamp(Mathf.FloorToInt(startParam) + 1, 0, pts.Length - 1);
            int lastVertex = Mathf.Clamp(Mathf.FloorToInt(endParam), 0, pts.Length - 1);
            for (int vi = firstVertex; vi <= lastVertex; vi++)
            {
                length += HorizontalDistance(prev, pts[vi]);
                prev = pts[vi];
            }
        }
        else
        {
            int firstVertex = Mathf.Clamp(Mathf.CeilToInt(startParam) - 1, 0, pts.Length - 1);
            int lastVertex = Mathf.Clamp(Mathf.CeilToInt(endParam), 0, pts.Length - 1);
            for (int vi = firstVertex; vi >= lastVertex; vi--)
            {
                length += HorizontalDistance(prev, pts[vi]);
                prev = pts[vi];
            }
        }

        length += HorizontalDistance(prev, to.onRoad);
        return length;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    static bool ChooseWalkIncreasing(Vector3[] pts, CheckpointAnchor from, CheckpointAnchor to)
    {
        float incLen = MeasurePolylineArc(pts, from, to, true);
        float decLen = MeasurePolylineArc(pts, from, to, false);
        if (incLen < float.PositiveInfinity && decLen < float.PositiveInfinity)
            return incLen <= decLen;
        if (incLen < float.PositiveInfinity) return true;
        if (decLen < float.PositiveInfinity) return false;

        float fromParam = AnchorParam(from);
        float toParam = AnchorParam(to);
        if (Mathf.Abs(toParam - fromParam) > 0.0001f)
            return toParam > fromParam;

        Vector3 travel = to.onRoad - from.onRoad;
        travel.y = 0f;
        if (travel.sqrMagnitude > 0.0001f)
        {
            Vector3 fwd = GetSplineForwardAt(pts, from.segmentIndex);
            return Vector3.Dot(travel.normalized, fwd) >= 0f;
        }

        return true;
    }

    static void AppendPolylineArc(
        Vector3[] pts,
        CheckpointAnchor from,
        CheckpointAnchor to,
        bool increasing,
        List<Vector3> waypoints,
        bool skipFirst)
    {
        if (pts == null || pts.Length < 2) return;

        if (!skipFirst) TryAddWaypoint(waypoints, from.onRoad);
        if (Vector3.Distance(from.onRoad, to.onRoad) < 0.01f) return;

        if (from.segmentIndex == to.segmentIndex)
        {
            TryAddWaypoint(waypoints, to.onRoad);
            return;
        }

        float startParam = AnchorParam(from);
        float endParam = AnchorParam(to);

        if (increasing)
        {
            if (endParam <= startParam) return;
            int firstVertex = Mathf.Clamp(Mathf.FloorToInt(startParam) + 1, 0, pts.Length - 1);
            int lastVertex = Mathf.Clamp(Mathf.FloorToInt(endParam), 0, pts.Length - 1);
            for (int vi = firstVertex; vi <= lastVertex; vi++)
                TryAddWaypoint(waypoints, pts[vi]);
        }
        else
        {
            if (endParam >= startParam) return;
            int firstVertex = Mathf.Clamp(Mathf.CeilToInt(startParam) - 1, 0, pts.Length - 1);
            int lastVertex = Mathf.Clamp(Mathf.CeilToInt(endParam), 0, pts.Length - 1);
            for (int vi = firstVertex; vi >= lastVertex; vi--)
                TryAddWaypoint(waypoints, pts[vi]);
        }

        TryAddWaypoint(waypoints, to.onRoad);
    }

    static void TryAddWaypoint(List<Vector3> waypoints, Vector3 point)
    {
        if (waypoints.Count > 0 && Vector3.Distance(waypoints[waypoints.Count - 1], point) < 0.5f)
            return;
        waypoints.Add(point);
    }

    static bool ProjectOntoPolyline(
        Vector3[] pts,
        Vector3 world,
        out int segmentIndex,
        out Vector3 onRoad,
        out float segmentT,
        out float distance)
    {
        segmentIndex = 0;
        onRoad = pts[0];
        segmentT = 0f;
        distance = float.MaxValue;

        if (pts == null || pts.Length < 2) return false;

        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[i + 1];
            Vector3 ab = b - a;
            ab.y = 0f;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f) continue;

            Vector3 ap = world - a;
            ap.y = 0f;
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / lenSq);
            Vector3 proj = a + (ab * t);
            Vector3 delta = world - proj;
            delta.y = 0f;
            float dist = delta.magnitude;

            if (dist < distance)
            {
                distance = dist;
                segmentIndex = i;
                segmentT = t;
                onRoad = proj;
            }
        }

        return distance < float.MaxValue;
    }

    static void AppendUnique(List<Vector3> dest, Vector3[] segment, bool includeFirst)
    {
        int start = includeFirst ? 0 : 1;
        for (int i = start; i < segment.Length; i++)
        {
            if (dest.Count > 0 && Vector3.Distance(dest[dest.Count - 1], segment[i]) < 0.5f)
                continue;
            dest.Add(segment[i]);
        }
    }

    static List<Vector3> Resample(List<Vector3> raw, float spacing, bool closed)
    {
        var result = new List<Vector3>();
        if (raw.Count == 0) return result;

        result.Add(raw[0]);
        float carry = 0f;
        int count = closed ? raw.Count : raw.Count - 1;

        for (int i = 0; i < count; i++)
        {
            Vector3 a = raw[i];
            Vector3 b = raw[(i + 1) % raw.Count];
            float segLen = Vector3.Distance(a, b);
            if (segLen < 0.001f) continue;

            float d = spacing - carry;
            while (d <= segLen)
            {
                float t = d / segLen;
                result.Add(Vector3.Lerp(a, b, t));
                d += spacing;
            }

            carry = segLen - (d - spacing);
            if (carry < 0f) carry = 0f;
        }

        if (!closed && Vector3.Distance(result[result.Count - 1], raw[raw.Count - 1]) > 0.5f)
            result.Add(raw[raw.Count - 1]);

        return result;
    }

    static int FindClosestSplineIndex(Vector3[] points, Vector3 position)
    {
        int best = 0;
        float min = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float d = Vector3.Distance(points[i], position);
            if (d < min)
            {
                min = d;
                best = i;
            }
        }

        return best;
    }

    static Vector3 GetSplineForwardAt(Vector3[] points, int index)
    {
        if (points.Length <= 1) return Vector3.forward;
        int prev = Mathf.Max(index - 1, 0);
        int next = Mathf.Min(index + 1, points.Length - 1);
        Vector3 forward = points[next] - points[prev];
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }
}

[CustomEditor(typeof(RaceManager))]
public class RaceManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (RaceManager)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Baked Race Path", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(manager.bakedRacePath == null))
        {
            if (GUILayout.Button("Bake Race Path"))
            {
                RacePathBakeSettings settings = manager.bakeSettings;
                if (settings.sampleSpacing < 0.1f)
                    settings = RacePathBakeSettings.Default;

                if (RacePathBaker.Bake(manager, manager.bakedRacePath, settings, out string msg))
                {
                    EditorUtility.DisplayDialog("Bake Race Path", msg, "OK");
                    SceneView.RepaintAll();
                }
                else
                    EditorUtility.DisplayDialog("Bake Race Path Failed", msg, "OK");
            }
        }

        if (GUILayout.Button("Reset Bake Settings To Defaults"))
        {
            Undo.RecordObject(manager, "Reset Bake Settings");
            manager.bakeSettings = RacePathBakeSettings.Default;
            EditorUtility.SetDirty(manager);
        }

        if (GUILayout.Button("Create & Assign BakedRacePath Asset"))
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Baked Race Path",
                "BakedRacePath",
                "asset",
                "Choose location for the baked path asset.");
            if (!string.IsNullOrEmpty(path))
            {
                var asset = ScriptableObject.CreateInstance<BakedRacePath>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                Undo.RecordObject(manager, "Assign Baked Path");
                manager.bakedRacePath = asset;
                EditorUtility.SetDirty(manager);
                EditorGUIUtility.PingObject(asset);
            }
        }

        if (GUILayout.Button("Test BakedPathMath"))
        {
            bool ok = BakedPathMath.RunSelfTest(out string report);
            EditorUtility.DisplayDialog("BakedPathMath", report, "OK");
        }
    }
}

public static class RacePathBakerMenu
{
    [MenuItem("Racing/Bake Selected RaceManager Path")]
    static void BakeSelected()
    {
        var manager = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<RaceManager>()
            : null;
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Bake Race Path", "Select a GameObject with RaceManager.", "OK");
            return;
        }

        if (manager.bakedRacePath == null)
        {
            EditorUtility.DisplayDialog("Bake Race Path", "Assign a BakedRacePath asset on RaceManager first.", "OK");
            return;
        }

        RacePathBakeSettings settings = manager.bakeSettings;
        if (settings.sampleSpacing < 0.1f)
            settings = RacePathBakeSettings.Default;

        if (RacePathBaker.Bake(manager, manager.bakedRacePath, settings, out string msg))
            EditorUtility.DisplayDialog("Bake Race Path", msg, "OK");
        else
            EditorUtility.DisplayDialog("Bake Race Path Failed", msg, "OK");
    }

    [MenuItem("Racing/Test BakedPathMath")]
    static void TestMath()
    {
        bool ok = BakedPathMath.RunSelfTest(out string report);
        EditorUtility.DisplayDialog("BakedPathMath", report, "OK");
    }
}