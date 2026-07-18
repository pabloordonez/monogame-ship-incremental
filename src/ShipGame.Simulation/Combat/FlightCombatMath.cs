using System.Numerics;

namespace ShipGame.Simulation;

internal static class FlightCombatMath
{
    public static Vector2 NormalizeOr(Vector2 value, Vector2 fallback) =>
        value.LengthSquared() > 0.0001f ? Vector2.Normalize(value) : fallback;

    public static Vector2 Perpendicular(Vector2 value) => new(-value.Y, value.X);
}
