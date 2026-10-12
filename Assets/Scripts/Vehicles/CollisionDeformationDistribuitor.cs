using System;
using System.Collections;
using UnityEngine;

public class CollisionDeformationDistribuitor : MonoBehaviour
{
    [Header("Major impact detection")] public float lethalSpeed = 12000f;
    public float damageHitSpeed = 9000f;
    public float reactionHitSpeed = 2000f;
    public float slowMotionTime = 0.5f;
    public float slowMotionDuration = 2f;
    public float slowMotionLerpStep = 0.2f;
    public int lifes = 3;
    public bool lethalImpactCount = false;
    public bool impactStarted = false;
    public bool isLethal = false;
    public bool reaction = false;
    
    private void OnCollisionEnter(Collision other)
    {
        foreach (ContactPoint contact in other.contacts)
        {
            if(contact.thisCollider.GetComponent<SoftMeshLight>())
                contact.thisCollider.GetComponent<SoftMeshLight>().HandleCollision(other);
        }
        
        Debug.Log(other.impulse.magnitude);
        if (!impactStarted && lethalImpactCount && other.impulse.magnitude > damageHitSpeed)
        {
            
            impactStarted = true;
            
            if(other.impulse.magnitude > lethalSpeed)
                isLethal = true;
            lifes--;
            if (lifes == 0)
                isLethal = true;
            
            StartCoroutine(lethalImpact());
        }

        if (other.impulse.magnitude > reactionHitSpeed && !reaction)
            reaction = true;
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
