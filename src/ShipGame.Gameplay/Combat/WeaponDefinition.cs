using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct WeaponDefinition(
    ContentId Id,
    WeaponBehavior Behavior,
    float Damage,
    int CadenceTicks,
    float ProjectileSpeed,
    float Range,
    float HeatPerTick = 0,
    float CoolPerTick = 0,
    int BurstCount = 1,
    float LockConeDegrees = 0,
    float TurnDegreesPerSecond = 0);
