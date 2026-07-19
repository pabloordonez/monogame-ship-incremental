namespace ShipGame.Ecs;

public interface IComponentView<T> where T : struct
{
    int Count { get; }
    IReadOnlyList<EntityId> Entities { get; }
    void CopyEntitiesTo(List<EntityId> destination);
    bool Has(EntityId entity);
    T Read(EntityId entity);
}
