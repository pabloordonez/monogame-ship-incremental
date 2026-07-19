namespace ShipGame.Gameplay;

internal sealed class NormalEnemyDestroyedFactHandler : IRunFactHandler
{
    public RunFactKind Kind => RunFactKind.NormalEnemyDestroyed;

    public void Handle(in RunFact fact, WorldRun simulation, List<WorldRunEvent> events) =>
        simulation.ApplyNormalEnemyDestroyedFact();
}
