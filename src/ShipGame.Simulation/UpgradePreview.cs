using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record UpgradePreview(
    UpgradeDefinition Definition,
    bool Purchased,
    bool Affordable,
    string Explanation);
