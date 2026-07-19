using System.Numerics;

namespace ShipGame.Gameplay;

internal static class FlightCombatMath
{
    public static Vector2 NormalizeOr(Vector2 value, Vector2 fallback) =>
        value.LengthSquared() > 0.0001f ? Vector2.Normalize(value) : fallback;

    public static Vector2 Perpendicular(Vector2 value) => new(-value.Y, value.X);

    /// <summary>
    /// Distance along a unit ray from <paramref name="origin"/> to the first intersection with a circle.
    /// Returns false when the ray misses or the circle is behind the origin.
    /// </summary>
    public static bool TryRayCircleEntry(
        Vector2 origin,
        Vector2 unitDirection,
        Vector2 center,
        float radius,
        out float entryDistance)
    {
        entryDistance = 0f;
        if (!float.IsFinite(radius) || radius < 0f)
            return false;
        var delta = center - origin;
        var along = Vector2.Dot(unitDirection, delta);
        if (along <= 0f)
            return false;
        var closestDistSq = delta.LengthSquared() - along * along;
        if (closestDistSq < 0f)
            closestDistSq = 0f;
        var radiusSq = radius * radius;
        if (closestDistSq > radiusSq)
            return false;
        entryDistance = along - MathF.Sqrt(radiusSq - closestDistSq);
        if (entryDistance < 0f)
            entryDistance = 0f;
        return true;
    }
}
