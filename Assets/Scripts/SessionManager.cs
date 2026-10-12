using UnityEngine;

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
        SubscribeRaceFinished();
    }

    void OnDisable()
    {
        UnsubscribeRaceFinished();
    }

    public void SetCurrentRaceManager(RaceManager raceManager)
    {
        UnsubscribeRaceFinished();
        currentRaceManager = raceManager;
        SubscribeRaceFinished();
    }

    void SubscribeRaceFinished()
    {
        if (currentRaceManager != null)
            currentRaceManager.OnRaceFinished += HandleRaceFinished;
    }

    void UnsubscribeRaceFinished()
    {
        if (currentRaceManager != null)
            currentRaceManager.OnRaceFinished -= HandleRaceFinished;
    }

    void Start()
    {
        if (activeVehicle != null)
            cdd = activeVehicle.GetComponent<CollisionDeformationDistribuitor>();

        WireMenuButtons();
    }

    void WireMenuButtons()
    {
        var buttons = GetComponentsInChildren<UnityEngine.UI.Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null)
                continue;

            var label = button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (label == null)
                continue;

            string text = label.text != null ? label.text.Trim() : string.Empty;
            if (text.Equals("Restart", System.StringComparison.OrdinalIgnoreCase))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(RestartSession);
            }
            else if (text.Equals("Main Menu", System.StringComparison.OrdinalIgnoreCase))
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(LoadMainMenu);
            }
        }
    }

    void Update()
    {
        if (sessionOver || crashUiShown || cdd == null || !cdd.isLethal)
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
        Time.fixedDeltaTime = 0.02f;
        if (uiManager != null)
            uiManager.HideSessionMenus();
        RaceSession.SelectFreeDrive();
        SceneLoader.LoadMainMenu();
    }

    void HandleRaceFinished(Racer racer)
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
            uiManager.ShowFinishMenu($"Position: {position}/{total}", $"Time: {UIManager.FormatRaceTime(time)}");
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

    public void RestartSession()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        if (uiManager != null)
            uiManager.HideSessionMenus();
        SceneLoader.ReloadActiveScene();
    }
}
