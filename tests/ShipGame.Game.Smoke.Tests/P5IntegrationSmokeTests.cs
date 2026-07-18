using ShipGame.Domain;
using ShipGame.Simulation;

namespace ShipGame.Game.Smoke.Tests;

public sealed class P5IntegrationSmokeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ShipGame-P5-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FreshAndContinuedComposedLoops_CommitRewardsWithoutDebugCommands()
    {
        Assert.Equal(0, SmokeRunner.Run(saveDirectory: _root));
        using var session = new MetaSession(_root);
        Assert.Equal(MetaScreen.Lobby, session.Screen);
        Assert.Equal(2, session.Profile.Snapshot.RunIndex);
        Assert.True(session.Profile.Snapshot.Balances.Ferrite > 0);
        Assert.NotNull(session.Profile.Snapshot.PreviousRun);
        Assert.True(session.Profile.Snapshot.PreviousRun!.Succeeded);
    }

    [Fact]
    public void MetaSessionLaunch_LocksRunIndexBeforeFieldEntry()
    {
        using var session = new MetaSession(_root, newProfileSeed: 9);
        Assert.Equal(0, session.Profile.Snapshot.RunIndex);
        session.Navigate(MetaScreen.Lobby);
        session.Navigate(MetaScreen.Map);
        Assert.True(session.Launch().Accepted);
        Assert.Equal(1, session.Profile.Snapshot.RunIndex);
        Assert.Equal(MetaScreen.Run, session.Screen);
    }

    [Fact]
    public void KeyboardAndGamepadAdapters_ProduceParityFrames()
    {
        var keyboard = FlightInputAdapters.Keyboard(
            3,
            new KeyboardFlightInput(true, false, false, false, new System.Numerics.Vector2(1, 0), true, false, false, false));
        var gamepad = FlightInputAdapters.Gamepad(
            3,
            new GamepadFlightInput(new System.Numerics.Vector2(0, -1), new System.Numerics.Vector2(1, 0), true, false, false, false));
        Assert.Equal(keyboard.AimX, gamepad.AimX);
        Assert.Equal(FlightAction.Fire, keyboard.Actions & FlightAction.Fire);
        Assert.Equal(FlightAction.Fire, gamepad.Actions & FlightAction.Fire);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
