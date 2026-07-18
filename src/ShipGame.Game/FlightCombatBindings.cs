using System.Numerics;
using Microsoft.Xna.Framework.Input;
using ShipGame.Ecs;
using ShipGame.Simulation;

namespace ShipGame.Game;

public readonly record struct KeyboardFlightInput(
    bool Up,
    bool Down,
    bool Left,
    bool Right,
    Vector2 Aim,
    bool Fire,
    bool Mine,
    bool Mobility,
    bool Interact);

public readonly record struct GamepadFlightInput(
    Vector2 Move,
    Vector2 Aim,
    bool Fire,
    bool Mine,
    bool Mobility,
    bool Interact);

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

public enum CombatCueKind : byte
{
    Weapon,
    Impact,
    Shield,
    ShieldBreak,
    Hull,
    Destruction,
    Mobility,
    Rejected,
    Spawn,
    Telegraph
}

public readonly record struct CombatCue(
    CombatCueKind Kind,
    long Tick,
    EntityId Entity,
    Vector2 Position,
    float Intensity,
    string AssetId);

public sealed class CombatPresentationBinding
{
    private readonly List<CombatCue> _cues = new(256);
    private readonly System.Collections.ObjectModel.ReadOnlyCollection<CombatCue> _view;

    public CombatPresentationBinding() => _view = _cues.AsReadOnly();

    public IReadOnlyList<CombatCue> Translate(
        IReadOnlyList<CombatEvent> events,
        Func<EntityId, CombatSnapshot?> snapshot)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(snapshot);
        _cues.Clear();
        for (var i = 0; i < events.Count; i++)
        {
            var value = events[i];
            var entitySnapshot = snapshot(value.Entity);
            var position = value.Position != default
                ? value.Position
                : entitySnapshot?.Position ?? Vector2.Zero;
            var (kind, asset) = value.Kind switch
            {
                CombatEventKind.WeaponFired => (CombatCueKind.Weapon, "sfx/combat/weapon"),
                CombatEventKind.CollisionDetected => (CombatCueKind.Impact, "sfx/combat/impact"),
                CombatEventKind.ShieldDamaged => (CombatCueKind.Shield, "sfx/combat/shield-hit"),
                CombatEventKind.ShieldDepleted => (CombatCueKind.ShieldBreak, "sfx/combat/shield-break"),
                CombatEventKind.HullDamaged => (CombatCueKind.Hull, "sfx/combat/hull-hit"),
                CombatEventKind.EntityDestroyed => (CombatCueKind.Destruction, "sfx/combat/destruction"),
                CombatEventKind.AbilityActivated => (CombatCueKind.Mobility, "sfx/combat/mobility"),
                CombatEventKind.AbilityRejected or CombatEventKind.CommandRejected =>
                    (CombatCueKind.Rejected, "sfx/ui/rejected"),
                CombatEventKind.EnemySpawned => (CombatCueKind.Spawn, "sfx/combat/spawn"),
                CombatEventKind.EliteActivated => (CombatCueKind.Spawn, "sfx/combat/elite"),
                CombatEventKind.MineTelegraphed => (CombatCueKind.Telegraph, "sfx/combat/mine-telegraph"),
                _ => throw new ArgumentOutOfRangeException()
            };
            _cues.Add(new CombatCue(kind, value.Tick, value.Entity, position, value.Amount, asset));
        }
        return _view;
    }
}
