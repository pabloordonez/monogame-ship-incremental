using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public sealed record MetaSaveLoadResult(
    CompatibilityStatus Status,
    MetaProfileSnapshot? Profile = null,
    bool RecoveredFromBackup = false,
    bool Migrated = false,
    IReadOnlyList<string>? Diagnostics = null);
