using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Reflection;

public class PSReflectionForUnity4 : PSBaseReflection
{
	private object animationWindowState;
	private MethodInfo getTimeMethod;
	private FieldInfo getClipField;
	private FieldInfo getFrameField;
	private FieldInfo getRootObjectField;

	public override void Init ()
	{
		if (UNITYEDITOR_DLL == null)
			return;
		
		System.Type animationWindowType = Assembly.LoadFile (UNITYEDITOR_DLL).GetType ("UnityEditor.AnimationWindow");
		object animationWindow = EditorWindow.GetWindow (animationWindowType);
		if (animationWindow == null)
			return;

		animationWindowState = animationWindow.GetType ().GetProperty ("state").GetValue (animationWindow, null);
		if (animationWindowState == null)
			return;

		getTimeMethod = animationWindowState.GetType ().GetMethod ("GetTimeSeconds");
		getClipField = animationWindowState.GetType ().GetField ("m_ActiveAnimationClip");
		getFrameField = animationWindowState.GetType ().GetField ("m_Frame");
		getRootObjectField = animationWindowState.GetType ().GetField ("m_RootGameObject");
	}

	public override float GetTime ()
	{
		if (getTimeMethod == null)
			Init ();
		return getTimeMethod == null ? 0.0f : (float)getTimeMethod.Invoke (animationWindowState, null);
	}

	public override int GetFrame ()
	{
		if (getFrameField == null)
			Init ();
		return getFrameField == null ? 0 : (int)getFrameField.GetValue (animationWindowState);
	}

	public override GameObject GetRootObject ()
	{
		if (getRootObjectField == null)
			Init ();
		return getRootObjectField == null ? null : (GameObject)getRootObjectField.GetValue (animationWindowState);
	}

	public override AnimationClip GetClip ()
	{
		if (getClipField == null)
			Init ();
		return getClipField == null ? null : (AnimationClip)getClipField.GetValue (animationWindowState);
	}
}
