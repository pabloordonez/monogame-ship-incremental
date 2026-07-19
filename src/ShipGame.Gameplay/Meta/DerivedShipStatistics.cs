using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record DerivedShipStatistics(
    int MaximumHull,
    int PickupRadius,
    int PullSpeed,
    int MaximumSpeed,
    int ShieldCapacity,
    decimal ShieldRechargePerSecond,
    decimal ShieldRechargeDelaySeconds,
    decimal WeaponDamage,
    decimal MiningDamagePerSecond,
    bool HasBlink,
    bool HasReflectiveShield,
    bool HasScoutDrone,
    decimal FerriteYieldMultiplier,
    int FailureFerriteRetentionPercent);
