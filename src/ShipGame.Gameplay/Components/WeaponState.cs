using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct WeaponState(int CooldownTicks, float Heat, bool HeatLocked, EntityId Target);
