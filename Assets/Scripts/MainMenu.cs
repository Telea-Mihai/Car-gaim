using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void loadScene(int index)
    {
        Application.LoadLevel(index);
    }
    
    public void quitGame()
    {
        Application.Quit();
    }
}
