using UnityEngine;

public class RaceBootstrap : MonoBehaviour
{
    [Header("Race Managers")]
    [SerializeField] RaceManager race1;
    [SerializeField] RaceManager race2;
    [SerializeField] RaceManager race3;

    [Header("Session")]
    [SerializeField] SessionManager sessionManager;

    [Header("Start Race UI")]
    [SerializeField] GameObject startRacePanel;
    [SerializeField] UIManager uiManager;

    RaceManager activeRace;

    void Awake()
    {
        ResolveReferences();
        DeactivateAllRaces();

        if (RaceSession.IsRaceSelected)
            ActivateSelectedRace();
        else
            PrepareFreeDrive();
    }

    void Start()
    {
        if (activeRace != null)
        {
            activeRace.PrepareGrid();
            ShowStartRacePanel(true);
        }
        else
        {
            ShowStartRacePanel(false);
        }
    }

    void ResolveReferences()
    {
        if (race1 == null)
            race1 = FindRaceByName("Race1");
        if (race2 == null)
            race2 = FindRaceByName("Race2");
        if (race3 == null)
            race3 = FindRaceByName("Race3");

        if (sessionManager == null)
        {
            SessionManager[] managers = FindObjectsByType<SessionManager>(FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null && managers[i].uiManager != null)
                {
                    sessionManager = managers[i];
                    break;
                }
            }

            if (sessionManager == null && managers.Length > 0)
                sessionManager = managers[0];
        }

        if (uiManager == null)
            uiManager = FindFirstObjectByType<UIManager>();

        if (startRacePanel == null && uiManager != null)
            startRacePanel = uiManager.startRacePanel;

        if (uiManager != null)
            uiManager.BindStartRaceButton(OnStartRaceClicked);
        else if (startRacePanel != null)
        {
            var button = startRacePanel.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnStartRaceClicked);
            }
        }
    }

    static RaceManager FindRaceByName(string objectName)
    {
        RaceManager[] all = FindObjectsByType<RaceManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == objectName)
                return all[i];
        }

        return null;
    }

    void DeactivateAllRaces()
    {
        SetRaceActive(race1, false);
        SetRaceActive(race2, false);
        SetRaceActive(race3, false);
        activeRace = null;

        if (sessionManager != null)
            sessionManager.SetCurrentRaceManager(null);
    }

    void ActivateSelectedRace()
    {
        RaceManager selected = RaceSession.SelectedRaceId switch
        {
            1 => race1,
            2 => race2,
            3 => race3,
            _ => null
        };

        if (selected == null)
        {
            Debug.LogWarning($"[RaceBootstrap] Race {RaceSession.SelectedRaceId} not found.");
            PrepareFreeDrive();
            return;
        }

        SetRaceActive(selected, true);
        activeRace = selected;

        if (sessionManager != null)
            sessionManager.SetCurrentRaceManager(selected);

        // Mirror onto every SessionManager so duplicates stay consistent.
        SessionManager[] managers = FindObjectsByType<SessionManager>(FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null)
                managers[i].SetCurrentRaceManager(selected);
        }
    }

    void PrepareFreeDrive()
    {
        activeRace = null;
        if (sessionManager != null)
            sessionManager.SetCurrentRaceManager(null);

        SessionManager[] managers = FindObjectsByType<SessionManager>(FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null)
                managers[i].SetCurrentRaceManager(null);
        }

        if (sessionManager != null && sessionManager.activeVehicle != null)
        {
            sessionManager.activeVehicle.transform.rotation = Quaternion.identity;
            sessionManager.activeVehicle.startCar();
        }
    }

    static void SetRaceActive(RaceManager race, bool active)
    {
        if (race != null)
            race.gameObject.SetActive(active);
    }

    void ShowStartRacePanel(bool show)
    {
        if (uiManager != null)
            uiManager.ShowStartRacePanel(show);
        else if (startRacePanel != null)
            startRacePanel.SetActive(show);
    }

    public void OnStartRaceClicked()
    {
        if (activeRace == null)
            return;

        ShowStartRacePanel(false);
        activeRace.BeginRace();
    }
}
