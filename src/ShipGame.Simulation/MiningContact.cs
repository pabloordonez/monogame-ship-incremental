using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct MiningContact(EntityId Source, EntityId Cell, int Damage);
