using System.Numerics;
using ShipGame.Game;
using ShipGame.Gameplay;

namespace ShipGame.Game.Smoke.Tests;

public sealed class FlightInputMergeTests
{
    [Fact]
    public void MergeKeepsKeyboardInteractWhenGamepadSticksAreActive()
    {
        var keyboard = new KeyboardFlightInput(
            Up: false,
            Down: false,
            Left: false,
            Right: false,
            Aim: Vector2.UnitX,
            Fire: false,
            Mine: false,
            Mobility: false,
            Interact: true);
        var gamepad = new GamepadFlightInput(
            Move: new Vector2(0.5f, 0f),
            Aim: new Vector2(0f, 0.5f),
            Fire: false,
            Mine: false,
            Mobility: false,
            Interact: false);

        var command = FlightInputAdapters.Merge(0, keyboard, gamepad, gamepadConnected: true);

        Assert.Equal(FlightAction.Interact, command.Actions & FlightAction.Interact);
        Assert.True(command.Move.X > 0.4f);
        Assert.True(command.Aim.Y > 0.4f);
    }

    [Fact]
    public void MergeWithoutGamepadReturnsKeyboardCommand()
    {
        var keyboard = new KeyboardFlightInput(
            Up: true,
            Down: false,
            Left: false,
            Right: false,
            Aim: Vector2.UnitX,
            Fire: true,
            Mine: false,
            Mobility: false,
            Interact: true);

        var command = FlightInputAdapters.Merge(3, keyboard, null, gamepadConnected: false);

        Assert.Equal(FlightAction.Fire | FlightAction.Interact, command.Actions);
        Assert.True(command.Move.Y < 0);
    }
}
