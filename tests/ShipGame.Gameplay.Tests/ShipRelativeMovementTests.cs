using System.Numerics;

namespace ShipGame.Gameplay.Tests;

public sealed class ShipRelativeMovementTests
{
    [Fact]
    public void ForwardStickMapsToAimDirection()
    {
        var forward = new Vector2(0, -1);
        var alongX = ShipRelativeMovement.ToWorld(forward, Vector2.UnitX, 0f);
        Assert.InRange(alongX.X, 0.99f, 1.01f);
        Assert.InRange(alongX.Y, -0.01f, 0.01f);

        var alongY = ShipRelativeMovement.ToWorld(forward, Vector2.UnitY, 0f);
        Assert.InRange(alongY.X, -0.01f, 0.01f);
        Assert.InRange(alongY.Y, 0.99f, 1.01f);
    }

    [Fact]
    public void StrafeStickMapsToStarboardOfAim()
    {
        var strafe = new Vector2(1, 0);
        var world = ShipRelativeMovement.ToWorld(strafe, Vector2.UnitX, 0f);
        Assert.InRange(world.X, -0.01f, 0.01f);
        Assert.InRange(world.Y, 0.99f, 1.01f);
    }
}
