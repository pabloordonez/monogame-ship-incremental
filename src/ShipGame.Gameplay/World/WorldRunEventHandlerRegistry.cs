namespace ShipGame.Simulation;

internal sealed class WorldRunEventHandlerRegistry
{
    private readonly Dictionary<WorldRunEventKind, IWorldRunEventHandler> _handlers;

    public WorldRunEventHandlerRegistry(IEnumerable<IWorldRunEventHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = new Dictionary<WorldRunEventKind, IWorldRunEventHandler>();
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (!_handlers.TryAdd(handler.Kind, handler))
                throw new ArgumentException($"Duplicate world-run event handler for '{handler.Kind}'.");
        }

        foreach (var kind in Enum.GetValues<WorldRunEventKind>())
        {
            if (!_handlers.ContainsKey(kind))
                throw new ArgumentException($"Missing world-run event handler for '{kind}'.");
        }
    }

    public void Dispatch(in WorldRunEvent worldEvent, IWorldRunEventHost host) =>
        _handlers[worldEvent.Kind].Handle(in worldEvent, host);

    public static WorldRunEventHandlerRegistry Create() => new(
    [
        new NoOpWorldEventHandler(WorldRunEventKind.HazardWarned),
        new HazardDamageWorldEventHandler(),
        new NoOpWorldEventHandler(WorldRunEventKind.ResourceCredited),
        new NoOpWorldEventHandler(WorldRunEventKind.UpgradeThresholdReached),
        new NoOpWorldEventHandler(WorldRunEventKind.UpgradeOffered),
        new NoOpWorldEventHandler(WorldRunEventKind.UpgradeSelected),
        new CheckpointWorldEventHandler(WorldRunEventKind.ObjectiveCompleted, "objective_complete"),
        new EliteActivationWorldEventHandler(),
        new NoOpWorldEventHandler(WorldRunEventKind.EliteDefeated),
        new DataCoreDropWorldEventHandler(),
        new CheckpointWorldEventHandler(WorldRunEventKind.ExtractionActivated, "extraction_ready"),
        new NoOpWorldEventHandler(WorldRunEventKind.ExtractionProgressed),
        new NoOpWorldEventHandler(WorldRunEventKind.ExtractionReset),
        new NoOpWorldEventHandler(WorldRunEventKind.CollapseWarning),
        new CheckpointWorldEventHandler(WorldRunEventKind.RunSucceeded, "run_succeeded"),
        new CheckpointWorldEventHandler(WorldRunEventKind.RunFailed, "run_failed"),
        new NoOpWorldEventHandler(WorldRunEventKind.RewardProposed)
    ]);
}
