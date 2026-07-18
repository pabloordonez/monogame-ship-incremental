namespace ShipGame.Simulation;

internal sealed class NormalEnemyDestroyedFactHandler : IRunFactHandler
{
    public RunFactKind Kind => RunFactKind.NormalEnemyDestroyed;

    public void Handle(in RunFact fact, WorldRunSimulation simulation, List<WorldRunEvent> events) =>
        simulation.ApplyNormalEnemyDestroyedFact();
}
