using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct ResourceCollectedFact(EntityId Pickup, ContentId ResourceId, int Quantity);
