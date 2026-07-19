using System.Numerics;

namespace ShipGame.Gameplay;

/// <summary>
/// Converts local stick Move (W/S/A/D or left stick) into world thrust relative to facing.
/// Input encoding: W=(0,-1), S=(0,+1), A=(-1,0), D=(+1,0).
/// </summary>
public static class ShipRelativeMovement
{
    public static float FacingFromAim(Vector2 aim, float fallbackRotation) =>
        aim.LengthSquared() > 0.0001f
            ? MathF.Atan2(aim.Y, aim.X)
            : fallbackRotation;

    public static Vector2 ToWorld(Vector2 localStick, float facingRadians)
    {
        if (localStick.LengthSquared() <= 0.0001f)
            return Vector2.Zero;
        // W→forward (1,0) local, D→starboard (0,1) local before rotate.
        var local = new Vector2(-localStick.Y, localStick.X);
        var cos = MathF.Cos(facingRadians);
        var sin = MathF.Sin(facingRadians);
        var world = new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
        return world.LengthSquared() > 1f ? Vector2.Normalize(world) : world;
    }

    public static Vector2 ToWorld(Vector2 localStick, Vector2 aim, float fallbackRotation) =>
        ToWorld(localStick, FacingFromAim(aim, fallbackRotation));
}
