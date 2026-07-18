using ShipGame.Domain;
using ShipGame.Simulation;

namespace ShipGame.Game;

public readonly record struct WorldRunPresentationCue(
    ContentId AssetId,
    ContentId AudioCueId,
    int Amount);

public static class WorldRunPresentationBindings
{
    public static WorldRunPresentationCue? Bind(WorldRunEvent fact) =>
        fact.Kind switch
        {
            WorldRunEventKind.HazardWarned => new(
                new ContentId(fact.ContentId == WorldRunIds.CinderBelt
                    ? "environment/cinder-belt/flare-warning"
                    : "environment/ion-veil/strike-warning"),
                new ContentId("sfx/hazard-warning"),
                fact.Amount),
            WorldRunEventKind.HazardDamageRequested => new(
                new ContentId(fact.ContentId == WorldRunIds.CinderBelt
                    ? "environment/cinder-belt/solar-flare"
                    : "environment/ion-veil/ion-strike"),
                new ContentId("sfx/hazard-impact"),
                fact.Amount),
            WorldRunEventKind.ResourceCredited => new(
                ResourceAsset(fact.ContentId),
                new ContentId("sfx/resource-collect"),
                fact.Amount),
            WorldRunEventKind.UpgradeOffered => new(
                new ContentId("ui/run-upgrade-offer"),
                new ContentId("sfx/upgrade-ready"),
                fact.Amount),
            WorldRunEventKind.ObjectiveCompleted => new(
                new ContentId("ui/objective-complete"),
                new ContentId("sfx/objective-complete"),
                0),
            WorldRunEventKind.EliteActivationRequested => new(
                new ContentId("ui/elite-marker"),
                new ContentId("sfx/elite-activated"),
                0),
            WorldRunEventKind.ExtractionActivated => new(
                new ContentId("ui/extraction-marker"),
                new ContentId("sfx/extraction-activated"),
                0),
            WorldRunEventKind.CollapseWarning => new(
                new ContentId("ui/collapse-warning"),
                new ContentId("sfx/collapse-warning"),
                0),
            WorldRunEventKind.RunSucceeded => new(
                new ContentId("ui/run-success"),
                new ContentId("sfx/run-success"),
                0),
            WorldRunEventKind.RunFailed => new(
                new ContentId("ui/run-failure"),
                new ContentId("sfx/run-failure"),
                0),
            _ => null
        };

    private static ContentId ResourceAsset(ContentId resourceId)
    {
        if (resourceId == WorldRunIds.Ferrite)
            return new("pickup/ferrite");
        if (resourceId == WorldRunIds.Lumen)
            return new("pickup/lumen");
        if (resourceId == WorldRunIds.DataCore)
            return new("pickup/data-core");
        throw new ArgumentException("Unknown resource presentation binding.", nameof(resourceId));
    }
}
