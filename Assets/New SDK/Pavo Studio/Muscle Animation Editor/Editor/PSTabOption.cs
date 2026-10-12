using UnityEngine;
using UnityEditor;
using System.Collections;

[System.Serializable]
public class PSTabOption : PSTab
{
	public bool showWindowTool = true;
	public float boneSize = 0.01f;
	public Color boneColor = Color.yellow;
	public Color colorSelected = Color.red;
	public int boneShape;
	public bool showBoneNames;
	public bool showSelectedNameOnly;
	public Color boneNameColor = Color.white;
	public bool showSkeleton;
	public bool showHumanSkeletonOnly;
	public float skeletonWidth = 2;
	public Color humanSkeletonColor = Color.green;
	public Color skeletonColor = Color.grey;
	public int rootShape = 1;
	public float rootSize = 0.01f;
	public Color rootColor = Color.white;

	public static Handles.CapFunction[] caps = new Handles.CapFunction[] {
		Handles.SphereHandleCap,
		Handles.CubeHandleCap,
		Handles.CylinderHandleCap,
		Handles.CircleHandleCap,
		Handles.RectangleHandleCap,
		Handles.ConeHandleCap,
		Handles.ArrowHandleCap,
		Handles.DotHandleCap
	};

	public static string[] capNames = {
		"SPHERE", "CUBE", "CYLINDER", "CIRCLE", "RECTANGLE", "CONE", "ARROW", "DOT"
	};

	public override void OnTabGUI ()
	{
		showWindowTool = EditorGUILayout.BeginToggleGroup ("Show Muscle Handles", showWindowTool);
		EditorGUI.indentLevel++;

		boneShape = EditorGUILayout.Popup ("Bone Shape", boneShape, capNames);
		boneSize = EditorGUILayout.Slider ("Bone Size", boneSize, 0, 0.2f);
		boneColor = EditorGUILayout.ColorField ("Bone Color", boneColor);
		rootShape = EditorGUILayout.Popup ("Root Shape", rootShape, capNames);
		rootSize = EditorGUILayout.Slider ("Root Size", rootSize, 0, 0.2f);
		rootColor = EditorGUILayout.ColorField ("Root Color", rootColor);
		colorSelected = EditorGUILayout.ColorField ("Color Selected", colorSelected);

		// bone names group
		showBoneNames = EditorGUILayout.BeginToggleGroup ("Show Bone Names", showBoneNames);
		EditorGUI.indentLevel++;

		showSelectedNameOnly = EditorGUILayout.Toggle ("Selected Name Only", showSelectedNameOnly);
		boneNameColor = EditorGUILayout.ColorField ("Bone Name Color", boneNameColor);

		EditorGUI.indentLevel--;
		EditorGUILayout.EndToggleGroup ();

		// skeleton group
		showSkeleton = EditorGUILayout.BeginToggleGroup ("Show Skeleton", showSkeleton);
		EditorGUI.indentLevel++;

		showHumanSkeletonOnly = EditorGUILayout.Toggle ("Human Skeleton Only", showHumanSkeletonOnly);
		humanSkeletonColor = EditorGUILayout.ColorField ("Human Skeleton Color", humanSkeletonColor);
		skeletonWidth = EditorGUILayout.Slider ("Skeleton Width", skeletonWidth, 1, 10);
		skeletonColor = EditorGUILayout.ColorField ("Skeleton Color", skeletonColor);

		EditorGUI.indentLevel--;
		EditorGUILayout.EndToggleGroup ();

		EditorGUI.indentLevel--;
		EditorGUILayout.EndToggleGroup ();

		UnityEditorInternal.InternalEditorUtility.RepaintAllViews ();
	}

	public override void OnTargetChange ()
	{
		
	}

	public override void OnUpdateValue ()
	{
		
	}

}
