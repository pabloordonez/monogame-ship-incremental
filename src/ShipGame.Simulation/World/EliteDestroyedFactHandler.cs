namespace ShipGame.Simulation;

internal sealed class EliteDestroyedFactHandler : IRunFactHandler
{
    public RunFactKind Kind => RunFactKind.EliteDestroyed;

    public void Handle(in RunFact fact, WorldRunSimulation simulation, List<WorldRunEvent> events) =>
        simulation.ApplyEliteDestroyedFact();
}
