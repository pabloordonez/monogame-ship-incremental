using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record ResolvedLoadout(
    LoadoutSelection Effective,
    IReadOnlyList<LoadoutDiagnostic> Diagnostics);
