using Microsoft.Xna.Framework;
using ShipGame.Game;
using ShipGame.Gameplay;

namespace ShipGame.Game.Smoke.Tests;

public sealed class RunGuidanceTests
{
    [Fact]
    public void ObjectiveBriefingExplainsEachPhase()
    {
        var objective = RunObjectiveBriefing.For(Hud(RunPhase.Objective, ferrite: 12, kills: 3));
        Assert.Equal("Field objective", objective.Title);
        Assert.Contains("12/30", objective.Body);
        Assert.Contains("3/8", objective.Body);
        Assert.DoesNotContain("E extract", objective.Controls);

        var elite = RunObjectiveBriefing.For(Hud(RunPhase.Elite));
        Assert.Equal("Elite threat", elite.Title);
        Assert.Contains("gunship", elite.Body, StringComparison.OrdinalIgnoreCase);

        var extract = RunObjectiveBriefing.For(Hud(RunPhase.Extraction));
        Assert.Equal("Extract", extract.Title);
        Assert.Contains("hold E", extract.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("E extract", extract.Controls);
    }

    [Fact]
    public void ToastCopyCoversPhaseTransitions()
    {
        Assert.Contains("elite", RunObjectiveBriefing.ToastFor(WorldRunEventKind.ObjectiveCompleted)!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("extraction", RunObjectiveBriefing.ToastFor(WorldRunEventKind.EliteActivationRequested)!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("E", RunObjectiveBriefing.ToastFor(WorldRunEventKind.ExtractionActivated)!);
        Assert.Null(RunObjectiveBriefing.ToastFor(WorldRunEventKind.ResourceCredited));
    }

    [Fact]
    public void EdgePingProjectsOffScreenTargetsAndSkipsOnScreen()
    {
        var onScreen = ScreenEdgePing.Project(new Vector2(320, 180), 640, 360);
        Assert.Null(onScreen);

        var offRight = ScreenEdgePing.Project(new Vector2(1200, 180), 640, 360);
        Assert.NotNull(offRight);
        Assert.InRange(offRight!.Value.ScreenPosition.X, 600, 640);
        Assert.InRange(offRight.Value.ScreenPosition.Y, 160, 200);

        var offUp = ScreenEdgePing.Project(new Vector2(320, -400), 640, 360);
        Assert.NotNull(offUp);
        Assert.InRange(offUp!.Value.ScreenPosition.Y, 0, 30);
    }

    private static ComposedRunHud Hud(
        RunPhase phase,
        int ferrite = 0,
        int kills = 0) =>
        new(
            RunTick: 1,
            Phase: phase,
            Hull: 100,
            Shield: 50,
            FerriteHeld: 0,
            LumenHeld: 0,
            DataCoresHeld: 0,
            ObjectiveFerrite: ferrite,
            ObjectiveKills: kills,
            ExtractionProgressTicks: 0,
            ExtractionHoldTicks: 360,
            ThreatCap: 4);
}
