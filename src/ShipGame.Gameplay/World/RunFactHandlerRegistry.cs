namespace ShipGame.Simulation;

internal sealed class RunFactHandlerRegistry
{
    private readonly Dictionary<RunFactKind, IRunFactHandler> _handlers;

    public RunFactHandlerRegistry(IEnumerable<IRunFactHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = new Dictionary<RunFactKind, IRunFactHandler>();
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (!_handlers.TryAdd(handler.Kind, handler))
                throw new ArgumentException($"Duplicate run-fact handler for '{handler.Kind}'.");
        }

        foreach (var kind in Enum.GetValues<RunFactKind>())
        {
            if (!_handlers.ContainsKey(kind))
                throw new ArgumentException($"Missing run-fact handler for '{kind}'.");
        }
    }

    public void Dispatch(in RunFact fact, WorldRunSimulation simulation, List<WorldRunEvent> events) =>
        _handlers[fact.Kind].Handle(in fact, simulation, events);

    public static RunFactHandlerRegistry Create() => new(
    [
        new ResourceCellBrokenFactHandler(),
        new NormalEnemyDestroyedFactHandler(),
        new ResourceCollectedFactHandler(),
        new EliteDestroyedFactHandler()
    ]);
}
