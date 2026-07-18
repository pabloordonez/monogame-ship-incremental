using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record SaveLoadResult(
    CompatibilityStatus Status,
    SaveEnvelope? Envelope = null,
    bool RecoveredFromBackup = false,
    string? Diagnostic = null);
