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
    Missing
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
    public CompatibilityStatus Classify(int version) =>
        version == ContractVersions.Save
            ? CompatibilityStatus.Supported
            : version > ContractVersions.Save
                ? CompatibilityStatus.IncompatibleNewer
                : CompatibilityStatus.Corrupt;
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
        Validate(envelope);
        var temp = _path + "." + Environment.ProcessId + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, envelope, JsonOptions);
                stream.Flush(true);
            }

            using (var verify = File.OpenRead(temp))
                Validate(JsonSerializer.Deserialize<SaveEnvelope>(verify) ?? throw new InvalidDataException("Empty temporary save."));

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

    public SaveLoadResult Load()
    {
        if (!File.Exists(_path) && !File.Exists(_backupPath))
            return new(CompatibilityStatus.Missing, Diagnostic: "No profile save exists.");

        var primary = TryRead(_path);
        if (primary.Status is CompatibilityStatus.Supported or CompatibilityStatus.IncompatibleNewer)
            return primary;

        var backup = TryRead(_backupPath);
        if (backup.Status == CompatibilityStatus.Supported)
            return backup with { RecoveredFromBackup = true, Diagnostic = "Primary was invalid; loaded known-good backup." };
        return primary with { Diagnostic = $"Primary: {primary.Diagnostic} Backup: {backup.Diagnostic}" };
    }

    private static SaveLoadResult TryRead(string path)
    {
        if (!File.Exists(path))
            return new(CompatibilityStatus.Missing, Diagnostic: "File missing.");
        try
        {
            using var stream = File.OpenRead(path);
            var envelope = JsonSerializer.Deserialize<SaveEnvelope>(stream)
                ?? throw new InvalidDataException("Save is empty.");
            var status = new SaveMigrationRegistry().Classify(envelope.Versions.Save);
            if (status == CompatibilityStatus.Supported)
                Validate(envelope);
            return new(status, status == CompatibilityStatus.Supported ? envelope : null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException or FormatException)
        {
            return new(CompatibilityStatus.Corrupt, Diagnostic: exception.Message);
        }
    }

    private static void Validate(SaveEnvelope envelope)
    {
        if (envelope.Versions.Save != ContractVersions.Save)
            throw new InvalidDataException("Only the current save schema can be written.");
        var expected = ComputeChecksum(envelope.Versions, envelope.BuildId, envelope.CatalogFingerprint, envelope.Profile);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected),
                Convert.FromHexString(envelope.Checksum)))
            throw new InvalidDataException("Save checksum mismatch.");
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
