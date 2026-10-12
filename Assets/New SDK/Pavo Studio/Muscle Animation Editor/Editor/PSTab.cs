//
//  PSTab.cs
//  PSMuscle Editor
//
//  Copyright (c) 2014-2015 Pavo Studio.
//

using UnityEngine;
using System.Collections;

public abstract class PSTab {

	public AnimationClip clip;
	public float time;
	public int frame;
	public GameObject target;
	

	public void SetClip(AnimationClip clip){
		this.clip = clip;
	}

	public void SetTarget(GameObject target){
		this.target = target;
		OnTargetChange ();
	}

	public void SetFrame(int frame){
		this.frame = frame;
	}

	public void SetTime(float time){
		this.time = time;
	}

	public void SetTimeFrame(float time, int frame){
		this.time = time;
		this.frame = frame;
	}

	public abstract void OnTargetChange();
	public abstract void OnUpdateValue();
	public abstract void OnTabGUI();

}
