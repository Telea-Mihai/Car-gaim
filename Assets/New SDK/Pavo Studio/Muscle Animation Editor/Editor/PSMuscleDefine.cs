//
//  PSMuscleDefine.cs
//  PSMuscle Editor
//
//  Copyright (c) 2014-2015 Pavo Studio.
//

using UnityEngine;
using System.Collections;

public class PSMuscleDefine
{

	public static string[] rootProperty = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
	public static string[] rootT = { "position.x", "position.y", "position.z" };
	public static string[] rootQ = { "rotation.x", "rotation.y", "rotation.z"};
	
	// 0 body, 1 head, 2 Arm L <-> 4 R, 3 Fingers L <-> 5 R, 6 Leg L <-> 7 R
	public static int[] mirrorMuscle = new int[]{ 0, 1, 4, 5, 2, 3, 7, 6 };

	public static string[] muscleBodyGroup = {
		"Body",
		"Head",
		"Left Arm",
		"Left Fingers",
		"Right Arm",
		"Right Fingers",
		"Left Leg",
		"Right Leg"
	};

	public static string[] muscleTabGroup = {
		"Body",
		"Fingers"
	};

	public static string[] muscleFingerGroup = {
		"Left Thumb Stretched",
		"Left Index Stretched",
		"Left Middle Stretched",
		"Left Ring Stretched",
		"Left Little Stretched",
		"Right Thumb Stretched",
		"Right Index Stretched",
		"Right Middle Stretched",
		"Right Ring Stretched",
		"Right Little Stretched"
	};

	public static string[] muscleTypeGroup = {
		"Open Close",
		"Left Right",
		"Roll Left Right",
		"In Out",
		"Roll In Out",
		"Left Finger Open Close",
		"Right Finger Open Close",
		"Left Finger In Out",
		"Right Finger In Out"
	};

	public static string[] muscleIK = 
	{
		"Left Hand T",
		"Left Hand Q",
		"Right Hand T",
		"Right Hand Q",
		"Left Foot T",
		"Left Foot Q",
		"Right Foot T",
		"Right Foot Q"
	};

	// index of HumanTrait.MuscleName
	public static int[][] muscle = new int[][]
	{
		new int[]
		{
			0,
			1,
			2,
			3,
			4,
			5,
			6,
			7,
			8
		},
		new int[]
		{
			9,
			10,
			11,
			12,
			13,
			14,
			15,
			16,
			17,
			18,
			19,
			20
		},
		new int[]
		{
			37,
			38,
			39,
			40,
			41,
			42,
			43,
			44,
			45
		},
		new int[]
		{
			55,
			56,
			57,
			58,
			59,
			60,
			61,
			62,
			63,
			64,
			65,
			66,
			67,
			68,
			69,
			70,
			71,
			72,
			73,
			74
		},
		new int[]
		{
			46,
			47,
			48,
			49,
			50,
			51,
			52,
			53,
			54
		},
		new int[]
		{
			75,
			76,
			77,
			78,
			79,
			80,
			81,
			82,
			83,
			84,
			85,
			86,
			87,
			88,
			89,
			90,
			91,
			92,
			93,
			94
		},
		new int[]
		{
			21,
			22,
			23,
			24,
			25,
			26,
			27,
			28
		},
		new int[]
		{
			29,
			30,
			31,
			32,
			33,
			34,
			35,
			36
		}
	};

	public static int[][] fingerMuscle = new int[][]{
		new int[]{// left thumb stretch
			55,
			57,
			58
		},
		new int[]{// left index
			59,
			61,
			62
		},
		new int[]{// left middle
			63,
			65,
			66
		},
		new int[]{// left ring
			67,
			69,
			70
		},
		new int[]{// left little
			71,
			73,
			74
		},
		new int[]{// right thumb
			75,
			77,
			78
		},
		new int[]{// right index
			79,
			81,
			82
		},
		new int[]{// right middle
			83,
			85,
			86
		},
		new int[]{// right ring
			87,
			89,
			90
		},
		new int[]{// right little
			91,
			93,
			94
		}
	};

	public static int[][] masterMuscle = new int[][]
	{
		new int[]
		{
			0,
			3,
			6,
			9,
			12,
			21,
			24,
			26,
			29,
			32,
			34,
			37,
			39,
			42,
			44,
			46,
			48,
			51,
			53
		},
		new int[]
		{
			1,
			4,
			7,
			10,
			13
		},
		new int[]
		{
			2,
			5,
			8,
			11,
			14
		},
		new int[]
		{
			22,
			27,
			30,
			35,
			38,
			40,
			45,
			47,
			49,
			54
		},
		new int[]
		{
			23,
			25,
			31,
			33,
			41,
			43,
			50,
			52
		},
		new int[]
		{
			55,
			57,
			58,
			59,
			61,
			62,
			63,
			65,
			66,
			67,
			69,
			70,
			71,
			73,
			74
		},
		new int[]
		{
			56,
			60,
			64,
			68,
			72
		},
		new int[]{
			75,
			77,
			78,
			79,
			81,
			82,
			83,
			85,
			86,
			87,
			89,
			90,
			91,
			93,
			94
		},
		new int[]
		{
			76,
			80,
			84,
			88,
			92
		}
	};

	public static string[] GetMuscleName(){
		// Clone: HumanTrait.MuscleName may return a shared/cached array.
		string[] muscleName = (string[])HumanTrait.MuscleName.Clone ();
		for (int j = 0; j < muscleName.Length; j++)
		{
			if (muscleName [j].StartsWith ("Right"))
			{
				muscleName [j] = muscleName [j].Substring (5).Trim ();
			}
			if (muscleName [j].StartsWith ("Left"))
			{
				muscleName [j] = muscleName [j].Substring (4).Trim ();
			}
		}

		return muscleName;
	}

	public static Vector2[] GetMuscleMinMaxValues(){
		Vector2[] values = new Vector2[HumanTrait.MuscleCount];

		for (int i = 0; i < HumanTrait.MuscleCount; i++)
		{
			values [i] = new Vector2 (HumanTrait.GetMuscleDefaultMin (i), HumanTrait.GetMuscleDefaultMax (i));
		}

		return values;
	}

	public static string GetBoneName(int muscleId){
		string[] boneName = HumanTrait.BoneName;
		return boneName [HumanTrait.BoneFromMuscle (muscleId)];
	}

	public static string[] GetPropertyMuscleName(){
		// Clone: mutating HumanTrait.MuscleName in-place breaks later lookups.
		string[] propertyMuscleName = (string[])HumanTrait.MuscleName.Clone ();
		for (int j = 0; j < propertyMuscleName.Length; j++)
		{
			if (propertyMuscleName [j].EndsWith("Stretched"))
			{
				string[] subString = propertyMuscleName[j].Split(' ');
				if (subString.Length >= 4)
					propertyMuscleName [j] = subString[0]+"Hand."+subString[1]+"."+subString[2]+" "+subString[3];
			}
			else if (propertyMuscleName [j].EndsWith("Spread"))
			{
				string[] subString = propertyMuscleName[j].Split(' ');
				if (subString.Length >= 3)
					propertyMuscleName [j] = subString[0]+"Hand."+subString[1]+"."+subString[2];
			}
		}

		return propertyMuscleName;
	}


}
