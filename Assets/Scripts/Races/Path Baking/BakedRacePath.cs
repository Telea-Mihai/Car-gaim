using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BakedRacePath", menuName = "Racing/Baked Race Path")]
public class BakedRacePath : ScriptableObject
{
    public Vector3[] points = Array.Empty<Vector3>();
    public Vector3[] tangents = Array.Empty<Vector3>();
    public float[] cumulativeDistance = Array.Empty<float>();
    public bool isClosed;

    public int PointCount => points != null ? points.Length : 0;

    public float TotalLength
    {
        get
        {
            if (cumulativeDistance == null || cumulativeDistance.Length == 0 || points == null || points.Length == 0)
                return 0f;

            float open = cumulativeDistance[cumulativeDistance.Length - 1];
            if (isClosed && points.Length >= 2)
                return open + Vector3.Distance(points[points.Length - 1], points[0]);
            return open;
        }
    }

    public bool IsValid => PointCount >= 2 &&
                           tangents != null && tangents.Length == PointCount &&
                           cumulativeDistance != null && cumulativeDistance.Length == PointCount;

    public void SetData(Vector3[] newPoints, Vector3[] newTangents, float[] newCumulative, bool closed)
    {
        points = newPoints ?? Array.Empty<Vector3>();
        tangents = newTangents ?? Array.Empty<Vector3>();
        cumulativeDistance = newCumulative ?? Array.Empty<float>();
        isClosed = closed;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void DrawGizmos(Color lineColor, float pointRadius = 0.35f)
    {
        if (points == null || points.Length < 2)
            return;

        Gizmos.color = lineColor;
        for (int i = 0; i < points.Length - 1; i++)
            Gizmos.DrawLine(points[i], points[i + 1]);

        if (isClosed)
            Gizmos.DrawLine(points[points.Length - 1], points[0]);

        Gizmos.color = lineColor;
        for (int i = 0; i < points.Length; i++)
            Gizmos.DrawSphere(points[i], pointRadius);
    }
}
