namespace ShipGame.Gameplay;

/// <summary>Outward loot explosion velocity in world units per tick, decaying over RemainingTicks.</summary>
public struct PickupBurst
{
    public int VelocityX;
    public int VelocityY;
    public int RemainingTicks;
}
