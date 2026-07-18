namespace ShipGame.Ecs;

public readonly record struct EntityId(int Index, uint Generation) : IComparable<EntityId>
{
    public int CompareTo(EntityId other)
    {
        var index = Index.CompareTo(other.Index);
        return index != 0 ? index : Generation.CompareTo(other.Generation);
    }
}
