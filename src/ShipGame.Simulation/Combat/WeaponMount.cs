using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct WeaponMount(ContentId BehaviorId, WeaponBehavior Behavior);
