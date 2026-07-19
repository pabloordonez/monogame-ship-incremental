using System.Text.Json.Serialization;

namespace ShipGame.Persistence;

public sealed record SettingsDto(
    int MasterVolume,
    int MusicVolume,
    int EffectsVolume,
    bool Vibration,
    bool ScreenShake,
    bool Flashes,
    bool Fullscreen,
    bool TelemetryConsent,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Particles = null);
