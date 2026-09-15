using UnityEngine;

public class ColissionParticles : MonoBehaviour
{
    public GameObject impactVFX;
    private void OnCollisionEnter(Collision other)
    {
        var cp = other.GetContact(0);
        Vector3 impactPointWS = cp.point;
        Vector3 impactNormalWS = -cp.normal;
        if (impactVFX != null)
        {
            GameObject effect = Instantiate(impactVFX, impactPointWS, Quaternion.Euler(impactNormalWS));
            effect.GetComponent<SelfDelete>().magnitude = other.impulse.magnitude;
        }
    }
}
