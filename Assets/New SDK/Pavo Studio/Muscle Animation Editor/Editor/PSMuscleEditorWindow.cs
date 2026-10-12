//
//  PSMuscleEditorWindow.cs
//  PSMuscle Editor
//
//  Copyright (c) 2014-2016 Pavo Studio.
//
using System;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;

class PSMuscleEditorWindow : EditorWindow
{

	public enum TAB
	{
		MUSCLE = 0,
		OPTIONS = 1
	}

	public PSTab curTab;
	public PSBaseReflection reflection;
	public AnimationClip clip;
	public float time;
	public int frame;
	public GameObject target;
	public PSTabMuscle tabMuscle;
	public PSTabOption tabOption;
	public PSMuscleHandle muscleHandle;
	public int cleanCount = 0;
	public bool onCleaning = false;
	public List<EditorCurveBinding> unusedBindings;
	public Vector2 scrollPos;

	public bool onFocus;

	[MenuItem ("Window/Muscle Animation Editor")]
	static void InitWindow ()
	{
		PSMuscleEditorWindow window = (PSMuscleEditorWindow)EditorWindow.GetWindow (typeof(PSMuscleEditorWindow));
		window.Show ();
	}

	public void Initialize ()
	{
		if (tabMuscle == null) {
			tabMuscle = new PSTabMuscle ();
			tabMuscle.Init ();
		}
			
		if (tabOption == null)
			tabOption = new PSTabOption ();
		if (muscleHandle == null)
			muscleHandle = new PSMuscleHandle ();
		
		muscleHandle.setTabs (tabMuscle, tabOption);
		if (curTab == null)
			curTab = tabMuscle;
	}

	void OnInspectorUpdate ()
	{
		this.Repaint ();
		UpdateValue ();
	}

	private void UpdateValue ()
	{
		if (reflection == null)
			reflection = PSReflectionFactory.GetInstance ().GetReflection ();	

		if (reflection == null)
			return;

		GameObject newTarget = reflection.GetRootObject ();

		if (!IsValidTarget (newTarget)) {
			target = null;
			return;
		}
		
		if (newTarget != target) {
			target = newTarget;
			tabMuscle.SetTarget (target);
			muscleHandle.SetTarget (target);

			PSLogger.Log ("New Target");
		}
		
		clip = reflection.GetClip ();
		time = reflection.GetTime ();
		frame = reflection.GetFrame ();
		
		tabMuscle.SetClip (clip);
		tabMuscle.SetTimeFrame (time, frame);

		if (AnimationMode.InAnimationMode () && !onFocus) {
			tabMuscle.OnUpdateValue ();
			//tabMuscle.ResampleAnimation();
		}
	}

	void OnFocus ()
	{
		//Debug.Log("OnFocus");
		onFocus = true;
	}

	void OnLostFocus ()
	{
		//Debug.Log("OnLostFocus");
		onFocus = false;
	}

	public void OnEnable ()
	{
		titleContent = new GUIContent ("Muscle Editor");
		Initialize ();
		SceneView.duringSceneGui += ShowHandles;
	}

	public void OnDisable ()
	{
		SceneView.duringSceneGui -= ShowHandles;
	}

	void ShowHandles (SceneView sceneview)
	{
		if (muscleHandle != null && AnimationMode.InAnimationMode ())
			muscleHandle.ShowHandles ();
	}

	/***********************************************
	 *  GUI METHODS
	 ***********************************************/
	
	void OnGUI ()
	{
		//Debug.Log ("OnGUI:"+Event.current.type);
		
		if (reflection == null) {
			reflection = PSReflectionFactory.GetInstance ().GetReflection ();	
		}
		
		if (target == null) {
			EditorGUILayout.HelpBox ("No humanoid target found, please select humanoid gameobject from the hierarchy.", MessageType.None);
			if (GUILayout.Button ("Refresh", EditorStyles.miniButton, GUILayout.Width (100))) {
				reflection.Init ();
			}
			return;
		}
		
		this.InfoGUI ();

		EditorGUILayout.Space ();
		EditorGUILayout.BeginHorizontal ();
		if (GUILayout.Toggle (curTab == tabMuscle, "MUSCLE", EditorStyles.toolbarButton))
			curTab = tabMuscle;
		
		if (GUILayout.Toggle (curTab == tabOption, "OPTIONS", EditorStyles.toolbarButton))
			curTab = tabOption;
		EditorGUILayout.EndHorizontal ();

		this.TabGUI ();
	}

	protected void TabGUI ()
	{
		scrollPos = EditorGUILayout.BeginScrollView (scrollPos);

		EditorGUI.BeginDisabledGroup (!AnimationMode.InAnimationMode ());
		
		curTab.OnTabGUI ();
		
		EditorGUI.EndDisabledGroup ();

		EditorGUILayout.EndScrollView ();

	}

	protected void InfoGUI ()
	{
		EditorGUILayout.BeginHorizontal (EditorStyles.toolbar);
		EditorGUILayout.LabelField ("Information");
		if (GUILayout.Button ("Refresh", EditorStyles.toolbarButton, GUILayout.Width (80))) {
			reflection.Init ();
		}
		EditorGUILayout.EndHorizontal ();

		Color tmpColor = GUI.color;
		GUI.color = Color.yellow;
		EditorGUILayout.HelpBox ("You should back up your clip files before using this tool!", MessageType.Warning);
		GUI.color = tmpColor;

		EditorGUILayout.HelpBox ("Target: " + target + "\nClip: " + clip + "\nTime: " + time + "s\nFrame: " + frame, MessageType.None);

		if (GUILayout.Button ("Remove useless properties", EditorStyles.miniButton, GUILayout.Width (180))) {

			clip = reflection.GetClip ();

			if (!onCleaning && clip != null && target != null) {
				EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings (clip);
				onCleaning = true;

				unusedBindings = new List<EditorCurveBinding> ();

				for (int i = 0; i < bindings.Length; i++) {
					if (!string.IsNullOrEmpty (bindings [i].path)
					    && target.transform.Find (bindings [i].path) == null) {
						unusedBindings.Add (bindings [i]);
					}
				}
			}

		}

		if (onCleaning) {

			if (cleanCount < unusedBindings.Count) {
				float progress = (float)cleanCount / (float)unusedBindings.Count;
				string info = "Removing: " + PSMuscleEditorUtil.PathTruncate (unusedBindings [cleanCount].path) + " : " + unusedBindings [cleanCount].propertyName;
				EditorUtility.DisplayProgressBar ("Remove useless properties", info, progress);
				AnimationUtility.SetEditorCurve (clip, unusedBindings [cleanCount], null);
				cleanCount++;
			} else {
				cleanCount = 0;
				onCleaning = false;
				EditorUtility.ClearProgressBar ();
			}
		}
	}

	/***********************************************
	 * 
	 ***********************************************/
	
	private bool IsValidTarget (GameObject obj)
	{
		if (obj != null) {
			Animator animator = obj.GetComponent (typeof(Animator)) as Animator;
			if (animator != null) {
				Avatar avatar = animator.avatar;
				if (avatar != null && avatar.isValid && avatar.isHuman) {
					return true;
				}
			}
		}

		return false;
	}
	
}


