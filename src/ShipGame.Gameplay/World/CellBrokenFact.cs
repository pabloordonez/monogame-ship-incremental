using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct CellBrokenFact(EntityId Cell, int CellId, AsteroidCellKind Kind);
