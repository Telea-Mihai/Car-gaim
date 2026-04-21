using System;
using UnityEngine;

public class CollisionDeformationDistribuitor : MonoBehaviour
{
    private void OnCollisionEnter(Collision other)
    {
        foreach (ContactPoint contact in other.contacts)
        {
            if(contact.thisCollider.GetComponent<SoftMeshLight>())
                contact.thisCollider.GetComponent<SoftMeshLight>().HandleCollision(other);
        }
    }
}
