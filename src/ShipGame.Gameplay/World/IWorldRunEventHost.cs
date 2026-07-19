using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

/// <summary>Narrow orchestrator capabilities for world-run event handlers.</summary>
internal interface IWorldRunEventHost
{
    EntityId PlayerEntity { get; }
    Vector2 EliteArenaWorldCenter { get; }
    Vector2 PlayerWorldPosition { get; }
    void InflictDamage(EntityId target, EntityId source, float amount, bool projectile);
    /// <summary>Spawns the elite once; returns false if already requested.</summary>
    bool TrySpawnEliteEnemy(ContentId enemyId, Vector2 worldPosition, out EntityId eliteEntity);
    void SpawnEliteDataCore(WorldPosition position);
    void NoteCheckpoint(string name);
}
