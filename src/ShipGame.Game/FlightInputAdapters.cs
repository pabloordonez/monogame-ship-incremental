using System.Numerics;
using Microsoft.Xna.Framework.Input;
using ShipGame.Ecs;
using ShipGame.Simulation;

namespace ShipGame.Game;

public static class FlightInputAdapters
{
    public static FlightCommandFrame Keyboard(long targetTick, KeyboardFlightInput input)
    {
        var move = new Vector2(
            (input.Right ? 1 : 0) - (input.Left ? 1 : 0),
            (input.Down ? 1 : 0) - (input.Up ? 1 : 0));
        return Create(targetTick, move, input.Aim, input.Fire, input.Mine, input.Mobility, input.Interact);
    }

    public static FlightCommandFrame Gamepad(long targetTick, GamepadFlightInput input) =>
        Create(targetTick, input.Move, input.Aim, input.Fire, input.Mine, input.Mobility, input.Interact);

    public static KeyboardFlightInput ReadKeyboard(KeyboardState keyboard, Vector2 worldAim) => new(
        keyboard.IsKeyDown(Keys.W),
        keyboard.IsKeyDown(Keys.S),
        keyboard.IsKeyDown(Keys.A),
        keyboard.IsKeyDown(Keys.D),
        worldAim,
        Mouse.GetState().LeftButton == ButtonState.Pressed,
        Mouse.GetState().RightButton == ButtonState.Pressed,
        keyboard.IsKeyDown(Keys.Space),
        keyboard.IsKeyDown(Keys.E));

    public static GamepadFlightInput ReadGamepad(GamePadState gamepad)
    {
        var left = gamepad.ThumbSticks.Left;
        var right = gamepad.ThumbSticks.Right;
        return new GamepadFlightInput(
            new Vector2(left.X, -left.Y),
            new Vector2(right.X, -right.Y),
            gamepad.Triggers.Right > 0.5f,
            gamepad.Triggers.Left > 0.5f,
            gamepad.Buttons.LeftShoulder == ButtonState.Pressed,
            gamepad.Buttons.X == ButtonState.Pressed);
    }

    private static FlightCommandFrame Create(
        long targetTick,
        Vector2 move,
        Vector2 aim,
        bool fire,
        bool mine,
        bool mobility,
        bool interact)
    {
        move = ClampUnit(move);
        aim = ClampUnit(aim);
        var actions = FlightAction.None;
        if (fire)
            actions |= FlightAction.Fire;
        else if (mine)
            actions |= FlightAction.Mine;
        if (mobility)
            actions |= FlightAction.Mobility;
        if (interact)
            actions |= FlightAction.Interact;
        return new FlightCommandFrame(
            targetTick,
            FlightCommandFrame.Quantize(move.X),
            FlightCommandFrame.Quantize(move.Y),
            FlightCommandFrame.Quantize(aim.X),
            FlightCommandFrame.Quantize(aim.Y),
            actions);
    }

    private static Vector2 ClampUnit(Vector2 value) =>
        value.LengthSquared() > 1 ? Vector2.Normalize(value) : value;
}
