using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipGame.Domain;

namespace ShipGame.Persistence.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ShipGame-Saves-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CurrentSaveRoundTrips()
    {
        var repository = new SaveRepository(_root);
        var envelope = repository.CreateEnvelope(new ProfileSnapshot(55, 3), "test", "catalog");
        repository.Write(envelope);

        var result = repository.Load("catalog");

        Assert.Equal(CompatibilityStatus.Supported, result.Status);
        Assert.Equal(envelope, result.Envelope);
    }

    [Fact]
    public void CorruptPrimaryRecoversKnownGoodBackup()
    {
        var repository = new SaveRepository(_root);
        repository.Write(repository.CreateEnvelope(new ProfileSnapshot(1, 1), "test", "catalog"));
        repository.Write(repository.CreateEnvelope(new ProfileSnapshot(1, 2), "test", "catalog"));
        File.WriteAllText(Path.Combine(_root, "profile.json"), "{broken");

        var result = repository.Load("catalog");

        Assert.True(result.RecoveredFromBackup);
        Assert.Equal(1, result.Envelope!.Profile.RunIndex);
    }

    [Fact]
    public void InterruptedTempFileDoesNotReplacePrimary()
    {
        var repository = new SaveRepository(_root);
        var envelope = repository.CreateEnvelope(new ProfileSnapshot(2, 4), "test", "catalog");
        repository.Write(envelope);
        File.WriteAllText(Path.Combine(_root, "profile.json.interrupted.tmp"), "partial");

        Assert.Equal(envelope, repository.Load("catalog").Envelope);
    }

    [Fact]
    public void NewerSaveIsRejectedExplicitly()
    {
        var repository = new SaveRepository(_root);
        WriteRaw(new ProfileSnapshot(3, 5), DurableVersions.Current with { Save = 2 }, "catalog");

        Assert.Equal(CompatibilityStatus.IncompatibleNewer, repository.Load("catalog").Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NullOrMissingRequiredPrimaryMemberRecoversBackup(bool useNull)
    {
        var repository = new SaveRepository(_root);
        repository.Write(repository.CreateEnvelope(new ProfileSnapshot(8, 1), "test", "catalog"));
        repository.Write(repository.CreateEnvelope(new ProfileSnapshot(8, 2), "test", "catalog"));
        var path = Path.Combine(_root, "profile.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (useNull)
            root["Versions"] = null;
        else
            root.Remove("Versions");
        File.WriteAllText(path, root.ToJsonString());

        var result = repository.Load("catalog");

        Assert.Equal(CompatibilityStatus.Supported, result.Status);
        Assert.True(result.RecoveredFromBackup);
        Assert.Equal(1, result.Envelope!.Profile.RunIndex);
    }

    [Theory]
    [InlineData("{\"Versions\":null}")]
    [InlineData("{}")]
    public void NullOrMissingRequiredMembersInPrimaryAndBackupAreCorrupt(string malformed)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "profile.json"), malformed);
        File.WriteAllText(Path.Combine(_root, "profile.json.bak"), malformed);

        var result = new SaveRepository(_root).Load("catalog");

        Assert.Equal(CompatibilityStatus.Corrupt, result.Status);
        Assert.Null(result.Envelope);
    }

    [Fact]
    public void ContentSchemaAndCatalogFingerprintAreClassifiedAsMissingContent()
    {
        WriteRaw(new ProfileSnapshot(1, 1), DurableVersions.Current with { Content = 999 }, "catalog");
        Assert.Equal(CompatibilityStatus.MissingContent, new SaveRepository(_root).Load("catalog").Status);

        WriteRaw(new ProfileSnapshot(1, 1), DurableVersions.Current, "other-catalog");
        Assert.Equal(CompatibilityStatus.MissingContent, new SaveRepository(_root).Load("catalog").Status);
    }

    [Theory]
    [InlineData("Generation")]
    [InlineData("Rng")]
    [InlineData("Replay")]
    [InlineData("Telemetry")]
    public void EveryNonContentDurableVersionIsClassified(string member)
    {
        var versions = member switch
        {
            "Generation" => DurableVersions.Current with { Generation = 999 },
            "Rng" => DurableVersions.Current with { Rng = 999 },
            "Replay" => DurableVersions.Current with { Replay = 999 },
            "Telemetry" => DurableVersions.Current with { Telemetry = 999 },
            _ => throw new ArgumentOutOfRangeException(nameof(member))
        };
        WriteRaw(new ProfileSnapshot(1, 1), versions, "catalog");

        Assert.Equal(CompatibilityStatus.IncompatibleNewer, new SaveRepository(_root).Load("catalog").Status);
    }

    [Fact]
    public void SaveFileNameCannotEscapeRoot()
    {
        Assert.Throws<ArgumentException>(() => new SaveRepository(_root, "../outside.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private void WriteRaw(ProfileSnapshot profile, DurableVersions versions, string fingerprint)
    {
        const string buildId = "test";
        var canonical = JsonSerializer.Serialize(new { versions, buildId, fingerprint, profile });
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var envelope = new SaveEnvelope(versions, buildId, fingerprint, profile, checksum);
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "profile.json"), JsonSerializer.Serialize(envelope));
    }
}
