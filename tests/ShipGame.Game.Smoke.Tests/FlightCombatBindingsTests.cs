using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;
using ShipGame.Simulation;

namespace ShipGame.Game.Smoke.Tests;

public sealed class FlightCombatBindingsTests
{
    [Fact]
    public void KeyboardAndGamepadAdaptersProduceIdenticalCommandFrames()
    {
        var keyboard = FlightInputAdapters.Keyboard(
            7,
            new KeyboardFlightInput(
                Up: false,
                Down: false,
                Left: false,
                Right: true,
                Aim: Vector2.Normalize(new Vector2(0.6f, -0.8f)),
                Fire: true,
                Mine: true,
                Mobility: true,
                Interact: false));
        var gamepad = FlightInputAdapters.Gamepad(
            7,
            new GamepadFlightInput(
                Move: Vector2.UnitX,
                Aim: Vector2.Normalize(new Vector2(0.6f, -0.8f)),
                Fire: true,
                Mine: true,
                Mobility: true,
                Interact: false));

        Assert.Equal(keyboard, gamepad);
        Assert.Equal(FlightAction.Fire | FlightAction.Mobility, keyboard.Actions);
        Assert.Equal(FlightCommandFrame.Quantize(1f), keyboard.MoveX);
        Assert.Equal(0, keyboard.MoveY);
    }

    [Fact]
    public void PresentationBindingsAreReadOnlyAndDoNotMutateSimulation()
    {
        var simulation = new FlightCombatSimulation(11);
        var player = simulation.SpawnPlayer(Vector2.Zero, new ContentId("MOD_WEAPON_PULSE"));
        simulation.SpawnEnemy(new ContentId("ENM_INTERCEPTOR"), new Vector2(80, 0));
        simulation.Queue(new FlightCommandFrame(
            0,
            FlightCommandFrame.Quantize(0),
            FlightCommandFrame.Quantize(0),
            FlightCommandFrame.Quantize(1),
            FlightCommandFrame.Quantize(0),
            FlightAction.Fire));
        simulation.Step();

        var beforeHash = simulation.LastStateHash;
        var beforeShield = simulation.Snapshot(player).Shield;
        var beforeEvents = simulation.Events.ToArray();
        var binding = new CombatPresentationBinding();
        CombatSnapshot? Snapshot(EntityId entity)
        {
            try
            {
                return simulation.Snapshot(entity);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        var cues = binding.Translate(beforeEvents, Snapshot);
        Assert.NotEmpty(cues);
        Assert.Equal(beforeHash, simulation.LastStateHash);
        Assert.Equal(beforeShield, simulation.Snapshot(player).Shield);
        Assert.Equal(beforeEvents, simulation.Events.ToArray());

        var first = cues[0];
        Assert.Throws<NotSupportedException>(() => ((ICollection<CombatCue>)cues).Add(first));
    }
}
