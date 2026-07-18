using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public static class MetaSaveSchema
{
    /// <summary>P0 foundation profile.json schema.</summary>
    public const int Foundation = 1;

    /// <summary>
    /// P4 meta profile-v2.json schema. Increment when persisted meta shape/meaning changes.
    /// Foundation <see cref="ContractVersions.Save"/> remains 1 for the walking-skeleton file.
    /// </summary>
    public const int Current = 2;

    public const int MaxFileBytes = 4 * 1024 * 1024;
    public const int MaxStringLength = 128;
    public const int MaxCollectionCount = 4096;
}

public sealed record MetaSaveVersions(
    int Save,
    int Content,
    int Generation,
    int Rng,
    int Replay,
    int Telemetry)
{
    public static MetaSaveVersions Current { get; } = new(
        MetaSaveSchema.Current,
        ContractVersions.Content,
        ContractVersions.Generation,
        ContractVersions.Rng,
        ContractVersions.Replay,
        ContractVersions.Telemetry);
}

public sealed record ResourceAmountsDto(long Ferrite, long Lumen, long DataCores);

public sealed record LifetimeCountersDto(
    long Extractions,
    long NormalKills,
    long EliteKills,
    long FerriteCollected,
    long ResourceCellsBroken,
    long IonVeilExtractions);

public sealed record LoadoutDto(
    string Weapon,
    string Mining,
    string Shield,
    string Engine,
    string Utility);

public sealed record SettingsDto(
    int MasterVolume,
    int MusicVolume,
    int EffectsVolume,
    bool Vibration,
    bool ScreenShake,
    bool Flashes,
    bool Fullscreen,
    bool TelemetryConsent);

public sealed record TransactionReceiptDto(string TransactionId, string Operation, ulong Fingerprint);

public sealed record RunSummaryDto(
    string RunId,
    string EnvironmentId,
    bool Succeeded,
    ResourceAmountsDto Earned,
    ResourceAmountsDto Banked,
    ResourceAmountsDto Retained,
    ResourceAmountsDto Lost);

public sealed record MetaProfileDto(
    ulong ProfileSeed,
    long RunIndex,
    ResourceAmountsDto Balances,
    LifetimeCountersDto Counters,
    IReadOnlyList<string> PurchasedResearchIds,
    IReadOnlyList<string> UnlockedEnvironmentIds,
    LoadoutDto RequestedLoadout,
    IReadOnlyList<TransactionReceiptDto> Transactions,
    SettingsDto Settings,
    RunSummaryDto? PreviousRun);

public sealed record MetaSaveEnvelope(
    MetaSaveVersions Versions,
    string BuildId,
    string CatalogFingerprint,
    MetaProfileDto Profile,
    string Checksum);

public sealed record MetaContentCompatibility(
    IReadOnlySet<string> ResearchIds,
    IReadOnlySet<string> EnvironmentIds,
    IReadOnlySet<string> ModuleIds);

public sealed record MetaSaveLoadResult(
    CompatibilityStatus Status,
    MetaProfileSnapshot? Profile = null,
    bool RecoveredFromBackup = false,
    bool Migrated = false,
    IReadOnlyList<string>? Diagnostics = null);

