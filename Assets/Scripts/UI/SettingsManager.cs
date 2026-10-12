using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Slider carSoundsVolumeSlider;
    [SerializeField] private Slider otherSoundsVolumeSlider;
    [SerializeField] private GameObject settingsPanel;

    private Resolution[] resolutions = System.Array.Empty<Resolution>();
    private readonly List<int> qualityIndices = new();
    private bool initialized;

    private const string QualityKey = "Settings.Quality";
    private const string ResolutionKey = "Settings.Resolution";
    private const string FullscreenKey = "Settings.Fullscreen";

    void Start()
    {
        InitializeQualityOptions();
        InitializeResolutionOptions();
        ConfigureVolumeSliders();
        LoadSettings();
        initialized = true;
        ApplySettings();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    void InitializeQualityOptions()
    {
        if (qualityDropdown == null)
            return;

        qualityDropdown.ClearOptions();
        qualityIndices.Clear();

        string[] names = QualitySettings.names;
        var options = new List<TMP_Dropdown.OptionData>(names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            options.Add(new TMP_Dropdown.OptionData(names[i]));
            qualityIndices.Add(i);
        }

        qualityDropdown.AddOptions(options);
    }

    void InitializeResolutionOptions()
    {
        if (resolutionDropdown == null)
            return;

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>(resolutions.Length);
        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution resolution = resolutions[i];
            options.Add(new TMP_Dropdown.OptionData(
                $"{resolution.width}x{resolution.height} ({resolution.refreshRateRatio}Hz)"));
        }

        resolutionDropdown.AddOptions(options);
    }

    void ConfigureVolumeSliders()
    {
        if (carSoundsVolumeSlider != null)
        {
            carSoundsVolumeSlider.minValue = 0f;
            carSoundsVolumeSlider.maxValue = 1f;
        }

        if (otherSoundsVolumeSlider != null)
        {
            otherSoundsVolumeSlider.minValue = 0f;
            otherSoundsVolumeSlider.maxValue = 1f;
        }
    }

    void LoadSettings()
    {
        GameAudioSettings.LoadFromPrefs();

        if (qualityDropdown != null && qualityDropdown.options.Count > 0)
        {
            int savedQuality = PlayerPrefs.GetInt(
                QualityKey,
                GetQualityDropdownIndex(QualitySettings.GetQualityLevel()));
            qualityDropdown.value = Mathf.Clamp(savedQuality, 0, qualityDropdown.options.Count - 1);
            qualityDropdown.RefreshShownValue();
        }

        if (resolutionDropdown != null && resolutions.Length > 0)
        {
            int currentResolution = FindCurrentResolution();
            int savedResolution = PlayerPrefs.GetInt(ResolutionKey, currentResolution);
            resolutionDropdown.value = Mathf.Clamp(savedResolution, 0, resolutions.Length - 1);
            resolutionDropdown.RefreshShownValue();
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = PlayerPrefs.GetInt(
                FullscreenKey,
                Screen.fullScreen ? 1 : 0) == 1;
        }

        if (carSoundsVolumeSlider != null)
            carSoundsVolumeSlider.value = GameAudioSettings.CarVolume;
        if (otherSoundsVolumeSlider != null)
            otherSoundsVolumeSlider.value = GameAudioSettings.OtherVolume;
    }

    int FindCurrentResolution()
    {
        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == Screen.width &&
                resolutions[i].height == Screen.height)
            {
                return i;
            }
        }

        return Mathf.Max(0, resolutions.Length - 1);
    }

    int GetQualityDropdownIndex(int qualityIndex)
    {
        int index = qualityIndices.IndexOf(qualityIndex);
        return index >= 0 ? index : 0;
    }

    public void ApplySettings()
    {
        if (!initialized)
            return;

        if (qualityDropdown != null && qualityIndices.Count > 0)
        {
            int selected = Mathf.Clamp(qualityDropdown.value, 0, qualityIndices.Count - 1);
            QualitySettings.SetQualityLevel(qualityIndices[selected], true);
            PlayerPrefs.SetInt(QualityKey, selected);
        }

        bool fullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : Screen.fullScreen;
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);

        if (resolutions.Length > 0 && resolutionDropdown != null)
        {
            int selected = Mathf.Clamp(resolutionDropdown.value, 0, resolutions.Length - 1);
            Resolution r = resolutions[selected];
            Screen.SetResolution(r.width, r.height, fullscreen);
            PlayerPrefs.SetInt(ResolutionKey, selected);
        }
        else
        {
            Screen.fullScreen = fullscreen;
        }

        float carVol = carSoundsVolumeSlider != null ? carSoundsVolumeSlider.value : 1f;
        float otherVol = otherSoundsVolumeSlider != null ? otherSoundsVolumeSlider.value : 1f;
        GameAudioSettings.Apply(carVol, otherVol);

        PlayerPrefs.Save();
    }

    public void OpenSettings()
    {
        if (settingsPanel == null)
            return;

        LoadSettings();
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
}
