using UnityEngine;
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;

public class PSReflectionForUnity510 : PSBaseReflection
{
	private object animationWindowState;
	private PropertyInfo timeProperty;
	private PropertyInfo clipProperty;
	private PropertyInfo frameProperty;
	private PropertyInfo rootObjectProperty;

	public override void Init ()
	{
		Type animationWindowType = Type.GetType ("UnityEditor.AnimationWindow,UnityEditor");
		if (animationWindowType == null && typeof(EditorWindow).Assembly != null)
			animationWindowType = typeof(EditorWindow).Assembly.GetType ("UnityEditor.AnimationWindow");

		if (animationWindowType == null)
			return;

		UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll (animationWindowType);
		object animationWindow = windows != null && windows.Length > 0 ? windows [0] : null;
		if (animationWindow == null)
			return;

		FieldInfo animEditorField = animationWindow.GetType ().GetField ("m_AnimEditor", getBindingFlags ());
		if (animEditorField == null)
			return;

		object animEditor = animEditorField.GetValue (animationWindow);
		if (animEditor == null)
			return;

		FieldInfo stateField = animEditor.GetType ().GetField ("m_State", getBindingFlags ());
		if (stateField == null)
			return;

		animationWindowState = stateField.GetValue (animEditor);
		if (animationWindowState == null)
			return;
		
		timeProperty = animationWindowState.GetType ().GetProperty ("currentTime");
		clipProperty = animationWindowState.GetType ().GetProperty ("activeAnimationClip");
		frameProperty = animationWindowState.GetType ().GetProperty ("currentFrame");
		rootObjectProperty = animationWindowState.GetType ().GetProperty ("activeRootGameObject");
	}
	
	public override float GetTime ()
	{
		if (timeProperty == null)
			Init ();

		return timeProperty == null || animationWindowState == null
			? 0.0f
			: (float)timeProperty.GetValue (animationWindowState, null);
	}
	
	public override int GetFrame ()
	{
		if (frameProperty == null)
			Init ();
		return frameProperty == null || animationWindowState == null
			? 0
			: (int)frameProperty.GetValue (animationWindowState, null);
	}
	
	public override GameObject GetRootObject ()
	{
		if (rootObjectProperty == null)
			Init ();
		return rootObjectProperty == null || animationWindowState == null
			? null
			: (GameObject)rootObjectProperty.GetValue (animationWindowState, null);

	}
	
	public override AnimationClip GetClip ()
	{
		if (clipProperty == null)
			Init ();
		return clipProperty == null || animationWindowState == null
			? null
			: (AnimationClip)clipProperty.GetValue (animationWindowState, null);
	}

	private BindingFlags getBindingFlags ()
	{
		return BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
	}
}
