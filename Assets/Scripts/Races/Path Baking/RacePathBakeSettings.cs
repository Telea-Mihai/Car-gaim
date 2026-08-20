using System;
using UnityEngine;

/// <summary>
/// Parameters for <see cref="RacePathBaker"/>.
/// </summary>
[Serializable]
public struct RacePathBakeSettings
{
    [Header("Sampling")]
    [Tooltip("Metres between resampled centerline points.")]
    [Min(0.5f)] public float sampleSpacing;

    [Tooltip("Max distance when matching a checkpoint to an EasyRoads spline.")]
    [Min(1f)] public float roadSearchRadius;

    [Header("Apex Racing Line")]
    [Tooltip("Apply outer-inner-outer offsets at detected corners.")]
    public bool applyRacingLine;

    [Tooltip("Minimum turn angle (deg, smoothed) to count as a corner.")]
    [Min(1f)] public float considerApexAngle;

    [Tooltip("Metres before the apex to run on the outside.")]
    [Min(1f)] public float distanceToApexEntry;

    [Tooltip("Metres after the apex to run on the outside.")]
    [Min(1f)] public float distanceToApexExit;

    [Tooltip("Max lateral shift toward the outside on entry/exit.")]
    [Min(0f)] public float maxOuterShift;

    [Tooltip("Max lateral shift toward the inside at the apex.")]
    [Min(0f)] public float maxInnerShift;

    [Tooltip("Blend length (m) between outside and inside around the apex.")]
    [Min(0.5f)] public float apexTransitionDistance;

    [Tooltip("Full road width used to clamp lateral shifts.")]
    [Min(1f)] public float roadWidth;

    [Tooltip("Clearance from the road edge when clamping.")]
    [Min(0f)] public float edgeMargin;

    [Tooltip("0 = centerline only, 1 = full OIO shifts.")]
    [Range(0f, 1f)] public float racingLineStrength;

    public static RacePathBakeSettings Default => new RacePathBakeSettings
    {
        sampleSpacing = 4f,
        roadSearchRadius = 60f,
        applyRacingLine = true,
        considerApexAngle = 12f,
        distanceToApexEntry = 30f,
        distanceToApexExit = 25f,
        maxOuterShift = 3f,
        maxInnerShift = 2f,
        apexTransitionDistance = 14f,
        roadWidth = 10f,
        edgeMargin = 0.5f,
        racingLineStrength = 1f
    };
}
