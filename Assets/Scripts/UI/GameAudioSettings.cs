using UnityEngine;

public static class GameAudioSettings
{
    public const string CarVolumeKey = "Settings.CarVolume";
    public const string OtherVolumeKey = "Settings.OtherVolume";

    public static float CarVolume { get; private set; } = 1f;
    public static float OtherVolume { get; private set; } = 1f;

    static GameAudioSettings()
    {
        LoadFromPrefs();
    }

    public static void LoadFromPrefs()
    {
        CarVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(CarVolumeKey, 1f));
        OtherVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(OtherVolumeKey, 1f));
    }

    public static void Apply(float carVolume, float otherVolume)
    {
        CarVolume = Mathf.Clamp01(carVolume);
        OtherVolume = Mathf.Clamp01(otherVolume);
        PlayerPrefs.SetFloat(CarVolumeKey, CarVolume);
        PlayerPrefs.SetFloat(OtherVolumeKey, OtherVolume);
    }
}
