using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct DamageSource(EntityId Owner, Faction Faction, float Damage, bool Projectile);
