using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct Mine(int ArmTicks, int LifetimeTicks, float Radius, float Damage, EntityId Owner);
