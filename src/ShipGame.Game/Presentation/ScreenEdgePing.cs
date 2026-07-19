using System.Numerics;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace ShipGame.Game;

public readonly record struct EdgePing(XnaVector2 ScreenPosition, float RotationRadians);

/// <summary>
/// Projects an off-screen world target onto the virtual viewport edge for direction pings.
/// Returns null when the target is already on-screen (within margin).
/// </summary>
public static class ScreenEdgePing
{
    public static EdgePing? Project(
        XnaVector2 screenTarget,
        int virtualWidth,
        int virtualHeight,
        int margin = 24,
        int inset = 18)
    {
        if (screenTarget.X >= margin &&
            screenTarget.X <= virtualWidth - margin &&
            screenTarget.Y >= margin &&
            screenTarget.Y <= virtualHeight - margin)
            return null;

        var center = new Vector2(virtualWidth * 0.5f, virtualHeight * 0.5f);
        var delta = new Vector2(screenTarget.X - center.X, screenTarget.Y - center.Y);
        if (delta.LengthSquared() < 0.0001f)
            delta = new Vector2(1f, 0f);

        var halfW = virtualWidth * 0.5f - inset;
        var halfH = virtualHeight * 0.5f - inset;
        var scaleX = halfW / MathF.Abs(delta.X);
        var scaleY = halfH / MathF.Abs(delta.Y);
        var scale = MathF.Min(scaleX, scaleY);
        var edge = center + delta * scale;
        var rotation = MathF.Atan2(delta.Y, delta.X);
        return new EdgePing(new XnaVector2(edge.X, edge.Y), rotation);
    }
}
