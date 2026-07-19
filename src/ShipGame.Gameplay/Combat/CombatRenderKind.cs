using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public enum CombatRenderKind : byte
{
    PlayerShip,
    EnemyShip,
    Projectile,
    Obstacle,
    Mine
}
