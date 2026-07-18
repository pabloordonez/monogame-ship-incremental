using ShipGame.Domain;

namespace ShipGame.Simulation;

public readonly record struct RunFact(
    ulong FactId,
    RunFactKind Kind,
    ContentId ResourceId = default,
    int Quantity = 0);
