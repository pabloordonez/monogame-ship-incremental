using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record UpgradeDefinition(
    ContentId Id,
    ResourceAmounts Cost,
    Func<TemporaryModifiers, TemporaryModifiers> Apply);
