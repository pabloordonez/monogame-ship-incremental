using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct LootSpawnedFact(EntityId Pickup, ContentId ResourceId, int Quantity);
