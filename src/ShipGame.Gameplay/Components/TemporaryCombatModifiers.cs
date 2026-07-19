using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct TemporaryCombatModifiers(
    float DamageMultiplier,
    float FireRateMultiplier,
    float SpeedMultiplier,
    float MobilityCooldownMultiplier,
    int ExtraProjectiles,
    float ExtraProjectileDamageMultiplier,
    int PierceCount,
    bool ShockTransit);
