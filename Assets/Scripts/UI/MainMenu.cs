using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] SettingsManager settingsManager;
    [SerializeField] Transform menuPanel;
    [SerializeField] bool rebuildMenuButtons = true;
    [SerializeField] private GameObject racesMenu;
   

    void Start()
    {
        if (settingsManager == null)
            settingsManager = GetComponent<SettingsManager>() ?? FindFirstObjectByType<SettingsManager>();
        
    }

    public void OpenRaceMenu()
    {
        racesMenu.SetActive(true);
    }

    public void CloseRaceMenu()
    {
        racesMenu.SetActive(false);
    }

    public void OnMenuButton(int index)
    {
        switch (index)
        {
            case 0:
                SelectRace(1);
                break;
            case 1:
                SelectRace(2);
                break;
            case 2:
                SelectRace(3);
                break;
            case 3:
                SelectFreeDrive();
                break;
            case 4:
                quitGame();
                break;
        }
    }
    

    public void SelectRace(int raceId)
    {
        RaceSession.SelectRace(raceId);
        SceneLoader.LoadRaceScene();
    }

    public void SelectFreeDrive()
    {
        RaceSession.SelectFreeDrive();
        SceneLoader.LoadRaceScene();
    }

    public void loadScene(int index)
    {
        if (index <= 0)
        {
            SceneLoader.LoadMainMenu();
            return;
        }

        RaceSession.SelectFreeDrive();
        SceneLoader.LoadRaceScene();
    }

    public void quitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
