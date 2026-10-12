using UnityEngine;
using System.Collections;

public class PSReflectionFactory
{
	private static PSReflectionFactory instance;

	public static PSReflectionFactory GetInstance ()
	{
		if (instance == null)
			instance = new PSReflectionFactory ();
		return instance;
	}

	public PSBaseReflection GetReflection ()
	{
		// Unity 5.1+ / 2017+ / 6000+ share the AnimationWindow reflection surface.
		// String-prefix compares like "2017." vs "5.1.0" incorrectly pick Unity 4.
		if (IsLegacyUnity4 ())
			return new PSReflectionForUnity4 ();

		return new PSReflectionForUnity510 ();
	}

	private static bool IsLegacyUnity4 ()
	{
		string version = Application.unityVersion;
		if (string.IsNullOrEmpty (version))
			return false;

		// "4.x.x" only. Year-based (2017+) and Unity 6 (6000+) are modern.
		char first = version [0];
		return first == '4';
	}

}
