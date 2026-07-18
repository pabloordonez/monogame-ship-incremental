namespace ShipGame.Simulation;

internal sealed class ResourceCollectedFactHandler : IRunFactHandler
{
    public RunFactKind Kind => RunFactKind.ResourceCollected;

    public void Handle(in RunFact fact, WorldRunSimulation simulation, List<WorldRunEvent> events) =>
        simulation.ApplyResourceCollectedFact(in fact, events);
}
