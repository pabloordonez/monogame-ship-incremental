using ShipGame.Content;
using ShipGame.Domain;
using ShipGame.Gameplay;

namespace ShipGame.Game;


public static class SmokeRunner
{
    public static int Run(string? repositoryRoot = null, string? saveDirectory = null)
    {
        repositoryRoot ??= ShipGameHost.FindRepositoryRoot();
        var contentRoot = Path.Combine(repositoryRoot, "content", "generated", "DesktopVK", "Content");
        var manifest = ContentValidator.LoadAndValidateManifest(contentRoot, "data/asset-manifest.json");
        var catalog = new FileAssetCatalog(contentRoot, manifest);
        if (!catalog.LoadText(new ContentId("data/title-placeholder")).Contains("SHIP GAME", StringComparison.Ordinal))
            return 10;

        saveDirectory ??= Path.Combine(Path.GetTempPath(), "ShipGame-Smoke-" + Guid.NewGuid().ToString("N"));
        using var session = new MetaSession(saveDirectory, newProfileSeed: 123456789UL);
        if (session.Screen == MetaScreen.Title)
            session.Navigate(MetaScreen.Station);
        session.Navigate(MetaScreen.Map);
        if (!session.Launch().Accepted)
            return 11;

        var snapshot = session.Profile.Snapshot;
        var run = new ComposedRunOrchestrator(
            new ContentId(session.Ui.SelectedEnvironmentId),
            snapshot.ProfileSeed,
            snapshot.RunIndex,
            session.Profile.ResolveLoadout(),
            session.Profile.DeriveStatistics(),
            recoveryProtocols: false,
            purchasedUpgradeIds: snapshot.PurchasedUpgradeIds);
        var reward = run.CompleteViaHarness(succeed: true);
        if (!run.Checkpoints.Contains("extracted") || !run.Checkpoints.Contains("reward_mapped"))
            return 12;
        if (session.CommitReward(reward).Status != ProfileMutationStatus.Applied)
            return 13;
        if (session.Screen != MetaScreen.Summary)
            return 14;
        session.Navigate(MetaScreen.Station);

        using var continued = new MetaSession(saveDirectory);
        if (continued.Screen != MetaScreen.Title || !continued.HasContinueSave)
            return 15;
        if (!continued.Navigate(MetaScreen.Station).Accepted)
            return 15;
        continued.Navigate(MetaScreen.Map);
        if (!continued.Launch().Accepted)
            return 16;
        var continuedSnapshot = continued.Profile.Snapshot;
        var second = new ComposedRunOrchestrator(
            new ContentId(continued.Ui.SelectedEnvironmentId),
            continuedSnapshot.ProfileSeed,
            continuedSnapshot.RunIndex,
            continued.Profile.ResolveLoadout(),
            continued.Profile.DeriveStatistics(),
            recoveryProtocols: false,
            purchasedUpgradeIds: continuedSnapshot.PurchasedUpgradeIds);
        var secondReward = second.CompleteViaHarness(succeed: true);
        if (continued.CommitReward(secondReward).Status != ProfileMutationStatus.Applied)
            return 17;
        return continued.Screen == MetaScreen.Summary ? 0 : 18;
    }
}
