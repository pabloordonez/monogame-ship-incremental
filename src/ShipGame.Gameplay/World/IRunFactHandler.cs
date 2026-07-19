namespace ShipGame.Gameplay;

internal interface IRunFactHandler
{
    RunFactKind Kind { get; }
    void Handle(in RunFact fact, WorldRun simulation, List<WorldRunEvent> events);
}
