using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct Projectile(int LifetimeTicks, float RemainingPierces, bool IsMissile);
