namespace ShipGame.Gameplay;

internal sealed class ResourceCollectedFactHandler : IRunFactHandler
{
    public RunFactKind Kind => RunFactKind.ResourceCollected;

    public void Handle(in RunFact fact, WorldRun simulation, List<WorldRunEvent> events) =>
        simulation.ApplyResourceCollectedFact(in fact, events);
}
