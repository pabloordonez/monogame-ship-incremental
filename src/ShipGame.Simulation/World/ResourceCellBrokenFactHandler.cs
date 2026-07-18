namespace ShipGame.Simulation;

internal sealed class ResourceCellBrokenFactHandler : IRunFactHandler
{
    public RunFactKind Kind => RunFactKind.ResourceCellBroken;

    public void Handle(in RunFact fact, WorldRunSimulation simulation, List<WorldRunEvent> events) =>
        simulation.ApplyResourceCellBrokenFact();
}
