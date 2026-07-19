using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record UpgradePreview(
    UpgradeDefinition Definition,
    bool Purchased,
    bool Affordable,
    string Explanation);
