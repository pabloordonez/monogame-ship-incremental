using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record UpgradeDefinition(
    ContentId Id,
    ResourceAmounts Cost,
    Func<TemporaryModifiers, TemporaryModifiers> Apply);
