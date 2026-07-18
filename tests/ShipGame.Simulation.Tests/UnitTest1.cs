using ShipGame.Domain;

namespace ShipGame.Simulation.Tests;

public class SimulationTests
{
    [Fact]
    public void SameCommandsProduceSameHashesAcrossRenderCadences()
    {
        static (ulong Hash, AppState State, long Tick) Run(double[] cadence)
        {
            var simulation = new FoundationSimulation(42);
            var driver = new FixedStepDriver(simulation);
            simulation.Queue(new CommandFrame(0, Confirm: true));
            simulation.Queue(new CommandFrame(1, Confirm: true));
            var frame = 0;
            while (simulation.Tick < 240)
                driver.Advance(cadence[frame++ % cadence.Length]);
            return (simulation.LastStateHash, simulation.State, simulation.Tick);
        }

        Assert.Equal(
            Run([1d / 60d]),
            Run([1d / 144d, 1d / 30d, 1d / 90d, 1d / 50d]));
    }

    [Fact]
    public void CommandsRejectStaleAndExcessivelyFutureTicks()
    {
        var simulation = new FoundationSimulation(7);
        simulation.Step();

        Assert.False(simulation.Queue(new CommandFrame(0, Confirm: true)));
        Assert.False(simulation.Queue(new CommandFrame(1000, Confirm: true)));
        Assert.True(simulation.Queue(CommandFrame.Neutral(simulation.Tick)));
    }

    [Fact]
    public void NamedRandomStreamsAreIsolated()
    {
        var baseline = new RandomStreams(99);
        var changed = new RandomStreams(99);
        _ = changed.Get(RngStream.Loot).NextUInt();
        _ = changed.Get(RngStream.Loot).NextUInt();

        Assert.Equal(
            baseline.Get(RngStream.Layout).NextUInt(),
            changed.Get(RngStream.Layout).NextUInt());
    }

    [Fact]
    public void PcgGoldenSequenceIsVersioned()
    {
        var random = new Pcg32(1234, 5678);
        Assert.Equal(
            new uint[] { 2425132909, 1536316163, 2210751970, 3245404908 },
            Enumerable.Range(0, 4).Select(_ => random.NextUInt()).ToArray());
    }
}
