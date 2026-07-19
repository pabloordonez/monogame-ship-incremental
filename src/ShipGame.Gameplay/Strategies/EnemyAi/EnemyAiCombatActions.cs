using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

/// <summary>Narrow spawn callbacks for enemy AI — not a second simulation facade.</summary>
internal sealed class EnemyAiCombatActions
{
    private readonly Action<EntityId, Vector2, float, float> _spawnHostileProjectile;
    private readonly Action<EntityId, Vector2, float> _spawnMine;

    public EnemyAiCombatActions(
        Action<EntityId, Vector2, float, float> spawnHostileProjectile,
        Action<EntityId, Vector2, float> spawnMine)
    {
        _spawnHostileProjectile = spawnHostileProjectile ?? throw new ArgumentNullException(nameof(spawnHostileProjectile));
        _spawnMine = spawnMine ?? throw new ArgumentNullException(nameof(spawnMine));
    }

    public void SpawnHostileProjectile(EntityId source, Vector2 direction, float damage, float speed) =>
        _spawnHostileProjectile(source, direction, damage, speed);

    public void SpawnMine(EntityId owner, Vector2 position, float damage) =>
        _spawnMine(owner, position, damage);
}
