using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class UIManager : MonoBehaviour
{
    [Header("Vehicle Data")]
    public Vehicle activeVehicle;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI rpmText;
    public TextMeshProUGUI gearText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (activeVehicle)
        {
            rpmText.text = "RPM " + activeVehicle.engineRPM;
            speedText.text ="KM/H" + Mathf.FloorToInt(activeVehicle.GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f) ;
            if(activeVehicle.CurrentGear == 0)
                gearText.text = "N";
            else if(activeVehicle.CurrentGear == -1)
                gearText.text = "R";
            else
                gearText.text = activeVehicle.CurrentGear.ToString();
        }
    }
}
