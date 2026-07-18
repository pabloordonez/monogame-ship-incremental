namespace ShipGame.Domain;

public sealed record GameSettings(
    int MasterVolume,
    int MusicVolume,
    int EffectsVolume,
    bool Vibration,
    bool ScreenShake,
    bool Flashes,
    bool Fullscreen,
    bool TelemetryConsent)
{
    public static GameSettings Default { get; } = new(100, 80, 100, true, true, true, false, false);

    public bool IsValid =>
        MasterVolume is >= 0 and <= 100 &&
        MusicVolume is >= 0 and <= 100 &&
        EffectsVolume is >= 0 and <= 100;
}
