using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

/// <summary>Narrow weapon callbacks — spawn/damage/target lookup only.</summary>
internal sealed class WeaponFireActions
{
    private readonly Func<EntityId, Vector2, float, float, EntityId> _findTargetInCone;
    private readonly Action<EntityId, EntityId, float, bool> _queueDamage;
    private readonly Action<CombatEvent> _addEvent;
    private readonly Action<PlayerProjectileSpawnRequest> _spawnPlayerProjectiles;

    public WeaponFireActions(
        Func<EntityId, Vector2, float, float, EntityId> findTargetInCone,
        Action<EntityId, EntityId, float, bool> queueDamage,
        Action<CombatEvent> addEvent,
        Action<PlayerProjectileSpawnRequest> spawnPlayerProjectiles)
    {
        _findTargetInCone = findTargetInCone ?? throw new ArgumentNullException(nameof(findTargetInCone));
        _queueDamage = queueDamage ?? throw new ArgumentNullException(nameof(queueDamage));
        _addEvent = addEvent ?? throw new ArgumentNullException(nameof(addEvent));
        _spawnPlayerProjectiles = spawnPlayerProjectiles ?? throw new ArgumentNullException(nameof(spawnPlayerProjectiles));
    }

    public EntityId FindTargetInCone(EntityId source, Vector2 aim, float range, float coneDegrees) =>
        _findTargetInCone(source, aim, range, coneDegrees);

    public void QueueDamage(EntityId target, EntityId source, float amount, bool projectile) =>
        _queueDamage(target, source, amount, projectile);

    public void AddEvent(CombatEvent value) => _addEvent(value);

    public void SpawnPlayerProjectiles(in PlayerProjectileSpawnRequest request) =>
        _spawnPlayerProjectiles(request);
}
