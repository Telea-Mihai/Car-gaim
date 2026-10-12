using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PSMuscleEditorUtil
{

	public enum OFFSET_FOOT
	{
		LEFT_FOOT = 0,
		RIGHT_FOOT = 1
	}

	public enum AREA
	{
		LEFT_FOOT_ENTER,
		LEFT_FOOT_LEAVE,
		RIGHT_FOOT_ENTER,
		RIGHT_FOOT_LEAVE
	}

	public static string PathTruncate (string value)
	{
		if (string.IsNullOrEmpty (value))
			return value;

		string[] strs = value.Split ('/');
		if (strs.Length > 2) {
			value = strs [0] + "/.../" + strs [strs.Length - 1];
		}

		return value; 
	}

	public static AnimationCurve GenerateRootTZMotion (AnimationCurve lFtTZCurve, AnimationCurve rFtTZCurve, int lFtIn, int lFtOut, int rFtIn, int rFtOut)
	{

		int curveLength = lFtTZCurve.keys.Length;

		List<Keyframe> footKeys = new List<Keyframe> ();


		int minIndex = lFtIn;
		AREA area = AREA.RIGHT_FOOT_LEAVE;
		
		if (minIndex > lFtOut) {
			minIndex = lFtOut;
			area = AREA.LEFT_FOOT_ENTER;
		}
		if (minIndex > rFtIn) {
			minIndex = rFtIn;
			area = AREA.LEFT_FOOT_LEAVE;
		}
		if (minIndex > rFtOut) {
			minIndex = rFtOut;
			area = AREA.RIGHT_FOOT_ENTER;
		}
		
		Debug.Log ("start area: " + area);
		
		for (int i=0; i<curveLength; i++) {
			
			if (i == lFtIn) {
				area = AREA.LEFT_FOOT_ENTER;
			} else if (i == lFtOut) {
				area = AREA.LEFT_FOOT_LEAVE;
			} else if (i == rFtIn) {
				area = AREA.RIGHT_FOOT_ENTER;
			} else if (i == rFtOut) {
				area = AREA.RIGHT_FOOT_LEAVE;
			}
			
			Debug.Log ("index: " + i + " current area: " + area);
			
			int preIndex;
			
			switch (area) {
			case AREA.LEFT_FOOT_ENTER:
				preIndex = (i - 1 + curveLength) % curveLength;
				footKeys.Add (new Keyframe (lFtTZCurve.keys [i].time, lFtTZCurve.keys [preIndex].value - lFtTZCurve.keys [i].value));
				break;
			case AREA.RIGHT_FOOT_ENTER:
				preIndex = (i - 1 + curveLength) % curveLength;
				footKeys.Add (new Keyframe (rFtTZCurve.keys [i].time, rFtTZCurve.keys [preIndex].value - rFtTZCurve.keys [i].value));
				break;
			case AREA.LEFT_FOOT_LEAVE:
				preIndex = (lFtOut - 1 + curveLength) % curveLength;
				footKeys.Add (new Keyframe (lFtTZCurve.keys [i].time, lFtTZCurve.keys [preIndex].value - lFtTZCurve.keys [lFtOut].value));
				break;
			case AREA.RIGHT_FOOT_LEAVE:
				preIndex = (rFtOut - 1 + curveLength) % curveLength;
				footKeys.Add (new Keyframe (rFtTZCurve.keys [i].time, rFtTZCurve.keys [preIndex].value - rFtTZCurve.keys [rFtOut].value));
				break;
			}
			
		}

		AnimationCurve rootCurve = new AnimationCurve ();
		
		float value = 0;
		
		for (int i=0; i<curveLength; i++) {
			value += Mathf.Abs (footKeys [i].value);
			rootCurve.AddKey (new Keyframe (footKeys [i].time, value));	
			rootCurve.SmoothTangents (i, 0);
		}

		return rootCurve;

	}

}
