using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public sealed class FixedStepDriver(FoundationSimulation simulation)
{
    public const double TickSeconds = 1d / FoundationSimulation.TickRate;
    public const int MaxCatchUpTicks = 8;
    private double _accumulator;

    public double InterpolationAlpha => _accumulator / TickSeconds;
    public double DroppedSeconds { get; private set; }

    public int Advance(double elapsedSeconds)
    {
        _accumulator += Math.Clamp(elapsedSeconds, 0, 0.25);
        var count = 0;
        while (_accumulator >= TickSeconds && count < MaxCatchUpTicks)
        {
            simulation.Step();
            _accumulator -= TickSeconds;
            count++;
        }
        if (_accumulator >= TickSeconds)
        {
            DroppedSeconds += _accumulator - (_accumulator % TickSeconds);
            _accumulator %= TickSeconds;
        }
        return count;
    }
}
