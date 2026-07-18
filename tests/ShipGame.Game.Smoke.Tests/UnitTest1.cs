namespace ShipGame.Game.Smoke.Tests;

public class SmokeTests
{
    [Fact]
    public void EmptyRunSavesAndContinues()
    {
        var saveRoot = Path.Combine(Path.GetTempPath(), "ShipGame-SmokeTest-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(0, SmokeRunner.Run(saveDirectory: saveRoot));
        }
        finally
        {
            if (Directory.Exists(saveRoot))
                Directory.Delete(saveRoot, true);
        }
    }
}
