/// <summary>
/// Menu → race scene handoff. 0 = Free Drive (no RaceManager), 1–3 = Race1–Race3.
/// </summary>
public static class RaceSession
{
    public const int FreeDrive = 0;

    public static int SelectedRaceId { get; set; } = FreeDrive;

    public static bool IsRaceSelected => SelectedRaceId >= 1 && SelectedRaceId <= 3;

    public static void SelectRace(int raceId)
    {
        SelectedRaceId = raceId;
    }

    public static void SelectFreeDrive()
    {
        SelectedRaceId = FreeDrive;
    }
}
