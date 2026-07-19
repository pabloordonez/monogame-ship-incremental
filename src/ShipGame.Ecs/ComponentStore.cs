namespace ShipGame.Ecs;

internal sealed class ComponentStore<T> : IComponentStore, IComponentView<T> where T : struct
{
    private readonly List<EntityId> _entities = [];
    private readonly List<T> _components = [];
    private int[] _sparse = [];

    public int Count => _entities.Count;
    public IReadOnlyList<EntityId> Entities => new EntitySnapshot(_entities.ToArray());

    public void CopyEntitiesTo(List<EntityId> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Clear();
        if (_entities.Count == 0)
            return;
        if (destination.Capacity < _entities.Count)
            destination.Capacity = _entities.Count;
        for (var i = 0; i < _entities.Count; i++)
            destination.Add(_entities[i]);
    }

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

    public T Read(EntityId entity)
    {
        if (!Has(entity))
            throw new KeyNotFoundException($"Entity {entity} has no {typeof(T).Name} component.");
        return _components[_sparse[entity.Index] - 1];
    }

    internal ref T Get(EntityId entity)
    {
        if (!Has(entity))
            throw new KeyNotFoundException($"Entity {entity} has no {typeof(T).Name} component.");
        return ref System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_components)[_sparse[entity.Index] - 1];
    }

    internal bool Remove(EntityId entity)
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

    private sealed class EntitySnapshot(EntityId[] entities) : IReadOnlyList<EntityId>
    {
        public int Count => entities.Length;
        public EntityId this[int index] => entities[index];
        public IEnumerator<EntityId> GetEnumerator() => ((IEnumerable<EntityId>)entities).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => entities.GetEnumerator();
    }
}
