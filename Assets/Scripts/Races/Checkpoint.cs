using System;
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public Checkpoint nextCheckpoint;
    public Checkpoint previousCheckpoint;
    public bool isFinish;
    public System.Action<Racer> OnCheckpointReached;
    
    void OnTriggerEnter(Collider other)
    {
        Racer racer = other.GetComponentInParent<Racer>();
        if (racer == null)
            return;

        if (racer.target == transform)
        {
            racer.target = nextCheckpoint != null ? nextCheckpoint.transform : null;
            racer.OnTargetChanged();
            Debug.Log("Checkpoint reached " + transform.name);

            if (isFinish)
            {
                Debug.Log("Finish reached " + OnCheckpointReached == null);
                OnCheckpointReached?.Invoke(racer);
            }
        }
    }
}
