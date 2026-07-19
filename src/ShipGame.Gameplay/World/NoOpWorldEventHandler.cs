namespace ShipGame.Simulation;

/// <summary>Presentation-only world events with no orchestrator side effects.</summary>
internal sealed class NoOpWorldEventHandler(WorldRunEventKind kind) : IWorldRunEventHandler
{
    public WorldRunEventKind Kind { get; } = kind;

    public void Handle(in WorldRunEvent worldEvent, IWorldRunEventHost host)
    {
    }
}
