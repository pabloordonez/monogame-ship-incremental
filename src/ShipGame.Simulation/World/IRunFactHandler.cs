namespace ShipGame.Simulation;

internal interface IRunFactHandler
{
    RunFactKind Kind { get; }
    void Handle(in RunFact fact, WorldRunSimulation simulation, List<WorldRunEvent> events);
}
