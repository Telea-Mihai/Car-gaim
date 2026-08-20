using UnityEngine;

/// <summary>
/// Race identity + checkpoint progress. Path following is owned by <see cref="RacePathFollower"/>
/// when a <see cref="BakedRacePath"/> is assigned on <see cref="RaceManager"/>.
/// Live EasyRoads / OIO / NavMesh racing rebuild has been retired from the race hot path.
/// </summary>

public class Racer : MonoBehaviour
{
    [Header("Progress")]
    public Transform target;
    

    void Awake()
    {
    }

    void Update()
    {
       
    }

    public void SetLivePathingEnabled(bool enabled)
    {
        
    }

   
    public void OnTargetChanged()
    {
        
    }

    public void ForceRefresh()
    {
        var follower = GetComponent<RacePathFollower>();
        if (follower != null && follower.isActiveAndEnabled)
            follower.SyncToNearest();
    }
}
