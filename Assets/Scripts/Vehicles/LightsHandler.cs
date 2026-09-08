using UnityEngine;


public class LightsHandler : MonoBehaviour
{
    private Material brakeLightMaterial;
    [Header("Brake Light")]
    public MeshRenderer brakeLightMesh;
    public int brakeLightMaterialIndex;
    public float defaultEmission;
    public float underBrakingEmmision;
    public Light[] brakeLights;
    public float defaultIntensity;
    public float underBrakingIntensity;
    [Header("Removable Brake Light")]
    public MeshRenderer removableBrakeLight;
    public int removableBrakeLightMaterialIndex;
    private Material removableBrakeLightMaterial;
    private bool brokenOff;
    
    
    private Vehicle vehicle;
    
    void Start()
    {
        brakeLightMaterial = brakeLightMesh.materials[brakeLightMaterialIndex];
        removableBrakeLightMaterial = removableBrakeLight.materials[removableBrakeLightMaterialIndex];
        vehicle = GetComponent<Vehicle>();
        brakeLightMaterial.SetColor("_EmissiveColor", Color.white * defaultEmission);
        removableBrakeLightMaterial.SetColor("_EmissiveColor", Color.white * defaultEmission);
    }
    
    void FixedUpdate()
    {
        
        if (vehicle.brakeInput < 0.1f)
        {
            brakeLightMaterial.SetColor("_EmissiveColor", Color.white * defaultEmission);
            if(!brokenOff)
                removableBrakeLightMaterial.SetColor("_EmissiveColor", Color.white * defaultEmission);
            
            foreach (Light brakeLight in brakeLights)
                brakeLight.intensity = defaultIntensity;
        }
        else
        {
            brakeLightMaterial.SetColor("_EmissiveColor", Color.white * underBrakingEmmision);
            if(!brokenOff)
                removableBrakeLightMaterial.SetColor("_EmissiveColor", Color.white * underBrakingEmmision);
                
            foreach (Light brakeLight in brakeLights)
            {
                brakeLight.intensity = underBrakingIntensity;
            }
        }

        if (!brokenOff)
        {
            if (removableBrakeLight.GetComponent<Joint>() == null)
            {
                removableBrakeLightMaterial.SetColor("_EmissiveColor", Color.black * 0);
                brokenOff = true;
            }
            
        }
    }
}
