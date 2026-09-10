using System;
using System.Collections;
using UnityEngine;

public class CollisionDeformationDistribuitor : MonoBehaviour
{
    [Header("Major impact detection")] public float lethalSpeed = 120f;
    public float slowMotionTime = 0.5f;
    public float slowMotionDuration = 2f;
    public float slowMotionLerpStep = 0.2f;
    public bool lethalImpactCount = false;
    public bool impactStarted = false;
    
    private void OnCollisionEnter(Collision other)
    {
        foreach (ContactPoint contact in other.contacts)
        {
            if(contact.thisCollider.GetComponent<SoftMeshLight>())
                contact.thisCollider.GetComponent<SoftMeshLight>().HandleCollision(other);
        }
        
        if (!impactStarted && lethalImpactCount && other.impulse.magnitude > lethalSpeed)
        {
            Debug.Log("Lethal impact");
            impactStarted = true;
            StartCoroutine(lethalImpact());
        }
    }

    IEnumerator lethalImpact()
    {
        while (!Mathf.Approximately(Time.timeScale, slowMotionTime))
        {
            Time.timeScale = Mathf.Lerp(Time.timeScale, slowMotionTime,  slowMotionLerpStep);
            Time.fixedDeltaTime = Time.timeScale * 0.02f; 
            yield return null;
        }
        Time.timeScale = slowMotionTime;
        yield return new WaitForSecondsRealtime(slowMotionDuration);
        while (Mathf.Approximately(Time.timeScale, 1))
        {
            Time.timeScale = Mathf.Lerp(Time.timeScale, 1, slowMotionLerpStep);
            Time.fixedDeltaTime = Time.timeScale * 0.02f; 
            yield return null;
        }
        Time.timeScale = 1;
        impactStarted = false;
    }
}
