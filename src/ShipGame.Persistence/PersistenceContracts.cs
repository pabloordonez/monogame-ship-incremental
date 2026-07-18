using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence;

public enum CompatibilityStatus
{
    Supported,
    Migratable,
    IncompatibleNewer,
    Corrupt,
    Missing,
    MissingContent
}

public sealed record DurableVersions(
    int Save,
    int Content,
    int Generation,
    int Rng,
    int Replay,
    int Telemetry)
{
    public static DurableVersions Current { get; } = new(
        ContractVersions.Save,
        ContractVersions.Content,
        ContractVersions.Generation,
        ContractVersions.Rng,
        ContractVersions.Replay,
        ContractVersions.Telemetry);
}

public sealed record SaveEnvelope(
    DurableVersions Versions,
    string BuildId,
    string CatalogFingerprint,
    ProfileSnapshot Profile,
    string Checksum);

public sealed record SaveLoadResult(
    CompatibilityStatus Status,
    SaveEnvelope? Envelope = null,
    bool RecoveredFromBackup = false,
    string? Diagnostic = null);

public sealed class SaveMigrationRegistry
{
    public IReadOnlyList<int> SupportedSaveVersions { get; } = [ContractVersions.Save];

    public CompatibilityStatus Classify(
        DurableVersions versions,
        string catalogFingerprint,
        string expectedCatalogFingerprint)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Save != ContractVersions.Save)
            return versions.Save > ContractVersions.Save
                ? CompatibilityStatus.IncompatibleNewer
                : CompatibilityStatus.Corrupt;
        if (versions.Content != ContractVersions.Content ||
            !string.Equals(catalogFingerprint, expectedCatalogFingerprint, StringComparison.Ordinal))
            return CompatibilityStatus.MissingContent;

        var remaining = new[]
        {
            (versions.Generation, ContractVersions.Generation),
            (versions.Rng, ContractVersions.Rng),
            (versions.Replay, ContractVersions.Replay),
            (versions.Telemetry, ContractVersions.Telemetry)
        };
        if (remaining.Any(pair => pair.Item1 > pair.Item2))
            return CompatibilityStatus.IncompatibleNewer;
        if (remaining.Any(pair => pair.Item1 < pair.Item2))
            return CompatibilityStatus.Corrupt;
        return CompatibilityStatus.Supported;
    }
}

public sealed class SaveRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly string _backupPath;

    public SaveRepository(string saveDirectory, string fileName = "profile.json")
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

    public SaveEnvelope CreateEnvelope(ProfileSnapshot profile, string buildId, string catalogFingerprint)
    {
        var checksum = ComputeChecksum(DurableVersions.Current, buildId, catalogFingerprint, profile);
        return new(DurableVersions.Current, buildId, catalogFingerprint, profile, checksum);
    }

    public void Write(SaveEnvelope envelope)
    {
        Validate(envelope, requireCurrentVersions: true);
        var temp = _path + "." + Environment.ProcessId + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, envelope, JsonOptions);
                stream.Flush(true);
            }

            using (var verify = File.OpenRead(temp))
                Validate(JsonSerializer.Deserialize<SaveEnvelope>(verify) ?? throw new InvalidDataException("Empty temporary save."), requireCurrentVersions: true);

            if (File.Exists(_path))
            {
                File.Replace(temp, _path, _backupPath, true);
            }
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

    public SaveLoadResult Load(string expectedCatalogFingerprint)
    {
        if (string.IsNullOrWhiteSpace(expectedCatalogFingerprint))
            throw new ArgumentException("An expected catalog fingerprint is required.", nameof(expectedCatalogFingerprint));
        if (!File.Exists(_path) && !File.Exists(_backupPath))
            return new(CompatibilityStatus.Missing, Diagnostic: "No profile save exists.");

        var primary = TryRead(_path, expectedCatalogFingerprint);
        if (primary.Status is CompatibilityStatus.Supported or CompatibilityStatus.IncompatibleNewer or CompatibilityStatus.MissingContent)
            return primary;

        var backup = TryRead(_backupPath, expectedCatalogFingerprint);
        if (backup.Status == CompatibilityStatus.Supported)
            return backup with { RecoveredFromBackup = true, Diagnostic = "Primary was invalid; loaded known-good backup." };
        return primary with { Diagnostic = $"Primary: {primary.Diagnostic} Backup: {backup.Diagnostic}" };
    }

    private static SaveLoadResult TryRead(string path, string expectedCatalogFingerprint)
    {
        if (!File.Exists(path))
            return new(CompatibilityStatus.Missing, Diagnostic: "File missing.");
        try
        {
            var json = File.ReadAllBytes(path);
            using var document = JsonDocument.Parse(json);
            ValidateRequiredJson(document.RootElement);
            var envelope = JsonSerializer.Deserialize<SaveEnvelope>(json)
                ?? throw new InvalidDataException("Save is empty.");
            Validate(envelope, requireCurrentVersions: false);
            var status = new SaveMigrationRegistry().Classify(
                envelope.Versions,
                envelope.CatalogFingerprint,
                expectedCatalogFingerprint);
            return new(status, status == CompatibilityStatus.Supported ? envelope : null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException or FormatException or ArgumentException or NullReferenceException)
        {
            return new(CompatibilityStatus.Corrupt, Diagnostic: exception.Message);
        }
    }

    private static void Validate(SaveEnvelope envelope, bool requireCurrentVersions)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Versions is null)
            throw new InvalidDataException("Save versions are required.");
        if (string.IsNullOrWhiteSpace(envelope.BuildId) ||
            string.IsNullOrWhiteSpace(envelope.CatalogFingerprint) ||
            string.IsNullOrWhiteSpace(envelope.Checksum))
            throw new InvalidDataException("Save build, catalog fingerprint, and checksum are required.");
        if (requireCurrentVersions && envelope.Versions != DurableVersions.Current)
            throw new InvalidDataException("Only current durable versions can be written.");
        var expected = ComputeChecksum(envelope.Versions, envelope.BuildId, envelope.CatalogFingerprint, envelope.Profile);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected),
                Convert.FromHexString(envelope.Checksum)))
            throw new InvalidDataException("Save checksum mismatch.");
    }

    private static void ValidateRequiredJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Save root must be an object.");
        var versions = RequiredObject(root, "Versions");
        foreach (var name in new[] { "Save", "Content", "Generation", "Rng", "Replay", "Telemetry" })
            RequiredNumber(versions, name);
        var profile = RequiredObject(root, "Profile");
        RequiredNumber(profile, "ProfileSeed");
        RequiredNumber(profile, "RunIndex");
        foreach (var name in new[] { "BuildId", "CatalogFingerprint", "Checksum" })
            RequiredString(root, name);
    }

    private static JsonElement RequiredObject(JsonElement parent, string name)
    {
        if (!TryGetProperty(parent, name, out var value) || value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Save member '{name}' must be an object.");
        return value;
    }

    private static void RequiredNumber(JsonElement parent, string name)
    {
        if (!TryGetProperty(parent, name, out var value) || value.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException($"Save member '{name}' must be a number.");
    }

    private static void RequiredString(JsonElement parent, string name)
    {
        if (!TryGetProperty(parent, name, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Save member '{name}' must be a nonempty string.");
    }

    private static bool TryGetProperty(JsonElement parent, string name, out JsonElement value)
    {
        foreach (var property in parent.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string ComputeChecksum(
        DurableVersions versions,
        string buildId,
        string fingerprint,
        ProfileSnapshot profile)
    {
        var canonical = JsonSerializer.Serialize(new { versions, buildId, fingerprint, profile });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
