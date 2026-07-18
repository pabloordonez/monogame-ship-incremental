using System.Text.Json;
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

        var result = repository.Load();

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

        var result = repository.Load();

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

        Assert.Equal(envelope, repository.Load().Envelope);
    }

    [Fact]
    public void NewerSaveIsRejectedExplicitly()
    {
        var repository = new SaveRepository(_root);
        var current = repository.CreateEnvelope(new ProfileSnapshot(3, 5), "test", "catalog");
        var newer = current with { Versions = current.Versions with { Save = 2 } };
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "profile.json"), JsonSerializer.Serialize(newer));

        Assert.Equal(CompatibilityStatus.IncompatibleNewer, repository.Load().Status);
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
}
