namespace ShipGame.Gameplay;

internal sealed class EliteDestroyedFactHandler : IRunFactHandler
{
    public RunFactKind Kind => RunFactKind.EliteDestroyed;

    public void Handle(in RunFact fact, WorldRun simulation, List<WorldRunEvent> events) =>
        simulation.ApplyEliteDestroyedFact();
}
