using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record ResearchDefinition(
    string Id,
    string Name,
    ResourceAmounts Cost,
    IReadOnlyList<string> Dependencies,
    string GateDescription,
    Func<LifetimeCounters, bool> Gate,
    string Grant);
