namespace ShipGame.Ecs;

public sealed class SystemScheduler
{
    private readonly List<ISystem> _systems = [];
    public IReadOnlyList<string> Order => _systems.Select(system => system.Name).ToArray();

    public void Add(ISystem system)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (_systems.Any(existing => existing.Name == system.Name))
            throw new InvalidOperationException($"System '{system.Name}' is already registered.");
        _systems.Add(system);
    }

    public void Tick(World world, long tick)
    {
        foreach (var system in _systems)
            system.Update(world, tick);
    }
}
