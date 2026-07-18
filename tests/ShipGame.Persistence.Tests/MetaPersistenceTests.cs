using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Persistence.Tests;

public sealed class MetaPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ShipGame-MetaSaves-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CurrentMetaSaveRoundTripsWithSettingsAndLoadout()
    {
        var repository = new MetaSaveRepository(_root);
        var profile = SampleProfile();
        var envelope = repository.CreateEnvelope(profile, "P4_META_UI", "catalog");
        repository.Write(envelope);

        var result = repository.Load("catalog", KnownContent());

        Assert.Equal(CompatibilityStatus.Supported, result.Status);
        Assert.Equal(profile.Balances, result.Profile!.Balances);
        Assert.Equal(profile.RequestedLoadout, result.Profile.RequestedLoadout);
        Assert.Equal(profile.Settings, result.Profile.Settings);
        Assert.Contains("RES_HULL_REINFORCEMENT", result.Profile.PurchasedResearchIds);
        Assert.False(result.Migrated);
    }

    [Fact]
    public void FoundationSaveMigratesToMetaSchema()
    {
        WriteFoundationSave(new ProfileSnapshot(99, 4), "catalog", atFoundationPath: false);
        var repository = new MetaSaveRepository(_root);

        var result = repository.Load("catalog", KnownContent());

        Assert.Equal(CompatibilityStatus.Supported, result.Status);
        Assert.True(result.Migrated);
        Assert.Equal(99UL, result.Profile!.ProfileSeed);
        Assert.Equal(4, result.Profile.RunIndex);
        Assert.Equal(ResourceAmounts.Zero, result.Profile.Balances);
        Assert.Equal(GameSettings.Default, result.Profile.Settings);
        Assert.Contains(MetaContentIds.CinderBelt, result.Profile.UnlockedEnvironmentIds);
        Assert.True(File.Exists(Path.Combine(_root, MetaSaveRepository.MetaFileName)));
    }

    [Fact]
    public void FoundationProfileJsonPathMigratesAtomicallyPreservingSeedAndRunIndex()
    {
        WriteFoundationSave(new ProfileSnapshot(99, 4), "catalog", atFoundationPath: true);
        var repository = new MetaSaveRepository(_root);

        var result = repository.Load("catalog", KnownContent());

        Assert.Equal(CompatibilityStatus.Supported, result.Status);
        Assert.True(result.Migrated);
        Assert.Equal(99UL, result.Profile!.ProfileSeed);
        Assert.Equal(4, result.Profile.RunIndex);
        Assert.True(File.Exists(Path.Combine(_root, MetaSaveRepository.MetaFileName)));
        Assert.True(File.Exists(Path.Combine(_root, MetaSaveRepository.FoundationFileName)));

        var continued = repository.Load("catalog", KnownContent());
        Assert.Equal(CompatibilityStatus.Supported, continued.Status);
        Assert.False(continued.Migrated);
        Assert.Equal(99UL, continued.Profile!.ProfileSeed);
        Assert.Equal(4, continued.Profile.RunIndex);
    }

    [Fact]
    public void GoldenFoundationFixtureAtProfileJsonMigratesToSchema2()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "golden-foundation-profile.json");
        Assert.True(File.Exists(fixture), $"Missing golden fixture at {fixture}");
        Directory.CreateDirectory(_root);
        File.Copy(fixture, Path.Combine(_root, MetaSaveRepository.FoundationFileName));

        var repository = new MetaSaveRepository(_root);
        var result = repository.Load("foundation-catalog-v1", KnownContent());

        Assert.Equal(CompatibilityStatus.Supported, result.Status);
        Assert.True(result.Migrated);
        Assert.Equal(12648430UL, result.Profile!.ProfileSeed);
        Assert.Equal(7, result.Profile.RunIndex);
        Assert.Equal(ResourceAmounts.Zero, result.Profile.Balances);
        Assert.Equal(GameSettings.Default, result.Profile.Settings);
        Assert.Contains(MetaContentIds.CinderBelt, result.Profile.UnlockedEnvironmentIds);

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, MetaSaveRepository.MetaFileName)));
        Assert.Equal(MetaSaveSchema.Current, document.RootElement.GetProperty("Versions").GetProperty("Save").GetInt32());
    }

    [Fact]
    public void CorruptPrimaryRecoversKnownGoodBackup()
    {
        var repository = new MetaSaveRepository(_root);
        var first = SampleProfile() with { RunIndex = 1 };
        var second = SampleProfile() with { RunIndex = 2 };
        repository.Write(repository.CreateEnvelope(first, "P4_META_UI", "catalog"));
        repository.Write(repository.CreateEnvelope(second, "P4_META_UI", "catalog"));
        File.WriteAllText(Path.Combine(_root, "profile-v2.json"), "{broken");

        var result = repository.Load("catalog", KnownContent());

        Assert.True(result.RecoveredFromBackup);
        Assert.Equal(1, result.Profile!.RunIndex);
    }

    [Fact]
    public void InterruptedTempFileDoesNotReplacePrimary()
    {
        var repository = new MetaSaveRepository(_root);
        var envelope = repository.CreateEnvelope(SampleProfile(), "P4_META_UI", "catalog");
        repository.Write(envelope);
        File.WriteAllText(Path.Combine(_root, "profile-v2.json.interrupted.tmp"), "partial");

        var result = repository.Load("catalog", KnownContent());

        Assert.Equal(CompatibilityStatus.Supported, result.Status);
        Assert.Equal(envelope.Profile.RunIndex, result.Profile!.RunIndex);
    }

    [Fact]
    public void NewerSaveIsRejectedExplicitly()
    {
        var repository = new MetaSaveRepository(_root);
        var dto = ToDto(SampleProfile());
        var versions = MetaSaveVersions.Current with { Save = MetaSaveSchema.Current + 1 };
        WriteRaw(versions, dto, "catalog");

        Assert.Equal(CompatibilityStatus.IncompatibleNewer, repository.Load("catalog").Status);
    }

    [Fact]
    public void UnknownIdsArePreservedWithDiagnostics()
    {
        var repository = new MetaSaveRepository(_root);
        var profile = SampleProfile() with
        {
            PurchasedResearchIds = ["RES_HULL_REINFORCEMENT", "RES_FUTURE_NODE"],
            RequestedLoadout = SampleProfile().RequestedLoadout with { Weapon = "MOD_FUTURE_WEAPON" }
        };
        repository.Write(repository.CreateEnvelope(profile, "P4_META_UI", "catalog"));

        var result = repository.Load("catalog", KnownContent());

        Assert.Equal(CompatibilityStatus.Supported, result.Status);
        Assert.Contains("RES_FUTURE_NODE", result.Profile!.PurchasedResearchIds);
        Assert.Equal("MOD_FUTURE_WEAPON", result.Profile.RequestedLoadout.Weapon);
        Assert.Contains(result.Diagnostics!, item => item.Contains("RES_FUTURE_NODE", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics!, item => item.Contains("MOD_FUTURE_WEAPON", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingContentFingerprintIsClassified()
    {
        var repository = new MetaSaveRepository(_root);
        repository.Write(repository.CreateEnvelope(SampleProfile(), "P4_META_UI", "other-catalog"));

        Assert.Equal(CompatibilityStatus.MissingContent, repository.Load("catalog").Status);
    }

    [Fact]
    public void SaveFileNameCannotEscapeRoot()
    {
        Assert.Throws<ArgumentException>(() => new MetaSaveRepository(_root, "../outside.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private void WriteFoundationSave(ProfileSnapshot profile, string fingerprint, bool atFoundationPath)
    {
        const string buildId = "P0_FOUNDATION";
        var versions = DurableVersions.Current;
        var canonical = JsonSerializer.Serialize(new { versions, buildId, fingerprint, profile });
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var envelope = new SaveEnvelope(versions, buildId, fingerprint, profile, checksum);
        Directory.CreateDirectory(_root);
        var fileName = atFoundationPath
            ? MetaSaveRepository.FoundationFileName
            : MetaSaveRepository.MetaFileName;
        File.WriteAllText(Path.Combine(_root, fileName), JsonSerializer.Serialize(envelope));
    }

    private void WriteRaw(MetaSaveVersions versions, MetaProfileDto profile, string fingerprint)
    {
        const string buildId = "P4_META_UI";
        var canonical = JsonSerializer.Serialize(new { versions, buildId, fingerprint, profile });
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var envelope = new MetaSaveEnvelope(versions, buildId, fingerprint, profile, checksum);
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "profile-v2.json"), JsonSerializer.Serialize(envelope));
    }

    private static MetaProfileSnapshot SampleProfile() =>
        new(
            42,
            3,
            new ResourceAmounts(100, 4, 2),
            new LifetimeCounters(2, 10, 1, 80, 20, 0),
            ["RES_HULL_REINFORCEMENT"],
            [],
            [MetaContentIds.CinderBelt],
            new LoadoutSelection(
                "MOD_WEAPON_PULSE",
                "MOD_MINING_LASER",
                "MOD_SHIELD_CAPACITOR",
                "MOD_ENGINE_VECTOR",
                "MOD_UTILITY_TRACTOR"),
            [new ProfileTransactionReceipt("TX_SAMPLE", "research", 1)],
            GameSettings.Default with { TelemetryConsent = true, MasterVolume = 70 },
            new RunSummarySnapshot(
                "RUN_1",
                MetaContentIds.CinderBelt,
                true,
                new(40, 2, 1),
                new(40, 2, 1),
                ResourceAmounts.Zero,
                ResourceAmounts.Zero));

    private static MetaProfileDto ToDto(MetaProfileSnapshot profile) =>
        new(
            profile.ProfileSeed,
            profile.RunIndex,
            new(profile.Balances.Ferrite, profile.Balances.Lumen, profile.Balances.DataCores),
            new(
                profile.Counters.Extractions,
                profile.Counters.NormalKills,
                profile.Counters.EliteKills,
                profile.Counters.FerriteCollected,
                profile.Counters.ResourceCellsBroken,
                profile.Counters.IonVeilExtractions),
            profile.PurchasedResearchIds.ToArray(),
            profile.PurchasedUpgradeIds.ToArray(),
            profile.UnlockedEnvironmentIds.ToArray(),
            new(
                profile.RequestedLoadout.Weapon,
                profile.RequestedLoadout.Mining,
                profile.RequestedLoadout.Shield,
                profile.RequestedLoadout.Engine,
                profile.RequestedLoadout.Utility),
            profile.Transactions.Select(receipt =>
                new TransactionReceiptDto(receipt.TransactionId, receipt.Operation, receipt.Fingerprint)).ToArray(),
            new(
                profile.Settings.MasterVolume,
                profile.Settings.MusicVolume,
                profile.Settings.EffectsVolume,
                profile.Settings.Vibration,
                profile.Settings.ScreenShake,
                profile.Settings.Flashes,
                profile.Settings.Fullscreen,
                profile.Settings.TelemetryConsent),
            null);

    private static MetaContentCompatibility KnownContent() =>
        new(
            new HashSet<string>(StringComparer.Ordinal) { "RES_HULL_REINFORCEMENT" },
            new HashSet<string>(StringComparer.Ordinal) { MetaContentIds.CinderBelt, MetaContentIds.IonVeil },
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MOD_WEAPON_PULSE",
                "MOD_MINING_LASER",
                "MOD_SHIELD_CAPACITOR",
                "MOD_ENGINE_VECTOR",
                "MOD_UTILITY_TRACTOR"
            });
}
