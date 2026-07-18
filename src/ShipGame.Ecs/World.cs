namespace ShipGame.Ecs;

public sealed class World
{
    private readonly List<uint> _generations = [];
    private readonly Stack<int> _free = [];
    private readonly Dictionary<Type, IComponentStore> _stores = [];
    private int _activeQueries;

    public EntityId Create()
    {
        EnsureStructuralMutationAllowed();
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
        EnsureStructuralMutationAllowed();
        EnsureAlive(entity);
        foreach (var store in _stores.Values)
            store.RemoveEntity(entity);
        _generations[entity.Index]++;
        if (_generations[entity.Index] == 0)
            _generations[entity.Index] = 1;
        _free.Push(entity.Index);
    }

    public IComponentView<T> Store<T>() where T : struct => GetOrCreateStore<T>();

    private ComponentStore<T> GetOrCreateStore<T>() where T : struct
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
        EnsureStructuralMutationAllowed();
        EnsureAlive(entity);
        GetOrCreateStore<T>().Set(entity, component);
    }

    public bool Remove<T>(EntityId entity) where T : struct
    {
        EnsureStructuralMutationAllowed();
        EnsureAlive(entity);
        return GetOrCreateStore<T>().Remove(entity);
    }

    public ref T Get<T>(EntityId entity) where T : struct
    {
        EnsureAlive(entity);
        return ref GetOrCreateStore<T>().Get(entity);
    }

    public IEnumerable<EntityId> Query<TA, TB>()
        where TA : struct where TB : struct
    {
        var a = GetOrCreateStore<TA>();
        var b = GetOrCreateStore<TB>();
        var smallest = a.Count <= b.Count ? a.Entities : b.Entities;
        var snapshot = smallest
            .Where(entity => IsAlive(entity) && a.Has(entity) && b.Has(entity))
            .Order()
            .ToArray();
        return EnumerateQuery(snapshot);
    }

    private IEnumerable<EntityId> EnumerateQuery(EntityId[] snapshot)
    {
        _activeQueries++;
        try
        {
            foreach (var entity in snapshot)
                yield return entity;
        }
        finally
        {
            _activeQueries--;
        }
    }

    private void EnsureStructuralMutationAllowed()
    {
        if (_activeQueries != 0)
            throw new InvalidOperationException("Structural mutation is forbidden during query iteration; enqueue it for a synchronization point.");
    }

    private void EnsureAlive(EntityId entity)
    {
        if (!IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is stale or dead.");
    }

}
