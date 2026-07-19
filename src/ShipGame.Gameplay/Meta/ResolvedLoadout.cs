using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record ResolvedLoadout(
    LoadoutSelection Effective,
    IReadOnlyList<LoadoutDiagnostic> Diagnostics);