public sealed class MetaSaveRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32
    };

    private readonly string _path;
    private readonly string _backupPath;

    public MetaSaveRepository(string saveDirectory, string fileName = "profile-v2.json")
    {
        if (string.IsNullOrWhiteSpace(saveDirectory))
            throw new ArgumentException("A save directory is required.", nameof(saveDirectory));
        if (Path.GetFileName(fileName) != fileName)
            throw new ArgumentException("Save file name cannot contain a path.", nameof(fileName));
        var root = Path.GetFullPath(saveDirectory);
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, fileName);
        _backupPath = _path + ".bak";
    }

    public MetaSaveEnvelope CreateEnvelope(
        MetaProfileSnapshot profile,
        string buildId,
        string catalogFingerprint)
    {
        ValidateHeader(buildId, catalogFingerprint);
        var dto = ToDto(profile);
        ValidateProfile(dto);
        return new(
            MetaSaveVersions.Current,
            buildId,
            catalogFingerprint,
            dto,
            ComputeChecksum(MetaSaveVersions.Current, buildId, catalogFingerprint, dto));
    }

    public void Write(MetaSaveEnvelope envelope)
    {
        ValidateEnvelope(envelope, requireCurrent: true);
        var temp = _path + "." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(
                       temp,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, envelope, JsonOptions);
                stream.Flush(true);
                if (stream.Length > MetaSaveSchema.MaxFileBytes)
                    throw new InvalidDataException("Serialized save exceeds the size limit.");
            }

            using (var verify = File.OpenRead(temp))
            {
                var decoded = JsonSerializer.Deserialize<MetaSaveEnvelope>(verify, JsonOptions)
                    ?? throw new InvalidDataException("Temporary save is empty.");
                ValidateEnvelope(decoded, requireCurrent: true);
            }

            if (File.Exists(_path))
                File.Replace(temp, _path, _backupPath, true);
            else
            {
                File.Move(temp, _path);
                File.Copy(_path, _backupPath, true);
            }
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    public MetaSaveLoadResult Load(
        string expectedCatalogFingerprint,
        MetaContentCompatibility? knownContent = null)
    {
        ValidateText(expectedCatalogFingerprint, nameof(expectedCatalogFingerprint));
        if (!File.Exists(_path) && !File.Exists(_backupPath))
            return new(CompatibilityStatus.Missing, Diagnostics: ["No profile save exists."]);

        var primary = TryRead(_path, expectedCatalogFingerprint, knownContent);
        if (primary.Status is CompatibilityStatus.Supported or
            CompatibilityStatus.IncompatibleNewer or
            CompatibilityStatus.MissingContent)
            return primary;

        var backup = TryRead(_backupPath, expectedCatalogFingerprint, knownContent);
        if (backup.Status == CompatibilityStatus.Supported)
            return backup with
            {
                RecoveredFromBackup = true,
                Diagnostics = (backup.Diagnostics ?? [])
                    .Prepend("Primary was invalid; loaded the known-good backup.")
                    .ToArray()
            };
        return primary with
        {
            Diagnostics = (primary.Diagnostics ?? [])
                .Concat((backup.Diagnostics ?? []).Select(message => "Backup: " + message))
                .ToArray()
        };
    }

    private static MetaSaveLoadResult TryRead(
        string path,
        string expectedCatalogFingerprint,
        MetaContentCompatibility? knownContent)
    {
        if (!File.Exists(path))
            return new(CompatibilityStatus.Missing, Diagnostics: ["File missing."]);
        try
        {
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > MetaSaveSchema.MaxFileBytes)
                throw new InvalidDataException("Save file size is invalid.");
            var bytes = File.ReadAllBytes(path);
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                MaxDepth = 32,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            var saveVersion = ReadRequiredInt(document.RootElement, "Versions", "Save");
            if (saveVersion > MetaSaveSchema.Current)
                return new(CompatibilityStatus.IncompatibleNewer, Diagnostics: ["Save schema is newer than this build."]);

            MetaSaveEnvelope envelope;
            var migrated = false;
            if (saveVersion == MetaSaveSchema.Foundation)
            {
                envelope = MigrateFoundation(bytes, expectedCatalogFingerprint);
                migrated = true;
            }
            else if (saveVersion == MetaSaveSchema.Current)
            {
                envelope = JsonSerializer.Deserialize<MetaSaveEnvelope>(bytes, JsonOptions)
                    ?? throw new InvalidDataException("Save is empty.");
                ValidateEnvelope(envelope, requireCurrent: false);
            }
            else
            {
                throw new InvalidDataException($"Unsupported old save schema {saveVersion}.");
            }

            var compatibility = Classify(envelope, expectedCatalogFingerprint);
            if (compatibility != CompatibilityStatus.Supported)
                return new(compatibility, Migrated: migrated, Diagnostics: [$"Compatibility status: {compatibility}."]);

            var profile = FromDto(envelope.Profile);
            var diagnostics = FindUnknownIds(profile, knownContent);
            return new(CompatibilityStatus.Supported, profile, Migrated: migrated, Diagnostics: diagnostics);
        }
        catch (Exception exception) when (exception is
            JsonException or IOException or InvalidDataException or FormatException or
            ArgumentException or OverflowException or NullReferenceException)
        {
            return new(CompatibilityStatus.Corrupt, Diagnostics: [exception.Message]);
        }
    }

    private static CompatibilityStatus Classify(
        MetaSaveEnvelope envelope,
        string expectedCatalogFingerprint)
    {
        if (envelope.Versions.Save > MetaSaveSchema.Current ||
            envelope.Versions.Generation > ContractVersions.Generation ||
            envelope.Versions.Rng > ContractVersions.Rng ||
            envelope.Versions.Replay > ContractVersions.Replay ||
            envelope.Versions.Telemetry > ContractVersions.Telemetry)
            return CompatibilityStatus.IncompatibleNewer;
        if (envelope.Versions.Content != ContractVersions.Content ||
            !string.Equals(envelope.CatalogFingerprint, expectedCatalogFingerprint, StringComparison.Ordinal))
            return CompatibilityStatus.MissingContent;
        if (envelope.Versions != MetaSaveVersions.Current)
            return CompatibilityStatus.Corrupt;
        return CompatibilityStatus.Supported;
    }

    private static MetaSaveEnvelope MigrateFoundation(byte[] bytes, string expectedCatalogFingerprint)
    {
        var old = JsonSerializer.Deserialize<SaveEnvelope>(bytes, JsonOptions)
            ?? throw new InvalidDataException("Foundation save is empty.");
        if (old.Versions.Save != MetaSaveSchema.Foundation)
            throw new InvalidDataException("Foundation migration received the wrong schema.");
        var oldExpected = ComputeFoundationChecksum(
            old.Versions,
            old.BuildId,
            old.CatalogFingerprint,
            old.Profile);
        ValidateChecksum(oldExpected, old.Checksum);
        if (!string.Equals(old.CatalogFingerprint, expectedCatalogFingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Foundation save references unavailable content.");
        if (old.Profile.RunIndex < 0)
            throw new InvalidDataException("Foundation run index is negative.");

        var migrated = new MetaProfileDto(
            old.Profile.ProfileSeed,
            old.Profile.RunIndex,
            new(0, 0, 0),
            new(0, 0, 0, 0, 0, 0),
            [],
            [MetaContentIds.CinderBelt],
            ToDto(LoadoutDefaults()),
            [],
            ToDto(GameSettings.Default),
            null);
        return new(
            MetaSaveVersions.Current,
            old.BuildId,
            old.CatalogFingerprint,
            migrated,
            ComputeChecksum(MetaSaveVersions.Current, old.BuildId, old.CatalogFingerprint, migrated));
    }

    private static void ValidateEnvelope(MetaSaveEnvelope envelope, bool requireCurrent)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Versions is null)
            throw new InvalidDataException("Save versions are required.");
        ValidateHeader(envelope.BuildId, envelope.CatalogFingerprint);
        if (requireCurrent && envelope.Versions != MetaSaveVersions.Current)
            throw new InvalidDataException("Only current-version saves may be written.");
        if (envelope.Profile is null)
            throw new InvalidDataException("Save profile is required.");
        ValidateProfile(envelope.Profile);
        var expected = ComputeChecksum(
            envelope.Versions,
            envelope.BuildId,
            envelope.CatalogFingerprint,
            envelope.Profile);
        ValidateChecksum(expected, envelope.Checksum);
    }

    private static void ValidateProfile(MetaProfileDto profile)
    {
        if (profile.RunIndex < 0)
            throw new InvalidDataException("Run index cannot be negative.");
        if (profile.Balances is null || profile.Counters is null ||
            !FromDto(profile.Balances).IsValid || !FromDto(profile.Counters).IsValid)
            throw new InvalidDataException("Profile balances and counters must be nonnegative.");
        ValidateStringList(profile.PurchasedResearchIds, "research IDs");
        ValidateStringList(profile.UnlockedEnvironmentIds, "environment IDs");
        if (profile.RequestedLoadout is null)
            throw new InvalidDataException("Requested loadout is required.");
        foreach (var id in new[]
                 {
                     profile.RequestedLoadout.Weapon, profile.RequestedLoadout.Mining,
                     profile.RequestedLoadout.Shield, profile.RequestedLoadout.Engine,
                     profile.RequestedLoadout.Utility
                 })
            ValidateBoundedString(id, "module ID", allowEmpty: true);
        if (profile.Transactions is null || profile.Transactions.Count > MetaSaveSchema.MaxCollectionCount)
            throw new InvalidDataException("Transaction history exceeds its bound.");
        var transactionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var receipt in profile.Transactions)
        {
            if (receipt is null)
                throw new InvalidDataException("Transaction receipts cannot be null.");
            ValidateBoundedString(receipt.TransactionId, "transaction ID");
            ValidateBoundedString(receipt.Operation, "transaction operation");
            if (!transactionIds.Add(receipt.TransactionId))
                throw new InvalidDataException("Transaction IDs must be unique.");
        }
        if (profile.Settings is null || !FromDto(profile.Settings).IsValid)
            throw new InvalidDataException("Settings are invalid.");
        if (profile.PreviousRun is not null)
        {
            ValidateBoundedString(profile.PreviousRun.RunId, "run ID");
            ValidateBoundedString(profile.PreviousRun.EnvironmentId, "environment ID");
            if (profile.PreviousRun.Earned is null || profile.PreviousRun.Banked is null ||
                profile.PreviousRun.Retained is null || profile.PreviousRun.Lost is null ||
                !FromDto(profile.PreviousRun.Earned).IsValid ||
                !FromDto(profile.PreviousRun.Banked).IsValid ||
                !FromDto(profile.PreviousRun.Retained).IsValid ||
                !FromDto(profile.PreviousRun.Lost).IsValid)
                throw new InvalidDataException("Previous run resource values are invalid.");
        }
    }

    private static MetaProfileDto ToDto(MetaProfileSnapshot profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new(
            profile.ProfileSeed,
            profile.RunIndex,
            ToDto(profile.Balances),
            ToDto(profile.Counters),
            profile.PurchasedResearchIds.ToArray(),
            profile.UnlockedEnvironmentIds.ToArray(),
            ToDto(profile.RequestedLoadout),
            profile.Transactions.Select(receipt =>
                new TransactionReceiptDto(receipt.TransactionId, receipt.Operation, receipt.Fingerprint)).ToArray(),
            ToDto(profile.Settings),
            profile.PreviousRun is null ? null : new(
                profile.PreviousRun.RunId,
                profile.PreviousRun.EnvironmentId,
                profile.PreviousRun.Succeeded,
                ToDto(profile.PreviousRun.Earned),
                ToDto(profile.PreviousRun.Banked),
                ToDto(profile.PreviousRun.Retained),
                ToDto(profile.PreviousRun.Lost)));
    }

    private static MetaProfileSnapshot FromDto(MetaProfileDto profile)
    {
        ValidateProfile(profile);
        return new(
            profile.ProfileSeed,
            profile.RunIndex,
            FromDto(profile.Balances),
            FromDto(profile.Counters),
            profile.PurchasedResearchIds.ToArray(),
            profile.UnlockedEnvironmentIds.ToArray(),
            new(
                profile.RequestedLoadout.Weapon,
                profile.RequestedLoadout.Mining,
                profile.RequestedLoadout.Shield,
                profile.RequestedLoadout.Engine,
                profile.RequestedLoadout.Utility),
            profile.Transactions.Select(receipt =>
                new ProfileTransactionReceipt(receipt.TransactionId, receipt.Operation, receipt.Fingerprint)).ToArray(),
            FromDto(profile.Settings),
            profile.PreviousRun is null ? null : new(
                profile.PreviousRun.RunId,
                profile.PreviousRun.EnvironmentId,
                profile.PreviousRun.Succeeded,
                FromDto(profile.PreviousRun.Earned),
                FromDto(profile.PreviousRun.Banked),
                FromDto(profile.PreviousRun.Retained),
                FromDto(profile.PreviousRun.Lost)));
    }

    private static IReadOnlyList<string> FindUnknownIds(
        MetaProfileSnapshot profile,
        MetaContentCompatibility? knownContent)
    {
        if (knownContent is null)
            return [];
        var diagnostics = new List<string>();
        diagnostics.AddRange(profile.PurchasedResearchIds
            .Where(id => !knownContent.ResearchIds.Contains(id))
            .Select(id => $"Unknown research ID preserved for recovery: '{id}'."));
        diagnostics.AddRange(profile.UnlockedEnvironmentIds
            .Where(id => !knownContent.EnvironmentIds.Contains(id))
            .Select(id => $"Unknown environment ID preserved for recovery: '{id}'."));
        foreach (var slot in Enum.GetValues<ModuleSlot>())
        {
            var id = profile.RequestedLoadout.For(slot);
            if (!knownContent.ModuleIds.Contains(id))
                diagnostics.Add($"Unknown {slot} module ID preserved for recovery: '{id}'.");
        }
        return diagnostics;
    }

    private static LoadoutSelection LoadoutDefaults() => new(
        "MOD_WEAPON_PULSE",
        "MOD_MINING_LASER",
        "MOD_SHIELD_CAPACITOR",
        "MOD_ENGINE_VECTOR",
        "MOD_UTILITY_TRACTOR");

    private static ResourceAmountsDto ToDto(ResourceAmounts value) =>
        new(value.Ferrite, value.Lumen, value.DataCores);

    private static LifetimeCountersDto ToDto(LifetimeCounters value) =>
        new(
            value.Extractions,
            value.NormalKills,
            value.EliteKills,
            value.FerriteCollected,
            value.ResourceCellsBroken,
            value.IonVeilExtractions);

    private static LoadoutDto ToDto(LoadoutSelection value) =>
        new(value.Weapon, value.Mining, value.Shield, value.Engine, value.Utility);

    private static SettingsDto ToDto(GameSettings value) =>
        new(
            value.MasterVolume,
            value.MusicVolume,
            value.EffectsVolume,
            value.Vibration,
            value.ScreenShake,
            value.Flashes,
            value.Fullscreen,
            value.TelemetryConsent);

    private static ResourceAmounts FromDto(ResourceAmountsDto value) =>
        new(value.Ferrite, value.Lumen, value.DataCores);

    private static LifetimeCounters FromDto(LifetimeCountersDto value) =>
        new(
            value.Extractions,
            value.NormalKills,
            value.EliteKills,
            value.FerriteCollected,
            value.ResourceCellsBroken,
            value.IonVeilExtractions);

    private static GameSettings FromDto(SettingsDto value) =>
        new(
            value.MasterVolume,
            value.MusicVolume,
            value.EffectsVolume,
            value.Vibration,
            value.ScreenShake,
            value.Flashes,
            value.Fullscreen,
            value.TelemetryConsent);

    private static void ValidateHeader(string buildId, string catalogFingerprint)
    {
        ValidateBoundedString(buildId, "build ID");
        ValidateBoundedString(catalogFingerprint, "catalog fingerprint");
    }

    private static void ValidateStringList(IReadOnlyList<string> values, string member)
    {
        if (values is null || values.Count > MetaSaveSchema.MaxCollectionCount)
            throw new InvalidDataException($"{member} exceed their bound.");
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            ValidateBoundedString(value, member, allowEmpty: true);
            if (!unique.Add(value))
                throw new InvalidDataException($"{member} contain duplicates.");
        }
    }

    private static void ValidateText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MetaSaveSchema.MaxStringLength)
            throw new ArgumentException("Value must contain 1-128 characters.", parameterName);
    }

    private static void ValidateBoundedString(string value, string member, bool allowEmpty = false)
    {
        if (value is null || value.Length > MetaSaveSchema.MaxStringLength ||
            (!allowEmpty && string.IsNullOrWhiteSpace(value)))
            throw new InvalidDataException($"{member} must contain 1-{MetaSaveSchema.MaxStringLength} characters.");
    }

    private static int ReadRequiredInt(JsonElement root, string objectName, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(root, objectName, out var child) ||
            child.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(child, propertyName, out var value) ||
            !value.TryGetInt32(out var number))
            throw new InvalidDataException($"Save member '{objectName}.{propertyName}' must be an integer.");
        return number;
    }

    private static bool TryGetProperty(JsonElement parent, string name, out JsonElement value)
    {
        foreach (var property in parent.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        value = default;
        return false;
    }

    private static string ComputeChecksum(
        MetaSaveVersions versions,
        string buildId,
        string fingerprint,
        MetaProfileDto profile)
    {
        var canonical = JsonSerializer.Serialize(new { versions, buildId, fingerprint, profile });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ComputeFoundationChecksum(
        DurableVersions versions,
        string buildId,
        string fingerprint,
        ProfileSnapshot profile)
    {
        var canonical = JsonSerializer.Serialize(new { versions, buildId, fingerprint, profile });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void ValidateChecksum(string expected, string actual)
    {
        if (actual is null || actual.Length != expected.Length)
            throw new InvalidDataException("Save checksum is invalid.");
        byte[] expectedBytes;
        byte[] actualBytes;
        try
        {
            expectedBytes = Convert.FromHexString(expected);
            actualBytes = Convert.FromHexString(actual);
        }
        catch (FormatException)
        {
            throw new InvalidDataException("Save checksum is malformed.");
        }
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            throw new InvalidDataException("Save checksum mismatch.");
    }
}
