using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Vehicle Data")]
    public Vehicle activeVehicle;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI rpmText;
    public TextMeshProUGUI gearText;

    [Header("Menus")]
    public GameObject crashMenu;
    public GameObject pauseMenu;
    public GameObject finishMenu;
    [Tooltip("Designer panel under this UI canvas. Keep inactive by default.")]
    public GameObject startRacePanel;
    [Tooltip("Optional; if empty, the first Button under startRacePanel is used.")]
    public Button startRaceButton;
    public TextMeshProUGUI positionText;
    public TextMeshProUGUI timeText;

    [Header("Race HUD")]
    public TextMeshProUGUI countDownText;
    public TextMeshProUGUI lapText;
    public TextMeshProUGUI lapTimeText;
    public TextMeshProUGUI bestLapTimeText;
    public TextMeshProUGUI lapDelta;
    public TextMeshProUGUI realTimePositionText;

    [Header("Race HUD Colors")]
    public Color deltaFasterColor = new Color(0.2f, 0.85f, 0.35f);
    public Color deltaSlowerColor = new Color(0.95f, 0.25f, 0.25f);
    public Color deltaNeutralColor = Color.white;

    void Awake()
    {
        if (startRacePanel != null)
            startRacePanel.SetActive(false);
    }

    void Update()
    {
        if (activeVehicle)
        {
            if (rpmText != null)
                rpmText.text = "RPM " + Mathf.FloorToInt(activeVehicle.engineRPM);
            if (speedText != null)
                speedText.text = "KM/H " + Mathf.FloorToInt(activeVehicle.GetComponent<Rigidbody>().linearVelocity.magnitude * 3.6f);

            if (gearText != null)
            {
                if (activeVehicle.CurrentGear == 0)
                    gearText.text = "N";
                else if (activeVehicle.CurrentGear == -1)
                    gearText.text = "R";
                else
                    gearText.text = activeVehicle.CurrentGear.ToString();
            }
        }
    }

    public void ToggleCrashMenu()
    {
        if (crashMenu != null)
            crashMenu.SetActive(true);
    }

    public void ShowStartRacePanel(bool show)
    {
        if (startRacePanel != null)
            startRacePanel.SetActive(show);
    }

    public void HideSessionMenus()
    {
        if (crashMenu != null)
            crashMenu.SetActive(false);
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
        if (finishMenu != null)
            finishMenu.SetActive(false);
        if (startRacePanel != null)
            startRacePanel.SetActive(false);
    }

    /// <summary>
    /// Wires the designer Start Race button. Does not create UI.
    /// </summary>
    public void BindStartRaceButton(UnityAction onStart)
    {
        Button button = startRaceButton;
        if (button == null && startRacePanel != null)
            button = startRacePanel.GetComponentInChildren<Button>(true);

        if (button == null)
        {
            if (startRacePanel == null)
            {
                Debug.LogWarning(
                    "[UIManager] Assign startRacePanel on the UI prefab so you can design the Start Race screen.");
            }

            return;
        }

        startRaceButton = button;
        button.onClick.RemoveAllListeners();
        if (onStart != null)
            button.onClick.AddListener(onStart);
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
        if (startRacePanel != null)
            startRacePanel.SetActive(false);

        if (positionText != null)
            positionText.text = position;
        if (timeText != null)
            timeText.text = time;
        if (finishMenu != null)
            finishMenu.SetActive(true);
    }

    public void setCountDownTimer(string text)
    {
        if (countDownText != null)
            countDownText.text = text;
    }

    public void UpdateRaceHud(
        int position,
        int totalRacers,
        int currentLap,
        int totalLaps,
        float currentLapTime,
        float lastLapTime,
        float bestLapTime,
        float lapDeltaSeconds,
        bool hasDelta,
        bool showCompletedLapStats)
    {
        if (realTimePositionText != null)
            realTimePositionText.text = $"{position}/{Mathf.Max(1, totalRacers)}";

        if (lapText != null)
            lapText.text = $"LAP {Mathf.Clamp(currentLap, 1, Mathf.Max(1, totalLaps))}/{Mathf.Max(1, totalLaps)}";
        
        if (lapTimeText != null)
            lapTimeText.text = FormatRaceTime(currentLapTime);

        if (bestLapTimeText != null)
        {
            bestLapTimeText.text = bestLapTime > 0f
                ? $"BEST {FormatRaceTime(bestLapTime)}"
                : "BEST --.---";
        }

        if (lapDelta != null && currentLap > 1)
        {
            if (showCompletedLapStats && hasDelta)
            {
                string sign = lapDeltaSeconds >= 0f ? "+" : "-";
                lapDelta.text = $"{sign}{FormatRaceTime(Mathf.Abs(lapDeltaSeconds))}";
                lapDelta.color = lapDeltaSeconds < 0f
                    ? deltaFasterColor
                    : lapDeltaSeconds > 0f
                        ? deltaSlowerColor
                        : deltaNeutralColor;
            }
            else if (showCompletedLapStats && lastLapTime > 0f && !hasDelta)
            {
                lapDelta.text = FormatRaceTime(lastLapTime);
                lapDelta.color = deltaNeutralColor;
            }
            else if (!showCompletedLapStats && string.IsNullOrEmpty(lapDelta.text))
            {
                lapDelta.text = "--.---";
                lapDelta.color = deltaNeutralColor;
            }
        }
    }

    public void SetCurrentLapTime(float seconds)
    {
        if (lapTimeText != null)
            lapTimeText.text = FormatRaceTime(seconds);
    }

    public static string FormatRaceTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int minutes = (int)(seconds / 60f);
        int secs = (int)(seconds % 60f);
        int ms = Mathf.Clamp(Mathf.FloorToInt((seconds - Mathf.Floor(seconds)) * 1000f), 0, 999);

        if (minutes > 0)
            return $"{minutes}:{secs:00}.{ms:000}";
        return $"{secs}.{ms:000}";
    }
}
