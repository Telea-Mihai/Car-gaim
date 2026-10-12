//
//  PSTabMuscle.cs
//  PSMuscle Editor
//
//  Copyright (c) 2014-2016 Pavo Studio.
//
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class PSTabMuscle : PSTab
{

	public bool[] muscleToggle;
	public string[] muscleName;
	public float[] muscleValue;
	public float[] muscleMasterValue;
	public float[] muscleFingerValue;
	public bool[] muscleBodyGroupToggle;
	public EditorCurveBinding[] curveBindings;
	public string[] propertyMuscleName;
	public int muscleCount;
	public int curSelectGroup;
	public int curSelectMasterGroup;
	public bool mirror;
	public bool rootT;
	public bool rootQ;
	public float[] rootValue;
	public bool[] rootToggle;
	public bool showRootGUI = true;
	public bool showMuscleGroupGUI = true;
	public bool showMuscleGUI = true;

	public PSTabMuscle ()
	{
		//Init ();
	}

	public void Init ()
	{
		this.muscleBodyGroupToggle = new bool[PSMuscleDefine.muscle.Length];
		for (int i = 0; i < PSMuscleDefine.muscle.Length; i++) {
			this.muscleBodyGroupToggle [i] = false;
		}
		this.muscleName = PSMuscleDefine.GetMuscleName ();

		this.propertyMuscleName = PSMuscleDefine.GetPropertyMuscleName ();

		this.muscleCount = HumanTrait.MuscleCount;
		this.muscleToggle = new bool[this.muscleCount];
		this.muscleValue = new float[this.muscleCount];
		for (int k = 0; k < this.muscleCount; k++) {
			this.muscleToggle [k] = false;
			this.muscleValue [k] = 0f;
		}

		this.muscleMasterValue = new float[PSMuscleDefine.masterMuscle.Length];
		for (int m = 0; m < PSMuscleDefine.masterMuscle.Length; m++) {
			this.muscleMasterValue [m] = 0f;
		}

		this.muscleFingerValue = new float[PSMuscleDefine.fingerMuscle.Length];
		for (int m = 0; m < PSMuscleDefine.fingerMuscle.Length; m++) {
			this.muscleFingerValue [m] = 0f;
		}

		rootValue = new float[PSMuscleDefine.rootProperty.Length];
		rootToggle = new bool[PSMuscleDefine.rootProperty.Length];

		Undo.undoRedoPerformed += UndoCallback;
	}

	void UndoCallback ()
	{
		ResampleAnimation ();
		OnUpdateValue ();
	}

	/***********************************************
	 *  override 
	 ***********************************************/

	public override void OnTargetChange ()
	{
		if (target == null) {
			PSLogger.Log ("target is null");
			return;
		}

		if (propertyMuscleName == null || propertyMuscleName.Length == 0) {
			propertyMuscleName = PSMuscleDefine.GetPropertyMuscleName ();
			muscleCount = HumanTrait.MuscleCount;
		}

		// Keep curveBindings index-aligned with HumanTrait muscle indices.
		// Older code only appended found bindings, so missing muscles shifted indices
		// and caused IndexOutOfRangeException in WriteMuscleValue.
		curveBindings = new EditorCurveBinding[propertyMuscleName.Length];

		GameObject animRoot = target;
		Animator animator = target.GetComponent<Animator> ();
		if (animator == null)
			animator = target.GetComponentInParent<Animator> ();
		if (animator != null)
			animRoot = animator.gameObject;

		EditorCurveBinding[] bindings = AnimationUtility.GetAnimatableBindings (animRoot, animRoot);

		for (int i = 0; i < propertyMuscleName.Length; i++) {
			bool found = false;
			for (int j = 0; j < bindings.Length; j++) {
				if (bindings [j].propertyName == propertyMuscleName [i]) {
					EditorCurveBinding match = bindings [j];
					match.path = "";
					curveBindings [i] = match;
					found = true;
					break;
				}
			}

			if (!found) {
				curveBindings [i] = new EditorCurveBinding {
					path = "",
					propertyName = propertyMuscleName [i],
					type = typeof(Animator)
				};
			}
		}
	}

	public override void OnUpdateValue ()
	{
		if (target == null || clip == null) {
			PSLogger.Log ("target is null");
			return;
		}


		this.muscleToggle = new bool[this.muscleCount];
		this.muscleValue = new float[this.muscleCount];
		rootValue = new float[PSMuscleDefine.rootProperty.Length];
		rootToggle = new bool[PSMuscleDefine.rootProperty.Length];
		rootT = false;
		rootQ = false;

		foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip)) {
			for (int i = 0; i < PSMuscleDefine.rootProperty.Length; i++) {
				if (binding.propertyName == PSMuscleDefine.rootProperty [i]) {
					rootValue [i] = EvaluateCurveValue (binding);
					rootToggle [i] = true;
					if (i < 3)
						rootT = true;
					else
						rootQ = true;
					break;
				}
			}

			for (int i = 0; i < this.propertyMuscleName.Length; i++) {
				if (binding.propertyName == this.propertyMuscleName [i]) {
					muscleValue [i] = EvaluateCurveValue (binding);
					muscleToggle [i] = true;
					break;
				}
			}
		}
	}

	public override void OnTabGUI ()
	{
		this.RootGUI ();
		EditorGUILayout.Space ();
		this.MuscleGroupGUI ();
		EditorGUILayout.Space ();
		this.MuscleGUI ();
		EditorGUILayout.Space ();
	}

	/***********************************************
	 *  GUI
	 ***********************************************/

	protected void RootGUI ()
	{

		EditorGUILayout.BeginHorizontal (EditorStyles.toolbar);
		showRootGUI = EditorGUILayout.Foldout (showRootGUI, "Root Transform");
		//EditorGUILayout.LabelField ("Root Transform");

		//Color tmpColor = GUI.color;
		//GUI.color = Color.cyan;
		if (GUILayout.Button ("Reset", EditorStyles.toolbarButton, GUILayout.Width (80))) {
			for (int i = 0; i < PSMuscleDefine.rootProperty.Length; i++) {
				rootValue [i] = 0;
				rootToggle [i] = false;
				WritePropertyValue (PSMuscleDefine.rootProperty [i], rootValue [i], rootToggle [i]);
			}

		}
		//GUI.color = tmpColor;
		EditorGUILayout.EndHorizontal ();

		if (!showRootGUI)
			return;

		RootTGUI ();
		RootQGUI ();
	}

	private void RootTGUI ()
	{
		bool toggle = EditorGUILayout.BeginToggleGroup ("RootT", rootT);
		bool toggleChange = toggle != rootT;
		rootT = toggle;

		EditorGUI.indentLevel++;
		for (int i = 0; i < 3; i++) {
			if (toggleChange) {
				rootToggle [i] = toggle;
				WritePropertyValue (PSMuscleDefine.rootProperty [i], rootValue [i], rootToggle [i]);
			}

			EditorGUILayout.BeginHorizontal ();

			float value = EditorGUILayout.FloatField (PSMuscleDefine.rootT [i], rootValue [i]);
			if (rootValue [i] != value) {
				rootValue [i] = value;
				rootToggle [i] = true;
				WritePropertyValue (PSMuscleDefine.rootProperty [i], rootValue [i], rootToggle [i]);
			}

			EditorGUILayout.EndHorizontal ();
		}
		EditorGUI.indentLevel--;

		EditorGUILayout.EndToggleGroup ();

	}

	private void RootQGUI ()
	{
		bool toggle = EditorGUILayout.BeginToggleGroup ("RootQ", rootQ);
		if (toggle != rootQ) {
			rootToggle [3] = toggle;
			rootToggle [4] = toggle;
			rootToggle [5] = toggle;
			rootToggle [6] = toggle;

			// init values
			if (rootValue [6] == 0) {
				Quaternion quat = Quaternion.identity;
				rootValue [3] = quat.x;
				rootValue [4] = quat.y;
				rootValue [5] = quat.z;
				rootValue [6] = quat.w;
			}

			WritePropertyValue (PSMuscleDefine.rootProperty [3], rootValue [3], rootToggle [3]);
			WritePropertyValue (PSMuscleDefine.rootProperty [4], rootValue [4], rootToggle [4]);
			WritePropertyValue (PSMuscleDefine.rootProperty [5], rootValue [5], rootToggle [5]);
			WritePropertyValue (PSMuscleDefine.rootProperty [6], rootValue [6], rootToggle [6]);

			rootQ = toggle;
		}

		EditorGUI.indentLevel++;

		Quaternion q = new Quaternion (rootValue [3], rootValue [4], rootValue [5], rootValue [6]);
		float x = EditorGUILayout.FloatField (PSMuscleDefine.rootQ [0], q.eulerAngles.x);
		float y = EditorGUILayout.FloatField (PSMuscleDefine.rootQ [1], q.eulerAngles.y);
		float z = EditorGUILayout.FloatField (PSMuscleDefine.rootQ [2], q.eulerAngles.z);

		if (x != q.eulerAngles.x || y != q.eulerAngles.y || z != q.eulerAngles.z) {
			q.eulerAngles = new Vector3 (x, y, z);
			rootValue [3] = q.x;
			rootValue [4] = q.y;
			rootValue [5] = q.z;
			rootValue [6] = q.w;
			WritePropertyValue (PSMuscleDefine.rootProperty [3], q.x, rootToggle [3]);
			WritePropertyValue (PSMuscleDefine.rootProperty [4], q.y, rootToggle [4]);
			WritePropertyValue (PSMuscleDefine.rootProperty [5], q.z, rootToggle [5]);
			WritePropertyValue (PSMuscleDefine.rootProperty [6], q.w, rootToggle [6]);
		}

		EditorGUI.indentLevel--;

		EditorGUILayout.EndToggleGroup ();

	}

	private void RootSubGUI (int start, int length, bool toggleChange, bool enable)
	{
		EditorGUI.indentLevel++;
		for (int i = start; i < length; i++) {
			if (toggleChange) {
				rootToggle [i] = enable;
				WritePropertyValue (PSMuscleDefine.rootProperty [i], rootValue [i], rootToggle [i]);
			}

			EditorGUILayout.BeginHorizontal ();

			float value = EditorGUILayout.FloatField (PSMuscleDefine.rootProperty [i], rootValue [i]);
			if (rootValue [i] != value) {
				rootValue [i] = value;
				rootToggle [i] = true;
				WritePropertyValue (PSMuscleDefine.rootProperty [i], rootValue [i], rootToggle [i]);
			}
			EditorGUILayout.EndHorizontal ();
		}
		EditorGUI.indentLevel--;
	}

	protected void MuscleGroupGUI ()
	{
		EditorGUILayout.BeginHorizontal (EditorStyles.toolbar);
		showMuscleGroupGUI = EditorGUILayout.Foldout (showMuscleGroupGUI, "Muscle Group");
		//EditorGUILayout.LabelField ("Muscle Group");
		EditorGUILayout.EndHorizontal ();

		if (!showMuscleGroupGUI)
			return;
		
		EditorGUILayout.BeginHorizontal ();
		for (int i = 0; i < PSMuscleDefine.muscleTabGroup.Length; i++) {
			if (GUILayout.Toggle (curSelectMasterGroup == i, PSMuscleDefine.muscleTabGroup [i], EditorStyles.toolbarButton)) {
				curSelectMasterGroup = i;
			}
		}
		EditorGUILayout.EndHorizontal ();

		//Color tmpColor = GUI.color;
		//GUI.color = Color.yellow;
		EditorGUILayout.HelpBox ("This window affects multiple muscles’ value at the same time. Note however, that the changes made here will affect the ‘Muscle Body Group’ as well. Changes made in Muscle Body Group will not affect the Muscle Group properties.", MessageType.None);
		//GUI.color = tmpColor;

		if (curSelectMasterGroup == 0) {
			for (int k = 0; k < PSMuscleDefine.masterMuscle.Length; k++) {
				float num = muscleMasterValue [k];
				muscleMasterValue [k] = TabSlider (PSMuscleDefine.muscleTypeGroup [k], muscleMasterValue [k]);
				if (muscleMasterValue [k] != num) {
					for (int l = 0; l < PSMuscleDefine.masterMuscle [k].Length; l++) {

						int index = PSMuscleDefine.masterMuscle [k] [l];

						if (index != -1) {
							muscleValue [index] = muscleMasterValue [k];
							muscleToggle [index] = true;
							WriteMuscleValue (index, muscleValue [index]);
						}
					}
				}
			}
		} else if (curSelectMasterGroup == 1) {
			for (int k = 0; k < PSMuscleDefine.fingerMuscle.Length; k++) {
				float num = muscleFingerValue [k];
				muscleFingerValue [k] = TabSlider (PSMuscleDefine.muscleFingerGroup [k], muscleFingerValue [k]);
				if (muscleFingerValue [k] != num) {
					for (int l = 0; l < PSMuscleDefine.fingerMuscle [k].Length; l++) {

						int index = PSMuscleDefine.fingerMuscle [k] [l];

						if (index != -1) {
							muscleValue [index] = muscleFingerValue [k];
							muscleToggle [index] = true;
							WriteMuscleValue (index, muscleValue [index]);
						}
					}
				}
			}
		}
	}

	protected void MuscleGUI ()
	{

		EditorGUILayout.BeginHorizontal (EditorStyles.toolbar);
		showMuscleGUI = EditorGUILayout.Foldout (showMuscleGUI, "Muscle Body Group");
		//EditorGUILayout.LabelField ("Muscle Body Group");
		//Color tmpColor = GUI.color;

		mirror = GUILayout.Toggle (mirror, "Mirror", EditorStyles.toolbarButton, GUILayout.Width (80));

		if (GUILayout.Button ("Add All", EditorStyles.toolbarButton, GUILayout.Width (80))) {
			for (int j = 0; j < muscleValue.Length; j++) {
				muscleToggle [j] = true;
			}
			this.WriteAllMuscleValue ();
		}

		//GUI.color = Color.cyan;
		if (GUILayout.Button ("Remove All", EditorStyles.toolbarButton, GUILayout.Width (80))) {
			for (int j = 0; j < muscleValue.Length; j++) {
				muscleValue [j] = 0f;
				muscleToggle [j] = false;
			}
			this.WriteAllMuscleValue ();
		}
		//GUI.color = tmpColor;
		EditorGUILayout.EndHorizontal ();

		if (!showMuscleGUI)
			return;

		EditorGUILayout.BeginHorizontal ();
		for (int i = 0; i < this.muscleBodyGroupToggle.Length; i++) {
			if (GUILayout.Toggle (curSelectGroup == i, PSMuscleDefine.muscleBodyGroup [i], EditorStyles.toolbarButton)) {
				curSelectGroup = i;
			}

		}

		EditorGUILayout.EndHorizontal ();

		EditorGUILayout.Space ();

		for (int i = 0; i < this.muscleBodyGroupToggle.Length; i++) {
			if (curSelectGroup == i) {
				GUILayout.BeginHorizontal ();
				EditorGUILayout.LabelField (PSMuscleDefine.muscleBodyGroup [i], GUILayout.MinWidth (100));

				if (GUILayout.Button ("Add All", EditorStyles.miniButtonLeft, GUILayout.Width (70))) {
					for (int j = 0; j < PSMuscleDefine.muscle [i].Length; j++) {
						WriteToggleValue (i, j, true);
					}
				}

				if (GUILayout.Button ("Remove All", EditorStyles.miniButtonRight, GUILayout.Width (70))) {
					for (int j = 0; j < PSMuscleDefine.muscle [i].Length; j++) {
						WriteToggleValue (i, j, false);
					}
				}

				GUILayout.EndHorizontal ();
				EditorGUILayout.Space ();

				for (int j = 0; j < PSMuscleDefine.muscle [i].Length; j++) {
					int index = PSMuscleDefine.muscle [i] [j];
					if (muscleToggle == null || muscleValue == null || muscleName == null
					    || index < 0 || index >= muscleToggle.Length
					    || index >= muscleValue.Length || index >= muscleName.Length)
						continue;

					GUILayout.BeginHorizontal ();

					bool toggle = EditorGUILayout.ToggleLeft (GUIContent.none, muscleToggle [index], GUILayout.Width (10));

					if (toggle != muscleToggle [index]) {
						WriteToggleValue (i, j, toggle);
					}

					EditorGUI.BeginDisabledGroup (!muscleToggle [index]);

					float value = TabSlider (this.muscleName [index], muscleValue [index]);

					if (muscleValue [index] != value) {
						muscleValue [index] = value;

						if (mirror) {
							WriteMirrorMuscleValue (i, j);
						}

						WriteMuscleValue (index, value);

					}

					EditorGUI.EndDisabledGroup ();

					GUILayout.EndHorizontal ();
				}

				EditorGUILayout.Space ();

				if (GUILayout.Button ("Copy values to the mirror side", EditorStyles.miniButton, GUILayout.Width (180))) {

					for (int j = 0; j < PSMuscleDefine.muscle [i].Length; j++) {
						WriteMirrorMuscleValue (i, j);
					}
				}
				
				// MCV
				if (GUILayout.Button ("Flip values Vertically", EditorStyles.miniButton, GUILayout.Width (180))) {
					for (int j = 0; j < PSMuscleDefine.muscle [i].Length; j++) {
						WriteFlipVerticalMuscleValue (i, j);
					}
				}

			}
		}

	}


	/***********************************************
	 *  Write Values
	 ***********************************************/

	public void WriteToggleValue (int i, int j, bool toggle)
	{
		if (i < 0 || i >= PSMuscleDefine.muscle.Length || j < 0 || j >= PSMuscleDefine.muscle [i].Length)
			return;

		int index = PSMuscleDefine.muscle [i] [j];
		if (muscleToggle == null || index < 0 || index >= muscleToggle.Length)
			return;

		muscleToggle [index] = toggle;

		if (mirror && PSMuscleDefine.mirrorMuscle [i] != i) {
			int mirrorGroup = PSMuscleDefine.mirrorMuscle [i];
			if (mirrorGroup >= 0 && mirrorGroup < PSMuscleDefine.muscle.Length
			    && j < PSMuscleDefine.muscle [mirrorGroup].Length) {
				int mirrorIndex = PSMuscleDefine.muscle [mirrorGroup] [j];
				if (mirrorIndex >= 0 && mirrorIndex < muscleToggle.Length) {
					muscleToggle [mirrorIndex] = toggle;
					WriteMuscleValue (mirrorIndex, muscleValue [mirrorIndex]);
				}
			}
		}

		WriteMuscleValue (index, muscleValue [index]);
	}

	public void WriteMirrorMuscleValue (int i, int j)
	{

		int index = PSMuscleDefine.muscle [i] [j];

		if (PSMuscleDefine.mirrorMuscle [i] != i) {
			int mirrorIndex = PSMuscleDefine.muscle [PSMuscleDefine.mirrorMuscle [i]] [j];
			muscleValue [mirrorIndex] = muscleValue [index];
			muscleToggle [mirrorIndex] = muscleToggle [index];
			WriteMuscleValue (mirrorIndex, muscleValue [mirrorIndex]);
		}
	}

	//MCV - FlipVertical
	// This should switch the Left & Right sides to invert the pose
	public void WriteFlipVerticalMuscleValue (int i, int j)
	{
		int index = PSMuscleDefine.muscle [i] [j];
		
		if (PSMuscleDefine.mirrorMuscle [i] != i) {
			float tempMuscleValue;
			bool tempMuscleToggle;
			
			int mirrorIndex = PSMuscleDefine.muscle [PSMuscleDefine.mirrorMuscle [i]] [j];
			tempMuscleValue = muscleValue [mirrorIndex];         // Save the current mirrored location value.
			tempMuscleToggle = muscleToggle [mirrorIndex];
			muscleValue [mirrorIndex] = muscleValue [index];      
			muscleToggle [mirrorIndex] = muscleToggle [index];
			WriteMuscleValue (mirrorIndex, muscleValue [mirrorIndex]);
			
			// Copy the temp to the original side and save.
			muscleValue [index] = tempMuscleValue;
			muscleToggle [index] = tempMuscleToggle;
			WriteMuscleValue (index, muscleValue [index]);
		}
	}

	public void WritePropertyValue (string propertyName, float value, bool enable)
	{

		if (clip == null)
			return;

		EditorCurveBinding binding = new EditorCurveBinding ();
		binding.path = "";
		binding.propertyName = propertyName;
		binding.type = typeof(Animator);

		Undo.RecordObject (clip, clip.name);

		if (enable)
			this.SetEditorCurve (binding, value);
		else
			AnimationUtility.SetEditorCurve (clip, binding, null);

		ResampleAnimation ();
	}

	public void WriteMuscleValue (int index, float value)
	{
		if (curveBindings == null || curveBindings.Length != muscleCount) {
			OnTargetChange ();
		}

		if (clip == null || curveBindings == null) {
			PSLogger.Log ("clip or curveBindings is null");
			return;
		}

		if (index < 0 || index >= curveBindings.Length || index >= muscleToggle.Length) {
			PSLogger.Log ("muscle index out of range: " + index);
			return;
		}

		if (string.IsNullOrEmpty (curveBindings [index].propertyName)) {
			PSLogger.Log ("curve binding missing for muscle index: " + index);
			return;
		}

		Undo.RecordObject (clip, clip.name);

		if (muscleToggle [index]) {
			this.SetEditorCurve (curveBindings [index], value);
		} else {
			AnimationUtility.SetEditorCurve (clip, curveBindings [index], null);
		}

		ResampleAnimation ();
	}

	public void WriteAllMuscleValue ()
	{
		if (curveBindings == null || curveBindings.Length != muscleCount) {
			OnTargetChange ();
		}

		if (clip == null || curveBindings == null)
			return;

		Undo.RecordObject (clip, clip.name);

		int count = Mathf.Min (propertyMuscleName.Length, curveBindings.Length);
		for (int i = 0; i < count; i++) {
			if (string.IsNullOrEmpty (curveBindings [i].propertyName))
				continue;

			if (muscleToggle [i]) {
				this.SetEditorCurve (curveBindings [i], muscleValue [i]);
			} else {
				AnimationUtility.SetEditorCurve (clip, curveBindings [i], null);
			}
		}

		ResampleAnimation ();
	}

	private void SetEditorCurve (EditorCurveBinding binding, float value)
	{
		AnimationCurve curve = AnimationUtility.GetEditorCurve (clip, binding);

		if (curve == null)
			curve = new AnimationCurve ();

		bool found = false;

		Keyframe[] keys = curve.keys;
		for (int i = 0; i < keys.Length; i++) {
			if (keys [i].time == time) {
				keys [i].value = value;
				found = true;
				break;
			}
		}

		if (found)
			curve.keys = keys;
		else
			curve.AddKey (new Keyframe (time, value));

		AnimationUtility.SetEditorCurve (clip, binding, curve);
	}

	private float EvaluateCurveValue (EditorCurveBinding binding)
	{
		AnimationCurve curve = AnimationUtility.GetEditorCurve (clip, binding);
		if (curve != null)
			return curve.Evaluate (time);

		try {
			float value;
			AnimationUtility.GetFloatValue (target, binding, out value);
			return value;
		} catch {
			return 0f;
		}
	}

	private float writeTransformProperty (AnimationCurve footCurve, AnimationCurve rootCurve, int start, int length, float value)
	{

		int len = footCurve.keys.Length;

		for (int i = start; i < length; i++) {

			int preIndex = (i - 1 + len) % len;
			value += Mathf.Abs ((footCurve.keys [preIndex].value - footCurve.keys [i].value));

			Keyframe key = new Keyframe (footCurve.keys [i].time, value);

			rootCurve.AddKey (key);	

			//value+=(data.curve.keys[i].value - data.curve.keys[(i+1)%len].value);
		}

		return value;
	}


	/***********************************************
	 * 
	 ***********************************************/


	public void ResampleAnimation ()
	{
		if (AnimationMode.InAnimationMode ()) {
			AnimationMode.BeginSampling ();
			AnimationMode.SampleAnimationClip (target, clip, time);
			AnimationMode.EndSampling ();
		}

		UnityEditorInternal.InternalEditorUtility.RepaintAllViews ();

	}

	private static float TabSlider (string label, float val)
	{
		if (label == null)
			val = EditorGUILayout.Slider (GUIContent.none, val, -1f, 1f);
		else
			val = EditorGUILayout.Slider (label, val, -1f, 1f);
		return val;
	}


}
