using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public struct Collectible
{
    public ContentId ResourceId;
    public int Quantity;
    public bool Credited;
    /// <summary>World-run tick at which credit/pull become active (grace for on-screen visibility).</summary>
    public long CollectibleAfterTick;
}
