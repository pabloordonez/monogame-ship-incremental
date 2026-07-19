using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public enum CombatRenderKind : byte
{
    PlayerShip,
    EnemyShip,
    Projectile,
    Obstacle,
    Mine
}
