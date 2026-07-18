using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public enum EnemyBehavior : byte
{
    Interceptor,
    Gunship,
    Sapper
}
