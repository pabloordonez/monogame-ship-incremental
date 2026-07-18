using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct MobilityAbility(
    ContentId BehaviorId,
    MobilityBehavior Behavior,
    float Distance,
    int DurationTicks,
    int CooldownTicks,
    int CooldownRemaining,
    int ActiveTicksRemaining,
    Vector2 Direction);
