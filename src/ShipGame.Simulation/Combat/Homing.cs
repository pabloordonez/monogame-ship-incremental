using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct Homing(EntityId Target, float Speed, float TurnRadiansPerSecond);
