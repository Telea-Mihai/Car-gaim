using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;
public class UIManager : MonoBehaviour
{
    [Header("Vehicle Data")]
    public Vehicle activeVehicle;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI rpmText;
    public TextMeshProUGUI gearText;
    [Header("Menus")] public GameObject crashMenu;
    public GameObject pauseMenu;

    public GameObject finishMenu;
    public TextMeshProUGUI positionText;
    public TextMeshProUGUI timeText;
    [Header("Other")]
    public AudioMixer MasterMixer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
        if (activeVehicle)
        {
            rpmText.text = "RPM " + Mathf.FloorToInt(activeVehicle.engineRPM);
            speedText.text ="KM/H" + Mathf.FloorToInt(activeVehicle.GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f) ;
            if(activeVehicle.CurrentGear == 0)
                gearText.text = "N";
            else if(activeVehicle.CurrentGear == -1)
                gearText.text = "R";
            else
                gearText.text = activeVehicle.CurrentGear.ToString();
        }
    }

    public void ToggleCrashMenu()
    {
        if (crashMenu != null)
            crashMenu.SetActive(true);
    }

    public void TogglePauseMenu()
    {
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(!pauseMenu.activeSelf);
            Time.timeScale = pauseMenu.activeSelf ? 0 : 1;
        }
    }

    public void ShowFinishMenu(string position, string time)
    {
        if (crashMenu != null)
            crashMenu.SetActive(false);
        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        if (positionText != null)
            positionText.text = position;
        if (timeText != null)
            timeText.text = time;
        if (finishMenu != null)
            finishMenu.SetActive(true);
    }
}
