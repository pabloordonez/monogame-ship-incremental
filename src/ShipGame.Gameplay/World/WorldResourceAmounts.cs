using ShipGame.Domain;

namespace ShipGame.Gameplay;

public readonly record struct WorldResourceAmounts(int Ferrite, int Lumen, int DataCores)
{
    public WorldResourceAmounts Add(ContentId id, int quantity)
    {
        if (quantity <= 0)
            return this;
        if (id == WorldRunIds.Ferrite)
            return this with { Ferrite = checked(Ferrite + quantity) };
        if (id == WorldRunIds.Lumen)
            return this with { Lumen = checked(Lumen + quantity) };
        if (id == WorldRunIds.DataCore)
            return this with { DataCores = checked(DataCores + quantity) };
        return this;
    }
}
