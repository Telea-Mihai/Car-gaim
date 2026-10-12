using UnityEngine;
using System.Collections;
using UnityEditor;
using System.IO;

public abstract class PSBaseReflection
{
	protected string UNITYEDITOR_DLL;

	public PSBaseReflection ()
	{
		if (UNITYEDITOR_DLL == null)
			UNITYEDITOR_DLL = GetDLL ();
		if (UNITYEDITOR_DLL == null)
			Debug.LogError ("Muscle Animation Editor: UnityEditor.dll not found, you must specify the path yourself in PSBaseReflection.cs.");
	}

	private string GetDLL ()
	{
		// Win & Unity 5.4.x on Mac
		string path = EditorApplication.applicationContentsPath + "/Managed/UnityEditor.dll";

		if (File.Exists (path))
			return path;

		// Unity earlier version on Mac
		path = EditorApplication.applicationContentsPath + "/Frameworks/Managed/UnityEditor.dll";

		if (File.Exists (path))
			return path;

		return null;
	}

	public abstract void Init ();

	public abstract float GetTime ();

	public abstract int GetFrame ();

	public abstract AnimationClip GetClip ();

	public abstract GameObject GetRootObject ();
}
