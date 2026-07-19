namespace ShipGame.Gameplay;

internal sealed class ResourceCellBrokenFactHandler : IRunFactHandler
{
    public RunFactKind Kind => RunFactKind.ResourceCellBroken;

    public void Handle(in RunFact fact, WorldRun simulation, List<WorldRunEvent> events) =>
        simulation.ApplyResourceCellBrokenFact();
}
