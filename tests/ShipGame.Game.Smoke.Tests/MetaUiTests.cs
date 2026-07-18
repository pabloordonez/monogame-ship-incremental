using ShipGame.Domain;
using ShipGame.Persistence;
using ShipGame.Simulation;
using ShipGame.Telemetry;

namespace ShipGame.Game.Smoke.Tests;

public sealed class MetaUiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ShipGame-MetaUi-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ScreenNavigationCoversTitleLobbyMapLoadoutResearchPauseSummary()
    {
        var ui = new MetaUiController(ProfileAggregate.CreateNew(1));
        Assert.Equal(MetaScreen.Title, ui.Screen);
        Assert.True(ui.EnterLobby().Accepted);
        Assert.True(ui.Open(MetaScreen.Map).Accepted);
        Assert.True(ui.Back().Accepted);
        Assert.Equal(MetaScreen.Lobby, ui.Screen);
        Assert.True(ui.Open(MetaScreen.Loadout).Accepted);
        Assert.True(ui.Back().Accepted);
        Assert.True(ui.Open(MetaScreen.Research).Accepted);
        Assert.True(ui.Back().Accepted);
        Assert.True(ui.Open(MetaScreen.Map).Accepted);
        Assert.True(ui.Launch().Accepted);
        Assert.Equal(MetaScreen.Run, ui.Screen);
        Assert.True(ui.Open(MetaScreen.Pause).Accepted);
        Assert.True(ui.Back().Accepted);
        Assert.Equal(MetaScreen.Run, ui.Screen);
        Assert.True(ui.ShowSummary().Accepted);
        Assert.Equal(MetaScreen.Summary, ui.Screen);
        Assert.True(ui.EnterLobby().Accepted);
    }

    [Fact]
    public void MapExplainsIonVeilCapabilityLock()
    {
        var ui = new MetaUiController(ProfileAggregate.CreateNew(1));
        ui.EnterLobby();
        ui.Open(MetaScreen.Map);
        var map = ui.BuildMapView();
        var ion = Assert.Single(map, view => view.EnvironmentId == MetaContentIds.IonVeil);
        Assert.False(ion.Accessible);
        Assert.Contains(MetaContentIds.TravelIonVeil, ion.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void MetaLoopPurchasesResearchChangesLoadoutSavesAndContinues()
    {
        var writes = 0;
        using (var session = new MetaSession(_root, () => new CountingSink(() => writes++), newProfileSeed: 7))
        {
            Assert.Equal(MetaScreen.Title, session.Screen);
            Assert.True(session.Navigate(MetaScreen.Settings).Accepted);
            Assert.True(session.ApplySettings(
                "TX_CONSENT",
                GameSettings.Default with { TelemetryConsent = true }).Accepted);
            Assert.True(session.Back().Accepted);
            Assert.True(session.Navigate(MetaScreen.Lobby).Accepted);
            Assert.True(session.Navigate(MetaScreen.Map).Accepted);
            Assert.True(session.Launch().Accepted);

            var reward = new RewardProposal(
                "TX_REWARD_LOOP",
                "RUN_LOOP_1",
                MetaContentIds.CinderBelt,
                true,
                new(40, 2, 1),
                new(40, 2, 1),
                ResourceAmounts.Zero,
                ResourceAmounts.Zero,
                new(1, 8, 1, 40, 12, 0));
            Assert.Equal(ProfileMutationStatus.Applied, session.CommitReward(reward).Status);
            Assert.Equal(MetaScreen.Summary, session.Screen);
            Assert.True(session.Navigate(MetaScreen.Lobby).Accepted);
            Assert.True(session.Navigate(MetaScreen.Research).Accepted);
            Assert.True(session.PurchaseResearch("TX_HULL", ResearchCatalog.HullReinforcement).Accepted);
            Assert.True(session.Back().Accepted);
            Assert.True(session.Navigate(MetaScreen.Loadout).Accepted);
            Assert.True(session.EquipModule("TX_EQUIP_PULSE", ModuleSlot.Weapon, ModuleCatalog.WeaponPulse).Accepted);
        }

        using var continued = new MetaSession(_root, () => new CountingSink(() => writes++));
        Assert.Equal(MetaScreen.Lobby, continued.Screen);
        Assert.Contains(ResearchCatalog.HullReinforcement, continued.Profile.Snapshot.PurchasedResearchIds);
        Assert.Equal(ModuleCatalog.WeaponPulse, continued.Profile.Snapshot.RequestedLoadout.Weapon);
        Assert.Equal(new ResourceAmounts(15, 2, 1), continued.Profile.Snapshot.Balances);
        Assert.Equal(115, continued.Profile.DeriveStatistics().MaximumHull);
        Assert.True(writes > 0);
    }

    [Fact]
    public void TelemetryFailureDuringMetaSessionDoesNotBlockPlay()
    {
        using var session = new MetaSession(_root, () => new FailingSink(), newProfileSeed: 3);
        Assert.True(session.Navigate(MetaScreen.Settings).Accepted);
        Assert.True(session.ApplySettings(
            "TX_CONSENT_FAIL",
            GameSettings.Default with { TelemetryConsent = true }).Accepted);
        Assert.True(session.Telemetry.Failed);
        Assert.True(session.Back().Accepted);
        Assert.True(session.Navigate(MetaScreen.Lobby).Accepted);
        Assert.True(session.Navigate(MetaScreen.Research).Accepted);
        Assert.Equal(MetaScreen.Research, session.Screen);
    }

    [Fact]
    public void FoundationProfileJsonContinuesWithMigratedSeedAndRunIndex()
    {
        Directory.CreateDirectory(_root);
        var foundation = new SaveRepository(_root);
        foundation.Write(foundation.CreateEnvelope(
            new ProfileSnapshot(0xC0FFEEUL, 7),
            "P0_FOUNDATION",
            MetaSession.CatalogFingerprint));

        using var session = new MetaSession(_root);
        Assert.Equal(CompatibilityStatus.Supported, session.LoadStatus);
        Assert.True(session.MigratedOnLoad);
        Assert.False(session.RequiresExplicitNewProfile);
        Assert.Equal(MetaScreen.Lobby, session.Screen);
        Assert.Equal(0xC0FFEEUL, session.Profile.Snapshot.ProfileSeed);
        Assert.Equal(7, session.Profile.Snapshot.RunIndex);
        Assert.True(File.Exists(Path.Combine(_root, MetaSaveRepository.MetaFileName)));
    }

    [Fact]
    public void DualCorruptMetaSavesRequireExplicitNewProfileWithoutSilentReset()
    {
        Directory.CreateDirectory(_root);
        var metaPath = Path.Combine(_root, MetaSaveRepository.MetaFileName);
        var backupPath = metaPath + ".bak";
        File.WriteAllText(metaPath, "{broken-primary");
        File.WriteAllText(backupPath, "{broken-backup");
        var primaryBefore = File.ReadAllText(metaPath);
        var backupBefore = File.ReadAllText(backupPath);

        using (var session = new MetaSession(_root, newProfileSeed: 11))
        {
            Assert.Equal(CompatibilityStatus.Corrupt, session.LoadStatus);
            Assert.True(session.RequiresExplicitNewProfile);
            Assert.Equal(MetaScreen.Title, session.Screen);
            var blocked = session.Navigate(MetaScreen.Lobby);
            Assert.False(blocked.Accepted);
            Assert.Equal("profile.unrecoverable", blocked.Code);
            Assert.Equal(primaryBefore, File.ReadAllText(metaPath));
            Assert.Equal(backupBefore, File.ReadAllText(backupPath));

            Assert.True(session.CreateNewProfile(99).Accepted);
            Assert.False(session.RequiresExplicitNewProfile);
            Assert.Equal(99UL, session.Profile.Snapshot.ProfileSeed);
            Assert.True(session.Navigate(MetaScreen.Lobby).Accepted);
        }

        using var continued = new MetaSession(_root);
        Assert.Equal(CompatibilityStatus.Supported, continued.LoadStatus);
        Assert.Equal(99UL, continued.Profile.Snapshot.ProfileSeed);
        Assert.Equal(MetaScreen.Lobby, continued.Screen);
    }

    [Fact]
    public void PersistFailureRejectsMutationAndRollsBackInMemoryState()
    {
        using (var session = new MetaSession(_root, newProfileSeed: 5))
        {
            Assert.True(session.Navigate(MetaScreen.Lobby).Accepted);
            Assert.True(session.Navigate(MetaScreen.Map).Accepted);
            Assert.True(session.Launch().Accepted);
            var reward = new RewardProposal(
                "TX_REWARD_PERSIST",
                "RUN_PERSIST_1",
                MetaContentIds.CinderBelt,
                true,
                new(40, 2, 1),
                new(40, 2, 1),
                ResourceAmounts.Zero,
                ResourceAmounts.Zero,
                new(1, 0, 0, 40, 0, 0));
            Assert.Equal(ProfileMutationStatus.Applied, session.CommitReward(reward).Status);
            Assert.True(session.Navigate(MetaScreen.Lobby).Accepted);
            Assert.True(session.Navigate(MetaScreen.Research).Accepted);

            Directory.Delete(_root, recursive: true);
            File.WriteAllText(_root, "not-a-directory");

            var purchase = session.PurchaseResearch("TX_HULL_FAIL", ResearchCatalog.HullReinforcement);
            Assert.False(purchase.Accepted);
            Assert.Equal("save.failed", purchase.Code);
            Assert.DoesNotContain(
                ResearchCatalog.HullReinforcement,
                session.Profile.Snapshot.PurchasedResearchIds);
            Assert.Equal(new ResourceAmounts(40, 2, 1), session.Profile.Snapshot.Balances);
        }
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_root))
                File.Delete(_root);
            else if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch (IOException)
        {
            // Best-effort cleanup for persist-failure fixtures that replace the directory with a file.
        }
    }

    private sealed class CountingSink : ITelemetrySink
    {
        private readonly Action _onWrite;
        public CountingSink(Action onWrite) => _onWrite = onWrite;
        public void Write(TelemetryRecord record) => _onWrite();
        public void Dispose() { }
    }

    private sealed class FailingSink : ITelemetrySink
    {
        public void Write(TelemetryRecord record) => throw new IOException("unavailable");
        public void Dispose() { }
    }
}
