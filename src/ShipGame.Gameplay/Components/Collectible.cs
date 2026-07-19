using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public struct Collectible
{
    public ContentId ResourceId;
    public int Quantity;
    public bool Credited;
}
