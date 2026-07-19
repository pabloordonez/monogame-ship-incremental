using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed class EnvironmentHazardSystem(FieldDescriptor descriptor)
{
    private readonly FieldDescriptor _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    private readonly HashSet<int> _warnings = [];
    private readonly HashSet<int> _resolutions = [];

    public int ShieldRechargeDelayAdditionTicks =>
        _descriptor.Identity.EnvironmentId == WorldRunIds.IonVeil ? 90 : 0;

    public IReadOnlyList<(WorldRunEventKind Kind, HazardDescriptor Hazard)> Resolve(
        long runTick,
        GridPoint playerCell,
        bool behindLargeAsteroid)
    {
        var events = new List<(WorldRunEventKind, HazardDescriptor)>();
        for (var index = 0; index < _descriptor.Hazards.Count; index++)
        {
            var hazard = _descriptor.Hazards[index];
            if (runTick == hazard.WarningTick && _warnings.Add(index))
                events.Add((WorldRunEventKind.HazardWarned, hazard));
            if (runTick != hazard.ResolveTick || !_resolutions.Add(index))
                continue;
            var impacts = _descriptor.Identity.EnvironmentId == WorldRunIds.CinderBelt
                ? !behindLargeAsteroid
                : DistanceSquared(playerCell, hazard.Center) <= (long)hazard.Radius * hazard.Radius;
            if (impacts)
                events.Add((WorldRunEventKind.HazardDamageRequested, hazard));
        }
        return events;
    }

    private static long DistanceSquared(GridPoint left, GridPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return (long)dx * dx + (long)dy * dy;
    }
}
