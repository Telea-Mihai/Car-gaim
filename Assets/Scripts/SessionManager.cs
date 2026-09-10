using UnityEngine;
using UnityEngine.SceneManagement;
public class SessionManager : MonoBehaviour
{
    public UIManager uiManager;
    public RaceManager currentRaceManager;
    public Vehicle activeVehicle;
    private CollisionDeformationDistribuitor cdd;
    private bool sessionOver;
    private bool crashUiShown;

    void OnEnable()
    {
        if (currentRaceManager != null)
            currentRaceManager.OnLapCompleted += HandleLapCompleted;
    }

    void OnDisable()
    {
        if (currentRaceManager != null)
            currentRaceManager.OnLapCompleted -= HandleLapCompleted;
    }

    void Start()
    {
        if (activeVehicle != null)
            cdd = activeVehicle.GetComponent<CollisionDeformationDistribuitor>();
    }

    void Update()
    {
        if (sessionOver || crashUiShown || cdd == null || !cdd.impactStarted)
            return;

        TriggerCrashUI();
    }

    public void TriggerCrashUI()
    {
        if (sessionOver || crashUiShown)
            return;

        crashUiShown = true;
        if (activeVehicle != null)
        {
            activeVehicle.MaxRPM = 0;
            activeVehicle.engineRPM = 0;
            activeVehicle.IdleRPM = 0;
            activeVehicle.BaseSteeringAngle = 0;
        }

        if (uiManager != null)
            uiManager.ToggleCrashMenu();
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        Application.LoadLevel(0);
    }

void HandleLapCompleted(Racer racer)
    {
        if (sessionOver || racer == null || !IsPlayer(racer))
            return;

        sessionOver = true;

        int position = currentRaceManager.GetFinishPosition(racer);
        int total = Mathf.Max(1, currentRaceManager.GetRacerCount());
        float time = currentRaceManager.GetFinishTime(racer);

        if (activeVehicle != null)
            activeVehicle.forceHoldStop();

        if (uiManager != null)
            uiManager.ShowFinishMenu($"Position: {position}/{total}", $"Time: {FormatRaceTime(time)}");
    }

    bool IsPlayer(Racer racer)
    {
        if (activeVehicle == null)
            return false;

        Vehicle vehicle = racer.GetComponent<Vehicle>();
        if (vehicle == null)
            vehicle = racer.GetComponentInParent<Vehicle>();
        if (vehicle == null)
            vehicle = racer.GetComponentInChildren<Vehicle>();

        return vehicle == activeVehicle;
    }

    static string FormatRaceTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int minutes = (int)(seconds / 60f);
        int secs = (int)(seconds % 60f);
        int ms = Mathf.Clamp(Mathf.RoundToInt((seconds - Mathf.Floor(seconds)) * 1000f), 0, 999);
        return $"{minutes}.{secs:00}.{ms:000}";
    }

    public void RestartSession()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        Application.LoadLevel(Application.loadedLevel);
    }
}
