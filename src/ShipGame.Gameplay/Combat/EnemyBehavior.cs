using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public enum EnemyBehavior : byte
{
    Interceptor,
    Gunship,
    Sapper
}
