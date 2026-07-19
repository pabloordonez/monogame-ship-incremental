using ShipGame.Gameplay;

namespace ShipGame.Gameplay.Tests;

public sealed class AsteroidSizingTests
{
    [Theory]
    [InlineData(AsteroidCellKind.Ferrite, AsteroidCellSize.Small, 1.0f, "asteroids/small/ferrite")]
    [InlineData(AsteroidCellKind.Ferrite, AsteroidCellSize.Small, 0.50f, "asteroids/small/ferrite-cracked")]
    [InlineData(AsteroidCellKind.Ferrite, AsteroidCellSize.Small, 0.20f, "asteroids/small/ferrite-shattered")]
    [InlineData(AsteroidCellKind.Lumen, AsteroidCellSize.Large, 0.70f, "asteroids/large/lumen")]
    [InlineData(AsteroidCellKind.Ordinary, AsteroidCellSize.Medium, 0.32f, "asteroids/medium/ordinary-shattered")]
    public void AtlasRegion_SelectsHealthTier(
        AsteroidCellKind kind,
        AsteroidCellSize size,
        float healthFraction,
        string expected) =>
        Assert.Equal(expected, AsteroidSizing.AtlasRegion(kind, size, healthFraction));
}
