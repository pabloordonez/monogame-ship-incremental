using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct FlightCommandFrame(
    long TargetTick,
    short MoveX,
    short MoveY,
    short AimX,
    short AimY,
    FlightAction Actions)
{
    public static FlightCommandFrame Neutral(long tick) => new(tick, 0, 0, 0, 0, FlightAction.None);

    public Vector2 Move => Decode(MoveX, MoveY);
    public Vector2 Aim => Decode(AimX, AimY);

    public static short Quantize(float value) =>
        (short)Math.Clamp(
            (int)MathF.Round(Math.Clamp(value, -1f, 1f) * FlightCombatConstants.CommandScale),
            -FlightCombatConstants.CommandScale,
            FlightCombatConstants.CommandScale);

    private static Vector2 Decode(short x, short y)
    {
        var value = new Vector2(x, y) / FlightCombatConstants.CommandScale;
        var lengthSquared = value.LengthSquared();
        return lengthSquared > 1f ? Vector2.Normalize(value) : value;
    }
}
