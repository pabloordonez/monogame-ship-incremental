using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct MiningContact(EntityId Source, EntityId Cell, int Damage);
