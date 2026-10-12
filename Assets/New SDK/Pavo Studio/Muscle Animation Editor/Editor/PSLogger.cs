using UnityEngine;
using System.Collections;

public class PSLogger {
	public const int LOG = 1;
	public const int WARNING = 2;
	public const int ERROR = 3;

	public const int LOG_LEVEL = 2;

	public static void Log(string log){
		if (LOG_LEVEL <= LOG)
			Debug.Log (log);
	}

	public static void Warning(string log){
		if (LOG_LEVEL <= WARNING)
			Debug.LogWarning (log);
	}

	public static void Error(string log){
		if (LOG_LEVEL <= ERROR)
			Debug.LogError (log);
	}
}
