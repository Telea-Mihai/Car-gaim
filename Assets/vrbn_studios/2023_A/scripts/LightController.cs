using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
// using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

public class LightController : MonoBehaviour
{
    float maxEmissionValue = 20;
    public float maxEmissionValueURP = 20;
    public float maxEmissionValueHDRP = 250000;
    public Transform sunTransform;

    bool lights = false;

    private EmissionController[] emissionControllers;

    // Start is called before the first frame update
    void Start()
    {
        emissionControllers = FindObjectsOfType<EmissionController>();
    }

    void setEnableLights(bool lights)
    {
        float weight = lights ? 0 : 1;
        sunTransform.localRotation = Quaternion.Euler(0, 0, lights ? -12 : -100);
    }

    void setEmissionValue(bool lights)
    {
        if (GraphicsSettings.currentRenderPipeline.GetType().ToString().Contains("HighDefinition"))
        {
            maxEmissionValue = maxEmissionValueHDRP;
        }
        else 
        {
            maxEmissionValue = maxEmissionValueURP;
        }

        foreach (EmissionController ec in emissionControllers)
        {
            // Debug.Log("maxEmissionValue = " + maxEmissionValue);
            ec.emissionIntensity = lights ? maxEmissionValue : 0;
            ec.Dirty();
        }
    }

    void updateLights(bool lights)
    {
        // enable/disable directional lights
        setEnableLights(lights);

        // setting up emission value
        setEmissionValue(lights);
    }

    public void toggleLight()
    {
        lights = !lights;
        updateLights(lights);
    }
}
