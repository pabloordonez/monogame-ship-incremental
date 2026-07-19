using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record ModuleDefinition(
    string Id,
    ModuleSlot Slot,
    string? RequiredResearchId,
    bool IsDefault);
