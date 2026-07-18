using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record SettingsDto(
    int MasterVolume,
    int MusicVolume,
    int EffectsVolume,
    bool Vibration,
    bool ScreenShake,
    bool Flashes,
    bool Fullscreen,
    bool TelemetryConsent);
