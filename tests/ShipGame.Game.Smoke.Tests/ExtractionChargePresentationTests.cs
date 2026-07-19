using ShipGame.Game;

namespace ShipGame.Game.Smoke.Tests;

public sealed class ExtractionChargePresentationTests
{
    [Theory]
    [InlineData(0, 360, 0f)]
    [InlineData(180, 360, 0.5f)]
    [InlineData(360, 360, 1f)]
    [InlineData(400, 360, 1f)]
    [InlineData(10, 0, 0f)]
    public void ProgressRatioClampsToZeroOne(int progressTicks, int holdTicks, float expected)
    {
        Assert.Equal(expected, MvpPresentation.ExtractionProgressRatio(progressTicks, holdTicks));
    }
}
