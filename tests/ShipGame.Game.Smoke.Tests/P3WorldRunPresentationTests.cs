using ShipGame.Domain;
using ShipGame.Simulation;

namespace ShipGame.Game.Smoke.Tests;

public class P3WorldRunPresentationTests
{
    [Fact]
    public void WorldRunEventsBindToStablePresentationCues()
    {
        var hazard = WorldRunPresentationBindings.Bind(
            new WorldRunEvent(1, 10, WorldRunEventKind.HazardWarned, WorldRunIds.CinderBelt, 25));
        Assert.NotNull(hazard);
        Assert.Equal("environment/cinder-belt/flare-warning", hazard!.Value.AssetId.Value);
        Assert.Equal("sfx/hazard-warning", hazard.Value.AudioCueId.Value);

        var credit = WorldRunPresentationBindings.Bind(
            new WorldRunEvent(2, 11, WorldRunEventKind.ResourceCredited, WorldRunIds.Ferrite, 3));
        Assert.NotNull(credit);
        Assert.Equal("pickup/ferrite", credit!.Value.AssetId.Value);

        var extraction = WorldRunPresentationBindings.Bind(
            new WorldRunEvent(3, 12, WorldRunEventKind.ExtractionActivated, WorldRunIds.StandardGate));
        Assert.NotNull(extraction);
        Assert.Equal("ui/extraction-marker", extraction!.Value.AssetId.Value);

        Assert.Null(WorldRunPresentationBindings.Bind(
            new WorldRunEvent(4, 13, WorldRunEventKind.RewardProposed)));
    }
}
