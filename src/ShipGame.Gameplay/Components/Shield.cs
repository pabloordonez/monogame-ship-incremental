using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct Shield(float Current, float Maximum, float RechargePerSecond, int RechargeDelayTicks, int TicksSinceDamage);
