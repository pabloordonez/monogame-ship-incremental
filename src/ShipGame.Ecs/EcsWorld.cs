namespace ShipGame.Ecs;

public readonly record struct EntityId(int Index, uint Generation) : IComparable<EntityId>
{
    public int CompareTo(EntityId other)
    {
        var index = Index.CompareTo(other.Index);
        return index != 0 ? index : Generation.CompareTo(other.Generation);
    }
}

public interface IComponentStore
{
    void RemoveEntity(EntityId entity);
}

public sealed class ComponentStore<T> : IComponentStore where T : struct
{
    private readonly List<EntityId> _entities = [];
    private readonly List<T> _components = [];
    private int[] _sparse = [];

    public int Count => _entities.Count;
    public IReadOnlyList<EntityId> Entities => _entities;

    public void Set(EntityId entity, T component)
    {
        EnsureSparse(entity.Index);
        var dense = _sparse[entity.Index] - 1;
        if (dense >= 0 && _entities[dense] == entity)
        {
            _components[dense] = component;
            return;
        }

        _sparse[entity.Index] = _entities.Count + 1;
        _entities.Add(entity);
        _components.Add(component);
    }

    public bool Has(EntityId entity)
    {
        if ((uint)entity.Index >= (uint)_sparse.Length)
            return false;
        var dense = _sparse[entity.Index] - 1;
        return dense >= 0 && dense < _entities.Count && _entities[dense] == entity;
    }

    public ref T Get(EntityId entity)
    {
        if (!Has(entity))
            throw new KeyNotFoundException($"Entity {entity} has no {typeof(T).Name} component.");
        return ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_components)[_sparse[entity.Index] - 1];
    }

    public bool Remove(EntityId entity)
    {
        if (!Has(entity))
            return false;

        var dense = _sparse[entity.Index] - 1;
        var last = _entities.Count - 1;
        if (dense != last)
        {
            _entities[dense] = _entities[last];
            _components[dense] = _components[last];
            _sparse[_entities[dense].Index] = dense + 1;
        }
        _entities.RemoveAt(last);
        _components.RemoveAt(last);
        _sparse[entity.Index] = 0;
        return true;
    }

    void IComponentStore.RemoveEntity(EntityId entity) => Remove(entity);

    private void EnsureSparse(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index >= _sparse.Length)
            Array.Resize(ref _sparse, Math.Max(index + 1, Math.Max(4, _sparse.Length * 2)));
    }
}

public sealed class World
{
    private readonly List<uint> _generations = [];
    private readonly Stack<int> _free = [];
    private readonly Dictionary<Type, IComponentStore> _stores = [];

    public EntityId Create()
    {
        if (_free.TryPop(out var index))
            return new EntityId(index, _generations[index]);
        _generations.Add(1);
        return new EntityId(_generations.Count - 1, 1);
    }

    public bool IsAlive(EntityId entity) =>
        (uint)entity.Index < (uint)_generations.Count &&
        _generations[entity.Index] == entity.Generation &&
        entity.Generation != 0;

    public void Destroy(EntityId entity)
    {
        EnsureAlive(entity);
        foreach (var store in _stores.Values)
            store.RemoveEntity(entity);
        _generations[entity.Index]++;
        if (_generations[entity.Index] == 0)
            _generations[entity.Index] = 1;
        _free.Push(entity.Index);
    }

    public ComponentStore<T> Store<T>() where T : struct
    {
        if (!_stores.TryGetValue(typeof(T), out var store))
        {
            store = new ComponentStore<T>();
            _stores.Add(typeof(T), store);
        }
        return (ComponentStore<T>)store;
    }

    public void Set<T>(EntityId entity, T component) where T : struct
    {
        EnsureAlive(entity);
        Store<T>().Set(entity, component);
    }

    public ref T Get<T>(EntityId entity) where T : struct
    {
        EnsureAlive(entity);
        return ref Store<T>().Get(entity);
    }

    public IEnumerable<EntityId> Query<TA, TB>()
        where TA : struct where TB : struct
    {
        var a = Store<TA>();
        var b = Store<TB>();
        var smallest = a.Count <= b.Count ? a.Entities : b.Entities;
        return smallest.Where(entity => IsAlive(entity) && a.Has(entity) && b.Has(entity)).Order();
    }

    private void EnsureAlive(EntityId entity)
    {
        if (!IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is stale or dead.");
    }

}

public sealed class CommandBuffer
{
    private readonly List<Action<World>> _commands = [];
    private bool _applying;

    public void Enqueue(Action<World> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_applying)
            throw new InvalidOperationException("Cannot enqueue structural changes while applying the buffer.");
        _commands.Add(command);
    }

    public void Apply(World world)
    {
        _applying = true;
        try
        {
            foreach (var command in _commands)
                command(world);
            _commands.Clear();
        }
        finally
        {
            _applying = false;
        }
    }
}

public interface ISimulationSystem
{
    string Name { get; }
    void Update(World world, long tick);
}

public sealed class SystemScheduler
{
    private readonly List<ISimulationSystem> _systems = [];
    public IReadOnlyList<string> Order => _systems.Select(system => system.Name).ToArray();

    public void Add(ISimulationSystem system)
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
